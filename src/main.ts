import './theme/theme.css';
import { GameLoop } from './core/GameLoop';
import { Rng } from './core/Rng';
import { Renderer, type SafeInsets } from './render/Renderer';
import { AudioManager } from './audio/Audio';
import { readTheme } from './theme/theme';
import { haptic } from './platform/Haptics';

const canvas = document.getElementById('game') as HTMLCanvasElement | null;
if (!canvas) throw new Error('Canvas #game не найден');

const renderer = new Renderer(canvas);
const audio = new AudioManager();
const rng = new Rng(Rng.runSeed());

let pulse = 0;
let crack = 0;
let shake = 0;
let audioReady = false;

function safeInsets(): SafeInsets {
  const cs = getComputedStyle(document.documentElement);
  const n = (name: string): number => parseFloat(cs.getPropertyValue(name)) || 0;
  return {
    top: n('--safe-top'),
    right: n('--safe-right'),
    bottom: n('--safe-bottom'),
    left: n('--safe-left'),
  };
}

function resize(): void {
  renderer.resize(window.innerWidth, window.innerHeight, safeInsets());
}
window.addEventListener('resize', resize, { passive: true });
window.addEventListener('orientationchange', resize, { passive: true });
resize();

const loop = new GameLoop({
  update: (dt: number): void => {
    if (pulse > 0) pulse = Math.max(0, pulse - dt / 0.42);
    if (shake > 0) shake = Math.max(0, shake - dt * 12);
  },
  render: (_alpha: number, elapsed: number): void => {
    renderer.draw({ time: elapsed, pulse, crack, shake });
  },
});
renderer.attach(loop);

async function ensureAudio(): Promise<void> {
  if (audioReady) return;
  audioReady = true;
  try {
    await audio.init();
    audio.startAmbient();
  } catch (error) {
    console.warn('Аудио недоступно:', error);
  }
}

canvas.addEventListener('pointerdown', (event: PointerEvent) => {
  event.preventDefault();
  pulse = 1;
  shake = readTheme().shakeMaxPx * (0.8 + rng.next() * 0.4);
  void ensureAudio();
  audio.play('tap', 0.96 + rng.next() * 0.08);
  void haptic('light');
}, { passive: false });

document.addEventListener('visibilitychange', () => {
  if (document.hidden) {
    loop.stop();
    audio.suspend();
  } else {
    loop.start();
    void ensureAudio().then(() => audio.resume());
  }
});

loop.start();
