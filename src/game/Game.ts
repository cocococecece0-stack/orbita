import { CONFIG, type Mode } from '../core/Config';
import { EventBus } from '../core/Events';
import { Rng } from '../core/Rng';
import { TAU, clamp, angDiff } from '../core/Mathx';
import { KIND, Pool, makeEntity, type Entity } from './Entities';

export type Screen = 'menu' | 'playing' | 'paused' | 'gameover';

export interface GameSnapshot {
  screen: Screen;
  score: number;
  best: number;
  combo: number;
  mult: number;
  level: number;
  orbs: number;
  shields: number;
  slowLeft: number;
  feverLeft: number;
  nearMisses: number;
  elapsed: number;
  isNewBest: boolean;
  mode: Mode;
}

const ACCENT = { r: 1.0, g: 0.706, b: 0.329 };
const DANGER = { r: 1.0, g: 0.361, b: 0.361 };
const REWARD = { r: 0.949, g: 0.886, b: 0.722 };
const SHIELD_C = { r: 0.61, g: 0.95, b: 0.78 };
const SLOW_C = { r: 0.85, g: 0.78, b: 1.0 };

export class Game {
  readonly events = new EventBus();
  private rng = new Rng(Rng.runSeed());
  private screen: Screen = 'menu';
  private mode: Mode = 'classic';

  private centerX = 0;
  private centerY = 0;
  private baseRadius = 120;

  private angle = -Math.PI / 2;
  private angularSpeed = 1.35;
  private orbitIndex = 1;
  private orbitRadii = [0.62, 1.0];
  private px = 0;
  private py = 0;
  private pRot = 0;

  private score = 0;
  private best = 0;
  private combo = 0;
  private comboTimer = 0;
  private mult = 1;
  private feverLeft = 0;
  private level = 1;
  private levelOrbs = 0;
  private orbsCollected = 0;
  private nearMisses = 0;
  private shieldTimer = 0;
  private slowLeft = 0;
  private elapsed = 0;
  private isNewBest = false;

  private hitStop = 0;
  private shake = 0;
  private zoom = 0;
  private pulse = 0;
  private crack = 0;
  private spawnTimer = 0;
  private hazardTimer = 0;
  private wavePhase = 0;

  readonly orbs = new Pool<Entity>(CONFIG.maxOrbs, makeEntity);
  readonly asteroids = new Pool<Entity>(CONFIG.maxAsteroids, makeEntity);
  readonly powerups = new Pool<Entity>(CONFIG.maxPowerups, makeEntity);
  readonly particles = new Pool<Entity>(CONFIG.maxParticles, makeEntity);

  private visual = { pulse: 0, crack: 0, shake: 0, zoom: 0 };

  private snapshot: GameSnapshot = {
    screen: 'menu', score: 0, best: 0, combo: 0, mult: 1, level: 1,
    orbs: 0, shields: 0, mode: 'classic', slowLeft: 0, feverLeft: 0, nearMisses: 0,
    elapsed: 0, isNewBest: false,
  };

  constructor(best: number) {
    this.best = best;
  }

  resize(cssW: number, cssH: number, top: number, bottom: number): void {
    this.centerX = cssW * 0.5;
    this.centerY = top + (cssH - top - bottom) * 0.52;
    this.baseRadius = Math.min(cssW, cssH - top - bottom) * 0.30;
    this.updatePlayerPosition();
  }

  setBest(value: number): void { this.best = value; }
  /** Скин меняет только визуальный акцент, не затрагивая симуляцию. */
  setAccent(r: number, g: number, b: number): void {
    ACCENT.r = r;
    ACCENT.g = g;
    ACCENT.b = b;
  }
  getBest(): number { return this.best; }
  getMode(): Mode { return this.mode; }
  getScreen(): Screen { return this.screen; }
  getSnapshot(): GameSnapshot { return this.sync(); }
  getPlanetVisual(): { pulse: number; crack: number; shake: number; zoom: number } {
    this.visual.pulse = this.pulse;
    this.visual.crack = this.crack;
    this.visual.shake = this.shake;
    this.visual.zoom = this.zoom;
    return this.visual;
  }

