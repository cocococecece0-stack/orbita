#!/usr/bin/env python3
"""Deterministic, stdlib-only WAV generator for ORBITA GALAXY.

Run from anywhere:

    python3 tools/generate_galaxy_audio.py

Writes 16-bit mono PCM files into unity/Assets/OrbitGalaxy/Resources/GalaxyAudio.
No samples, no third-party modules, no timestamps: repeated runs are byte-identical.
"""

import math
import os
import random
import struct
import wave

SFX_SR = 22050
MUSIC_SR = 16000
TAU = 2.0 * math.pi

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TARGET = os.path.join(ROOT, "unity", "Assets", "OrbitGalaxy", "Resources", "GalaxyAudio")


def write_wav(name, samples, sample_rate):
    """Normalize, clamp and write one mono 16-bit WAV file."""
    peak = max(1e-9, max(abs(s) for s in samples))
    gain = 0.92 / peak if peak > 0.92 else 1.0
    pcm = bytearray()
    for value in samples:
        clipped = min(1.0, max(-1.0, value * gain))
        pcm.extend(struct.pack("<h", int(round(clipped * 32767.0))))
    path = os.path.join(TARGET, name + ".wav")
    with wave.open(path, "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(sample_rate)
        handle.writeframes(bytes(pcm))
    return path


def envelope(index, count, attack=0.006, release=0.35):
    """Attack/release curve that reaches exactly zero at the end of the note."""
    if count <= 0:
        return 0.0
    up = min(1.0, index / max(1.0, attack))
    down = max(0.0, 1.0 - index / float(count))
    return up * (down ** release)


def tone(freq, duration, sample_rate=SFX_SR, amp=0.5, harmonics=(1.0,),
         freq_end=None, attack=0.006, release=0.35, phase=0.0):
    count = int(sample_rate * duration)
    out = [0.0] * count
    end = freq if freq_end is None else freq_end
    for i in range(count):
        k = i / float(count) if count > 1 else 0.0
        f = freq * ((end / freq) ** k) if freq > 0.0 and end > 0.0 else freq
        base = phase + TAU * f * i / sample_rate
        wave_value = 0.0
        for h, weight in enumerate(harmonics, start=1):
            wave_value += weight * math.sin(base * h)
        out[i] = amp * wave_value * envelope(i, count, attack, release)
    return out


def noise(duration, sample_rate=SFX_SR, amp=0.4, decay=4.0, smooth=0.2, seed=1):
    rng = random.Random(seed)
    count = int(sample_rate * duration)
    out = [0.0] * count
    state = 0.0
    for i in range(count):
        target = rng.uniform(-1.0, 1.0)
        state += smooth * (target - state)
        k = i / float(count) if count > 1 else 0.0
        out[i] = amp * state * min(1.0, i / max(1.0, sample_rate * 0.002)) * math.exp(-decay * k)
    return out


def place(layers, delay, samples, sample_rate=SFX_SR):
    """Mix a layer at a delay in seconds; extends the target list when needed."""
    offset = int(delay * sample_rate)
    needed = offset + len(samples)
    if len(layers) < needed:
        layers.extend([0.0] * (needed - len(layers)))
    for i, value in enumerate(samples):
        layers[offset + i] += value
    return layers


def mix(*parts):
    length = max(len(part) for part in parts)
    out = [0.0] * length
    for part in parts:
        for i, value in enumerate(part):
            out[i] += value
    return out


def build_sfx():
    sounds = {}

    sounds["collect"] = mix(
        tone(880.0, 0.11, amp=0.42, harmonics=(1.0, 0.35), release=2.2),
        place([], 0.07, tone(1318.5, 0.17, amp=0.36, harmonics=(1.0, 0.2), release=2.6)),
    )

    sounds["level"] = mix(
        tone(523.25, 0.17, amp=0.36, harmonics=(1.0, 0.3), release=2.4),
        place([], 0.13, tone(659.25, 0.18, amp=0.36, harmonics=(1.0, 0.3), release=2.4)),
        place([], 0.27, tone(987.77, 0.34, amp=0.42, harmonics=(1.0, 0.35, 0.12), release=2.0)),
        place([], 0.0, noise(0.2, amp=0.05, decay=8.0, smooth=0.5, seed=11)),
    )

    sounds["evolve"] = mix(
        tone(220.0, 0.85, amp=0.30, freq_end=660.0, harmonics=(1.0, 0.5, 0.25), release=1.1),
        tone(329.63, 0.8, amp=0.22, harmonics=(1.0, 0.4), release=1.2),
        place([], 0.16, tone(493.88, 0.62, amp=0.24, harmonics=(1.0, 0.3), release=1.4)),
        noise(0.7, amp=0.07, decay=2.6, smooth=0.32, seed=23),
    )

    sounds["kill"] = mix(
        tone(420.0, 0.22, amp=0.46, freq_end=90.0, harmonics=(1.0, 0.55, 0.2), release=2.2),
        noise(0.19, amp=0.30, decay=9.0, smooth=0.18, seed=31),
    )

    sounds["absorb"] = mix(
        tone(760.0, 0.42, amp=0.34, freq_end=120.0, harmonics=(1.0, 0.35), release=1.7),
        noise(0.42, amp=0.13, decay=3.4, smooth=0.14, seed=37),
        place([], 0.18, tone(110.0, 0.3, amp=0.26, harmonics=(1.0, 0.6), release=2.0)),
    )

    sounds["ability"] = mix(
        tone(310.0, 0.38, amp=0.40, freq_end=1250.0, harmonics=(1.0, 0.35, 0.15), release=1.8),
        place([], 0.12, tone(1244.5, 0.22, amp=0.22, harmonics=(1.0, 0.2), release=2.8)),
    )

    sounds["boss"] = mix(
        tone(72.0, 1.35, amp=0.45, freq_end=48.0, harmonics=(1.0, 0.6, 0.35), attack=0.03, release=0.8),
        tone(108.0, 1.2, amp=0.20, harmonics=(1.0, 0.5), attack=0.05, release=0.9),
        noise(1.3, amp=0.16, decay=1.6, smooth=0.05, seed=41),
        place([], 0.55, tone(155.56, 0.6, amp=0.16, freq_end=110.0, harmonics=(1.0, 0.4), release=1.5)),
    )

    sounds["warning"] = mix(
        tone(880.0, 0.20, amp=0.32, harmonics=(1.0, 0.25), release=1.8),
        place([], 0.26, tone(659.25, 0.20, amp=0.32, harmonics=(1.0, 0.25), release=1.8)),
        place([], 0.52, tone(880.0, 0.22, amp=0.32, harmonics=(1.0, 0.25), release=1.8)),
        place([], 0.78, tone(659.25, 0.30, amp=0.32, harmonics=(1.0, 0.25), release=1.8)),
    )

    sounds["victory"] = mix(
        tone(523.25, 0.30, amp=0.34, harmonics=(1.0, 0.35), release=2.0),
        place([], 0.20, tone(659.25, 0.30, amp=0.34, harmonics=(1.0, 0.35), release=2.0)),
        place([], 0.40, tone(783.99, 0.32, amp=0.34, harmonics=(1.0, 0.35), release=2.0)),
        place([], 0.62, tone(1046.5, 0.85, amp=0.42, harmonics=(1.0, 0.45, 0.18), release=1.3)),
        place([], 0.62, tone(523.25, 0.85, amp=0.24, harmonics=(1.0, 0.4), release=1.3)),
    )

    sounds["defeat"] = mix(
        tone(392.0, 0.42, amp=0.34, freq_end=310.0, harmonics=(1.0, 0.4), release=1.6),
        place([], 0.32, tone(293.66, 0.52, amp=0.34, freq_end=220.0, harmonics=(1.0, 0.4), release=1.5)),
        place([], 0.78, tone(196.0, 0.85, amp=0.40, freq_end=98.0, harmonics=(1.0, 0.5, 0.2), release=1.2)),
    )

    sounds["event"] = mix(
        tone(659.25, 0.72, amp=0.28, harmonics=(1.0, 0.4, 0.15), release=1.5),
        place([], 0.10, tone(987.77, 0.6, amp=0.22, harmonics=(1.0, 0.3), release=1.6)),
        tone(196.0, 0.7, amp=0.18, freq_end=392.0, harmonics=(1.0, 0.4), release=1.5),
    )

    sounds["ui"] = tone(1280.0, 0.065, amp=0.32, freq_end=940.0,
                        harmonics=(1.0, 0.22), attack=0.001, release=2.5)

    sounds["impact"] = mix(
        tone(140.0, 0.30, amp=0.46, freq_end=48.0, harmonics=(1.0, 0.6, 0.3), release=1.9),
        noise(0.28, amp=0.34, decay=11.0, smooth=0.16, seed=53),
    )

    return sounds


def build_ambient(duration=10.0):
    """Seamless drone: every partial completes an integer number of cycles."""
    count = int(MUSIC_SR * duration)
    out = [0.0] * count
    partials = [
        (55.0, 0.18, 5.0, 0.0),
        (82.5, 0.11, 2.0, 1.1),
        (110.0, 0.075, 1.0, 2.4),
        (165.0, 0.045, 5.0, 3.3),
        (220.0, 0.03, 2.0, 4.7),
        (330.0, 0.015, 1.0, 0.8),
    ]
    for freq, amp, lfo_cycles, phase in partials:
        cycles = int(round(freq * duration))
        actual_freq = cycles / duration
        for i in range(count):
            t = i / float(MUSIC_SR)
            lfo = 0.7 + 0.3 * math.sin(TAU * lfo_cycles * t / duration + phase)
            out[i] += amp * lfo * math.sin(TAU * actual_freq * t + phase)
    return out


def semitone(freq, steps):
    return freq * (2.0 ** (steps / 12.0))


def add_note(buffer, start, frequency, duration, amp, harmonics=(1.0, 0.35), seed_offset=0, sample_rate=MUSIC_SR):
    offset = int(start * sample_rate)
    count = int(duration * sample_rate)
    for i in range(count):
        if offset + i >= len(buffer):
            break
        k = i / float(count) if count > 1 else 0.0
        env = min(1.0, i / max(1.0, sample_rate * 0.008)) * ((1.0 - k) ** 1.6)
        value = 0.0
        for h, weight in enumerate(harmonics, start=1):
            value += weight * math.sin(TAU * frequency * h * i / sample_rate)
        buffer[offset + i] += amp * env * value


def add_kick(buffer, start, seed=0, sample_rate=MUSIC_SR):
    count = int(0.20 * sample_rate)
    offset = int(start * sample_rate)
    phase = 0.0
    for i in range(count):
        if offset + i >= len(buffer):
            break
        k = i / float(count)
        freq = 110.0 - 62.0 * k
        phase += TAU * freq / sample_rate
        buffer[offset + i] += 0.38 * math.sin(phase) * ((1.0 - k) ** 2.2)


def add_hat(buffer, start, amp, seed, sample_rate=MUSIC_SR):
    rng = random.Random(seed)
    count = int(0.055 * sample_rate)
    offset = int(start * sample_rate)
    state = 0.0
    for i in range(count):
        if offset + i >= len(buffer):
            break
        target = rng.uniform(-1.0, 1.0)
        state += 0.55 * (target - state)
        k = i / float(count)
        buffer[offset + i] += amp * state * ((1.0 - k) ** 2.4)


def build_music(track, duration=12.0):
    """Four short seamless loops with distinct roles (menu / arena / boss / flow)."""
    count = int(MUSIC_SR * duration)
    out = [0.0] * count
    step_duration = 0.25
    steps = int(duration / step_duration)

    configs = [
        {"root": 110.0, "minor": False, "bass": (0, 0, 7, 0, 5, 0, 7, 0),
         "lead_steps": (0, 3, 6, 10, 12, 15), "lead": (12, 15, 19, 16, 24, 19),
         "kick_every": 8, "hat_amp": 0.020, "bass_amp": 0.17, "lead_amp": 0.13, "seed": 101},
        {"root": 98.0, "minor": True, "bass": (0, 0, 0, 7, 0, 0, 10, 7),
         "lead_steps": (2, 5, 7, 10, 13, 15), "lead": (12, 15, 19, 22, 19, 15),
         "kick_every": 4, "hat_amp": 0.045, "bass_amp": 0.21, "lead_amp": 0.12, "seed": 202},
        {"root": 82.41, "minor": True, "bass": (0, 0, 1, 0, 6, 0, 1, 0),
         "lead_steps": (0, 4, 6, 8, 11, 14), "lead": (12, 13, 18, 12, 20, 13),
         "kick_every": 2, "hat_amp": 0.055, "bass_amp": 0.23, "lead_amp": 0.11, "seed": 303},
        {"root": 130.81, "minor": False, "bass": (0, 7, 5, 7, 0, 7, 9, 7),
         "lead_steps": (1, 3, 4, 7, 9, 11, 13, 15), "lead": (16, 19, 24, 19, 21, 24, 28, 24),
         "kick_every": 4, "hat_amp": 0.038, "bass_amp": 0.19, "lead_amp": 0.14, "seed": 404},
    ]
    cfg = configs[track % len(configs)]
    root = cfg["root"]

    # Sustained pad: whole-loop integer cycles keep the seam silent and clean.
    for steps_in_cycle, amp in ((1, 0.050), (2, 0.032), (3, 0.022)):
        cycles = steps // steps_in_cycle
        cycles = max(1, cycles)
        freq = semitone(root, 12)
        actual = round(freq * duration) / duration
        for i in range(count):
            t = i / float(MUSIC_SR)
            modulation = 0.65 + 0.35 * math.sin(TAU * cycles * t / duration)
            out[i] += amp * modulation * math.sin(TAU * actual * t + 0.4)
        fifth = semitone(root, 19)
        actual_fifth = round(fifth * duration) / duration
        for i in range(count):
            t = i / float(MUSIC_SR)
            modulation = 0.6 + 0.4 * math.sin(TAU * cycles * t / duration + 1.7)
            out[i] += amp * 0.6 * modulation * math.sin(TAU * actual_fifth * t)

    lead_index = {step: note for step, note in zip(cfg["lead_steps"], cfg["lead"])}
    for step in range(steps):
        start = step * step_duration
        if step % cfg["kick_every"] == 0:
            add_kick(out, start, seed=step)
        if step % 2 == 1:
            add_hat(out, start, cfg["hat_amp"], cfg["seed"] + step)
        bass_note = cfg["bass"][step % len(cfg["bass"])]
        add_note(out, start, semitone(root, bass_note), step_duration * 0.82,
                 cfg["bass_amp"], harmonics=(1.0, 0.28))
        if step in lead_index:
            note = lead_index[step]
            available = duration - start - 0.002
            note_duration = min(step_duration * 1.6, available)
            if note_duration > 0.02:
                add_note(out, start, semitone(root, note), note_duration,
                         cfg["lead_amp"], harmonics=(1.0, 0.30, 0.12))
    return out


def main():
    if not os.path.isdir(TARGET):
        os.makedirs(TARGET)

    produced = []
    for name, samples in build_sfx().items():
        produced.append((name, write_wav(name, samples, SFX_SR)))

    produced.append(("ambient", write_wav("ambient", build_ambient(), MUSIC_SR)))
    for track in range(4):
        name = "music_{0}".format(track)
        produced.append((name, write_wav(name, build_music(track), MUSIC_SR)))

    for name, path in produced:
        print("{0:<12} {1:>7.1f} KB  {2}".format(name, os.path.getsize(path) / 1024.0, path))
    print("generated {0} WAV files in {1}".format(len(produced), TARGET))


if __name__ == "__main__":
    main()
