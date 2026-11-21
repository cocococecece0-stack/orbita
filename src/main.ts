import './theme/theme.css';
import { GameLoop } from './core/GameLoop';
import { Rng } from './core/Rng';
import { Renderer } from './render/Renderer';
import { EntityRenderer } from './render/EntityRenderer';
import { Game } from './game/Game';
import { AudioManager, type SfxName } from './audio/Audio';
import { haptic, type HapticPreset } from './platform/Haptics';
import { Save } from './save/Save';
import { UI } from './ui/UI';

const canvas = document.getElementById('game') as HTMLCanvasElement | null;
const uiRoot = document.getElementById('ui') as HTMLDivElement | null;
if (!canvas || !uiRoot) throw new Error('Не найдены #game или #ui');

const save = new Save();
const data = save.load();
document.documentElement.dataset['palette'] = data.settings.palette;
document.documentElement.lang = data.settings.locale;

const renderer = new Renderer(canvas);
const entities = new EntityRenderer(renderer.getGl());
const audio = new AudioManager();
const game = new Game(save.get().best);
const ui = new UI(uiRoot);
ui.setMenuBest(data.best);

let audioReady = false;
let pointerDownAt = 0;
let diveTimer = 0;

function insets(): { top: number; right: number; bottom: number; left: number } {
  const cs = getComputedStyle(document.documentElement);
  const n = (name: string): number => parseFloat(cs.getPropertyValue(name)) || 0;
  return { top: n('--safe-top'), right: n('--safe-right'), bottom: n('--safe-bottom'), left: n('--safe-left') };
}

function resize(): void {
  const i = insets();
  renderer.resize(window.innerWidth, window.innerHeight, i);
  game.resize(window.innerWidth, window.innerHeight, i.top, i.bottom);
}
window.addEventListener('resize', resize, { passive: true });
window.addEventListener('orientationchange', resize, { passive: true });
resize();

function sfx(name: SfxName, pitch = 1): void {
  if (!data.settings.sound) return;
  void ensureAudio().then(() => audio.play(name, pitch));
}

function tapHaptic(preset: HapticPreset): void {
  if (data.settings.haptics) void haptic(preset);
}

async function ensureAudio(): Promise<void> {
  if (audioReady) return;
  audioReady = true;
  try {
    await audio.init();
    audio.setMuted(!data.settings.sound);
    if (data.settings.music) audio.startAmbient();
  } catch (error) {
    console.warn('Аудио недоступно:', error);
  }
}

game.events.on('orb', (mult) => {
  sfx('orb', 0.92 + mult * 0.09);
  tapHaptic('light');
});
game.events.on('nearMiss', () => { sfx('dodge', 1.18); tapHaptic('medium'); });
game.events.on('shieldOn', () => { sfx('shield', 0.9); tapHaptic('medium'); });
game.events.on('shieldBreak', () => { sfx('shield', 0.7); tapHaptic('heavy'); });
game.events.on('slowOn', () => { sfx('powerup', 0.85); tapHaptic('medium'); });
game.events.on('levelUp', (level) => { sfx('level', 0.95 + level * 0.01); tapHaptic('success'); });
game.events.on('feverOn', () => { sfx('powerup', 1.25); tapHaptic('success'); });
game.events.on('newBest', () => { sfx('level', 1.3); tapHaptic('success'); });
game.events.on('death', () => { sfx('crash'); tapHaptic('heavy'); });

function startRun(): void {
  void ensureAudio();
  game.startRun(Rng.runSeed());
  ui.show('playing');
}

ui.on('ui-play', startRun);
ui.on('ui-again', startRun);
ui.on('ui-pause', () => { game.pause(); ui.show('paused'); });
ui.on('ui-resume', () => { game.resume(); ui.show('playing'); });
ui.on('ui-pause-menu', () => { game.toMenu(); ui.setMenuBest(save.get().best); ui.show('menu'); });
ui.on('ui-over-menu', () => { game.toMenu(); ui.setMenuBest(save.get().best); ui.show('menu'); });
ui.on('ui-sound', () => {
  data.settings.sound = !data.settings.sound;
  audio.setMuted(!data.settings.sound);
  save.save();
});

canvas.addEventListener('pointerdown', (e: PointerEvent) => {
  e.preventDefault();
  pointerDownAt = performance.now();
  void ensureAudio();
  if (game.getScreen() === 'menu') { startRun(); return; }
  if (game.getScreen() === 'gameover') { startRun(); return; }
  if (game.getScreen() === 'paused') { game.resume(); ui.show('playing'); return; }
  game.tap();
  sfx('tap');
  // Удержание >140 мс — рискованный нырок на внутреннюю орбиту.
  diveTimer = window.setTimeout(() => game.setDiving(true), 140);
}, { passive: false });

canvas.addEventListener('pointerup', () => {
  if (diveTimer) { clearTimeout(diveTimer); diveTimer = 0; }
  game.setDiving(false);
}, { passive: true });
canvas.addEventListener('pointercancel', () => {
  if (diveTimer) { clearTimeout(diveTimer); diveTimer = 0; }
  game.setDiving(false);
}, { passive: true });

let wasPlaying = false;
const loop = new GameLoop({
  update: (dt: number): void => {
    game.update(dt);
    const snapshot = game.getSnapshot();
    if (snapshot.screen === 'gameover' && wasPlaying) {
      wasPlaying = false;
      save.recordRun(snapshot.score, snapshot.orbs, snapshot.elapsed, snapshot.mult);
      ui.showGameOver(snapshot, save.get());
    }
    if (snapshot.screen === 'playing') wasPlaying = true;
  },
  render: (_alpha: number, elapsed: number): void => {
    renderer.draw({ time: elapsed, ...game.getPlanetVisual() });
    const view = renderer.getView();
    const n = game.fillInstances(entities.bufferData, entities.capacity);
    const v = game.getPlanetVisual();
    const t = elapsed;
    entities.draw(n, view.width, view.height,
      Math.sin(t * 71) * v.shake, Math.cos(t * 83) * v.shake);
    ui.update(game.getSnapshot());
  },
});
renderer.attach(loop);
loop.start();

document.addEventListener('visibilitychange', () => {
  if (document.hidden) {
    loop.stop();
    audio.suspend();
    if (game.getScreen() === 'playing') game.pause();
  } else {
    loop.start();
    void ensureAudio().then(() => audio.resume());
  }
});

// Debug-код переноса доступен из консоли: window.__orbita.export()
(window as unknown as { __orbita: unknown }).__orbita = {
  export: (): string => save.exportCode(),
  import: (code: string): boolean => save.importCode(code),
  rng: (seed: number): Rng => new Rng(seed),
};
