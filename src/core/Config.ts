import { readTheme } from '../theme/theme';

/** Единая конфигурация. Значения берутся из theme.css, Dumper не дублирует числа. */
export const CONFIG = {
  get theme() { return readTheme(); },
  fixedDt: 1 / 120,
  maxSteps: 8,
  hitStopMs: 75,
  nearMissMs: 120,
  nearMissScale: 0.35,
  comboDecaySec: 4,
  comboMax: 5,
  feverCombo: 8,
  feverSec: 6,
  telegraphSec: 0.4,
  maxParticles: 256,
  maxOrbs: 64,
  maxAsteroids: 48,
  maxPowerups: 16,
  restartMs: 280,
} as const;

export type Locale = 'ru' | 'en';
