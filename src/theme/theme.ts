/** Typed-чтение CSS-переменных. Единственный мост theme.css -> GLSL/движок. */
export type Vec3 = readonly [number, number, number];

export interface ThemeSnapshot {
  accent: Vec3;
  danger: Vec3;
  voidColor: Vec3;
  grain: number;
  fxIntensity: number;
  shakeMaxPx: number;
  targetRadius: number;
  offsetX: number;
  offsetY: number;
}

function vec3(cs: CSSStyleDeclaration, name: string, fallback: Vec3): Vec3 {
  const raw = cs.getPropertyValue(name).trim();
  if (!raw) return fallback;
  const parts = raw.split(',').map((v) => Number(v.trim()));
  return parts.length === 3 && parts.every(Number.isFinite)
    ? [parts[0] ?? 0, parts[1] ?? 0, parts[2] ?? 0]
    : fallback;
}

function num(cs: CSSStyleDeclaration, name: string, fallback: number): number {
  const raw = cs.getPropertyValue(name).trim();
  if (!raw) return fallback;
  const v = parseFloat(raw);
  return Number.isFinite(v) ? v : fallback;
}

/** Вызывается при старте и при смене палитры/настроек. */
export function readTheme(): ThemeSnapshot {
  const cs = getComputedStyle(document.documentElement);
  return {
    accent: vec3(cs, '--gl-acc', [1, 0.706, 0.329]),
    danger: vec3(cs, '--gl-dang', [1, 0.361, 0.361]),
    voidColor: vec3(cs, '--gl-void', [0.039, 0.035, 0.031]),
    grain: num(cs, '--grain', 0.04),
    fxIntensity: num(cs, '--fx-intensity', 1),
    shakeMaxPx: num(cs, '--shake-max', 4),
    targetRadius: num(cs, '--target-radius', 0.18),
    offsetX: num(cs, '--target-offset-x', 0.02),
    offsetY: num(cs, '--target-offset-y', 0.04),
  };
}
