export const KIND = {
  ORB: 0,
  ASTEROID: 1,
  PLAYER: 2,
  PARTICLE: 3,
  SHIELD: 4,
  SLOW: 5,
  TRAIL: 6,
  MAGNET: 7,
  DOUBLE: 8,
  PHASE: 9,
  FREEZE: 10,
  SECOND: 11,
  WORMHOLE: 12,
} as const;

export interface Entity {
  active: boolean;
  /** 0 — обычный, 1 — босс, 2 — червоточина. */
  kind: number;
  x: number;
  y: number;
  vx: number;
  vy: number;
  rot: number;
  spin: number;
  size: number;
  timer: number;
  life: number;
  seed: number;
  r: number;
  g: number;
  b: number;
}

export function makeEntity(): Entity {
  return {
    active: false, kind: 0, x: 0, y: 0, vx: 0, vy: 0, rot: 0, spin: 0,
    size: 0, timer: 0, life: 0, seed: 0, r: 1, g: 1, b: 1,
  };
}

export class Pool<T extends Entity> {
  readonly items: T[];
  private cursor = 0;

  constructor(size: number, factory: () => T) {
    this.items = new Array<T>(size);
    for (let i = 0; i < size; i++) {
      const item = factory();
      this.items[i] = item;
    }
  }

  spawn(): T | null {
    const n = this.items.length;
    for (let i = 0; i < n; i++) {
      const idx = (this.cursor + i) % n;
      const item = this.items[idx];
      if (item && !item.active) {
        item.active = true;
        this.cursor = (idx + 1) % n;
        return item;
      }
    }
    return null;
  }

  clear(): void {
    for (let i = 0; i < this.items.length; i++) {
      const item = this.items[i];
      if (item) item.active = false;
    }
  }
}
