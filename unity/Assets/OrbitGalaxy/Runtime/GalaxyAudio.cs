using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orbit.Galaxy.Unity
{
    /// <summary>
    /// Звук Galaxy: сначала клипы Resources/GalaxyAudio, при отсутствии — процедурные AudioClip.
    /// Музыка состоит из четырёх слоёв интенсивности и слоя опасности, громкости разделены.
    /// </summary>
    public sealed class GalaxyAudio : MonoBehaviour
    {
        private const string ResourceFolder = "GalaxyAudio/";
        private const int SampleRate = 22050;
        private const int SfxVoices = 8;

        private static readonly string[] ClipNames =
        {
            "collect", "level", "evolve", "kill", "absorb", "ability", "boss", "warning",
            "victory", "defeat", "event", "ui", "ambient", "music_0", "music_1", "music_2", "music_3"
        };

        private GameSettings _settings;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly List<AudioClip> _generated = new List<AudioClip>();
        private AudioSource[] _sfx;
        private AudioSource[] _music;
        private AudioSource _ambient;
        private int _sfxCursor;
        private bool _configured;
        private float _intensityTarget;
        private float _dangerTarget;
        private float _intensity;
        private float _danger;
        private float _lastSfx;
        private float _lastHaptic;

        public void Configure(GameSettings settings)
        {
            _settings = settings;
            EnsureSources();
            if (!_configured)
            {
                _configured = true;
                LoadClips();
                for (int i = 0; i < 4; i++)
                    if (_music[i] != null) _music[i].clip = GetClip("music_" + i);
                if (_ambient != null) _ambient.clip = GetClip("ambient");
            }
            ApplySettings();
        }

        public void ApplySettings()
        {
            EnsureSources();
            GameSettings settings = _settings;
            float music = settings == null ? 0.6f : Mathf.Clamp01(settings.MusicVolume);
            float sfx = settings == null ? 0.85f : Mathf.Clamp01(settings.SfxVolume);
            bool muted = settings != null && settings.Muted;
            if (muted) { music = 0f; sfx = 0f; }

            for (int i = 0; i < _sfx.Length; i++) _sfx[i].volume = sfx;
            RefreshMusicVolumes(music);
            if (music <= 0.001f)
            {
                StopMusic();
            }
            else
            {
                StartMusic();
            }
        }

        public void Play(SimEvent ev)
        {
            if (ev == null) return;
            string name = SoundFor(ev);
            if (string.IsNullOrEmpty(name)) return;
            float volume = ev.Kind == SimEventKind.Damage ? 0.42f : 0.92f;
            PlaySfx(name, volume);

            bool haptics = _settings != null && _settings.Haptics && !_settings.Muted;
            if (!haptics) return;
            if (ev.Kind == SimEventKind.Kill || ev.Kind == SimEventKind.Evolution
                || ev.Kind == SimEventKind.BossSpawn || ev.Kind == SimEventKind.Victory
                || ev.Kind == SimEventKind.Defeat)
            {
                if (Time.unscaledTime - _lastHaptic > 0.35f)
                {
                    _lastHaptic = Time.unscaledTime;
                    Handheld.Vibrate();
                }
            }
        }

        public void SetMusicIntensity(float intensity, float danger)
        {
            _intensityTarget = Mathf.Clamp01(intensity);
            _dangerTarget = Mathf.Clamp01(danger);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _generated.Count; i++)
            {
                if (_generated[i] != null) Destroy(_generated[i]);
            }
            _generated.Clear();
            _clips.Clear();
        }

        private void Update()
        {
            if (!_configured) return;
            _intensity = Mathf.MoveTowards(_intensity, _intensityTarget, Time.unscaledDeltaTime * 0.7f);
            _danger = Mathf.MoveTowards(_danger, _dangerTarget, Time.unscaledDeltaTime * 0.9f);
            float volume = _settings == null ? 0.6f : Mathf.Clamp01(_settings.MusicVolume);
            if (_settings != null && _settings.Muted) volume = 0f;
            RefreshMusicVolumes(volume);
        }

        private void EnsureSources()
        {
            if (_sfx == null)
            {
                _sfx = new AudioSource[SfxVoices];
                for (int i = 0; i < _sfx.Length; i++) _sfx[i] = MakeSource("SFX", false);
            }
            if (_music == null)
            {
                _music = new AudioSource[4];
                for (int i = 0; i < _music.Length; i++) _music[i] = MakeSource("Музыка " + i, true);
            }
            if (_ambient == null) _ambient = MakeSource("Амбиент", true);
        }

        private AudioSource MakeSource(string name, bool loop)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        private void LoadClips()
        {
            for (int i = 0; i < ClipNames.Length; i++)
            {
                string name = ClipNames[i];
                AudioClip clip = null;
                try { clip = Resources.Load<AudioClip>(ResourceFolder + name); }
                catch (Exception ex) { Debug.LogWarning("ORBITA GALAXY: звук " + name + ": " + ex.Message); }
                if (clip == null) clip = Generate(name);
                _clips[name] = clip;
            }
        }

        private AudioClip GetClip(string name)
        {
            AudioClip clip;
            return _clips.TryGetValue(name, out clip) ? clip : null;
        }

        private AudioClip Generate(string name)
        {
            AudioClip clip;
            switch (name)
            {
                case "collect": clip = Blend("collect", Tone(880f, 0.10f, 0.035f), Tone(1320f, 0.12f, 0.04f), 0.04f); break;
                case "level": clip = Chord("level", new[] { 523.25f, 659.25f, 783.99f }, 0.55f); break;
                case "evolve": clip = Blend("evolve", Sweep(220f, 1100f, 0.7f), Chord("evolve_chord", new[] { 392f, 587.33f, 784f }, 0.7f), 0.3f); break;
                case "kill": clip = Blend("kill", Noise("kill", 0.18f, 0.06f), Sweep(240f, 90f, 0.22f), 0.06f); break;
                case "absorb": clip = Sweep(620f, 120f, 0.36f); break;
                case "ability": clip = Sweep(320f, 1250f, 0.28f); break;
                case "boss": clip = Blend("boss", Chord("boss_chord", new[] { 82.4f, 110f, 164.8f }, 1.1f), Noise("boss_noise", 0.7f, 0.3f), 0.25f); break;
                case "warning": clip = Blend("warning", Tone(440f, 0.14f, 0.05f), Tone(440f, 0.14f, 0.05f), 0.2f); break;
                case "victory": clip = Chord("victory", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 1.3f); break;
                case "defeat": clip = Sweep(330f, 78f, 1.0f); break;
                case "event": clip = Tone(660f, 0.36f, 0.16f); break;
                case "ui": clip = Tone(1180f, 0.06f, 0.02f); break;
                case "ambient": clip = Drone("ambient", 55f, 6f, false); break;
                case "music_0": clip = Pad("music_0", new[] { 110f, 164.81f, 220f }, 8f); break;
                case "music_1": clip = Arp("music_1", new[] { 220f, 261.63f, 329.63f, 440f }, 8f, 4f); break;
                case "music_2": clip = Arp("music_2", new[] { 164.81f, 220f, 261.63f, 329.63f, 440f }, 8f, 8f); break;
                case "music_3": clip = Drone("music_3", 92.5f, 8f, true); break;
                default: clip = Tone(440f, 0.1f, 0.04f); break;
            }
            _generated.Add(clip);
            return clip;
        }

        private static string SoundFor(SimEvent ev)
        {
            switch (ev.Kind)
            {
                case SimEventKind.Pickup: return "collect";
                case SimEventKind.LevelUp: return "level";
                case SimEventKind.Evolution: return "evolve";
                case SimEventKind.Kill:
                case SimEventKind.LeaderKilled: return "kill";
                case SimEventKind.Absorb: return "absorb";
                case SimEventKind.Damage: return "ui";
                case SimEventKind.Ability: return "ability";
                case SimEventKind.BossSpawn: return "boss";
                case SimEventKind.EventWarning:
                case SimEventKind.Warning: return "warning";
                case SimEventKind.EventStart:
                case SimEventKind.EventEnd:
                case SimEventKind.RankUp:
                case SimEventKind.Streak: return "event";
                case SimEventKind.Victory: return "victory";
                case SimEventKind.Defeat: return "defeat";
                case SimEventKind.MatchEnd: return ev.Value > 0 ? "victory" : "defeat";
                default: return null;
            }
        }

        private void PlaySfx(string name, float volume)
        {
            EnsureSources();
            if (_settings != null && (_settings.Muted || _settings.SfxVolume <= 0.001f)) return;
            if (Time.unscaledTime - _lastSfx < 0.015f) return;
            AudioClip clip = GetClip(name);
            if (clip == null) return;
            _sfx[_sfxCursor % _sfx.Length].PlayOneShot(clip, volume);
            _sfxCursor++;
            _lastSfx = Time.unscaledTime;
        }

        private void StartMusic()
        {
            for (int i = 0; i < _music.Length; i++)
            {
                if (_music[i] == null || _music[i].clip == null || _music[i].isPlaying) continue;
                _music[i].Play();
            }
            if (_ambient != null && _ambient.clip != null && !_ambient.isPlaying) _ambient.Play();
        }

        private void StopMusic()
        {
            for (int i = 0; i < _music.Length; i++) if (_music[i] != null && _music[i].isPlaying) _music[i].Stop();
            if (_ambient != null && _ambient.isPlaying) _ambient.Stop();
        }

        private void RefreshMusicVolumes(float master)
        {
            if (_music == null) return;
            _music[0].volume = master * 0.52f;
            _music[1].volume = master * Mathf.Clamp01(_intensity) * 0.78f;
            _music[2].volume = master * Mathf.Clamp01(_intensity * _intensity) * 0.64f;
            _music[3].volume = master * Mathf.Clamp01(_danger) * 0.70f;
            if (_ambient != null) _ambient.volume = master * (0.18f + Mathf.Clamp01(_danger) * 0.5f);
        }

        private static AudioClip Tone(float frequency, float duration, float decay)
        {
            return MakeClip("tone_" + frequency.ToString("F0"), duration, delegate(int i)
            {
                float t = i / (float)SampleRate;
                return Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t / Mathf.Max(0.001f, decay));
            });
        }

        private static AudioClip Sweep(float from, float to, float duration)
        {
            return MakeClip("sweep_" + from.ToString("F0"), duration, delegate(int i)
            {
                float t = i / (float)SampleRate;
                float p = Mathf.Clamp01(t / Mathf.Max(0.001f, duration));
                float frequency = Mathf.Lerp(from, to, p);
                return Mathf.Sin(2f * Mathf.PI * frequency * t) * (1f - p) * EdgeFade(t, duration);
            });
        }

        private static AudioClip Noise(string name, float duration, float decay)
        {
            return MakeClip(name, duration, delegate(int i)
            {
                float t = i / (float)SampleRate;
                return UnityEngine.Random.Range(-1f, 1f) * Mathf.Exp(-t / Mathf.Max(0.001f, decay)) * 0.7f;
            });
        }

        private static AudioClip Chord(string name, float[] notes, float duration)
        {
            return MakeClip(name, duration, delegate(int i)
            {
                float t = i / (float)SampleRate;
                float sum = 0f;
                for (int n = 0; n < notes.Length; n++)
                    sum += Mathf.Sin(2f * Mathf.PI * notes[n] * t);
                return sum / Mathf.Max(1, notes.Length) * Mathf.Exp(-t / (duration * 0.45f));
            });
        }

        private static AudioClip Pad(string name, float[] notes, float duration)
        {
            return MakeClip(name, duration, delegate(int i)
            {
                float t = i / (float)SampleRate;
                float sum = 0f;
                for (int n = 0; n < notes.Length; n++)
                    sum += Mathf.Sin(2f * Mathf.PI * notes[n] * t + Mathf.Sin(t * 0.7f + n) * 0.35f);
                return sum / Mathf.Max(1, notes.Length) * 0.45f * EdgeFade(t, duration);
            });
        }

        private static AudioClip Arp(string name, float[] notes, float duration, float noteRate)
        {
            return MakeClip(name, duration, delegate(int i)
            {
                float t = i / (float)SampleRate;
                int step = Mathf.FloorToInt(t * noteRate);
                float note = notes[step % notes.Length];
                float local = t - step / noteRate;
                float envelope = Mathf.Exp(-local * 7f);
                return Mathf.Sin(2f * Mathf.PI * note * t) * envelope * 0.5f * EdgeFade(t, duration);
            });
        }

        private static AudioClip Drone(string name, float frequency, float duration, bool tense)
        {
            return MakeClip(name, duration, delegate(int i)
            {
                float t = i / (float)SampleRate;
                float baseWave = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.5f;
                float detune = Mathf.Sin(2f * Mathf.PI * frequency * (tense ? 1.06f : 0.503f) * t) * 0.3f;
                float wobble = Mathf.Sin(t * (tense ? 3.1f : 0.42f)) * 0.08f;
                return (baseWave + detune + wobble) * EdgeFade(t, duration);
            });
        }

        private static AudioClip Blend(string name, AudioClip first, AudioClip second, float offset)
        {
            float length = Mathf.Max(first.length, second.length + offset);
            float[] a = new float[first.samples];
            float[] b = new float[second.samples];
            first.GetData(a, 0);
            second.GetData(b, 0);
            int count = Mathf.CeilToInt(length * SampleRate);
            float[] result = new float[count];
            int offsetSamples = Mathf.RoundToInt(offset * SampleRate);
            for (int i = 0; i < count; i++)
            {
                if (i < a.Length) result[i] += a[i];
                int j = i - offsetSamples;
                if (j >= 0 && j < b.Length) result[i] += b[j] * 0.75f;
                result[i] = Mathf.Clamp(result[i], -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("Galaxy_" + name, count, 1, SampleRate, false);
            clip.SetData(result, 0);
            return clip;
        }

        private static float EdgeFade(float time, float duration)
        {
            float start = Mathf.Clamp01(time * 12f);
            float end = Mathf.Clamp01((duration - time) * 12f);
            return Mathf.Min(start, end);
        }

        private static AudioClip MakeClip(string name, float duration, Func<int, float> generator)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate));
            float[] data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(generator(i), -1f, 1f);
            AudioClip clip = AudioClip.Create("Galaxy_" + name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
