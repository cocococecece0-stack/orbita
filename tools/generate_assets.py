#!/usr/bin/env python3
"""Генератор всего контента «Орбиты»: HD-фоны, текстуры планет, спрайты, музыка и SFX.

Детерминированный, только стандартная библиотека. Каждый файл реально используется
движком/рендером на обеих платформах. Выход пишется сразу в Android res/ и iOS Resources/.
"""
import math
import os
import random
import struct
import wave
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ANDROID_DRAW = os.path.join(ROOT, "app", "src", "main", "res", "drawable-nodpi")
ANDROID_RAW = os.path.join(ROOT, "app", "src", "main", "res", "raw")
IOS_RES = os.path.join(ROOT, "ios", "Orbit", "Resources")
TAU = 2.0 * math.pi

BG_W, BG_H = 720, 1280
PLANET_SIZE = 512
SPRITE_SIZE = 128
SR = 22050
MUSIC_SECONDS = 48


# ---------------------------------------------------------------- PNG ------
def png_chunk(tag: bytes, data: bytes) -> bytes:
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)


def write_png(path: str, width: int, height: int, buf: bytearray) -> None:
    raw = bytearray()
    stride = width * 4
    for y in range(height):
        raw.append(0)
        raw += buf[y * stride:(y + 1) * stride]
    data = (
        b"\x89PNG\r\n\x1a\n"
        + png_chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
        + png_chunk(b"IDAT", zlib.compress(bytes(raw), 9))
        + png_chunk(b"IEND", b"")
    )
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(data)


def low_noise(seed: int, w: int, h: int) -> list:
    r = random.Random(seed)
    return [r.random() for _ in range(w * h)]


def upscale(field: list, fw: int, fh: int, w: int, h: int) -> list:
    """Separable bilinear upscale; быстрее, чем хеш-шум на каждый пиксель."""
    row = [0.0] * w
    tmp = [0.0] * (w * fh)
    for y in range(fh):
        base = y * fw
        for x in range(w):
            pos = x * (fw - 1) / max(1, w - 1)
            x0 = int(pos)
            x1 = min(fw - 1, x0 + 1)
            t = pos - x0
            row[x] = field[base + x0] * (1 - t) + field[base + x1] * t
        tmp[y * w:(y + 1) * w] = row
    out = [0.0] * (w * h)
    for y in range(h):
        pos = y * (fh - 1) / max(1, h - 1)
        y0 = int(pos)
        y1 = min(fh - 1, y0 + 1)
        t = pos - y0
        a = y0 * w
        b = y1 * w
        d = y * w
        for x in range(w):
            out[d + x] = tmp[a + x] * (1 - t) + tmp[b + x] * t
    return out


def clamp(v: int) -> int:
    return 0 if v < 0 else 255 if v > 255 else v


# ------------------------------------------------------------- sectors ----
SECTORS = [
    dict(name="ОРБИТА-01", top=(26, 16, 58), bot=(4, 3, 14), cloud=(90, 70, 200), accent=(34, 211, 238)),
    dict(name="ПЕПЕЛ", top=(58, 20, 22), bot=(12, 4, 8), cloud=(230, 96, 62), accent=(255, 138, 76)),
    dict(name="НЕОНОВЫЙ ПОЯС", top=(44, 10, 56), bot=(9, 3, 20), cloud=(236, 64, 176), accent=(255, 92, 205)),
    dict(name="ЛЕДЯНОЕ КОЛЬЦО", top=(12, 34, 60), bot=(3, 9, 22), cloud=(92, 176, 240), accent=(120, 220, 255)),
    dict(name="СЕРДЦЕ ШТОРМА", top=(40, 20, 12), bot=(10, 6, 4), cloud=(250, 186, 70), accent=(255, 196, 84)),
    dict(name="ЧЕРВОТОЧИНА", top=(20, 16, 54), bot=(5, 3, 16), cloud=(126, 96, 244), accent=(168, 128, 255)),
    dict(name="ЯДРО ГИГАНТА", top=(52, 8, 20), bot=(14, 2, 8), cloud=(255, 62, 92), accent=(255, 88, 112)),
    dict(name="СЕКРЕТ", top=(8, 32, 30), bot=(2, 10, 12), cloud=(64, 240, 192), accent=(88, 255, 204)),
]


