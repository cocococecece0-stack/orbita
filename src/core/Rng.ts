/** Детерминированный xorshift32: одинаковый результат на iOS и Android. */
export class Rng {
  private state: number;

  constructor(seed: number) {
    // Нулевое состояние вырождено — подменяем константой золотого сечения.
    this.state = (seed >>> 0) || 0x9e3779b9;
  }

  /** Следующее число в [0, 1). */
  next(): number {
    let x = this.state;
    x ^= x << 13;
    x ^= x >>> 17;
    x ^= x << 5;
    this.state = x >>> 0;
    return this.state / 0x1_0000_0000;
  }

  /** Целое в [0, maxExclusive). */
  int(maxExclusive: number): number {
    return Math.floor(this.next() * maxExclusive);
  }

  /** Вещественное в [min, max). */
  range(min: number, max: number): number {
    return min + this.next() * (max - min);
  }

  /** Daily challenge: сид = UTC-дата, одинаков на всех устройствах. */
  static dailySeedUTC(now: Date = new Date()): number {
    const y = now.getUTCFullYear();
    const m = now.getUTCMonth() + 1;
    const d = now.getUTCDate();
    return ((y * 10000 + m * 100 + d) >>> 0) || 0x9e3779b9;
  }

  /** Обычный забег: сид = timestamp, смешанный с монотонными часами. */
  static runSeed(): number {
    const t = (Date.now() ^ ((performance.now() * 1000) >>> 0)) >>> 0;
    return t || 0x9e3779b9;
  }
}
