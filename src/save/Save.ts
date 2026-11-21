import { decodeTransfer, encodeTransfer } from './TransferCode';

export interface SaveDataV2 {
  version: 2;
  best: number;
  bestByMode: Record<string, number>;
  totalScore: number;
  runs: number;
  totalOrbs: number;
  totalPlaySec: number;
  maxCombo: number;
  achievements: string[];
  unlockedSkins: string[];
  settings: {
    sound: boolean;
    music: boolean;
    haptics: boolean;
    fx: number;
    palette: 'default' | 'protan' | 'deutan' | 'tritan';
    reducedMotion: boolean;
    locale: 'ru' | 'en';
  };
}

const KEY = 'orbita.save.v2';
const LEGACY_KEYS = ['best', 'orbita_best', 'bestScore', 'haptics', 'muted'];

function defaults(): SaveDataV2 {
  return {
    version: 2,
    best: 0,
    bestByMode: {},
    totalScore: 0,
    runs: 0,
    totalOrbs: 0,
    totalPlaySec: 0,
    maxCombo: 0,
    achievements: [],
    unlockedSkins: ['ember'],
    settings: {
      sound: true, music: true, haptics: true, fx: 1,
      palette: 'default', reducedMotion: false, locale: 'ru',
    },
  };
}

/** Локальное сохранение JSON v2. Никаких бэкендов и аккаунтов. */
export class Save {
  private data: SaveDataV2 = defaults();

  load(): SaveDataV2 {
    try {
      const raw = localStorage.getItem(KEY);
      if (raw) {
        const parsed = JSON.parse(raw) as Partial<SaveDataV2>;
        if (parsed.version === 2) {
          this.data = { ...defaults(), ...parsed, settings: { ...defaults().settings, ...parsed.settings } };
          return this.data;
        }
      }
      // Миграция v1: старые ключи из DataStore/UserDefaults, если доступны.
      for (const key of LEGACY_KEYS) {
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

  recordRun(score: number, orbs: number, sec: number, combo: number): boolean {
    const isBest = score > this.data.best;
    if (isBest) this.data.best = score;
    this.data.runs++;
    this.data.totalScore += score;
    this.data.totalOrbs += orbs;
    this.data.totalPlaySec += Math.round(sec);
    this.data.maxCombo = Math.max(this.data.maxCombo, combo);
    this.save();
    return isBest;
  }

  unlock(id: string): void {
    if (!this.data.unlockedSkins.includes(id)) {
      this.data.unlockedSkins.push(id);
      this.save();
    }
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