def gen_backgrounds() -> None:
    fw, fh = 180, 320
    for idx, s in enumerate(SECTORS):
        n1 = upscale(low_noise(1000 + idx, fw, fh), fw, fh, BG_W, BG_H)
        n2 = upscale(low_noise(2000 + idx, fw // 2, fh // 2), fw // 2, fh // 2, BG_W, BG_H)
        n3 = upscale(low_noise(3000 + idx, fw // 4, fh // 4), fw // 4, fh // 4, BG_W, BG_H)
        buf = bytearray(BG_W * BG_H * 4)
        rnd = random.Random(9000 + idx)
        stars = [(rnd.random() * BG_W, rnd.random() * BG_H, rnd.random()) for _ in range(320)]
        star_px = {}
        for sx, sy, bright in stars:
            x, y = int(sx), int(sy)
            b = int(90 + bright * 165)
            star_px[(x, y)] = (b, b, min(255, b + 25))
            if bright > 0.85:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    star_px[(x + dx, y + dy)] = (b // 3, b // 3, b // 2)
        top, bot, cloud, accent = s["top"], s["bot"], s["cloud"], s["accent"]
        for y in range(BG_H):
            ky = y / BG_H
            grad = ky * ky * 0.6 + ky * 0.4
            for x in range(BG_W):
                i = y * BG_W + x
                v = n1[i] * 0.55 + n2[i] * 0.30 + n3[i] * 0.15
                # Мягкие облачные полосы, вытянутые по горизонтали.
                band = 0.5 + 0.5 * math.sin(y * 0.011 + v * 7.0)
                density = max(0.0, v - 0.42) * (0.55 + band * 0.75)
                d = i * 4
                base_r = top[0] * (1 - grad) + bot[0] * grad
                base_g = top[1] * (1 - grad) + bot[1] * grad
                base_b = top[2] * (1 - grad) + bot[2] * grad
                buf[d] = clamp(int(base_r + cloud[0] * density))
                buf[d + 1] = clamp(int(base_g + cloud[1] * density))
                buf[d + 2] = clamp(int(base_b + cloud[2] * density))
                buf[d + 3] = 255
        for (x, y), (r, g, b) in star_px.items():
            if 0 <= x < BG_W and 0 <= y < BG_H:
                d = (y * BG_W + x) * 4
                buf[d] = max(buf[d], r)
                buf[d + 1] = max(buf[d + 1], g)
                buf[d + 2] = max(buf[d + 2], b)
        # Акцентная дуга-горизонт внизу — «подпись» сектора.
        for y in range(int(BG_H * 0.72), BG_H):
            t = (y - BG_H * 0.72) / (BG_H * 0.28)
            a = max(0.0, 1 - abs(t - 0.55) * 5.0) * 0.5
            for x in range(BG_W):
                d = (y * BG_W + x) * 4
                buf[d] = clamp(int(buf[d] * (1 - a) + accent[0] * a))
                buf[d + 1] = clamp(int(buf[d + 1] * (1 - a) + accent[1] * a))
                buf[d + 2] = clamp(int(buf[d + 2] * (1 - a) + accent[2] * a))
        write_png(os.path.join(ANDROID_DRAW, f"bg_{idx}.png"), BG_W, BG_H, buf)
        write_png(os.path.join(IOS_RES, f"bg_{idx}.png"), BG_W, BG_H, buf)
        print(f"  bg_{idx}.png {SECTORS[idx]['name']}")


PLANET_PALETTES = [
    ((110, 86, 255), (36, 18, 92), (172, 150, 255)),
    ((226, 108, 60), (86, 28, 20), (255, 188, 128)),
    ((236, 84, 190), (68, 14, 76), (255, 176, 236)),
    ((128, 206, 244), (24, 68, 112), (226, 248, 255)),
    ((250, 190, 84), (94, 52, 12), (255, 236, 176)),
    ((150, 116, 246), (38, 26, 96), (214, 190, 255)),
    ((255, 88, 112), (88, 12, 30), (255, 184, 184)),
    ((80, 240, 196), (10, 68, 60), (184, 255, 232)),
]


def gen_planets() -> None:
    fw, fh = 64, 64
    size = PLANET_SIZE
    radius = size * 0.40
    for idx, (base, dark, light) in enumerate(PLANET_PALETTES):
        n = upscale(low_noise(4000 + idx, fw, fh), fw, fh, size, size)
        detail = upscale(low_noise(5000 + idx, fw // 2, fh // 2), fw // 2, fh // 2, size, size)
        buf = bytearray(size * size * 4)
        for y in range(size):
            ny = (y - size / 2) / radius
            for x in range(size):
                nx = (x - size / 2) / radius
                dist = math.sqrt(nx * nx + ny * ny)
                d = (y * size + x) * 4
                if dist > 1.16:
                    buf[d + 3] = 0
                    continue
                # Атмосфера за пределами диска.
                if dist > 1.0:
                    a = max(0.0, 1 - (dist - 1.0) / 0.16) * 0.28
                    buf[d] = clamp(int(light[0]))
                    buf[d + 1] = clamp(int(light[1]))
                    buf[d + 2] = clamp(int(light[2]))
                    buf[d + 3] = clamp(int(a * 255))
                    continue
                i = y * size + x
                v = n[i] * 0.65 + detail[i] * 0.35
                bands = 0.5 + 0.5 * math.sin(ny * 9 + v * 5)
                shade = max(0.20, 0.42 + 0.58 * math.sqrt(max(0.0, 1 - nx * nx - ny * ny)))
                lambert = max(0.0, (-nx * 0.55 - ny * 0.35 + 0.75))
                shade *= 0.55 + 0.45 * lambert
                polar = max(0.0, abs(ny) - 0.72) * 2.4
                r = base[0] * (0.75 + v * 0.5) * shade + bands * 12 + polar * light[0] * 0.3
                g = base[1] * (0.75 + v * 0.5) * shade + bands * 8 + polar * light[1] * 0.3
                b = base[2] * (0.75 + v * 0.5) * shade + bands * 14 + polar * light[2] * 0.3
                edge = max(0.0, (dist - 0.93) / 0.07)
                r += light[0] * edge * 0.55
                g += light[1] * edge * 0.55
                b += light[2] * edge * 0.55
                buf[d] = clamp(int(r))
                buf[d + 1] = clamp(int(g))
                buf[d + 2] = clamp(int(b))
                buf[d + 3] = min(255, int(255 * max(0.0, (1.0 - dist) * 26)))
        write_png(os.path.join(ANDROID_DRAW, f"planet_{idx}.png"), size, size, buf)
        write_png(os.path.join(IOS_RES, f"planet_{idx}.png"), size, size, buf)
        print(f"  planet_{idx}.png")


SHIP_COLORS = [
    (34, 211, 238), (251, 113, 133), (163, 230, 53), (192, 132, 252),
    (255, 184, 84), (255, 92, 205), (120, 220, 255), (88, 255, 204),
]


def sprite_canvas() -> bytearray:
    return bytearray(SPRITE_SIZE * SPRITE_SIZE * 4)


def put(buf, x, y, r, g, b, a) -> None:
    if 0 <= x < SPRITE_SIZE and 0 <= y < SPRITE_SIZE:
        d = (y * SPRITE_SIZE + x) * 4
        if a >= buf[d + 3]:
            buf[d], buf[d + 1], buf[d + 2], buf[d + 3] = clamp(int(r)), clamp(int(g)), clamp(int(b)), clamp(int(a))


def blend(buf, x, y, r, g, b, a) -> None:
    if not (0 <= x < SPRITE_SIZE and 0 <= y < SPRITE_SIZE):
        return
    d = (y * SPRITE_SIZE + x) * 4
    na = a + buf[d + 3] * (1 - a)
    if na <= 0:
        return
    buf[d] = clamp(int((r * a + buf[d] * buf[d + 3] * (1 - a)) / na))
    buf[d + 1] = clamp(int((g * a + buf[d + 1] * buf[d + 3] * (1 - a)) / na))
    buf[d + 2] = clamp(int((b * a + buf[d + 2] * buf[d + 3] * (1 - a)) / na))
    buf[d + 3] = clamp(int(na * 255))


def disc(buf, cx, cy, rad, color, soft=0.0):
    for y in range(int(cy - rad - 2), int(cy + rad + 3)):
        for x in range(int(cx - rad - 2), int(cx + rad + 3)):
            dist = math.hypot(x - cx, y - cy)
            if dist > rad + soft:
                continue
            a = 1.0 if dist < rad - soft else max(0.0, (rad + soft - dist) / (2 * soft + 0.001))
            blend(buf, x, y, color[0], color[1], color[2], a)


def ring(buf, cx, cy, rad, width, color):
    for y in range(int(cy - rad - 2), int(cy + rad + 3)):
        for x in range(int(cx - rad - 2), int(cx + rad + 3)):
            dist = math.hypot(x - cx, y - cy)
            if abs(dist - rad) <= width:
                blend(buf, x, y, color[0], color[1], color[2], 1.0 - abs(dist - rad) / (width + 0.001))


def gen_sprites() -> None:
    c = SPRITE_SIZE / 2
    for idx, col in enumerate(SHIP_COLORS):
        b = sprite_canvas()
        disc(b, c, c, 44, (col[0] // 4, col[1] // 4, col[2] // 4), 16)
        # Корпус: треугольник, повёрнутый вверх, с крыльями и кокпитом.
        for y in range(20, 104):
            for x in range(24, 104):
                dx, dy = x - c, y - c
                nose = dy < -8 and abs(dx) < (-(dy + 8)) * 0.62
                wing = -8 <= dy <= 28 and abs(dx) < (28 - dy) * 1.4 + 10
                if nose or wing:
                    edge = abs(dx) / max(8.0, (28 - dy) * 1.4 + 10 if wing else (-(dy + 8)) * 0.62 + 6)
                    shade = 0.55 + 0.45 * (1 - min(1.0, edge))
                    blend(b, x, y, col[0] * shade + 20, col[1] * shade + 20, col[2] * shade + 20, 0.96)
        disc(b, c, c - 4, 9, (240, 252, 255), 4)
        disc(b, c, c + 26, 6, (255, 220, 150), 6)
        ring(b, c, c, 50, 2.5, col)
        save_sprite(f"ship_{idx}.png", b)

    for idx in range(8):
        b = sprite_canvas()
        rnd = random.Random(6000 + idx)
        pts = [(rnd.uniform(0.72, 1.18)) for _ in range(9)]
        for y in range(8, 120):
            for x in range(8, 120):
                ang = math.atan2(y - c, x - c)
                dist = math.hypot(x - c, y - c)
                seg = (ang / TAU * 9) % 9
                i0 = int(seg) % 9
                frac = seg - int(seg)
                rr = 40 * (pts[i0] * (1 - frac) + pts[(i0 + 1) % 9] * frac)
                if dist <= rr:
                    shade = 0.45 + 0.55 * max(0.0, (-math.cos(ang) * 0.5 - math.sin(ang) * 0.4 + 0.9))
                    blend(b, x, y, 78 * shade + 40, 62 * shade + 28, 130 * shade + 50, 0.95)
                    if dist > rr - 4:
                        blend(b, x, y, 170, 146, 255, 0.9)
        disc(b, c - 10, c - 12, 8, (30, 22, 56), 4)
        disc(b, c + 14, c + 8, 6, (30, 22, 56), 3)
        save_sprite(f"ast_{idx}.png", b)

    for idx in range(3):
        b = sprite_canvas()
        tail_col = [(255, 184, 107), (255, 120, 190), (140, 220, 255)][idx]
        for i in range(46):
            x0 = int(c + i * 1.15)
            disc(b, x0, c, 20 - i * 0.35, (tail_col[0], tail_col[1], tail_col[2]), 8)
        disc(b, c + 10, c, 24, (255, 244, 208), 8)
        disc(b, c + 10, c, 14, tail_col, 5)
        disc(b, c + 10, c, 6, (255, 255, 255), 3)
        save_sprite(f"comet_{idx}.png", b)

    power_cols = [
        (74, 222, 128), (167, 139, 250), (251, 146, 60), (255, 214, 84),
        (120, 240, 255), (255, 120, 190), (255, 236, 200),
    ]
    for idx, col in enumerate(power_cols):
        b = sprite_canvas()
        disc(b, c, c, 44, (col[0] // 6, col[1] // 6, col[2] // 6), 18)
        for y in range(6, 122):
            for x in range(6, 122):
                d = abs(x - c) + abs(y - c)
                if d < 34:
                    shade = 0.7 + 0.3 * (1 - d / 34)
                    blend(b, x, y, col[0] * shade, col[1] * shade, col[2] * shade, 0.94)
        ring(b, c, c, 38, 3, (255, 255, 255))
        # Символ: римские цифры I..VII в виде штрихов.
        for k in range(idx + 1):
            x = int(c - idx * 5 + k * 10)
            for y in range(int(c - 11), int(c + 12)):
                blend(b, x, y, 16, 12, 32, 0.95)
                blend(b, x + 1, y, 16, 12, 32, 0.95)
        save_sprite(f"power_{idx}.png", b)

    orb_cols = [(255, 200, 80), (255, 246, 190), (140, 255, 220)]
    for idx, col in enumerate(orb_cols):
        b = sprite_canvas()
        disc(b, c, c, 46, (col[0] // 5, col[1] // 5, col[2] // 5), 18)
        disc(b, c, c, 27, col, 8)
        disc(b, c - 8, c - 10, 8, (255, 255, 255), 6)
        ring(b, c, c, 34, 2, (255, 255, 255))
        save_sprite(f"orb_{idx}.png", b)

    b = sprite_canvas()
    disc(b, c, c, 50, (255, 255, 255), 34)
    save_sprite("fx_glow.png", b)
    b = sprite_canvas()
    ring(b, c, c, 46, 6, (255, 255, 255))
    save_sprite("fx_ring.png", b)
    b = sprite_canvas()
    for i in range(30):
        ang = i / 30 * TAU
        disc(b, c + math.cos(ang) * 30, c + math.sin(ang) * 30, 6, (255, 255, 255), 4)
    save_sprite("fx_burst.png", b)


def save_sprite(name: str, buf: bytearray) -> None:
    write_png(os.path.join(ANDROID_DRAW, name), SPRITE_SIZE, SPRITE_SIZE, buf)
    write_png(os.path.join(IOS_RES, name), SPRITE_SIZE, SPRITE_SIZE, buf)


# ------------------------------------------------------------- audio ------
def write_wav(path: str, samples: list, sr: int = SR) -> None:
    peak = max(1e-9, max(abs(s) for s in samples))
    gain = 0.94 / peak if peak > 0.94 else 1.0
    data = b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s * gain)) * 32767)) for s in samples)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(sr)
        f.writeframes(data)


def note(freq: float, phase: float) -> float:
    return math.sin(phase * freq)


MUSIC = [
    dict(root=110.0, scale=[0, 3, 5, 7, 10], tempo=104, pad=0.30, arp=0.22, drums=0.35, wave="sine"),
    dict(root=98.0, scale=[0, 2, 3, 7, 8], tempo=92, pad=0.36, arp=0.16, drums=0.28, wave="tri"),
    dict(root=123.5, scale=[0, 4, 7, 9, 11], tempo=126, pad=0.24, arp=0.28, drums=0.48, wave="square"),
    dict(root=87.3, scale=[0, 3, 7, 10, 14], tempo=84, pad=0.42, arp=0.14, drums=0.20, wave="sine"),
    dict(root=130.8, scale=[0, 2, 5, 7, 9], tempo=138, pad=0.20, arp=0.34, drums=0.55, wave="saw"),
    dict(root=116.5, scale=[0, 3, 6, 7, 10], tempo=112, pad=0.34, arp=0.24, drums=0.32, wave="tri"),
    dict(root=82.4, scale=[0, 1, 5, 7, 8], tempo=96, pad=0.44, arp=0.18, drums=0.42, wave="saw"),
    dict(root=146.8, scale=[0, 4, 6, 9, 11], tempo=144, pad=0.22, arp=0.30, drums=0.50, wave="square"),
]


def osc(phase: float, wave: str) -> float:
    s = math.sin(phase)
    if wave == "sine":
        return s
    if wave == "tri":
        return 2 / math.pi * math.asin(s)
    if wave == "square":
        return 1.0 if s >= 0 else -1.0
    if wave == "saw":
        return 2 * ((phase / TAU) % 1.0) - 1
    return s


def gen_music() -> None:
    n = int(SR * MUSIC_SECONDS)
    for idx, cfg in enumerate(MUSIC):
        out = [0.0] * n
        rnd = random.Random(7000 + idx)
        beat = 60.0 / cfg["tempo"]
        notes = cfg["scale"]
        root = cfg["root"]
        # Pad: медленно меняющийся аккорд.
        chord_len = beat * 8
        for start in range(0, n, int(chord_len * SR)):
            deg = notes[rnd.randrange(len(notes))]
            freqs = [root * 2 ** (deg / 12), root * 2 ** ((deg + notes[2]) / 12), root * 2 ** ((deg + notes[4]) / 12)]
            length = min(int(chord_len * SR), n - start)
            for i in range(length):
                env = min(1.0, i / (0.6 * SR)) * min(1.0, (length - i) / (0.9 * SR))
                t = (start + i) / SR
                v = sum(math.sin(TAU * f * t + k * 0.7) for k, f in enumerate(freqs)) / 3
                out[start + i] += v * cfg["pad"] * env
        # Bass: пульсирующий тон на каждую долю.
        for b in range(int(MUSIC_SECONDS / beat)):
            start = int(b * beat * SR)
            deg = notes[(b // 2) % len(notes)]
            f = root / 2 * 2 ** (deg / 12)
            length = min(int(beat * 0.85 * SR), n - start)
            for i in range(length):
                env = math.exp(-i / (0.22 * SR))
                out[start + i] += osc(TAU * f * i / SR, cfg["wave"]) * cfg["arp"] * 0.75 * env
        # Arp: шестнадцатые ноты с затуханием.
        step = beat / 4
        for s in range(int(MUSIC_SECONDS / step)):
            start = int(s * step * SR)
            deg = notes[(s * 2 + (s // 8)) % len(notes)]
            f = root * 2 * 2 ** (deg / 12)
            length = min(int(step * 0.9 * SR), n - start)
            for i in range(length):
                env = math.exp(-i / (0.055 * SR))
                out[start + i] += math.sin(TAU * f * i / SR) * cfg["arp"] * env * 0.55
        # Перкуссия: мягкий шум + низкий тон.
        for b in range(int(MUSIC_SECONDS / (beat / 2))):
            start = int(b * beat / 2 * SR)
            length = min(int(0.12 * SR), n - start)
            kick = b % 4 == 0
            for i in range(length):
                env = math.exp(-i / (0.028 * SR if kick else 0.018 * SR))
                if kick:
                    out[start + i] += math.sin(TAU * 62 * i / SR) * cfg["drums"] * env * 0.8
                else:
                    out[start + i] += rnd.uniform(-1, 1) * cfg["drums"] * env * 0.30
        # Кроссфейд beginning/end, чтобы петля не щёлкала.
        fade = int(0.35 * SR)
        for i in range(fade):
            t = i / fade
            out[i] *= t
            out[n - 1 - i] *= t
        name = f"music_{idx}.wav"
        for target in (ANDROID_RAW, IOS_RES):
            write_wav(os.path.join(target, name), out)
        print(f"  {name} {MUSIC_SECONDS}s")


def gen_sfx() -> None:
    def tone(f0, f1, dur, decay, harmonics=(1.0,), amp=0.6):
        n = int(SR * dur)
        out = [0.0] * n
        phase = 0.0
        for i in range(n):
            k = i / n
            freq = f0 * (f1 / f0) ** k
            phase += TAU * freq / SR
            s = sum(a * math.sin(phase * (j + 1)) for j, a in enumerate(harmonics))
            out[i] = amp * s * min(1.0, i / (0.003 * SR)) * math.exp(-decay * k)
        return out

    def noise(dur, decay, smooth=0.2, amp=0.5):
        n = int(SR * dur)
        out = []
        y = 0.0
        r = random.Random(1234 + int(dur * 1000))
        for i in range(n):
            y += smooth * (r.uniform(-1, 1) - y)
            out.append(amp * y * min(1.0, i / (0.002 * SR)) * math.exp(-decay * i / n))
        return out

    def mix(*layers):
        length = max(len(x) for x in layers)
        out = [0.0] * length
        for layer in layers:
            for i, v in enumerate(layer):
                out[i] += v
        return out

    sfx = {
        "tap": tone(420, 260, 0.09, 7),
        "dodge": tone(520, 900, 0.14, 6, (1.0, 0.3)),
        "orb": mix(tone(880, 1320, 0.20, 5, (1.0, 0.4)), tone(1320, 1760, 0.12, 7)),
        "powerup": mix(tone(440, 880, 0.28, 4, (1.0, 0.5, 0.2)), tone(660, 1320, 0.24, 5)),
        "shield": tone(300, 180, 0.34, 4, (1.0, 0.6, 0.3)),
        "crash": mix(noise(0.55, 5, 0.05, 0.8), tone(180, 42, 0.5, 5, (1.0, 0.4))),
        "level": mix(tone(523, 784, 0.35, 3, (1.0, 0.3)), tone(784, 1046, 0.28, 4)),
        "storm": mix(noise(0.7, 2.5, 0.08, 0.5), tone(120, 90, 0.7, 3)),
        "ui": tone(640, 720, 0.06, 10),
        "reward": mix(tone(659, 988, 0.30, 4), tone(988, 1318, 0.34, 5)),
        "achievement": mix(tone(523, 659, 0.22, 4), tone(659, 880, 0.26, 4), tone(880, 1175, 0.34, 4)),
        "buy": mix(tone(392, 523, 0.14, 6), tone(784, 784, 0.18, 6)),
        "boss": mix(tone(90, 60, 0.7, 2, (1.0, 0.7, 0.5)), noise(0.6, 3, 0.04, 0.35)),
        "boss_hit": mix(noise(0.16, 7, 0.1, 0.5), tone(220, 110, 0.18, 6)),
        "boss_down": mix(tone(330, 66, 0.8, 3, (1.0, 0.6)), noise(0.7, 3, 0.06, 0.5)),
        "wormhole": mix(tone(240, 1200, 0.42, 3, (1.0, 0.3)), tone(1200, 300, 0.42, 3)),
        "flare": mix(noise(0.45, 4, 0.12, 0.6), tone(160, 320, 0.4, 4)),
        "magnet": tone(300, 700, 0.3, 4, (1.0, 0.4)),
        "phase": tone(700, 200, 0.36, 4, (1.0, 0.5)),
        "freeze": mix(tone(1200, 400, 0.5, 3), tone(900, 300, 0.5, 4)),
        "revive": mix(tone(220, 440, 0.4, 3), tone(440, 880, 0.5, 3)),
        "secret": mix(tone(440, 554, 0.25, 4), tone(554, 659, 0.25, 4), tone(659, 880, 0.5, 3)),
        "victory": mix(tone(523, 659, 0.25, 3), tone(659, 784, 0.3, 3), tone(784, 1046, 0.55, 3), tone(1046, 1046, 0.5, 4)),
        "defeat": mix(tone(392, 294, 0.4, 3), tone(294, 196, 0.6, 3)),
        "tick": tone(900, 900, 0.05, 14),
        "gameover": mix(tone(330, 220, 0.5, 3), tone(220, 110, 0.8, 3)),
    }
    for name, samples in sfx.items():
        for target in (ANDROID_RAW, IOS_RES):
            write_wav(os.path.join(target, f"{name}.wav"), samples)
    print(f"  {len(sfx)} SFX")


def main() -> None:
    print("Генерация HD-фонов...")
    gen_backgrounds()
    print("Генерация текстур планет...")
    gen_planets()
    print("Генерация спрайтов...")
    gen_sprites()
    print("Генерация музыки...")
    gen_music()
    print("Генерация SFX...")
    gen_sfx()
    print("Готово.")


if __name__ == "__main__":
    main()