  startRun(seed = Rng.runSeed(), mode: Mode = 'classic'): void {
    this.mode = mode;
    this.rng = new Rng(seed);
    this.orbs.clear();
    this.asteroids.clear();
    this.powerups.clear();
    this.particles.clear();
    this.score = 0;
    this.combo = 0;
    this.comboTimer = 0;
    this.mult = 1;
    this.feverLeft = 0;
    this.level = 1;
    this.levelOrbs = 0;
    this.orbsCollected = 0;
    this.nearMisses = 0;
    this.shieldTimer = 0;
    this.slowLeft = 0;
    this.elapsed = 0;
    this.isNewBest = false;
    this.hitStop = 0;
    this.crack = 0;
    this.pulse = 1;
    this.shake = 0;
    this.zoom = 0.08;
    this.angle = -Math.PI / 2;
    this.angularSpeed = mode === 'hardcore' ? 2.1 : 1.35;
    this.orbitIndex = mode === 'hardcore' ? 0 : 1;
    this.spawnTimer = 0;
    this.hazardTimer = 1.2;
    this.screen = 'playing';
    this.snapshot.mode = mode;
    this.updatePlayerPosition();
    this.sync();
  }

  toMenu(): void { this.screen = 'menu'; this.sync(); }
  pause(): void { if (this.screen === 'playing') { this.screen = 'paused'; this.sync(); } }
  resume(): void { if (this.screen === 'paused') { this.screen = 'playing'; this.sync(); } }

  /** Тап: мгновенная смена орбиты (отклик в том же кадре). */
  tap(): void {
    if (this.screen !== 'playing') return;
    // Hardcore: одна орбита, тап даёт только визуальный отклик.
    if (this.mode === 'hardcore') {
      this.zoom = Math.max(this.zoom, 0.02);
      return;
    }
    this.orbitIndex = this.orbitIndex === 0 ? 1 : 0;
    this.zoom = Math.max(this.zoom, 0.035);
    this.burst(this.px, this.py, 5, ACCENT, 34);
  }

  /** Удержание: нырок на внутреннюю орбиту с риском. */
  setDiving(active: boolean): void {
    if (this.screen !== 'playing' || this.mode === 'hardcore') return;
    this.orbitIndex = active ? 0 : 1;
  }

  update(dt: number): void {
    if (this.screen !== 'playing') {
      this.decayVisual(dt);
      return;
    }

    if (this.hitStop > 0) {
      this.hitStop -= dt;
      this.decayVisual(dt);
      return;
    }

    const slow = this.slowLeft > 0 ? CONFIG.nearMissScale : 1;
    const sdt = dt * slow;
    this.elapsed += dt;
    this.wavePhase += sdt;

    if (this.feverLeft > 0) this.feverLeft = Math.max(0, this.feverLeft - dt);
    if (this.shieldTimer > 0) this.shieldTimer = Math.max(0, this.shieldTimer - dt);
    if (this.slowLeft > 0) this.slowLeft = Math.max(0, this.slowLeft - dt);

    if (this.comboTimer > 0) {
      this.comboTimer -= dt;
      if (this.comboTimer <= 0 && !this.feverActive()) this.setCombo(0);
    }

    const speed = this.angularSpeed + this.level * 0.035 + (this.feverActive() ? 0.2 : 0);
    this.angle = (this.angle + speed * sdt) % TAU;
    this.updatePlayerPosition();
    this.pRot = this.angle + Math.PI / 2;

    // Трейл: одна частица каждые ~2 кадра без аллокаций.
    if (this.rng.next() < 0.5) {
      const t = this.particles.spawn();
      if (t) {
        t.x = this.px; t.y = this.py;
        t.vx = -Math.sin(this.angle) * 8; t.vy = Math.cos(this.angle) * 8;
        t.rot = 0; t.spin = 0; t.size = 9; t.life = 0.42; t.timer = 0.42;
        t.r = ACCENT.r; t.g = ACCENT.g; t.b = ACCENT.b;
      }
    }

    this.updateOrbs(sdt);
    this.updateAsteroids(sdt);
    this.updatePowerups(sdt);
    this.updateParticles(sdt);
    this.updateSpawns(dt);

    this.decayVisual(dt);
    this.sync();
  }

  private feverActive(): boolean { return this.feverLeft > 0; }

  private updatePlayerPosition(): void {
    const r = this.baseRadius * (this.orbitRadii[this.orbitIndex] ?? 1);
    this.px = this.centerX + Math.cos(this.angle) * r;
    this.py = this.centerY + Math.sin(this.angle) * r;
  }

