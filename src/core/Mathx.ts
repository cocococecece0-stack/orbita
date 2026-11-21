export const TAU = Math.PI * 2;

export function clamp(v: number, min: number, max: number): number {
  return v < min ? min : v > max ? max : v;
}

export function lerp(a: number, b: number, t: number): number {
  return a + (b - a) * t;
}

/** Без аллокаций: пишет результат в out. */
export function approach(current: number, target: number, maxDelta: number, out: { v: number }): void {
  const d = target - current;
  out.v = Math.abs(d) <= maxDelta ? target : current + Math.sign(d) * maxDelta;
}

export function angDiff(a: number, b: number): number {
  let d = (a - b) % TAU;
  if (d > Math.PI) d -= TAU;
  if (d < -Math.PI) d += TAU;
  return d;
}

export function f32(v: number): number {
  return Math.fround(v);
}
