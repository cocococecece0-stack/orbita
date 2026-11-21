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
  /** Битовая маска активных эффектов. */
  effects: number;
  bossHp: number;
  bossCharge: number;
  stormWarn: number;
  bossKills: number;
  powerupsTaken: number;
  wormholesUsed: number;
}

const FX = {
  SHIELD: 1, SLOW: 2, MAGNET: 4, DOUBLE: 8,
  PHASE: 16, FREEZE: 32, SECOND: 64,
} as const;

const ACCENT = { r: 1.0, g: 0.706, b: 0.329 };
const DANGER = { r: 1.0, g: 0.361, b: 0.361 };
const REWARD = { r: 0.949, g: 0.886, b: 0.722 };
const SHIELD_C = { r: 0.61, g: 0.95, b: 0.78 };
const SLOW_C = { r: 0.85, g: 0.78, b: 1.0 };
const MAGNET_C = { r: 0.43, g: 0.83, b: 1.0 };
const DOUBLE_C = { r: 1.0, g: 0.82, b: 0.4 };
const PHASE_C = { r: 0.71, g: 0.61, b: 1.0 };
const FREEZE_C = { r: 0.62, g: 0.91, b: 1.0 };
const SECOND_C = { r: 1.0, g: 0.54, b: 0.54 };

