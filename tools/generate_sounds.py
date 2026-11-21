#!/usr/bin/env python3
"""Synthesizes all game sounds from scratch (no external samples, no licensing issues).

Outputs 16-bit mono PCM WAV files into the Android raw resources and the iOS
bundle resources. Run from the repository root:

    python3 tools/generate_sounds.py
"""

import math
import os
import random
import struct
import wave

SR = 44100
AMBIENT_SR = 22050
TAU = 2.0 * math.pi

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TARGETS = [
    os.path.join(ROOT, "app", "src", "main", "res", "raw"),
    os.path.join(ROOT, "ios", "Orbit", "Resources"),
]


def write_wav(path, samples, sr):
    peak = max(1e-9, max(abs(s) for s in samples))
    gain = 0.92 / peak if peak > 0.92 else 1.0
    data = b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s * gain)) * 32767)) for s in samples)
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(sr)
        f.writeframes(data)


def tone(f0, f1, dur, decay=5.0, amp=0.6, harmonics=(1.0,), sr=SR):
    n = int(sr * dur)
    out = [0.0] * n
    phase = 0.0
    for i in range(n):
        k = i / n
        freq = f0 * (f1 / f0) ** k
        phase += TAU * freq / sr
        sample = 0.0
        for h, a in enumerate(harmonics, start=1):
            sample += a * math.sin(phase * h)
        attack = min(1.0, (i / sr) / 0.004)
        out[i] = amp * sample * attack * math.exp(-decay * k)
    return out


def noise_burst(dur, decay=6.0, amp=0.5, smooth=0.25, sr=SR):
    n = int(sr * dur)
    out = [0.0] * n
    y = 0.0
    for i in range(n):
        x = random.uniform(-1.0, 1.0)
        y += smooth * (x - y)
        k = i / n
        attack = min(1.0, (i / sr) / 0.002)
        out[i] = amp * y * attack * math.exp(-decay * k)
    return out


def mix(*layers):
    length = max(len(layer) for layer in layers)
    out = [0.0] * length
    for layer in layers:
        for i, value in enumerate(layer):
            out[i] += value
    return out


def silence(dur, sr=SR):
    return [0.0] * int(sr * dur)


def build_sounds():
    sounds = {}

    # UI tap: short downward blip.
    sounds["tap"] = tone(1350, 950, 0.055, decay=9.0, amp=0.45)

    # Dodge: tiny tick, gets pitched up by the combo multiplier at playback.
    sounds["dodge"] = tone(760, 720, 0.045, decay=14.0, amp=0.35, harmonics=(1.0, 0.25))

    # Orb pickup: bright two-note chime.
    sounds["orb"] = mix(
        tone(1046.5, 1046.5, 0.18, decay=7.0, amp=0.5),
        tone(1568.0, 1568.0, 0.16, decay=8.0, amp=0.35),
        silence(0.03) + tone(2093.0, 2093.0, 0.14, decay=10.0, amp=0.18),
    )

    # Power-up: rising sweep.
    sounds["powerup"] = mix(
        tone(420, 1500, 0.26, decay=3.2, amp=0.45, harmonics=(1.0, 0.3)),
        noise_burst(0.2, decay=9.0, amp=0.06, smooth=0.4),
    )

    # Shield break: metallic hit.
    sounds["shield"] = mix(
        noise_burst(0.32, decay=7.0, amp=0.55, smooth=0.18),
        tone(320, 90, 0.3, decay=6.0, amp=0.5, harmonics=(1.0, 0.5, 0.25)),
    )

    # Crash: noise + falling low sine.
    sounds["crash"] = mix(
        noise_burst(0.7, decay=4.5, amp=0.7, smooth=0.12),
        tone(190, 42, 0.65, decay=4.0, amp=0.6, harmonics=(1.0, 0.6, 0.3)),
        tone(95, 30, 0.5, decay=5.0, amp=0.4),
    )

    # Level up: three-note arpeggio.
    sounds["level"] = mix(
        tone(523.25, 523.25, 0.16, decay=8.0, amp=0.4),
        silence(0.09) + tone(659.25, 659.25, 0.16, decay=8.0, amp=0.4),
        silence(0.18) + tone(783.99, 783.99, 0.28, decay=6.0, amp=0.45),
    )

    # Storm warning: deep rumble that rises then falls.
    sounds["storm"] = mix(
        noise_burst(1.1, decay=2.4, amp=0.55, smooth=0.05),
        tone(78, 44, 1.1, decay=1.7, amp=0.55, harmonics=(1.0, 0.5)),
        tone(160, 80, 0.9, decay=2.5, amp=0.18),
    )

    return sounds


def build_ambient(dur=12.0):
    """Seamless loop: every partial is an integer number of cycles per loop."""
    n = int(AMBIENT_SR * dur)
    out = [0.0] * n
    partials = [
        (110.0, 0.16, 6.0, 0.0),
        (165.0, 0.10, 4.0, 1.2),
        (220.0, 0.07, 3.0, 2.1),
        (275.0, 0.05, 6.0, 4.0),
        (330.0, 0.04, 4.0, 0.7),
        (440.0, 0.025, 3.0, 5.1),
        (660.0, 0.012, 6.0, 2.6),
    ]
    for freq, amp, lfo_period, phase in partials:
        for i in range(n):
            t = i / AMBIENT_SR
            lfo = 0.65 + 0.35 * math.sin(TAU * t / lfo_period + phase)
            out[i] += amp * lfo * math.sin(TAU * freq * t + phase)
    # Fade the very start/end only by a tiny amount to avoid a DC click;
    # the loop itself is seamless because of integer cycle counts.
    fade = int(AMBIENT_SR * 0.01)
    for i in range(fade):
        g = i / fade
        out[i] *= g
        out[-1 - i] *= g
    return out


def main():
    random.seed(7)
    for target in TARGETS:
        os.makedirs(target, exist_ok=True)

    for name, samples in build_sounds().items():
        for target in TARGETS:
            path = os.path.join(target, name + ".wav")
            write_wav(path, samples, SR)
            print(f"{path}  {os.path.getsize(path) / 1024:.1f} KB")

    ambient = build_ambient()
    for target in TARGETS:
        path = os.path.join(target, "ambient.wav")
        write_wav(path, ambient, AMBIENT_SR)
        print(f"{path}  {os.path.getsize(path) / 1024:.1f} KB")


if __name__ == "__main__":
    main()
