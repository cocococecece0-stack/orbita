import { decodeTransfer, encodeTransfer } from './TransferCode';
import type { Locale, Mode } from '../core/Config';
import { ACHIEVEMENTS, type RunContext } from '../game/Achievements';

export interface SaveDataV2 {
  version: 2;
  best: number;
  bestByMode: Record<string, number>;
  bestSeed: number;
  currency: number;
  totalScore: number;
  runs: number;
  totalOrbs: number;
  totalPlaySec: number;
  maxCombo: number;
  achievements: string[];
  unlockedSkins: string[];
  selectedSkin: string;
  unlockedTrails: string[];
  selectedTrail: string;
  dailyDate: string;
  settings: {
    sound: boolean;
    music: boolean;
    haptics: boolean;
    fx: number;
    palette: 'default' | 'protan' | 'deutan' | 'tritan';
    reducedMotion: boolean;
    locale: Locale;
    lowFps: boolean;
  };
}

const KEY = 'orbita.save.v2';
const LEGACY = ['best', 'orbita_best', 'bestScore', 'haptics', 'muted'];

export const SKINS = [
  { id: 'ember', name: 'УГОЛЬ', cost: 0, accent: '#FFB454', gl: [1, 0.706, 0.329] },
  { id: 'ghost', name: 'ПРИЗРАК', cost: 120, accent: '#EDE3D2', gl: [0.929, 0.890, 0.824] },
  { id: 'solar', name: 'СОЛНЦЕ', cost: 320, accent: '#FFD166', gl: [1, 0.820, 0.400] },
  { id: 'void', name: 'ПУСТОТА', cost: 640, accent: '#FF9A8A', gl: [1, 0.604, 0.541] },
] as const;

export const TRAILS = [
  { id: 'dither', name: 'ДИЗЗЕРИНГ', cost: 0 },
  { id: 'spark', name: 'ИСКРЫ', cost: 100 },
  { id: 'plasma', name: 'ПЛАЗМА', cost: 250 },
  { id: 'ash', name: 'ПЕПЕЛ', cost: 400 },
] as const;

function defaults(): SaveDataV2 {
  return {
    version: 2,
    best: 0,
    bestByMode: {},
    bestSeed: 0,
    currency: 0,
    totalScore: 0,
    runs: 0,
    totalOrbs: 0,
    totalPlaySec: 0,
    maxCombo: 0,
    achievements: [],
    unlockedSkins: ['ember'],
    selectedSkin: 'ember',
    unlockedTrails: ['dither'],
    selectedTrail: 'dither',
    dailyDate: '',
    settings: {
      sound: true, music: true, haptics: true, fx: 1,
      palette: 'default', reducedMotion: false, locale: 'ru', lowFps: false,
    },
  };
}

/** Строго локальное сохранение: JSON v2 + версионирование + миграция v1. */
export class Save {
  private data: SaveDataV2 = defaults();

  load(): SaveDataV2 {
    try {
      const raw = localStorage.getItem(KEY);
      if (raw) {
        const p = JSON.parse(raw) as Partial<SaveDataV2>;
        if (p.version === 2) {
          const d = defaults();
          this.data = {
            ...d, ...p,
            bestByMode: { ...p.bestByMode },
            unlockedSkins: p.unlockedSkins?.length ? p.unlockedSkins : d.unlockedSkins,
            unlockedTrails: p.unlockedTrails?.length ? p.unlockedTrails : d.unlockedTrails,
            settings: { ...d.settings, ...p.settings },
          };
          return this.data;
        }
      }
      for (const key of LEGACY) {
        const v = localStorage.getItem(key);
        if (v === null) continue;
        if (key.includes('best')) {
          const n = Number(v);
          if (Number.isFinite(n)) this.data.best = Math.max(this.data.best, n);
        }
        if (key === 'muted') this.data.settings.sound = v !== 'true';
        if (key === 'haptics') this.data.settings.haptics = v !== 'false';
      }
    } catch {
      this.data = defaults();
    }
    return this.data;
  }

  get(): SaveDataV2 { return this.data; }

  save(): void {
    try { localStorage.setItem(KEY, JSON.stringify(this.data)); } catch { /* квота */ }
  }

  setLocale(locale: Locale): void { this.data.settings.locale = locale; this.save(); }

  recordRun(
    score: number, orbs: number, sec: number, combo: number,
    mode: Mode, isNewBest: boolean, seed: number, context: RunContext,
  ): string[] {
    const d = this.data;
    if (isNewBest) {
      d.best = score;
      d.bestSeed = seed;
    }
    d.bestByMode[mode] = Math.max(d.bestByMode[mode] ?? 0, score);
    d.runs++;
    d.totalScore += score;
    d.totalOrbs += orbs;
    d.totalPlaySec += Math.round(sec);
    d.maxCombo = Math.max(d.maxCombo, combo);
    d.currency += Math.floor(score / 10) + orbs;

    const earned: string[] = [];
    for (const a of ACHIEVEMENTS) {
      if (!d.achievements.includes(a.id) && a.check(context)) {
        d.achievements.push(a.id);
        earned.push(a.title);
        d.currency += 50;
      }
    }
    this.save();
    return earned;
  }

  buySkin(id: string, cost: number): boolean {
    if (this.data.unlockedSkins.includes(id) || this.data.currency < cost) return false;
    this.data.currency -= cost;
    this.data.unlockedSkins.push(id);
    this.save();
    return true;
  }

  selectSkin(id: string): void {
    if (!this.data.unlockedSkins.includes(id)) return;
    this.data.selectedSkin = id;
    this.save();
  }

  buyTrail(id: string, cost: number): boolean {
    if (this.data.unlockedTrails.includes(id) || this.data.currency < cost) return false;
    this.data.currency -= cost;
    this.data.unlockedTrails.push(id);
    this.save();
    return true;
  }

  selectTrail(id: string): void {
    if (!this.data.unlockedTrails.includes(id)) return;
    this.data.selectedTrail = id;
    this.save();
  }

  exportCode(): string { return encodeTransfer(this.data); }

  importCode(code: string): boolean {
    const parsed = decodeTransfer<SaveDataV2>(code);
    if (!parsed || parsed.version !== 2) return false;
    this.data = parsed;
    this.save();
    return true;
  }
}