const GHOST_LEN = 512;
const GHOST_STEP = 0.2;

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
  private effectTypeA = 0;
  private effectTimeA = 0;
  private effectTypeB = 0;
  private effectTimeB = 0;
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
  private reducedMotion = false;
  private fxScale = 1;

  private boss: Entity | null = null;
  private bossAngle = 0;
  private bossCharge = 0;
  private eventTimer = 45;
  private stormTimer = 0;
  private stormPending = false;
  private wormTimer = 12;
  private bossKills = 0;
  private powerupsTaken = 0;
  private wormholesUsed = 0;

  private ghost = new Float32Array(GHOST_LEN);
  private ghostCount = 0;
  private ghostTick = 0;
  private bestGhost = new Float32Array(GHOST_LEN);
  private bestGhostCount = 0;

  readonly orbs = new Pool<Entity>(CONFIG.maxOrbs, makeEntity);
  readonly asteroids = new Pool<Entity>(CONFIG.maxAsteroids, makeEntity);
  readonly powerups = new Pool<Entity>(CONFIG.maxPowerups, makeEntity);
  readonly particles = new Pool<Entity>(CONFIG.maxParticles, makeEntity);

  private visual = { pulse: 0, crack: 0, shake: 0, zoom: 0 };
  private readonly effectTypes = new Int32Array(7);

  private snapshot: GameSnapshot = {
    screen: 'menu', score: 0, best: 0, combo: 0, mult: 1, level: 1,
    orbs: 0, shields: 0, mode: 'classic', slowLeft: 0, feverLeft: 0,
    nearMisses: 0, elapsed: 0, isNewBest: false, effects: 0,
    bossHp: 0, bossCharge: 0, stormWarn: 0,
    bossKills: 0, powerupsTaken: 0, wormholesUsed: 0,
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
  setAccent(r: number, g: number, b: number): void {
    ACCENT.r = r; ACCENT.g = g; ACCENT.b = b;
  }
  setReducedMotion(value: boolean): void { this.reducedMotion = value; }
  setFxScale(value: number): void { this.fxScale = clamp(value, 0, 1); }

  /** Копия лучшей траектории для призрака (только чтение). */
  getBestGhost(): { data: Float32Array; count: number } {
    return { data: this.bestGhost, count: this.bestGhostCount };
  }
  setBestGhost(data: Float32Array, count: number): void {
    const n = Math.min(count, GHOST_LEN);
    this.bestGhost.set(data.subarray(0, n));
    this.bestGhostCount = n;
  }

  getBest(): number { return this.best; }
  getMode(): Mode { return this.mode; }
  getScreen(): Screen { return this.screen; }
  getSnapshot(): GameSnapshot { return this.sync(); }

  getPlanetVisual(): { pulse: number; crack: number; shake: number; zoom: number } {
    this.visual.pulse = this.pulse;
    this.visual.crack = this.crack;
    this.visual.shake = this.reducedMotion ? 0 : this.shake;
    this.visual.zoom = this.reducedMotion ? 0 : this.zoom;
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
    this.effectTypeA = 0; this.effectTimeA = 0;
    this.effectTypeB = 0; this.effectTimeB = 0;
    this.elapsed = 0;
    this.isNewBest = false;
    this.hitStop = 0;
    this.crack = 0;
    this.pulse = 1;
    this.shake = 0;
    this.zoom = this.reducedMotion ? 0 : 0.08;
    this.angle = -Math.PI / 2;
    this.angularSpeed = mode === 'hardcore' ? 2.1 : 1.35;
    this.orbitIndex = mode === 'hardcore' ? 0 : 1;
    this.spawnTimer = 0;
    this.hazardTimer = 1.2;
    this.boss = null;
    this.bossCharge = 0;
    this.eventTimer = 45;
    this.stormTimer = 0;
    this.stormPending = false;
    this.wormTimer = 12;
    this.bossKills = 0;
    this.powerupsTaken = 0;
    this.wormholesUsed = 0;
    this.ghostCount = 0;
    this.ghostTick = 0;
    this.screen = 'playing';
    this.snapshot.mode = mode;
    this.updatePlayerPosition();
    this.sync();
  }

  toMenu(): void { this.screen = 'menu'; this.sync(); }
  pause(): void { if (this.screen === 'playing') { this.screen = 'paused'; this.sync(); } }
  resume(): void { if (this.screen === 'paused') { this.screen = 'playing'; this.sync(); } }

  tap(): void {
    if (this.screen !== 'playing') return;
    if (this.mode === 'hardcore') {
      if (!this.reducedMotion) this.zoom = Math.max(this.zoom, 0.02);
      return;
    }
    this.orbitIndex = this.orbitIndex === 0 ? 1 : 0;
    if (!this.reducedMotion) this.zoom = Math.max(this.zoom, 0.035);
    this.burst(this.px, this.py, 5, ACCENT, 34);
  }

  setDiving(active: boolean): void {
    if (this.screen !== 'playing' || this.mode === 'hardcore') return;
    this.orbitIndex = active ? 0 : 1;
  }

  /** Явная установка орбиты для клавиатуры. */
  setOrbit(index: 0 | 1): void {
    if (this.screen !== 'playing' || this.mode === 'hardcore') return;
    this.orbitIndex = index;
    if (!this.reducedMotion) this.zoom = Math.max(this.zoom, 0.02);
  }

  /** Траектория текущего забега (только для сохранения рекорда). */
  getCurrentGhost(): { data: Float32Array; count: number } {
    return { data: this.ghost, count: this.ghostCount };
  }

  update(dt: number): void {
    if (this.screen !== 'playing') { this.decayVisual(dt); return; }
    if (this.hitStop > 0) {
      this.hitStop -= dt;
      this.decayVisual(dt);
      return;
    }

    const slow = this.slowLeft > 0 ? CONFIG.nearMissScale : 1;
    const frozen = this.hasEffect(FX.FREEZE);
    const sdt = dt * slow;
    this.elapsed += dt;
    this.wavePhase += sdt;
    this.tickEffects(dt);

    if (this.comboTimer > 0) {
      this.comboTimer -= dt;
      if (this.comboTimer <= 0 && !this.feverActive()) this.setCombo(0);
    }

    const speed = this.angularSpeed + this.level * 0.035 + (this.feverActive() ? 0.2 : 0);
    this.angle = (this.angle + speed * sdt) % TAU;
    this.updatePlayerPosition();
    this.pRot = this.angle + Math.PI / 2;
    this.recordGhost(dt);

    if (this.rng.next() < 0.5 * this.fxScale) {
      const t = this.particles.spawn();
      if (t) {
        t.kind = 0;
        t.x = this.px; t.y = this.py;
        t.vx = -Math.sin(this.angle) * 8; t.vy = Math.cos(this.angle) * 8;
        t.rot = 0; t.spin = 0; t.size = 9; t.life = 0.42; t.timer = 0.42;
        t.r = ACCENT.r; t.g = ACCENT.g; t.b = ACCENT.b;
      }
    }

    this.updateBoss(dt);
    this.updateOrbs(sdt, dt);
    this.updateAsteroids(frozen ? 0 : sdt);
    this.updatePowerups(sdt);
    this.updateParticles(sdt);
    this.updateSpawns(dt);
    this.updateEvents(dt);

    this.decayVisual(dt);
    this.sync();
  }

  private feverActive(): boolean { return this.feverLeft > 0; }

  private updatePlayerPosition(): void {
    const r = this.baseRadius * (this.orbitRadii[this.orbitIndex] ?? 1);
    this.px = this.centerX + Math.cos(this.angle) * r;
    this.py = this.centerY + Math.sin(this.angle) * r;
  }

  private recordGhost(dt: number): void {
    this.ghostTick += dt;
    if (this.ghostTick < GHOST_STEP || this.ghostCount >= GHOST_LEN) return;
    this.ghostTick = 0;
    this.ghost[this.ghostCount++] = this.angle;
  }

  // --- эффекты -------------------------------------------------------------

  private activeCount(): number {
    let n = 0;
    if (this.shieldTimer > 0) n++;
    if (this.slowLeft > 0) n++;
    if (this.effectTypeA !== 0) n++;
    if (this.effectTypeB !== 0) n++;
    return n;
  }

  hasEffect(type: number): boolean {
    if (type === FX.SHIELD) return this.shieldTimer > 0;
    if (type === FX.SLOW) return this.slowLeft > 0;
    return this.effectTypeA === type || this.effectTypeB === type;
  }

  private consumeEffect(type: number): void {
    if (type === FX.SHIELD) this.shieldTimer = 0;
    else if (type === FX.SLOW) this.slowLeft = 0;
    else if (this.effectTypeA === type) { this.effectTypeA = 0; this.effectTimeA = 0; }
    else if (this.effectTypeB === type) { this.effectTypeB = 0; this.effectTimeB = 0; }
  }

  private grantEffect(type: number, seconds: number): boolean {
    if (type !== FX.MAGNET && type !== FX.DOUBLE && type !== FX.PHASE
        && type !== FX.FREEZE && type !== FX.SECOND) return false;
    if (this.activeCount() >= 2) return false;
    if (this.effectTypeA === 0) { this.effectTypeA = type; this.effectTimeA = seconds; return true; }
    if (this.effectTypeB === 0) { this.effectTypeB = type; this.effectTimeB = seconds; return true; }
    return false;
  }

  private tickEffects(dt: number): void {
    if (this.shieldTimer > 0) this.shieldTimer = Math.max(0, this.shieldTimer - dt);
    if (this.slowLeft > 0) this.slowLeft = Math.max(0, this.slowLeft - dt);
    if (this.effectTypeA !== 0) {
      this.effectTimeA -= dt;
      if (this.effectTimeA <= 0) this.effectTypeA = 0;
    }
    if (this.effectTypeB !== 0) {
      this.effectTimeB -= dt;
      if (this.effectTimeB <= 0) this.effectTypeB = 0;
    }
  }

  private effectMask(): number {
    let m = 0;
    if (this.hasEffect(FX.SHIELD)) m |= FX.SHIELD;
    if (this.hasEffect(FX.SLOW)) m |= FX.SLOW;
    if (this.hasEffect(FX.MAGNET)) m |= FX.MAGNET;
    if (this.hasEffect(FX.DOUBLE)) m |= FX.DOUBLE;
    if (this.hasEffect(FX.PHASE)) m |= FX.PHASE;
    if (this.hasEffect(FX.FREEZE)) m |= FX.FREEZE;
    if (this.hasEffect(FX.SECOND)) m |= FX.SECOND;
    return m;
  }

  // --- орбы и комбо --------------------------------------------------------

  private updateOrbs(dt: number, rawDt: number): void {
    const magnet = this.hasEffect(FX.MAGNET);
    const list = this.orbs.items;
    for (let i = 0; i < list.length; i++) {
      const o = list[i];
      if (!o || !o.active) continue;
      o.timer -= rawDt;
      if (o.timer <= 0) { o.active = false; continue; }
      o.rot += o.spin * dt;
      const dx = o.x - this.px;
      const dy = o.y - this.py;
      const d2 = dx * dx + dy * dy;
      if (magnet && d2 < 150 * 150 && d2 > 1) {
        const d = Math.sqrt(d2);
        o.x -= dx / d * 170 * dt;
        o.y -= dy / d * 170 * dt;
      }
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
    if (this.boss) this.bossCharge++;
    let gain = this.mult * (this.feverActive() ? 2 : 1) * (this.mode === 'hardcore' ? 2 : 1);
    if (this.hasEffect(FX.DOUBLE)) gain *= 2;
    this.score += 10 * gain;
    this.hitStop = CONFIG.hitStopMs / 1000;
    if (!this.reducedMotion) this.zoom = Math.max(this.zoom, 0.06 + this.mult * 0.008);
    this.pulse = Math.max(this.pulse, 0.35);
    this.burst(o.x, o.y, Math.round((10 + this.mult * 2) * this.fxScale), REWARD, 90);
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
    if (!this.reducedMotion) this.zoom = 0.12;
    this.events.emit('levelUp', this.level);
    if (this.level % 5 === 0) this.spawnBoss();
  }

  // --- босс ----------------------------------------------------------------

  private spawnBoss(): void {
    if (this.boss || this.mode === 'zen') return;
    const a = this.asteroids.spawn();
    if (!a) return;
    this.bossAngle = this.angle + Math.PI;
    const r = this.baseRadius * 1.35;
    a.kind = 1;
    a.x = this.centerX + Math.cos(this.bossAngle) * r;
    a.y = this.centerY + Math.sin(this.bossAngle) * r;
    a.vx = 0; a.vy = 0;
    a.size = 54;
    a.rot = 0;
    a.spin = 0.55;
    a.timer = 20;
    a.life = 3;
    a.seed = 7;
    a.r = DANGER.r; a.g = DANGER.g; a.b = DANGER.b;
    this.boss = a;
    this.bossCharge = 0;
    this.events.emit('bossSpawn', this.level);
  }

  private updateBoss(dt: number): void {
    const b = this.boss;
    if (!b || !b.active) { this.boss = null; return; }
    b.timer -= dt;
    b.rot += b.spin * dt;
    this.bossAngle = (this.bossAngle + 0.28 * dt) % TAU;
    const r = this.baseRadius * 1.35;
    b.x = this.centerX + Math.cos(this.bossAngle) * r;
    b.y = this.centerY + Math.sin(this.bossAngle) * r;

    if (this.bossCharge >= 3 && Math.abs(angDiff(this.angle, this.bossAngle)) < 0.3) {
      this.bossCharge = 0;
      b.life--;
      this.burst(b.x, b.y, 18, DOUBLE_C, 130);
      if (!this.reducedMotion) this.shake = 3;
      if (b.life <= 0) {
        b.active = false;
        this.boss = null;
        this.score += 500 * this.level;
        this.bossKills++;
        this.pulse = 1;
        this.events.emit('bossDown', this.level);
      } else {
        this.events.emit('bossHit', b.life);
      }
      return;
    }

    if (b.timer <= 0) {
      b.active = false;
      this.boss = null;
      this.events.emit('bossEscape', this.level);
    }
  }

  // --- астероиды -----------------------------------------------------------

  private updateAsteroids(dt: number): void {
    const list = this.asteroids.items;
    for (let i = 0; i < list.length; i++) {
      const a = list[i];
      if (!a || !a.active || a.kind === 1) continue;
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
        if (this.hasEffect(FX.PHASE)) {
          this.consumeEffect(FX.PHASE);
          this.burst(a.x, a.y, 20, PHASE_C, 140);
          this.events.emit('phase', 0);
        } else if (this.shieldTimer > 0) {
          this.consumeEffect(FX.SHIELD);
          if (!this.reducedMotion) this.shake = 3.5;
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
        if (this.activeCount() < 2) this.slowLeft = CONFIG.nearMissMs / 1000;
        this.events.emit('nearMiss', this.nearMisses);
      }
    }
  }

  // --- бонусы --------------------------------------------------------------

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
      if (dx * dx + dy * dy >= (p.size + 14) * (p.size + 14)) continue;
      p.active = false;
      this.powerupsTaken++;

      if (p.kind === 2) {
        this.teleportWormhole(list, p);
        continue;
      }

      switch (p.kind) {
        case 1:
          if (this.activeCount() < 2) {
            this.shieldTimer = 8;
            this.events.emit('shieldOn', 0);
            this.burst(p.x, p.y, 14, SHIELD_C, 80);
          }
          break;
        case 3:
          if (this.grantEffect(FX.MAGNET, 8)) {
            this.events.emit('magnet', 0);
            this.burst(p.x, p.y, 14, MAGNET_C, 80);
          }
          break;
        case 4:
          if (this.grantEffect(FX.DOUBLE, 10)) {
            this.events.emit('double', 0);
            this.burst(p.x, p.y, 14, DOUBLE_C, 80);
          }
          break;
        case 5:
          if (this.grantEffect(FX.PHASE, 12)) {
            this.events.emit('phase', 0);
            this.burst(p.x, p.y, 14, PHASE_C, 80);
          }
          break;
        case 6:
          if (this.grantEffect(FX.FREEZE, 5)) {
            this.events.emit('freeze', 0);
            this.burst(p.x, p.y, 14, FREEZE_C, 80);
          }
          break;
        case 7:
          if (this.grantEffect(FX.SECOND, 60)) {
            this.events.emit('secondChance', 0);
            this.burst(p.x, p.y, 14, SECOND_C, 80);
          }
          break;
        default:
          if (this.activeCount() < 2) {
            this.slowLeft = 2.4;
            this.events.emit('slowOn', 0);
            this.burst(p.x, p.y, 14, SLOW_C, 80);
          }
      }
    }
  }

  private teleportWormhole(list: Entity[], from: Entity): void {
    for (let i = 0; i < list.length; i++) {
      const other = list[i];
      if (!other || other === from || !other.active || other.kind !== 2) continue;
      this.angle = Math.atan2(other.y - this.centerY, other.x - this.centerX);
      this.wormholesUsed++;
      other.active = false;
      this.burst(from.x, from.y, 16, PHASE_C, 120);
      this.burst(other.x, other.y, 16, PHASE_C, 120);
      this.updatePlayerPosition();
      this.pRot = this.angle + Math.PI / 2;
      this.events.emit('wormhole', 0);
      return;
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

  // --- спавн-режиссёр ------------------------------------------------------

  private updateSpawns(dt: number): void {
    this.spawnTimer -= dt;
    if (this.spawnTimer <= 0) {
      this.spawnTimer = Math.max(0.24, 0.72 - this.level * 0.035);
      const o = this.orbs.spawn();
      if (o) {
        const orbit = this.rng.next() < 0.5 ? 0.62 : 1.0;
        const r = this.baseRadius * orbit;
        const a = this.rng.next() * TAU;
        o.kind = 0;
        o.x = this.centerX + Math.cos(a) * r;
        o.y = this.centerY + Math.sin(a) * r;
        o.vx = 0; o.vy = 0;
        o.size = 8; o.rot = 0; o.spin = 0.6;
        o.timer = 7; o.life = 7; o.seed = 0;
        o.r = REWARD.r; o.g = REWARD.g; o.b = REWARD.b;
      }
    }

    if (this.mode === 'zen') return;

    this.hazardTimer -= dt;
    if (this.hazardTimer <= 0) {
      this.hazardTimer = Math.max(0.7, 2.4 - this.level * 0.09);
      this.spawnAsteroid(this.rng.next() * TAU, 1.9, 1.0);
    }

    if (this.rng.next() < 0.0032) this.spawnPowerup();

    this.wormTimer -= dt;
    if (this.wormTimer <= 0 && this.level >= 4) {
      this.wormTimer = 22;
      this.spawnWormholes();
    }
  }

  private spawnAsteroid(angle: number, fromScale: number, toScale: number): void {
    const a = this.asteroids.spawn();
    if (!a) return;
    const dist = this.baseRadius * fromScale;
    a.kind = 0;
    a.x = this.centerX + Math.cos(angle) * dist;
    a.y = this.centerY + Math.sin(angle) * dist;
    const targetR = this.baseRadius * (this.rng.next() < 0.5 ? 0.62 : toScale);
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

  private spawnPowerup(): void {
    const p = this.powerups.spawn();
    if (!p) return;
    const r = this.baseRadius * (this.rng.next() < 0.5 ? 0.62 : 1.0);
    const a = this.rng.next() * TAU;
    const v = this.rng.next();
    p.kind = v < 0.20 ? 1 : v < 0.36 ? 2 : v < 0.52 ? 3
      : v < 0.68 ? 4 : v < 0.80 ? 5 : v < 0.91 ? 6 : 7;
    p.x = this.centerX + Math.cos(a) * r;
    p.y = this.centerY + Math.sin(a) * r;
    p.vx = 0; p.vy = 0; p.rot = 0; p.spin = 1.4;
    p.size = 11; p.timer = 8; p.life = 8;
    p.seed = v;
    const c = p.kind === 1 ? SHIELD_C : p.kind === 2 ? SLOW_C : p.kind === 3 ? MAGNET_C
      : p.kind === 4 ? DOUBLE_C : p.kind === 5 ? PHASE_C : p.kind === 6 ? FREEZE_C : SECOND_C;
    p.r = c.r; p.g = c.g; p.b = c.b;
  }

  private spawnWormholes(): void {
    const a = this.rng.next() * TAU;
    for (let i = 0; i < 2; i++) {
      const p = this.powerups.spawn();
      if (!p) return;
      const ang = a + i * Math.PI;
      const r = this.baseRadius * 1.15;
      p.kind = 2;
      p.x = this.centerX + Math.cos(ang) * r;
      p.y = this.centerY + Math.sin(ang) * r;
      p.vx = 0; p.vy = 0; p.rot = 0; p.spin = i === 0 ? 1.6 : -1.6;
      p.size = 16; p.timer = 8; p.life = 8; p.seed = i;
      p.r = PHASE_C.r; p.g = PHASE_C.g; p.b = PHASE_C.b;
    }
    this.events.emit('wormholeWarn', 0);
  }

  // --- события -------------------------------------------------------------

  private updateEvents(dt: number): void {
    if (this.mode === 'zen') return;
    this.eventTimer -= dt;
    if (this.eventTimer <= 0 && !this.stormPending) {
      this.eventTimer = 45;
      this.stormPending = true;
      this.stormTimer = 0.4;
      this.events.emit('stormWarn', 0);
    }
    if (this.stormPending) {
      this.stormTimer -= dt;
      if (this.stormTimer <= 0) {
        this.stormPending = false;
        this.spawnStorm();
      }
    }
  }

  private spawnStorm(): void {
    const base = this.rng.next() * TAU;
    for (let i = 0; i < 6; i++) {
      const ang = base + i * (TAU / 6);
      // Честное окно: сектор рядом с игроком остаётся свободным.
      if (Math.abs(angDiff(ang, this.angle)) < 0.6) continue;
      this.spawnAsteroid(ang, 2.0, 1.0);
    }
    this.events.emit('storm', 0);
  }

  // --- частицы и смерть ----------------------------------------------------

  private burst(x: number, y: number, count: number,
                color: { r: number; g: number; b: number }, speed: number): void {
    const n = Math.max(1, Math.round(count * this.fxScale));
    for (let i = 0; i < n; i++) {
      const p = this.particles.spawn();
      if (!p) return;
      const a = this.rng.next() * TAU;
      const v = speed * (0.35 + this.rng.next() * 0.65);
      p.kind = 0;
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
    if (this.hasEffect(FX.SECOND)) {
      this.consumeEffect(FX.SECOND);
      this.shieldTimer = 3;
      const list = this.asteroids.items;
      for (let i = 0; i < list.length; i++) {
        const a = list[i];
        if (a && a.active && a.kind === 0
            && Math.hypot(a.x - this.px, a.y - this.py) < this.baseRadius * 0.8) {
          a.active = false;
        }
      }
      this.pulse = 1;
      this.events.emit('secondChance', 0);
      return;
    }

    this.screen = 'gameover';
    this.crack = 1;
    this.shake = this.reducedMotion ? 0 : 4;
    this.isNewBest = this.score > this.best;
    if (this.isNewBest) {
      this.best = this.score;
      this.bestGhost.set(this.ghost.subarray(0, this.ghostCount));
      this.bestGhostCount = this.ghostCount;
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
    s.effects = this.effectMask();
    s.bossHp = this.boss ? this.boss.life : 0;
    s.bossCharge = this.bossCharge;
    s.stormWarn = this.stormPending ? 1 - this.stormTimer / 0.4 : 0;
    s.bossKills = this.bossKills;
    s.powerupsTaken = this.powerupsTaken;
    s.wormholesUsed = this.wormholesUsed;
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
      if (!a || !a.active) continue;
      n = Game.write(out, n, max, a.x, a.y, a.size, a.rot, a.r, a.g, a.b, 1,
        a.kind === 1 ? KIND.ASTEROID : KIND.ASTEROID, a.seed, a.kind === 1 ? 1 : 0.4, 0);
      if (a.kind === 1) {
        for (let c = 0; c < a.life; c++) {
          const ang = a.rot + c * (TAU / 3);
          n = Game.write(out, n, max,
            a.x + Math.cos(ang) * a.size * 0.55,
            a.y + Math.sin(ang) * a.size * 0.55,
            9, 0, DOUBLE_C.r, DOUBLE_C.g, DOUBLE_C.b, 1, KIND.ORB, 0, 1, 0);
        }
      }
    }
    const pw = this.powerups.items;
    for (let i = 0; i < pw.length; i++) {
      const p = pw[i];
      if (!p || !p.active) continue;
      const type = p.kind === 1 ? KIND.SHIELD : p.kind === 2 ? KIND.SLOW
        : p.kind === 3 ? KIND.MAGNET : p.kind === 4 ? KIND.DOUBLE
        : p.kind === 5 ? KIND.PHASE : p.kind === 6 ? KIND.FREEZE
        : p.kind === 7 ? KIND.SECOND : KIND.WORMHOLE;
      n = Game.write(out, n, max, p.x, p.y, p.size, p.rot, p.r, p.g, p.b, 1, type, p.seed, 0.7, 0);
    }
    const pt = this.particles.items;
    for (let i = 0; i < pt.length; i++) {
      const p = pt[i];
      if (p && p.active) {
        const a = p.timer / p.life;
        n = Game.write(out, n, max, p.x, p.y, p.size * (0.5 + a * 0.5), p.rot,
          p.r, p.g, p.b, a, KIND.PARTICLE, 0, 0.3, 0);
      }
    }

    // Призрак лучшего забега.
    if (this.bestGhostCount > 1) {
      const r = this.baseRadius;
      for (let i = 0; i < this.bestGhostCount; i += 8) {
        const a = this.bestGhost[i] ?? 0;
        n = Game.write(out, n, max,
          this.centerX + Math.cos(a) * r, this.centerY + Math.sin(a) * r,
          12, a + Math.PI / 2, ACCENT.r, ACCENT.g, ACCENT.b, 0.22,
          KIND.PLAYER, 0, 0.1, 0);
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

    // Спутники активных эффектов вокруг кометы.
    const mask = this.effectMask();
    const effects = this.effectTypes;
    let count = 0;
    if (mask & FX.SHIELD) effects[count++] = KIND.SHIELD;
    if (mask & FX.SLOW) effects[count++] = KIND.SLOW;
    if (mask & FX.MAGNET) effects[count++] = KIND.MAGNET;
    if (mask & FX.DOUBLE) effects[count++] = KIND.DOUBLE;
    if (mask & FX.PHASE) effects[count++] = KIND.PHASE;
    if (mask & FX.FREEZE) effects[count++] = KIND.FREEZE;
    if (mask & FX.SECOND) effects[count++] = KIND.SECOND;
    for (let i = 0; i < count; i++) {
      const type = effects[i] ?? 0;
      const a = this.elapsed * 2.2 + i * (TAU / count);
      const c = this.effectColor(type);
      n = Game.write(out, n, max,
        this.px + Math.cos(a) * 34, this.py + Math.sin(a) * 34,
        7, a, c.r, c.g, c.b, 0.95, type, 0, 0.9, 0);
    }
    return n;
  }

  private effectColor(type: number): { r: number; g: number; b: number } {
    if (type === KIND.SHIELD) return SHIELD_C;
    if (type === KIND.SLOW) return SLOW_C;
    if (type === KIND.MAGNET) return MAGNET_C;
    if (type === KIND.DOUBLE) return DOUBLE_C;
    if (type === KIND.PHASE) return PHASE_C;
    if (type === KIND.FREEZE) return FREEZE_C;
    return SECOND_C;
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

  getPlayerScreen(): { x: number; y: number } { return { x: this.px, y: this.py }; }
  getCenter(): { x: number; y: number } { return { x: this.centerX, y: this.centerY }; }
  getRadius(): number { return this.baseRadius; }
  getAngle(): number { return this.angle; }
  getWave(): number { return this.wavePhase; }
  getOrbitCount(): number { return this.orbitRadii.length; }

  debugMinSpawnDistance(): number {
    let min = Infinity;
    const list = this.asteroids.items;
    for (let i = 0; i < list.length; i++) {
      const a = list[i];
      if (a && a.active && a.kind === 0) {
        const d = Math.hypot(a.x - this.px, a.y - this.py);
        if (d < min) min = d;
      }
    }
    return min;
  }
}