  private updateOrbs(dt: number): void {
    const list = this.orbs.items;
    for (let i = 0; i < list.length; i++) {
      const o = list[i];
      if (!o || !o.active) continue;
      o.timer -= dt;
      if (o.timer <= 0) { o.active = false; continue; }
      o.rot += o.spin * dt;
      const dx = o.x - this.px;
      const dy = o.y - this.py;
      const d2 = dx * dx + dy * dy;
      const reach = o.size + 14;
      if (d2 < reach * reach) {
        o.active = false;
        this.collectOrb(o);
      }
    }
  }

  private collectOrb(o: Entity): void {
    this.orbsCollected++;
    this.levelOrbs++;
    this.setCombo(this.combo + 1);
    const gain = this.mult * (this.feverActive() ? 2 : 1) * (this.mode === 'hardcore' ? 2 : 1);
    this.score += 10 * gain;
    this.hitStop = CONFIG.hitStopMs / 1000;
    this.zoom = Math.max(this.zoom, 0.06 + this.mult * 0.008);
    this.pulse = Math.max(this.pulse, 0.35);
    this.burst(o.x, o.y, 10 + this.mult * 2, REWARD, 90);
    this.events.emit('orb', this.mult);

    const need = 12 + this.level * 4;
    if (this.levelOrbs >= need) this.levelUp();
  }

  private setCombo(value: number): void {
    const clamped = clamp(value, 0, CONFIG.comboMax);
    if (clamped > this.combo) this.events.emit('comboUp', clamped);
    this.combo = clamped;
    this.mult = Math.max(1, clamped);
    this.comboTimer = CONFIG.comboDecaySec;
    if (this.combo >= CONFIG.feverCombo && this.feverLeft <= 0) {
      this.feverLeft = CONFIG.feverSec;
      this.events.emit('feverOn', 0);
    }
  }

  private levelUp(): void {
    this.level++;
    this.levelOrbs = 0;
    this.angularSpeed = Math.min(3.4, this.angularSpeed * 1.06);
    this.pulse = 1;
    this.zoom = 0.12;
    this.events.emit('levelUp', this.level);
  }

  private updateAsteroids(dt: number): void {
    const list = this.asteroids.items;
    for (let i = 0; i < list.length; i++) {
      const a = list[i];
      if (!a || !a.active) continue;
      a.timer -= dt;
      if (a.timer <= 0) { a.active = false; continue; }
      a.x += a.vx * dt;
      a.y += a.vy * dt;
      a.rot += a.spin * dt;

      const dx = a.x - this.px;
      const dy = a.y - this.py;
      const dist = Math.sqrt(dx * dx + dy * dy) || 1;

      if (dist < a.size + 10) {
        a.active = false;
        if (this.shieldTimer > 0) {
          this.shieldTimer = 0;
          this.shake = 3.5;
          this.burst(a.x, a.y, 22, SHIELD_C, 150);
          this.events.emit('shieldBreak', 0);
        } else if (this.feverActive()) {
          this.burst(a.x, a.y, 16, ACCENT, 120);
        } else {
          this.die();
          return;
        }
      } else if (dist < a.size + 30 && a.life === 0) {
        a.life = 1;
        this.nearMisses++;
        this.score += 5 * this.mult;
        this.slowLeft = CONFIG.nearMissMs / 1000;
        this.events.emit('nearMiss', this.nearMisses);
      }
    }
  }

  private updatePowerups(dt: number): void {
    const list = this.powerups.items;
    for (let i = 0; i < list.length; i++) {
      const p = list[i];
      if (!p || !p.active) continue;
      p.timer -= dt;
      if (p.timer <= 0) { p.active = false; continue; }
      p.rot += p.spin * dt;
      const dx = p.x - this.px;
      const dy = p.y - this.py;
      if (dx * dx + dy * dy < (p.size + 14) * (p.size + 14)) {
        p.active = false;
        if (p.seed < 0.5) {
          this.shieldTimer = 8;
          this.events.emit('shieldOn', 0);
          this.burst(p.x, p.y, 14, SHIELD_C, 80);
        } else {
          this.slowLeft = 2.4;
          this.events.emit('slowOn', 0);
          this.burst(p.x, p.y, 14, SLOW_C, 80);
        }
      }
    }
  }

  private updateParticles(dt: number): void {
    const list = this.particles.items;
    for (let i = 0; i < list.length; i++) {
      const p = list[i];
      if (!p || !p.active) continue;
      p.timer -= dt;
      if (p.timer <= 0) { p.active = false; continue; }
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      p.vx *= 0.96;
      p.vy *= 0.96;
      p.rot += p.spin * dt;
    }
  }

