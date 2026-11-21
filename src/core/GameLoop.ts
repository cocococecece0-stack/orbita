export type FpsCap = 0 | 30 | 60 | 90 | 120;

export interface LoopCallbacks {
  /** Фиксированный шаг: только детерминированная симуляция. */
  update(dt: number): void;
  /** alpha = остаток аккумулятора в долях FIXED_DT; elapsed = время симуляции. */
  render(alpha: number, elapsed: number): void;
}

/**
 * requestAnimationFrame + аккумулятор.
 * На 60/90/120 Гц физика идёт шагами ровно 1/120 с — скорость не меняется.
 */
export class GameLoop {
  static readonly FIXED_DT = 1 / 120;
  private static readonly MAX_STEPS = 8;

  private readonly onUpdate: (dt: number) => void;
  private readonly onRender: (alpha: number, elapsed: number) => void;

  private accumulator = 0;
  private elapsed = 0;
  private last = 0;
  private lastRender = 0;
  private rafId = 0;
  private running = false;

  private fpsCap: FpsCap = 0;
  private minRenderMs = 0;

  constructor(callbacks: LoopCallbacks) {
    this.onUpdate = callbacks.update;
    this.onRender = callbacks.render;
  }

  /** 0 — без ограничения, иначе 30/60/90/120. */
  setFpsCap(cap: FpsCap): void {
    this.fpsCap = cap;
    this.minRenderMs = cap === 0 ? 0 : 1000 / cap;
    this.lastRender = 0;
  }

  getFpsCap(): FpsCap {
    return this.fpsCap;
  }

  getElapsed(): number {
    return this.elapsed;
  }

  start(): void {
    if (this.running) return;
    this.running = true;
    this.last = performance.now();
    this.lastRender = this.last;
    this.accumulator = 0;
    this.rafId = requestAnimationFrame(this.frame);
  }

  stop(): void {
    if (!this.running) return;
    this.running = false;
    cancelAnimationFrame(this.rafId);
  }

  /** Сброс времени и аккумулятора после рестарта забега. */
  reset(): void {
    this.accumulator = 0;
    this.elapsed = 0;
    this.last = performance.now();
  }

  private readonly frame = (now: number): void => {
    if (!this.running) return;

    const raw = (now - this.last) / 1000;
    this.last = now;

    // Защита от «залипания» вкладки: не более 0.25 с за кадр.
    const dt = Number.isFinite(raw) ? Math.min(Math.max(raw, 0), 0.25) : 0;
    this.accumulator += dt;

    // Ограничение частоты рендера; физика продолжает копить аккумулятор.
    if (this.minRenderMs > 0 && now - this.lastRender + 0.5 < this.minRenderMs) {
      this.rafId = requestAnimationFrame(this.frame);
      return;
    }
    this.lastRender = now;

    let steps = 0;
    while (this.accumulator >= GameLoop.FIXED_DT && steps < GameLoop.MAX_STEPS) {
      this.onUpdate(GameLoop.FIXED_DT);
      this.accumulator -= GameLoop.FIXED_DT;
      this.elapsed += GameLoop.FIXED_DT;
      steps++;
    }
    if (steps === GameLoop.MAX_STEPS) this.accumulator = 0;

    const alpha = this.accumulator / GameLoop.FIXED_DT;
    this.onRender(alpha, this.elapsed);

    this.rafId = requestAnimationFrame(this.frame);
  };
}
