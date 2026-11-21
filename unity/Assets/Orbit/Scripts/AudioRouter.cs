using System;
using System.Collections.Generic;
using Orbit.Core;
using UnityEngine;

namespace Orbit.Unity
{
    /// <summary>
    /// Звук: сначала нативные клипы из Resources/Audio, при отсутствии — процедурные AudioClip.Create.
    /// </summary>
    public sealed class AudioRouter : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const string ResourceFolder = "Audio/";
        private const float TickInterval = 0.5f;

        private GameSettings _settings;
        private AudioSource _sfx;
        private AudioSource _music;
        private readonly Dictionary<GameEventKind, AudioClip> _clips = new Dictionary<GameEventKind, AudioClip>();
        private readonly Dictionary<string, AudioClip> _native = new Dictionary<string, AudioClip>();
        private readonly List<AudioClip> _generated = new List<AudioClip>();
        private AudioClip _musicClip;
        private float _lastPlayed;
        private float _lastTick = -10f;

        public void Configure(GameSettings settings)
        {
            _settings = settings;
            EnsureSources();
            ApplySettings();
        }

        private void Awake()
        {
            EnsureSources();
        }

        private void EnsureSources()
        {
            if (_sfx == null)
            {
                _sfx = gameObject.AddComponent<AudioSource>();
                _sfx.playOnAwake = false;
                _sfx.loop = false;
                _sfx.spatialBlend = 0f;
            }
            if (_music == null)
            {
                _music = gameObject.AddComponent<AudioSource>();
                _music.playOnAwake = false;
                _music.loop = true;
                _music.spatialBlend = 0f;
            }
        }

        public void ApplySettings()
        {
            EnsureSources();
            GameSettings s = _settings;
            float sfx = s == null ? 0.85f : Mathf.Clamp01(s.sfxVolume);
            float music = s == null ? 0.6f : Mathf.Clamp01(s.musicVolume);
            bool muted = s != null && s.muted;
            _sfx.volume = muted ? 0f : sfx;
            _music.volume = muted ? 0f : music * 0.65f;

            if (muted || music <= 0.001f)
            {
                if (_music.isPlaying) _music.Stop();
                return;
            }

            if (_musicClip == null) _musicClip = ResolveMusic();
            if (_musicClip == null) return;
            if (_music.clip != _musicClip)
            {
                if (_music.isPlaying) _music.Stop();
                _music.clip = _musicClip;
            }
            if (!_music.isPlaying) _music.Play();
        }

        public void PlayFor(GameEventKind kind)
        {
            GameSettings s = _settings;
            if (s != null && (s.muted || s.sfxVolume <= 0.001f)) return;
            if (_sfx == null) EnsureSources();

            if (kind == GameEventKind.Tick)
            {
                if (Time.unscaledTime - _lastTick < TickInterval) return;
                _lastTick = Time.unscaledTime;
            }
            else if (Time.unscaledTime - _lastPlayed < 0.018f)
            {
                // Защита от звуковой каши при массовых событиях.
                return;
            }

            AudioClip clip = GetClip(kind);
            if (clip == null) return;
            _sfx.PlayOneShot(clip, 0.9f);
            _lastPlayed = Time.unscaledTime;
        }

        /// <summary>Полное событие: для финиша Value различает победу и поражение.</summary>
        public void PlayFor(GameEvent ev)
        {
            if (ev.Kind == GameEventKind.Finish)
            {
                string name = ev.Value != 0 ? "victory" : "gameover";
                GameSettings s = _settings;
                if (s == null || (!s.muted && s.sfxVolume > 0.001f))
                {
                    AudioClip clip = Native(name);
                    if (clip != null && _sfx != null)
                    {
                        _sfx.PlayOneShot(clip, 0.9f);
                        return;
                    }
                }
            }
            PlayFor(ev.Kind);
        }

        private static string NativeNameFor(GameEventKind kind)
        {
            switch (kind)
            {
                case GameEventKind.Tap: return "tap";
                case GameEventKind.BlockMove: return "ui";
                case GameEventKind.BlockRotate: return "ui";
                case GameEventKind.BlockHold: return "ui";
                case GameEventKind.BlockDrop: return "tap";
                case GameEventKind.BlockClear: return "reward";
                case GameEventKind.BlockTopOut: return "gameover";
                case GameEventKind.Dodge: return "dodge";
                case GameEventKind.Orb: return "orb";
                case GameEventKind.GoldenOrb: return "reward";
                case GameEventKind.Artifact: return "secret";
                case GameEventKind.PowerUp: return "powerup";
                case GameEventKind.ShieldBreak: return "shield";
                case GameEventKind.Crash: return "crash";
                case GameEventKind.LevelUp: return "level";
                case GameEventKind.NearMiss: return "dodge";
                case GameEventKind.Storm: return "storm";
                case GameEventKind.Wormhole: return "wormhole";
                case GameEventKind.Flare: return "flare";
                case GameEventKind.BossSpawn: return "boss";
                case GameEventKind.BossHit: return "boss_hit";
                case GameEventKind.BossDown: return "boss_down";
                case GameEventKind.Resonance: return "powerup";
                case GameEventKind.Finish: return "victory";
                case GameEventKind.Tick: return "tick";
                case GameEventKind.PerkDraft: return "ui";
                default: return "ui";
            }
        }

        /// <summary>Resources.Load с кешем отсутствующих клипов, чтобы не спамить загрузкой.</summary>
        private AudioClip Native(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            AudioClip clip;
            if (_native.TryGetValue(name, out clip)) return clip;
            try
            {
                clip = Resources.Load<AudioClip>(ResourceFolder + name);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Orbit Audio: не удалось загрузить " + ResourceFolder + name + " — " + ex.Message);
                clip = null;
            }
            if (clip == null)
                Debug.LogWarning("Orbit Audio: нет ресурса " + ResourceFolder + name + " — используется процедурный звук.");
            _native[name] = clip;
            return clip;
        }

