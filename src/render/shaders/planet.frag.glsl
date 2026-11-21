#version 300 es
precision highp float;

// gl_FragCoord имеет начало СНИЗУ слева, CSS — СВЕРХУ слева.
// Флип делается в JS (Renderer.resize/draw):
//   glCenterX = cssCenterX * dpr;
//   glCenterY = (cssHeight - cssCenterY) * dpr;
// Сюда приходят уже перевёрнутые координаты; шейдер про CSS не знает.
uniform vec2  uCenter;
uniform float uRadius;       // framebuffer-пиксели
uniform float uTime;         // накопленное время симуляции, сек
uniform float uPulse;        // 0..1, конверт левел-апа
uniform float uCrack;        // 0..1, трещины при смерти
uniform float uShake;        // px, из theme --shake-max
uniform float uGrain;        // --grain
uniform float uFxIntensity;  // --fx-intensity: общий рычаг CSS -> GLSL
uniform vec3  uAccent;       // --gl-acc
uniform vec3  uDanger;       // --gl-dang
uniform vec3  uVoid;         // --gl-void

out vec4 fragColor;

const float TAU = 6.28318530718;
// cos(-0.92), sin(-0.92) — направление разрыва кольца.
// Хордовая дистанция дешевле atan на каждый пиксель.
const vec2 GAP_DIR = vec2(0.605520, -0.795830);

float hash21(vec2 p) {
  p = fract(p * vec2(123.34, 456.21));
  p += dot(p, p + 45.32);
  return fract(p.x * p.y);
}

float noise(vec2 p) {
  vec2 i = floor(p);
  vec2 f = fract(p);
  f = f * f * (3.0 - 2.0 * f);
  return mix(
    mix(hash21(i),               hash21(i + vec2(1.0, 0.0)), f.x),
    mix(hash21(i + vec2(0.0,1.0)), hash21(i + vec2(1.0, 1.0)), f.x),
    f.y
  );
}

float fbm(vec2 p) {
  float v = 0.0;
  float a = 0.5;
  for (int i = 0; i < 4; i++) {
    v += a * noise(p);
    p *= 2.03;
    a *= 0.5;
  }
  return v;
}

void main() {
  // Тряска называет только выборку пикселя и не трогает симуляцию.
  vec2 frag = gl_FragCoord.xy + vec2(
    uShake * sin(uTime * 71.0),
    uShake * cos(uTime * 83.0)
  ) * uFxIntensity;

  vec2  p    = frag - uCenter;
  float dist = length(p);

  // EARLY EXIT: далеко за пределами планеты, кольца и пульса fbm не считается.
  if (dist > uRadius * 1.85) {
    fragColor = vec4(0.0);
    return;
  }

  float a0 = atan(p.y, p.x); // вычисляется только внутри раннего выхода
  float wobble = 1.0 + uFxIntensity * (
      0.010 * sin(a0 * 7.0 + uTime * 0.17)
    + 0.006 * (fbm(normalize(p) * 3.0 + uTime * 0.02) - 0.5)
  );

  float r = dist / max(uRadius * wobble, 1.0);
  vec2  q = p / max(uRadius, 1.0);
  vec2  dir = p * inversesqrt(dot(p, p) + 1e-6);

  vec3  col   = vec3(0.0);
  float alpha = 0.0;

  /* ---------- 1. Поверхность: полосы + терминатор + трещины ---------- */
  if (r < 1.0) {
    float z = sqrt(max(0.0, 1.0 - r * r));
    vec3  n = normalize(vec3(q, z));
    vec3  L = normalize(vec3(-0.55, 0.45, 0.78));
    float ndl  = dot(n, L);
    float term = smoothstep(-0.14, 0.24, ndl);

    float warp  = fbm(q * 3.2 + vec2(uTime * 0.03, 0.0));
    float bands = smoothstep(-0.30, 0.35, sin((q.y + warp * 0.35) * 26.0));
    float lat   = mix(0.62, 1.0, smoothstep(-1.0, 1.0, q.y));

    vec3 dark = mix(uVoid * 1.7, uAccent * 0.16, 0.30);
    vec3 lit  = mix(uAccent * 0.52, vec3(0.93, 0.86, 0.68), bands * 0.65);
    vec3 surf = mix(dark, lit, term * lat);

    // СВЕТ #1 из бюджета 2: только лимб.
    float limb = pow(1.0 - abs(dot(n, vec3(0.0, 0.0, 1.0))), 9.0);
    surf += uAccent * limb * 0.10 * term * uFxIntensity;

    // Трещины при смерти проступают только на свету.
    float cf     = fbm(q * 7.0 + 3.7);
    float cracks = 1.0 - smoothstep(0.0, 0.035, abs(cf - 0.5));
    surf = mix(surf, uDanger, cracks * uCrack * term * uFxIntensity);

    col   = surf;
    alpha = 1.0;
  }

  /* ---------- 2. Тонкое кольцо с разрывом (без atan для gap) ---------- */
  // СВЕТ #2 из бюджета 2: только разрыв кольца.
  float ringR = 1.28 + 0.012 * sin(uTime * 0.5) * uFxIntensity;
  float ringW = 0.010;
  float mask  = 1.0 - smoothstep(ringW, ringW * 2.0, abs(r - ringR));
  float gap   = smoothstep(0.10, 0.18, length(dir - GAP_DIR));
  float dash  = step(0.38, fract((a0 / TAU + 0.5) * 12.0));
  float ringA = mask * gap * mix(0.30, 1.0, dash) * uFxIntensity;

  col   = mix(col, mix(uAccent, vec3(0.95, 0.90, 0.76), 0.40), ringA);
  alpha = max(alpha, ringA);

  /* ---------- 3. Пульс левел-апа ---------- */
  float pulseR = mix(0.95, 1.70, uPulse);
  float pulse  = (1.0 - smoothstep(0.0, 0.06, abs(r - pulseR)))
               * (1.0 - uPulse) * uPulse * 4.0 * uFxIntensity;
  col   += uAccent * pulse;
  alpha  = max(alpha, pulse);

  /* ---------- 4. Плёночное зерно ---------- */
  float g = (hash21(frag + fract(uTime) * 137.0) - 0.5) * uGrain * uFxIntensity;
  col += g * max(alpha, 0.15);

  fragColor = vec4(col, clamp(alpha, 0.0, 1.0));
}
