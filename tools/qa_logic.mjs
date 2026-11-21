#!/usr/bin/env node
/** Логический QA без браузера: собирает чистые модули esbuild'ом и проверяет инварианты. */
import { build } from 'esbuild';

const source = `
import { Rng } from './src/core/Rng.ts';
import { Game } from './src/game/Game.ts';
import { applyRun, freshDailyState, missionsFor } from './src/game/Daily.ts';
import { claimSeasonTier, currentSeason, freshSeasonState, syncSeason, SEASON_TIERS } from './src/game/Seasons.ts';

function fail(message) { throw new Error(message); }
function assert(ok, message) { if (!ok) fail(message); }
function same(a, b) { return JSON.stringify(a) === JSON.stringify(b); }

const a = new Rng(424242);
const b = new Rng(424242);
const seqA = [a.next(), a.next(), a.next(), a.next()];
const seqB = [b.next(), b.next(), b.next(), b.next()];
assert(same(seqA, seqB), 'Rng не детерминирован');
console.log('OK Rng: одинаковая последовательность для одного сида');

function simulate(taps) {
  const g = new Game(0);
  g.resize(400, 800, 0, 0);
  g.startRun(777, 'classic');
  for (let i = 0; i < 3000; i++) {
    if (taps.includes(i)) g.tap();
    g.update(1 / 120);
  }
  const s = g.getSnapshot();
  const ghost = g.getCurrentGhost();
  return {
    score: s.score,
    level: s.level,
    angle: g.getAngle(),
    x: g.getPlayerScreen().x,
    ghostCount: ghost.count,
    effectBits: [1, 2, 4, 8, 16, 32, 64].reduce((n, bit) => n + ((s.effects & bit) ? 1 : 0), 0),
  };
}

const taps = [12, 180, 240, 500, 900, 1400, 2100];
const run1 = simulate(taps);
const run2 = simulate(taps);
assert(same(run1, run2), 'Симуляция не детерминирована: ' + JSON.stringify(run1) + ' != ' + JSON.stringify(run2));
assert(run1.ghostCount > 0, 'Ghost-траектория не записана');
assert(run1.effectBits <= 2, 'Активных эффектов больше двух: ' + run1.effectBits);
console.log('OK Game: 3000 шагов идентичны, ghost записан, эффектов <= 2');

const date = '2026-11-21';
assert(same(missionsFor(date), missionsFor(date)), 'Ежедневные задания не детерминированы');
const daily = freshDailyState(date);
const completed = applyRun(daily, {
  orbs: 100, combo: 8, seconds: 120, nearMisses: 10, level: 10, bossKills: 1,
});
assert(completed.length === 3, 'Задания дня не выполнились: ' + completed.length);
assert(applyRun(daily, {
  orbs: 100, combo: 8, seconds: 120, nearMisses: 10, level: 10, bossKills: 1,
}).length === 0, 'Награда за задание выдаётся повторно');
console.log('OK Daily: UTC-сид стабилен, награда выдаётся один раз');

const season = currentSeason(new Date('2026-11-21T00:00:00Z'));
const later = currentSeason(new Date(season.endUtc + 86400000));
assert(later.id !== season.id, 'Сезон не сменился через 8 недель');
const st = syncSeason(freshSeasonState(season), season);
assert(SEASON_TIERS.every((_, i) => !st.claimed[i]), 'Сезонные награды не сброшены');
st.xp = SEASON_TIERS[SEASON_TIERS.length - 1].xp;
assert(claimSeasonTier(st, 0) > 0, 'Сезонная награда не выдана');
assert(claimSeasonTier(st, 0) === 0, 'Сезонная награда выдана повторно');
console.log('OK Seasons: смена сезона и одноразовые награды');

export const qaOk = true;
`;

const result = await build({
  stdin: { contents: source, resolveDir: process.cwd(), sourcefile: 'qa-entry.ts', loader: 'ts' },
  bundle: true,
  format: 'esm',
  platform: 'node',
  write: false,
  logLevel: 'silent',
});
const output = result.outputFiles?.[0]?.text;
if (!output) throw new Error('esbuild не вернул код');
const module = await import(`data:text/javascript;base64,${Buffer.from(output).toString('base64')}`);
if (module.qaOk !== true) throw new Error('QA не подтверждён');
console.log('OK: логический QA пройден');