        private AudioClip ResolveMusic()
        {
            // Музыка привязана к сезону: 8 треков чередуются между сезонами.
            int season = Mathf.Abs(Content.SeasonId());
            for (int i = 0; i < 8; i++)
            {
                AudioClip clip = Native("music_" + ((season + i) % 8));
                if (clip != null) return clip;
            }
            AudioClip generated = MakeMusic();
            _generated.Add(generated);
            return generated;
        }

        /// <summary>Сначала нативный клип, затем процедурный запасной.</summary>
        private AudioClip GetClip(GameEventKind kind)
        {
            AudioClip nativeClip = Native(NativeNameFor(kind));
            if (nativeClip != null) return nativeClip;

            AudioClip clip;
            if (_clips.TryGetValue(kind, out clip) && clip != null) return clip;

            switch (kind)
            {
                case GameEventKind.Tap:
                case GameEventKind.BlockMove:
                    clip = Tone("tap", 520f, 0.075f, false, 26f); break;
                case GameEventKind.BlockRotate:
                case GameEventKind.BlockHold:
                    clip = Tone("rotate", 690f, 0.09f, false, 20f); break;
                case GameEventKind.BlockDrop:
                    clip = Tone("drop", 220f, 0.14f, true, 18f); break;
                case GameEventKind.Orb:
                    clip = Tone("orb", 880f, 0.11f, false, 17f); break;
                case GameEventKind.GoldenOrb:
                case GameEventKind.Artifact:
                    clip = Chord("golden", new float[] { 880f, 1320f }, 0.24f); break;
                case GameEventKind.BlockClear:
                    clip = Chord("clear", new float[] { 660f, 880f, 1100f }, 0.26f); break;
                case GameEventKind.BlockTopOut:
                case GameEventKind.Crash:
                    clip = Tone("crash", 92f, 0.42f, true, 7f); break;
                case GameEventKind.ShieldBreak:
                    clip = Tone("shield", 310f, 0.3f, true, 9f); break;
                case GameEventKind.Dodge:
                case GameEventKind.NearMiss:
                    clip = Tone("dodge", 440f, 0.07f, false, 30f); break;
                case GameEventKind.PowerUp:
                case GameEventKind.Resonance:
                    clip = Chord("power", new float[] { 520f, 780f }, 0.3f); break;
                case GameEventKind.LevelUp:
                case GameEventKind.BossDown:
                    clip = Chord("victory", new float[] { 523f, 659f, 784f }, 0.42f); break;
                case GameEventKind.Storm:
                case GameEventKind.Flare:
                case GameEventKind.BossSpawn:
                    clip = Tone("alarm", 160f, 0.34f, true, 8f); break;
                case GameEventKind.BossHit:
                case GameEventKind.Wormhole:
                    clip = Tone("hit", 240f, 0.16f, true, 14f); break;
                case GameEventKind.Finish:
                    clip = Chord("finish", new float[] { 659f, 880f, 1175f }, 0.5f); break;
                case GameEventKind.PerkDraft:
                    clip = Chord("draft", new float[] { 440f, 660f }, 0.28f); break;
                case GameEventKind.Tick:
                    clip = Tone("tick", 1000f, 0.025f, false, 60f); break;
                default:
                    clip = Tone("blip", 600f, 0.07f, false, 28f); break;
            }
            _clips[kind] = clip;
            if (clip != null) _generated.Add(clip);
            return clip;
        }

        private static AudioClip Tone(string name, float frequency, float duration, bool square, float decay)
        {
            int count = Mathf.Max(64, Mathf.RoundToInt(SampleRate * duration));
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float phase = 2f * Mathf.PI * frequency * t;
                float wave = square ? Mathf.Sign(Mathf.Sin(phase)) : Mathf.Sin(phase);
                float envelope = Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 160f);
                data[i] = wave * envelope * 0.38f;
            }
            AudioClip clip = AudioClip.Create("orbit_" + name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Chord(string name, float[] frequencies, float duration)
        {
            int count = Mathf.Max(64, Mathf.RoundToInt(SampleRate * duration));
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float value = 0f;
                for (int f = 0; f < frequencies.Length; f++)
                    value += Mathf.Sin(2f * Mathf.PI * frequencies[f] * t);
                value /= Mathf.Max(1f, frequencies.Length);
                float envelope = Mathf.Exp(-t * 6f) * Mathf.Clamp01(t * 120f);
                data[i] = value * envelope * 0.42f;
            }
            AudioClip clip = AudioClip.Create("orbit_" + name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeMusic()
        {
            const float duration = 4f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[count];
            float[] notes = { 220f, 261.6f, 329.6f, 392f, 329.6f, 261.6f, 196f, 261.6f };
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                int step = Mathf.FloorToInt(t / 0.5f) % notes.Length;
                float local = t % 0.5f;
                float env = Mathf.Exp(-local * 4.5f) * Mathf.Clamp01(local * 40f);
                data[i] = Mathf.Sin(2f * Mathf.PI * notes[step] * t) * env * 0.28f;
            }
            AudioClip clip = AudioClip.Create("orbit_music", count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (_music != null && _music.isPlaying) _music.Stop();
            // Нативные клипы из Resources не уничтожаем — только синтезированные.
            for (int i = 0; i < _generated.Count; i++)
            {
                if (_generated[i] != null) Destroy(_generated[i]);
            }
            _generated.Clear();
            _clips.Clear();
            _native.Clear();
            _musicClip = null;
        }
    }
}
