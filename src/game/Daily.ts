/** Ежедневные задания: сид = UTC-дата, прогресс хранится только локально. */
export type DailyKind = 'orbs' | 'combo' | 'survive' | 'nearmiss' | 'level' | 'boss';

export interface DailyMission {
  id: string;
  title: string;
  kind: DailyKind;
  goal: number;
  reward: number;
}

export interface DailyState {
  date: string;
  progress: number[];
  claimed: boolean[];
}

export interface DailyRun {
  orbs: number;
  combo: number;
  seconds: number;
  nearMisses: number;
  level: number;
  bossKills: number;
}

const BASE: readonly DailyMission[] = [
  { id: 'orbs', title: 'СОБРАТЬ СФЕРЫ', kind: 'orbs', goal: 30, reward: 60 },
  { id: 'combo', title: 'ДЕРЖАТЬ КОМБО', kind: 'combo', goal: 6, reward: 80 },
  { id: 'survive', title: 'ВЫЖИТЬ', kind: 'survive', goal: 60, reward: 70 },
  { id: 'nearmiss', title: 'РИСКОВАТЬ', kind: 'nearmiss', goal: 5, reward: 75 },
  { id: 'level', title: 'ДОСТИЧЬ УРОВНЯ', kind: 'level', goal: 5, reward: 90 },
  { id: 'boss', title: 'СБИТЬ ЯДРО', kind: 'boss', goal: 1, reward: 120 },
];

function hashDate(date: string): number {
  let h = 0x811c9dc5;
  for (let i = 0; i < date.length; i++) {
    h ^= date.charCodeAt(i);
    h = Math.imul(h, 0x01000193);
  }
  return h >>> 0;
}

export function dailyKey(now: Date = new Date()): string {
  return now.toISOString().slice(0, 10);
}

export function missionsFor(date: string): DailyMission[] {
  const h = hashDate(date);
  const offset = h % BASE.length;
  const out: DailyMission[] = [];
  for (let i = 0; i < 3; i++) {
    const base = BASE[(offset + i) % BASE.length];
    if (!base) continue;
    const scale = 1 + ((h >>> (i * 3 + 2)) & 3) * 0.15;
    const bonus = 1 + ((h >>> (i * 5 + 6)) & 3) * 0.1;
    out.push({
      ...base,
      goal: Math.max(1, Math.round(base.goal * scale)),
      reward: Math.round(base.reward * bonus),
    });
  }
  return out;
}

export function freshDailyState(date: string): DailyState {
  return { date, progress: [0, 0, 0], claimed: [false, false, false] };
}

function valueFor(kind: DailyKind, run: DailyRun): number {
  switch (kind) {
    case 'orbs': return run.orbs;
    case 'combo': return run.combo;
    case 'survive': return Math.floor(run.seconds);
    case 'nearmiss': return run.nearMisses;
    case 'level': return run.level;
    case 'boss': return run.bossKills;
  }
}

/** Возвращает индексы заданий, которые только что стали выполненными. */
export function applyRun(state: DailyState, run: DailyRun): number[] {
  const missions = missionsFor(state.date);
  const completed: number[] = [];
  for (let i = 0; i < missions.length; i++) {
    const m = missions[i];
    if (!m) continue;
    const value = valueFor(m.kind, run);
    const before = state.progress[i] ?? 0;
    state.progress[i] = Math.min(m.goal, Math.max(before, value));
    if (state.progress[i] >= m.goal && !state.claimed[i]) {
      state.claimed[i] = true;
      completed.push(i);
    }
  }
  return completed;
}