  private updateSpawns(dt: number): void {
    this.spawnTimer -= dt;
    if (this.spawnTimer <= 0) {
      this.spawnTimer = Math.max(0.24, 0.72 - this.level * 0.035);
      const o = this.orbs.spawn();
      if (o) {
        const orbit = this.rng.next() < 0.5 ? 0.62 : 1.0;
        const r = this.baseRadius * orbit;
        const a = this.rng.next() * TAU;
        o.x = this.centerX + Math.cos(a) * r;
        o.y = this.centerY + Math.sin(a) * r;
        o.vx = 0; o.vy = 0;
        o.size = 8; o.rot = 0; o.spin = 0.6;
        o.timer = 7; o.life = 7; o.seed = 0;
        o.r = REWARD.r; o.g = REWARD.g; o.b = REWARD.b;
      }
    }

    this.hazardTimer -= dt;
    if (this.mode === 'zen') return;
    if (this.hazardTimer <= 0) {
      this.hazardTimer = Math.max(0.7, 2.4 - this.level * 0.09);
      const a = this.asteroids.spawn();
      if (a) {
        const ang = this.rng.next() * TAU;
        const dist = this.baseRadius * 1.9;
        a.x = this.centerX + Math.cos(ang) * dist;
        a.y = this.centerY + Math.sin(ang) * dist;
        const targetR = this.baseRadius * (this.rng.next() < 0.5 ? 0.62 : 1.0);
        const tAng = this.rng.next() * TAU;
        const tx = this.centerX + Math.cos(tAng) * targetR;
        const ty = this.centerY + Math.sin(tAng) * targetR;
        const travel = Math.max(0.1, 1.0 + this.rng.next() * 0.5);
        a.vx = (tx - a.x) / travel;
        a.vy = (ty - a.y) / travel;
        a.size = 16 + this.rng.next() * 10;
        a.rot = this.rng.next() * TAU;
        a.spin = (this.rng.next() - 0.5) * 3.2;
        a.timer = 4.5; a.life = 0;
        a.seed = this.rng.next() * 100;
        a.r = DANGER.r; a.g = DANGER.g; a.b = DANGER.b;
      }
    }

    if (this.rng.next() < 0.0016) {
      const p = this.powerups.spawn();
      if (p) {
        const r = this.baseRadius * (this.rng.next() < 0.5 ? 0.62 : 1.0);
        const a = this.rng.next() * TAU;
        p.x = this.centerX + Math.cos(a) * r;
        p.y = this.centerY + Math.sin(a) * r;
        p.vx = 0; p.vy = 0; p.rot = 0; p.spin = 1.4;
        p.size = 11; p.timer = 8; p.life = 8;
        p.seed = this.rng.next();
        const c = p.seed < 0.5 ? SHIELD_C : SLOW_C;
        p.r = c.r; p.g = c.g; p.b = c.b;
      }
    }
  }

  private burst(x: number, y: number, count: number, color: { r: number; g: number; b: number }, speed: number): void {
    for (let i = 0; i < count; i++) {
      const p = this.particles.spawn();
      if (!p) return;
      const a = this.rng.next() * TAU;
      const v = speed * (0.35 + this.rng.next() * 0.65);
      p.x = x; p.y = y;
      p.vx = Math.cos(a) * v; p.vy = Math.sin(a) * v;
      p.rot = 0; p.spin = (this.rng.next() - 0.5) * 8;
      p.size = 3 + this.rng.next() * 4;
      p.life = 0.3 + this.rng.next() * 0.4;
      p.timer = p.life;
      p.r = color.r; p.g = color.g; p.b = color.b;
    }
  }

  private die(): void {
    this.screen = 'gameover';
    this.crack = 1;
    this.shake = 4;
    this.isNewBest = this.score > this.best;
    if (this.isNewBest) {
      this.best = this.score;
      this.events.emit('newBest', this.best);
    }
    this.burst(this.px, this.py, 40, DANGER, 170);
    this.events.emit('death', this.score);
    this.sync();
  }

  private decayVisual(dt: number): void {
    if (this.pulse > 0) this.pulse = Math.max(0, this.pulse - dt * 2.2);
    if (this.shake > 0) this.shake = Math.max(0, this.shake - dt * 12);
    if (this.zoom > 0) this.zoom = Math.max(0, this.zoom - dt * 0.45);
    if (this.feverLeft > 0 && this.combo > CONFIG.comboMax) {
      this.combo = CONFIG.comboMax;
    }
  }

