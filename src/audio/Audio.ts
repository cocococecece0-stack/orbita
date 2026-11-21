/** WebAudio: AAC/M4A для SFX + четыре генеративных слоя музыки. Один набор на обе платформы. */
export type SfxName =
  | 'tap' | 'dodge' | 'orb' | 'powerup' | 'shield'
  | 'crash' | 'level' | 'storm' | 'ambient';

const FILES: Record<SfxName, string> = {
  tap: 'tap.m4a',
  dodge: 'dodge.m4a',
  orb: 'orb.m4a',
  powerup: 'powerup.m4a',
  shield: 'shield.m4a',
  crash: 'crash.m4a',
  level: 'level.m4a',
  storm: 'storm.m4a',
  ambient: 'ambient.m4a',
};

const STEP_SEC = 0.15;
const SCALE = [0, 3, 5, 7, 10, 12, 15];

export class AudioManager {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private musicGain: GainNode | null = null;
  private sfxGain: GainNode | null = null;
  private readonly buffers = new Map<SfxName, AudioBuffer>();
  private ambient: AudioBufferSourceNode | null = null;
  private noiseBuffer: AudioBuffer | null = null;
  private muted = false;
  private musicOn = true;
  private masterVolume = 0.9;
  private musicVolume = 0.55;
  private sfxVolume = 1;
  private nextNoteTime = 0;
  private step = 0;
  private level = 1;
  private fever = false;
  private playing = false;

  async init(): Promise<void> {
    if (this.ctx) return;
    const Ctor: typeof AudioContext =
      window.AudioContext ?? (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
    const ctx = new Ctor();
    const master = ctx.createGain();
    const music = ctx.createGain();
    const sfx = ctx.createGain();
    master.gain.value = this.muted ? 0 : this.masterVolume;
    music.gain.value = this.musicVolume;
    sfx.gain.value = this.sfxVolume;
    music.connect(master);
    sfx.connect(master);
    master.connect(ctx.destination);
    this.ctx = ctx;
    this.master = master;
    this.musicGain = music;
    this.sfxGain = sfx;

    const noise = ctx.createBuffer(1, Math.floor(ctx.sampleRate * 0.25), ctx.sampleRate);
    const data = noise.getChannelData(0);
    // Детерминированный шум без Math.random: одинаков на всех устройствах.
    let seed = 0x9e3779b9;
    for (let i = 0; i < data.length; i++) {
      seed ^= seed << 13; seed ^= seed >>> 17; seed ^= seed << 5;
      data[i] = ((seed >>> 0) / 0x1_0000_0000) * 2 - 1;
    }
    this.noiseBuffer = noise;

    await Promise.all((Object.keys(FILES) as SfxName[]).map(async (name) => {
      const url = new URL(`../../assets/audio/${FILES[name]}`, import.meta.url).href;
      const res = await fetch(url);
      if (!res.ok) throw new Error(`Аудио ${name}: HTTP ${res.status}`);
      this.buffers.set(name, await ctx.decodeAudioData(await res.arrayBuffer()));
    }));
    this.nextNoteTime = ctx.currentTime + 0.12;
  }

  play(name: SfxName, pitch = 1, gain = 1): void {
    const ctx = this.ctx;
    const out = this.sfxGain;
    const buf = this.buffers.get(name);
    if (!ctx || !out || !buf) return;
    const src = ctx.createBufferSource();
    src.buffer = buf;
    src.playbackRate.value = pitch;
    const g = ctx.createGain();
    g.gain.value = gain;
    src.connect(g).connect(out);
    src.start();
  }

  startAmbient(): void {
    const ctx = this.ctx;
    const out = this.musicGain;
    const buf = this.buffers.get('ambient');
    if (!ctx || !out || !buf || this.ambient) return;
    const src = ctx.createBufferSource();
    src.buffer = buf;
    src.loop = true;
    const g = ctx.createGain();
    g.gain.value = 0.14;
    src.connect(g).connect(out);
    src.start();
    this.ambient = src;
  }

  /** Вызывается каждый кадр: планирует ноты за 0.12 с, без таймеров и аллокаций в тике. */
  update(level: number, fever: boolean, playing: boolean): void {
    this.level = level;
    this.fever = fever;
    this.playing = playing;
    const ctx = this.ctx;
    if (!ctx || !this.musicGain || this.muted || !this.musicOn) return;
    if (this.nextNoteTime < ctx.currentTime) this.nextNoteTime = ctx.currentTime + 0.02;
    const horizon = ctx.currentTime + 0.12;
    let guard = 0;
    while (this.nextNoteTime < horizon && guard++ < 8) {
      this.scheduleStep(this.step, this.nextNoteTime);
      this.nextNoteTime += STEP_SEC;
      this.step++;
    }
  }

  /** Слои: pad всегда, bass с 3 уровня, arp с 6, perc в лихорадке. */
  private scheduleStep(step: number, time: number): void {
    if (!this.playing) return;
    const bar = step % 16;
    if (bar === 0) {
      const root = 55 * Math.pow(2, ((this.level - 1) % 5) / 12);
      this.tone('sine', [root, root * 1.5, root * 2], time, 2.2, 0.035);
    }
    if (this.level >= 3 && bar % 4 === 0) {
      this.tone('triangle', [55], time, 0.42, 0.075);
    }
    if (this.level >= 6 && bar % 2 === 0) {
      const idx = (step / 2 + this.level) % SCALE.length;
      this.tone('square', [220 * Math.pow(2, (SCALE[idx] ?? 0) / 12)], time, 0.11, 0.018);
    }
    if (this.fever && bar % 2 === 1) this.perc(time, bar % 4 === 3 ? 0.045 : 0.024);
  }

  private tone(type: OscillatorType, freqs: number[], time: number, dur: number, gain: number): void {
    const ctx = this.ctx;
    const out = this.musicGain;
    if (!ctx || !out) return;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, time);
    g.gain.exponentialRampToValueAtTime(gain, time + 0.03);
    g.gain.exponentialRampToValueAtTime(0.0001, time + dur);
    g.connect(out);
    for (const f of freqs) {
      const osc = ctx.createOscillator();
      osc.type = type;
      osc.frequency.value = f;
      osc.connect(g);
      osc.start(time);
      osc.stop(time + dur + 0.02);
    }
  }

