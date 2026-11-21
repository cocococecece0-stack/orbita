/** Сезоны: восемь недель, локальный XP, награды без сервера. */
export interface Season {
  id: string;
  name: string;
  index: number;
  startUtc: number;
  endUtc: number;
}

export interface SeasonTier {
  title: string;
  xp: number;
  reward: number;
}

export interface SeasonState {
  id: string;
  xp: number;
  claimed: boolean[];
}

const EPOCH = Date.UTC(2026, 0, 5);
const WEEK_MS = 7 * 24 * 60 * 60 * 1000;
const WEEKS = 8;
const NAMES = ['ПЕПЕЛ', 'СИГНАЛ', 'ИМПУЛЬС', 'ОРБИТА'] as const;

export const SEASON_TIERS: readonly SeasonTier[] = [
  { title: 'ПЕРВЫЙ СИГНАЛ', xp: 100, reward: 80 },
  { title: 'СТАБИЛЬНАЯ ОРБИТА', xp: 350, reward: 140 },
  { title: 'ГЛУБОКИЙ КОСМОС', xp: 900, reward: 260 },
  { title: 'ФИНАЛЬНЫЙ ПРОХОД', xp: 2000, reward: 500 },
];

export function currentSeason(now: Date = new Date()): Season {
  const week = Math.floor((now.getTime() - EPOCH) / WEEK_MS);
  const index = Math.max(1, Math.floor(week / WEEKS) + 1);
  const startUtc = EPOCH + (index - 1) * WEEKS * WEEK_MS;
  const name = NAMES[(index - 1) % NAMES.length] ?? 'ОРБИТА';
  return {
    id: `S${String(index).padStart(2, '0')}`,
    name: `СЕЗОН ${index} · ${name}`,
    index,
    startUtc,
    endUtc: startUtc + WEEKS * WEEK_MS,
  };
}

export function freshSeasonState(season: Season): SeasonState {
  return { id: season.id, xp: 0, claimed: SEASON_TIERS.map(() => false) };
}

export function syncSeason(state: SeasonState, season: Season): SeasonState {
  if (!state || state.id !== season.id || !Array.isArray(state.claimed)) {
    return freshSeasonState(season);
  }
  const claimed = SEASON_TIERS.map((_, i) => state.claimed[i] === true);
  return { id: season.id, xp: Math.max(0, state.xp || 0), claimed };
}

export function claimSeasonTier(state: SeasonState, index: number): number {
  const tier = SEASON_TIERS[index];
  if (!tier || state.claimed[index] || state.xp < tier.xp) return 0;
  state.claimed[index] = true;
  return tier.reward;
}
