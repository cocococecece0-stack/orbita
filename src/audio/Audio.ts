/** WebAudio-менеджер: один набор AAC/M4A на обе платформы, без JS-декодеров. */
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

export class AudioManager {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private readonly buffers = new Map<SfxName, AudioBuffer>();
  private ambient: AudioBufferSourceNode | null = null;
  private muted = false;

  async init(): Promise<void> {
    if (this.ctx) return;
    const Ctor: typeof AudioContext =
      window.AudioContext ?? (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
    const ctx = new Ctor();
    const master = ctx.createGain();
    master.gain.value = this.muted ? 0 : 0.9;
    master.connect(ctx.destination);
    this.ctx = ctx;
    this.master = master;

    await Promise.all((Object.keys(FILES) as SfxName[]).map(async (name) => {
      const url = new URL(`../../assets/audio/${FILES[name]}`, import.meta.url).href;
      const res = await fetch(url);
      if (!res.ok) throw new Error(`Аудио ${name}: HTTP ${res.status}`);
      this.buffers.set(name, await ctx.decodeAudioData(await res.arrayBuffer()));
    }));
  }

  play(name: SfxName, pitch = 1, gain = 1): void {
    const ctx = this.ctx;
    const master = this.master;
    const buf = this.buffers.get(name);
    if (!ctx || !master || !buf) return;
    const src = ctx.createBufferSource();
    src.buffer = buf;
    src.playbackRate.value = pitch;
    const g = ctx.createGain();
    g.gain.value = gain;
    src.connect(g).connect(master);
    src.start();
  }

  startAmbient(): void {
    const ctx = this.ctx;
    const master = this.master;
    const buf = this.buffers.get('ambient');
    if (!ctx || !master || !buf || this.ambient) return;
    const src = ctx.createBufferSource();
    src.buffer = buf;
    src.loop = true;
    const g = ctx.createGain();
    g.gain.value = 0.35;
    src.connect(g).connect(master);
    src.start();
    this.ambient = src;
  }

  setMuted(value: boolean): void {
    this.muted = value;
    if (this.ctx && this.master) {
      this.master.gain.setTargetAtTime(value ? 0 : 0.9, this.ctx.currentTime, 0.02);
    }
  }

  isMuted(): boolean {
    return this.muted;
  }

  suspend(): void {
    if (this.ctx) void this.ctx.suspend();
  }

  resume(): void {
    if (this.ctx) void this.ctx.resume();
  }
}