  private sync(): GameSnapshot {
    const s = this.snapshot;
    s.screen = this.screen;
    s.score = this.score;
    s.best = this.best;
    s.combo = this.combo;
    s.mult = this.mult;
    s.level = this.level;
    s.orbs = this.orbsCollected;
    s.shields = this.shieldTimer > 0 ? 1 : 0;
    s.slowLeft = this.slowLeft;
    s.feverLeft = this.feverLeft;
    s.nearMisses = this.nearMisses;
    s.elapsed = this.elapsed;
    s.isNewBest = this.isNewBest;
    s.mode = this.mode;
    return s;
  }

  /** Заполняет инстанс-буфер рендера. Ни одного new, без замыканий. */
  fillInstances(out: Float32Array, max: number): number {
    let n = 0;
    const list = this.orbs.items;
    for (let i = 0; i < list.length; i++) {
      const o = list[i];
      if (o && o.active) n = Game.write(out, n, max, o.x, o.y, o.size, o.rot, o.r, o.g, o.b, 1, KIND.ORB, o.seed, 0.5, 0);
    }
    const ast = this.asteroids.items;
    for (let i = 0; i < ast.length; i++) {
      const a = ast[i];
      if (a && a.active) n = Game.write(out, n, max, a.x, a.y, a.size, a.rot, a.r, a.g, a.b, 1, KIND.ASTEROID, a.seed, 0.4, 0);
    }
    const pw = this.powerups.items;
    for (let i = 0; i < pw.length; i++) {
      const p = pw[i];
      if (p && p.active) n = Game.write(out, n, max, p.x, p.y, p.size, p.rot, p.r, p.g, p.b, 1, p.seed < 0.5 ? KIND.SHIELD : KIND.SLOW, 0, 0.6, 0);
    }
    const pt = this.particles.items;
    for (let i = 0; i < pt.length; i++) {
      const p = pt[i];
      if (p && p.active) {
        const a = p.timer / p.life;
        n = Game.write(out, n, max, p.x, p.y, p.size * (0.5 + a * 0.5), p.rot, p.r, p.g, p.b, a, KIND.PARTICLE, 0, 0.3, 0);
      }
    }

    n = Game.write(out, n, max, this.px, this.py, 17, this.pRot,
      ACCENT.r, ACCENT.g, ACCENT.b, 1, KIND.PLAYER, 0, this.feverActive() ? 1 : 0.5, this.mult);
    for (let i = 0; i < this.combo; i++) {
      const a = this.pRot + (i - (this.combo - 1) / 2) * 0.28;
      const rr = 26;
      n = Game.write(out, n, max,
        this.px + Math.cos(a) * rr, this.py + Math.sin(a) * rr,
        3.5, 0, ACCENT.r, ACCENT.g, ACCENT.b, 0.9, KIND.PARTICLE, 0, 0.8, 0);
    }
    if (this.shieldTimer > 0) {
      n = Game.write(out, n, max, this.px, this.py, 30, 0,
        SHIELD_C.r, SHIELD_C.g, SHIELD_C.b, 0.8, KIND.SHIELD, 0, 1, 0);
    }
    return n;
  }

  private static write(out: Float32Array, n: number, max: number,
    x: number, y: number, size: number, rot: number,
    r: number, g: number, b: number, a: number,
    type: number, seed: number, glow: number, extra: number): number {
    if (n >= max) return n;
    const i = n * 12;
    out[i] = x; out[i + 1] = y; out[i + 2] = size; out[i + 3] = rot;
    out[i + 4] = r; out[i + 5] = g; out[i + 6] = b; out[i + 7] = a;
    out[i + 8] = type; out[i + 9] = seed; out[i + 10] = glow; out[i + 11] = extra;
    return n + 1;
  }

  /** Позиция кометы для HUD-привязки множителя. */
  getPlayerScreen(): { x: number; y: number } { return { x: this.px, y: this.py }; }
  getCenter(): { x: number; y: number } { return { x: this.centerX, y: this.centerY }; }
  getRadius(): number { return this.baseRadius; }
  getAngle(): number { return this.angle; }
  getWave(): number { return this.wavePhase; }
  getOrbitCount(): number { return this.orbitRadii.length; }

  /** Тест на «честность» спавна: расстояние до игрока. Используется QA-скриптом. */
  debugMinSpawnDistance(): number {
    let min = Infinity;
    const list = this.asteroids.items;
    for (let i = 0; i < list.length; i++) {
      const a = list[i];
      if (a && a.active) {
        const d = Math.hypot(a.x - this.px, a.y - this.py);
        if (d < min) min = d;
      }
    }
    return min;
  }
}
