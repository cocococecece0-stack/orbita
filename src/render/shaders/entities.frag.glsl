#version 300 es
precision highp float;
in vec2 vUv;
in vec4 vColor;
in vec4 vMeta;
out vec4 fragColor;

void main() {
  float type = vMeta.x;
  float seed = vMeta.y;
  float glow = vMeta.z;
  vec2 p = vUv;
  float d = length(p);
  vec3 col = vColor.rgb;
  float a = vColor.a;

  if (type < 0.5) {
    // Сфера: тёплое ядро + тонкий обод.
    float core = 1.0 - smoothstep(0.10, 0.48, d);
    float rim = smoothstep(0.30, 0.46, d) * (1.0 - smoothstep(0.46, 0.58, d));
    col = mix(col, vec3(1.0, 0.96, 0.82), core * 0.65);
    col += col * rim * glow;
    a *= 1.0 - smoothstep(0.46, 0.58, d);
  } else if (type < 1.5) {
    // Астероид: неровный силуэт + кратеры.
    float ang = atan(p.y, p.x);
    float rad = 0.80 + 0.10 * sin(ang * 5.0 + seed * 6.0)
                     + 0.07 * sin(ang * 9.0 + seed * 2.0);
    float body = 1.0 - smoothstep(rad - 0.03, rad + 0.02, d);
    float crater = (1.0 - smoothstep(0.07, 0.13, length(p - vec2(0.24, -0.18))))
                 + (1.0 - smoothstep(0.05, 0.10, length(p - vec2(-0.28, 0.14))));
    vec3 ln = normalize(vec3(p, 0.6));
    col *= 0.62 + 0.38 * max(0.0, dot(ln, normalize(vec3(-0.45, 0.6, 0.7))));
    col *= 1.0 - crater * 0.32;
    col += col * glow * pow(max(0.0, 1.0 - d), 3.0) * 0.5;
    a *= body;
  } else if (type < 2.5) {
    // Комета: ромб с ядром, форма отличается от сферы.
    float dia = abs(p.x) + abs(p.y);
    float body = 1.0 - smoothstep(0.62, 0.72, dia);
    float core = 1.0 - smoothstep(0.14, 0.30, d);
    col = mix(col, vec3(1.0, 0.93, 0.72), core * 0.8);
    col += col * glow * pow(max(0.0, 1.0 - dia), 3.0);
    a *= body;
  } else if (type < 3.5) {
    // Частица: мягкая точка.
    float g = pow(max(0.0, 1.0 - d), 2.0);
    col *= 1.0 + g * glow;
    a *= g;
  } else if (type < 4.5) {
    // Щит: кольцо.
    float ring = (1.0 - smoothstep(0.70, 0.80, d)) * smoothstep(0.56, 0.65, d);
    col *= 1.0 + glow * 0.7;
    a *= ring;
  } else if (type < 6.5) {
    // Замедление: ромб с внутренней перегородкой.
    float dia = abs(p.x) + abs(p.y);
    float body = 1.0 - smoothstep(0.60, 0.70, dia);
    float bar = 1.0 - smoothstep(0.05, 0.09, abs(p.x));
    col = mix(col * 0.8, col * 1.4, bar);
    a *= body;
  } else if (type < 7.5) {
    // Магнит: две дуги.
    float ang = atan(p.y, p.x);
    float arc = step(0.35, abs(sin(ang * 2.0)));
    float ring = (1.0 - smoothstep(0.68, 0.78, d)) * smoothstep(0.52, 0.62, d) * arc;
    a *= ring;
  } else if (type < 8.5) {
    // Двойные очки: двойной ромб.
    float d1 = abs(p.x - 0.16) + abs(p.y);
    float d2 = abs(p.x + 0.16) + abs(p.y);
    a *= max(1.0 - smoothstep(0.34, 0.42, d1), 1.0 - smoothstep(0.34, 0.42, d2));
  } else if (type < 9.5) {
    // Фаза: пустой контур с трещиной.
    float ring = smoothstep(0.52, 0.60, d) * (1.0 - smoothstep(0.70, 0.78, d));
    float cut = smoothstep(0.04, 0.10, abs(p.y - p.x * 0.4));
    a *= ring * cut;
  } else if (type < 10.5) {
    // Заморозка: шестилучевая звезда.
    float ang = atan(p.y, p.x);
    float star = pow(abs(cos(ang * 3.0)), 8.0);
    a *= (1.0 - smoothstep(0.30, 0.72, d)) * star;
  } else if (type < 11.5) {
    // Второй шанс: крест.
    float cross = 1.0 - smoothstep(0.10, 0.16, min(abs(p.x), abs(p.y)));
    a *= cross * (1.0 - smoothstep(0.55, 0.65, d));
  } else {
    // Червоточина: спираль.
    float ang = atan(p.y, p.x);
    float spiral = abs(sin(ang * 2.0 + d * 14.0));
    float ring = (1.0 - smoothstep(0.70, 0.80, d)) * smoothstep(0.16, 0.24, d);
    a *= ring * spiral;
    col *= 1.0 + glow * 0.6;
  }

  if (a <= 0.003) discard;
  fragColor = vec4(col, a);
}
