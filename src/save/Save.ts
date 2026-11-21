import { decodeTransfer, encodeTransfer } from './TransferCode';
import type { Locale, Mode } from '../core/Config';
import { ACHIEVEMENTS, type RunContext } from '../game/Achievements';
import { applyRun, dailyKey, freshDailyState, missionsFor, type DailyMission, type DailyState } from '../game/Daily';
import { claimSeasonTier, currentSeason, freshSeasonState, SEASON_TIERS, syncSeason, type SeasonState } from '../game/Seasons';

export type ThemeId = 'observatory' | 'neon' | 'sunset';

export const THEMES: ReadonlyArray<{ id: ThemeId; name: string }> = [
  { id: 'observatory', name: 'ОБСЕРВАТОРИЯ' },
  { id: 'neon', name: 'НЕОН' },
  { id: 'sunset', name: 'ЗАКАТ' },
];

export interface RunResult {
  score: number;
  orbs: number;
  sec: number;
  combo: number;
  mode: Mode;
  isNewBest: boolean;
  seed: number;
  context: RunContext;
  ghost: { data: Float32Array; count: number };
}

export interface RunOutcome {
  achievements: string[];
  missions: DailyMission[];
  seasonTiers: number[];
}

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
  ghost: number[];
  ghostScore: number;
  daily: DailyState;
  season: SeasonState;
  settings: {
    sound: boolean;
    music: boolean;
    haptics: boolean;
    fx: number;
    palette: 'default' | 'protan' | 'deutan' | 'tritan';
    reducedMotion: boolean;
    locale: Locale;
    theme: ThemeId;
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
    ghost: [],
    ghostScore: 0,
    daily: freshDailyState(dailyKey()),
    season: freshSeasonState(currentSeason()),
    settings: {
      sound: true, music: true, haptics: true, fx: 1,
      palette: 'default', reducedMotion: false, locale: 'ru', lowFps: false, theme: 'observatory',
    },
  };
}

/** Строго локальное сохранение: JSON v2 + версионирование + миграция v1. */
export class Save {
  private data: SaveDataV2 = defaults();

  private normalize(p: Partial<SaveDataV2>): SaveDataV2 {
    const d = defaults();
    const day = dailyKey();
    const season = currentSeason();
    const daily = p.daily && p.daily.date === day && Array.isArray(p.daily.progress)
      ? { ...freshDailyState(day), ...p.daily,
          progress: p.daily.progress.slice(0, 3),
          claimed: Array.isArray(p.daily.claimed) ? p.daily.claimed.slice(0, 3) : [false, false, false] }
      : freshDailyState(day);
    return {
      ...d, ...p,
      version: 2,
      bestByMode: { ...p.bestByMode },
      unlockedSkins: p.unlockedSkins?.length ? p.unlockedSkins : d.unlockedSkins,
      unlockedTrails: p.unlockedTrails?.length ? p.unlockedTrails : d.unlockedTrails,
      achievements: Array.isArray(p.achievements) ? p.achievements : [],
      ghost: Array.isArray(p.ghost) ? p.ghost.filter((v): v is number => typeof v === 'number' && Number.isFinite(v)) : [],
      daily,
      season: syncSeason(p.season ?? d.season, season),
      settings: { ...d.settings, ...p.settings },
    };
  }

  load(): SaveDataV2 {
    try {
      const raw = localStorage.getItem(KEY);
      if (raw) {
        const p = JSON.parse(raw) as Partial<SaveDataV2>;
        if (p.version === 2) {
          this.data = this.normalize(p);
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

  recordRun(run: RunResult): RunOutcome {
    const d = this.data;
    const day = dailyKey();
    if (d.daily.date !== day) d.daily = freshDailyState(day);
    d.season = syncSeason(d.season, currentSeason());

    if (run.isNewBest) {
      d.best = run.score;
      d.bestSeed = run.seed;
      const n = Math.min(run.ghost.count, run.ghost.data.length);
      const ghost: number[] = [];
      for (let i = 0; i < n; i++) ghost.push(Math.round((run.ghost.data[i] ?? 0) * 1000) / 1000);
      d.ghost = ghost;
      d.ghostScore = run.score;
    }
    d.bestByMode[run.mode] = Math.max(d.bestByMode[run.mode] ?? 0, run.score);
    d.runs++;
    d.totalScore += run.score;
    d.totalOrbs += run.orbs;
    d.totalPlaySec += Math.round(run.sec);
    d.maxCombo = Math.max(d.maxCombo, run.combo);
    d.currency += Math.floor(run.score / 10) + run.orbs;

    const earned: string[] = [];
    for (const a of ACHIEVEMENTS) {
      if (!d.achievements.includes(a.id) && a.check(run.context)) {
        d.achievements.push(a.id);
        earned.push(a.title);
        d.currency += 50;
      }
    }

    const completed = applyRun(d.daily, {
      orbs: run.orbs, combo: run.combo, seconds: run.sec,
      nearMisses: run.context.nearMisses, level: run.context.level,
      bossKills: run.context.bossKills,
    });
    const missions = missionsFor(day);
    const missionList: DailyMission[] = [];
    for (const i of completed) {
      const m = missions[i];
      if (!m) continue;
      missionList.push(m);
      d.currency += m.reward;
    }

    d.season.xp += Math.floor(run.score / 10);
    const seasonTiers: number[] = [];
    for (let i = 0; i < SEASON_TIERS.length; i++) {
      const tier = SEASON_TIERS[i];
      if (!tier || d.season.claimed[i] || d.season.xp < tier.xp) continue;
      d.season.claimed[i] = true;
      d.currency += tier.reward;
      seasonTiers.push(i);
    }

    this.save();
    return { achievements: earned, missions: missionList, seasonTiers };
  }

  claimDaily(index: number): boolean {
    const m = missionsFor(this.data.daily.date)[index];
    if (!m || this.data.daily.claimed[index] || (this.data.daily.progress[index] ?? 0) < m.goal) return false;
    this.data.daily.claimed[index] = true;
    this.data.currency += m.reward;
    this.save();
    return true;
  }

  claimSeason(index: number): boolean {
    const reward = claimSeasonTier(this.data.season, index);
    if (reward === 0) return false;
    this.data.currency += reward;
    this.save();
    return true;
  }

  setTheme(theme: ThemeId): void { this.data.settings.theme = theme; this.save(); }

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
    this.data = this.normalize(parsed);
    this.save();
    return true;
  }
}