  private perc(time: number, gain: number): void {
    const ctx = this.ctx;
    const out = this.musicGain;
    const noise = this.noiseBuffer;
    if (!ctx || !out || !noise) return;
    const src = ctx.createBufferSource();
    src.buffer = noise;
    const filter = ctx.createBiquadFilter();
    filter.type = 'highpass';
    filter.frequency.value = 4200;
    const g = ctx.createGain();
    g.gain.setValueAtTime(gain, time);
    g.gain.exponentialRampToValueAtTime(0.0001, time + 0.09);
    src.connect(filter).connect(g).connect(out);
    src.start(time);
    src.stop(time + 0.1);
  }

  /** Дакинг при смерти: музыка уходит вниз и возвращается без таймеров. */
  duck(): void {
    const ctx = this.ctx;
    const g = this.musicGain;
    if (!ctx || !g) return;
    const now = ctx.currentTime;
    g.gain.cancelScheduledValues(now);
    g.gain.setValueAtTime(Math.max(0.0001, g.gain.value), now);
    g.gain.linearRampToValueAtTime(this.musicVolume * 0.08, now + 0.1);
    g.gain.linearRampToValueAtTime(this.musicVolume, now + 1.6);
  }

  setVolumes(master: number, music: number, sfx: number): void {
    this.masterVolume = master;
    this.musicVolume = music;
    this.sfxVolume = sfx;
    const ctx = this.ctx;
    if (!ctx || !this.master || !this.musicGain || !this.sfxGain) return;
    this.master.gain.setTargetAtTime(this.muted ? 0 : master, ctx.currentTime, 0.02);
    this.musicGain.gain.setTargetAtTime(music, ctx.currentTime, 0.02);
    this.sfxGain.gain.setTargetAtTime(sfx, ctx.currentTime, 0.02);
  }

  setMuted(value: boolean): void {
    this.muted = value;
    if (this.ctx && this.master) {
      this.master.gain.setTargetAtTime(value ? 0 : this.masterVolume, this.ctx.currentTime, 0.02);
    }
  }

  setMusicEnabled(value: boolean): void {
    this.musicOn = value;
    if (this.ctx && this.musicGain) {
      this.musicGain.gain.setTargetAtTime(value ? this.musicVolume : 0, this.ctx.currentTime, 0.05);
    }
  }

  isMuted(): boolean { return this.muted; }

  suspend(): void { if (this.ctx) void this.ctx.suspend(); }
  resume(): void { if (this.ctx) void this.ctx.resume(); }
}
