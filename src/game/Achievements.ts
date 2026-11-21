export interface RunContext {
  score: number;
  orbs: number;
  combo: number;
  elapsed: number;
  nearMisses: number;
  level: number;
  mode: string;
  runs: number;
  isNewBest: boolean;
}

export interface Achievement {
  id: string;
  title: string;
  desc: string;
  icon: string;
  check: (c: RunContext) => boolean;
}

export const ACHIEVEMENTS: Achievement[] = [
  { id: 'first', title: 'ПЕРВЫЙ ПОЛЁТ', desc: 'Первый завершённый забег', icon: '✦', check: (c) => c.runs >= 1 },
  { id: 'hunter', title: 'ОХОТНИК', desc: '500 очков за забег', icon: '◆', check: (c) => c.score >= 500 },
  { id: 'combo', title: 'МАСТЕР КОМБО', desc: 'Множитель x5', icon: 'x5', check: (c) => c.combo >= 5 },
  { id: 'survivor', title: 'ВЫЖИВШИЙ', desc: '60 секунд в игре', icon: 'T', check: (c) => c.elapsed >= 60 },
  { id: 'record', title: 'РЕКОРДСМЕН', desc: 'Новый личный рекорд', icon: '★', check: (c) => c.isNewBest },
  { id: 'perfect', title: 'МАСТЕР БЛИЗКИХ', desc: '5 опасных пролётов за забег', icon: '◎', check: (c) => c.nearMisses >= 5 },
  { id: 'zen', title: 'СОЗЕРЦАНИЕ', desc: 'Забег в режиме Дзен', icon: '◌', check: (c) => c.mode === 'zen' },
  { id: 'daily', title: 'ЕЖЕДНЕВНЫЙ', desc: 'Забег в Daily Challenge', icon: 'D', check: (c) => c.mode === 'daily' },
];
