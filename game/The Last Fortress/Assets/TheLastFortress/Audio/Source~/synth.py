"""
The Last Fortress — procedural audio for the MVP.

Every clip is synthesized from scratch by this script (numpy only): no samples, recordings or third-party sound
libraries are used, so the output is original project material. Rerunning it with the same numpy version reproduces
the clips (fixed seeds). Usage:

    python -I synth.py <output directory>

Writes 16-bit PCM mono WAV files at 44.1 kHz. Effects peak at about -1 dBFS; loops (ambience, music) are rendered
with their tails wrapped onto the start so they repeat without a seam.
"""
import os
import sys
import wave

import numpy as np

SR = 44100
rng = np.random.default_rng(7007)


# ---------------------------------------------------------------- primitives

def t_axis(seconds):
    return np.arange(int(seconds * SR)) / SR


def env_ad(n, attack, decay_tau):
    """Linear attack (s), exponential decay (time constant s)."""
    t = np.arange(n) / SR
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    return a * np.exp(-np.maximum(t - attack, 0) / decay_tau)


def env_adsr(n, a, d, s, r):
    t = np.arange(n) / SR
    total = n / SR
    out = np.ones(n) * s
    out[t < a] = t[t < a] / a
    m = (t >= a) & (t < a + d)
    out[m] = 1 - (1 - s) * (t[m] - a) / d
    rel = t > total - r
    out[rel] *= np.clip((total - t[rel]) / r, 0, 1)
    return out


def onepole_lp(x, cutoff):
    """Simple one-pole low-pass; cutoff may be a scalar or per-sample array (Hz)."""
    cutoff = np.broadcast_to(np.asarray(cutoff, dtype=float), x.shape)
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x)
    prev = 0.0
    for i in range(len(x)):
        prev = (1 - a[i]) * x[i] + a[i] * prev
        y[i] = prev
    return y


def lp(x, cutoff, order=2):
    for _ in range(order):
        x = onepole_lp(x, cutoff)
    return x


def hp(x, cutoff):
    return x - onepole_lp(x, cutoff)


def biquad_bp(x, f0, q):
    w0 = 2 * np.pi * f0 / SR
    alpha = np.sin(w0) / (2 * q)
    b0, b1, b2 = alpha, 0.0, -alpha
    a0, a1, a2 = 1 + alpha, -2 * np.cos(w0), 1 - alpha
    b0, b1, b2, a1, a2 = b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0
    y = np.zeros_like(x)
    x1 = x2 = y1 = y2 = 0.0
    for i in range(len(x)):
        v = b0 * x[i] + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1 = x1, x[i]
        y2, y1 = y1, v
        y[i] = v
    return y


def noise(seconds):
    return rng.standard_normal(int(seconds * SR))


def pink(seconds):
    n = int(seconds * SR)
    white = rng.standard_normal(n)
    spectrum = np.fft.rfft(white)
    f = np.fft.rfftfreq(n, 1 / SR)
    f[0] = 1
    spectrum /= np.sqrt(f)
    out = np.fft.irfft(spectrum, n)
    return out / np.max(np.abs(out))


def sine(freq, seconds, phase=0.0):
    t = t_axis(seconds)
    if np.ndim(freq):
        return np.sin(2 * np.pi * np.cumsum(freq) / SR + phase)
    return np.sin(2 * np.pi * freq * t + phase)


def saw(freq, seconds):
    """Band-limited-ish sawtooth from a handful of harmonics (keeps it soft and alias-free)."""
    t = t_axis(seconds)
    out = np.zeros_like(t)
    k = 1
    while k * freq < 9000 and k <= 24:
        out += np.sin(2 * np.pi * freq * k * t) / k
        k += 1
    return out * 0.6


def pluck(freq, seconds, brightness=0.5, seed=None):
    """Karplus-Strong string: lute/bowstring timbre."""
    r = np.random.default_rng(seed) if seed is not None else rng
    n = int(seconds * SR)
    period = max(2, int(round(SR / freq)))
    buf = r.uniform(-1, 1, period)
    buf = lp(buf, 2000 + 6000 * brightness, 1)
    out = np.empty(n)
    damp = 0.4985 + 0.0013 * brightness
    idx = 0
    for i in range(n):
        out[i] = buf[idx]
        nxt = (idx + 1) % period
        buf[idx] = damp * (buf[idx] + buf[nxt])
        idx = nxt
    return hp(out, 40)  # Karplus-Strong noise bursts carry DC


def modal(freqs, amps, taus, seconds):
    """Sum of exponentially damped partials: wood, metal, bells."""
    t = t_axis(seconds)
    out = np.zeros_like(t)
    for f, a, tau in zip(freqs, amps, taus):
        out += a * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * np.exp(-t / tau)
    return out


def place(dst, src, at_seconds, gain=1.0):
    i = int(at_seconds * SR)
    end = min(len(dst), i + len(src))
    if end > i:
        dst[i:end] += src[: end - i] * gain
    return dst


def wrap_place(dst, src, at_seconds, gain=1.0):
    """Like place, but anything past the end wraps to the start (seamless loops)."""
    i = int(at_seconds * SR) % len(dst)
    n = len(src)
    first = min(n, len(dst) - i)
    dst[i:i + first] += src[:first] * gain
    rest = n - first
    while rest > 0:
        chunk = min(rest, len(dst))
        dst[:chunk] += src[first:first + chunk] * gain
        first += chunk
        rest -= chunk
    return dst


def fade(x, fin=0.003, fout=0.02):
    n = len(x)
    a = min(n, int(fin * SR))
    b = min(n, int(fout * SR))
    if a > 0:
        x[:a] *= np.linspace(0, 1, a)
    if b > 0:
        x[-b:] *= np.linspace(1, 0, b)
    return x


def normalize(x, peak_db=-1.0):
    peak = np.max(np.abs(x))
    return x if peak == 0 else x / peak * (10 ** (peak_db / 20))


def soft_clip(x, drive=1.0):
    return np.tanh(x * drive) / np.tanh(drive)


def write(directory, name, x, peak_db=-1.0, loop=False):
    x = normalize(np.asarray(x, dtype=float), peak_db)
    if not loop:
        x = fade(x)
    pcm = np.clip(np.round(x * 32767), -32768, 32767).astype("<i2")
    path = os.path.join(directory, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print(f"{name}.wav  {len(x) / SR:5.2f} s")


def note(name):
    """'A4' style note names to Hz."""
    names = {"C": -9, "D": -7, "E": -5, "F": -4, "G": -2, "A": 0, "B": 2}
    semis = names[name[0]]
    rest = name[1:]
    if rest.startswith("#"):
        semis += 1
        rest = rest[1:]
    elif rest.startswith("b"):
        semis -= 1
        rest = rest[1:]
    return 440.0 * 2 ** ((semis + (int(rest) - 4) * 12) / 12)


# ---------------------------------------------------------------- instruments

def wood_knock(f=180, seconds=0.25, brightness=1.0):
    body = modal([f, f * 2.3, f * 3.9, f * 6.1], [1, 0.5 * brightness, 0.25 * brightness, 0.1], [0.05, 0.03, 0.02, 0.012], seconds)
    click = lp(noise(seconds), 3500, 1) * env_ad(int(seconds * SR), 0.0005, 0.006) * 0.6
    return body + click


def thud(f=90, seconds=0.35, grit=0.5):
    n = int(seconds * SR)
    t = t_axis(seconds)
    sweep = f * (1 + 1.5 * np.exp(-t / 0.02))
    tone = sine(sweep, seconds) * env_ad(n, 0.002, 0.08)
    body = lp(noise(seconds), 600, 2) * env_ad(n, 0.001, 0.05) * 3.0 * grit
    return tone + body


def bell(f, seconds=2.0, bright=1.0):
    ratios = [1, 2.0, 2.76, 3.9, 5.4, 6.8]
    amps = [1, 0.55, 0.4 * bright, 0.25 * bright, 0.15 * bright, 0.08 * bright]
    taus = [seconds * 0.45, seconds * 0.3, seconds * 0.22, seconds * 0.15, seconds * 0.1, seconds * 0.07]
    return modal([f * r for r in ratios], amps, taus, seconds)


def horn(freq, seconds, bright=0.6):
    """Soft brass: harmonic stack through a low-pass that opens with the envelope."""
    n = int(seconds * SR)
    vib = 1 + 0.004 * np.sin(2 * np.pi * 5.2 * t_axis(seconds)) * np.clip(t_axis(seconds) / 0.4, 0, 1)
    raw = saw(freq * vib.mean(), seconds)
    env = env_adsr(n, 0.08, 0.25, 0.75, min(0.4, seconds * 0.4))
    cutoff = 300 + 2600 * bright * env
    return lp(raw, cutoff, 2) * env


def pad(freqs, seconds, attack=1.5, release=1.5, cutoff=1400):
    n = int(seconds * SR)
    out = np.zeros(n)
    for f in freqs:
        for detune in (-0.25, 0.25):
            out += saw(f * 2 ** (detune / 12 / 10), seconds)
    out = lp(out, cutoff, 2)
    return out * env_adsr(n, attack, 0.5, 0.85, release) / max(1, len(freqs))


def drum(f=70, seconds=0.5, snap=0.3):
    n = int(seconds * SR)
    t = t_axis(seconds)
    sweep = f * (1 + 2.2 * np.exp(-t / 0.03))
    tone = sine(sweep, seconds) * env_ad(n, 0.001, 0.16)
    skin = lp(noise(seconds), 1800, 1) * env_ad(n, 0.0005, 0.025) * snap
    return tone + skin


def frame_drum_hit(seconds=0.25):
    n = int(seconds * SR)
    return biquad_bp(noise(seconds), 900, 1.2) * env_ad(n, 0.0005, 0.03) * 0.7 + drum(160, seconds, 0.2) * 0.4


# ---------------------------------------------------------------- effects

def ui_click():
    s = 0.06
    return modal([2100, 3400], [1, 0.4], [0.008, 0.005], s) + hp(noise(s), 2000) * env_ad(int(s * SR), 0.0003, 0.002) * 0.4


def ui_confirm():
    out = np.zeros(int(0.45 * SR))
    place(out, pluck(note("E5"), 0.4, 0.8, seed=1), 0.0, 0.8)
    place(out, pluck(note("A5"), 0.35, 0.8, seed=2), 0.07, 1.0)
    return out


def ui_reject():
    out = np.zeros(int(0.32 * SR))
    for i, f in enumerate((220, 175)):
        n = int(0.12 * SR)
        tone = lp(saw(f, 0.12), 900, 2) * env_ad(n, 0.004, 0.05)
        place(out, tone, i * 0.11, 1.0)
    return out


def build():
    out = np.zeros(int(0.55 * SR))
    place(out, wood_knock(170, 0.3), 0.0, 1.0)
    place(out, wood_knock(205, 0.3, 0.8), 0.13, 0.8)
    place(out, thud(110, 0.3, 0.6), 0.0, 0.5)
    return out


def sell():
    out = np.zeros(int(0.5 * SR))
    for i, f in enumerate((2600, 2200, 1900)):
        place(out, modal([f, f * 1.5, f * 2.7], [1, 0.5, 0.3], [0.08, 0.05, 0.03], 0.25), i * 0.07, 0.8 - i * 0.15)
    return out


def repair():
    out = np.zeros(int(0.6 * SR))
    for i in range(3):
        hammer = modal([900 + i * 40, 2300, 4100], [1, 0.5, 0.25], [0.04, 0.02, 0.012], 0.15) + wood_knock(240, 0.15) * 0.6
        place(out, hammer, i * 0.16, 0.9 - i * 0.1)
    return out


def reward():
    out = np.zeros(int(1.4 * SR))
    for i, n_ in enumerate(("D5", "F#5", "A5", "D6")):
        place(out, bell(note(n_), 1.1, 0.7), i * 0.09, 0.7)
    return out


def bow_shot(variant):
    s = 0.22
    n = int(s * SR)
    t = t_axis(s)
    f0 = [430, 470, 400][variant]
    twang = sine(f0 * (1 - 0.25 * t / s), s) * env_ad(n, 0.001, 0.035)
    whoosh = biquad_bp(noise(s), 2400 + 300 * variant, 2.0) * env_adsr(n, 0.02, 0.05, 0.4, 0.12) * 0.5
    return twang * 0.8 + whoosh


def impact(variant):
    s = 0.22
    n = int(s * SR)
    f = [180, 160, 205][variant]
    knock = wood_knock(f * 1.6, s, 0.5) * 0.5
    dull = lp(noise(s), 1200, 2) * env_ad(n, 0.0005, 0.018) * 2.5
    return knock + dull + thud(f, s, 0.2) * 0.4


def trap():
    s = 0.45
    n = int(s * SR)
    snap = hp(noise(s), 3000) * env_ad(n, 0.0002, 0.01)
    ring = modal([1250, 1830, 3150, 4400], [1, 0.6, 0.4, 0.2], [0.12, 0.08, 0.05, 0.03], s) * 0.5
    return snap + ring + thud(140, s, 0.3) * 0.4


def heavy_strike():
    s = 0.6
    n = int(s * SR)
    boom = thud(55, s, 1.0)
    crunch = lp(noise(s), 2200, 1) * env_ad(n, 0.001, 0.06) * 0.8
    splinter = wood_knock(140, s, 1.0) * 0.6
    return soft_clip(boom + crunch + splinter, 1.5)


def destroy():
    s = 1.4
    n = int(s * SR)
    rumble = lp(noise(s), 260, 2) * env_adsr(n, 0.01, 0.3, 0.5, 0.9) * 6
    out = rumble + thud(60, s, 1.0)
    times = np.sort(rng.uniform(0.02, 0.9, 14))
    for i, at in enumerate(times):
        place(out, wood_knock(rng.uniform(150, 400), 0.25, rng.uniform(0.3, 1.0)), at, 0.6 * (1 - at))
    return soft_clip(out, 1.2)


def keep_hit():
    s = 1.1
    gong = modal([118, 176, 243, 331, 467], [1, 0.7, 0.45, 0.3, 0.15], [0.55, 0.4, 0.28, 0.18, 0.1], s)
    out = gong * 0.9 + thud(70, s, 1.0) * 0.8
    stone = lp(noise(s), 1500, 1) * env_ad(int(s * SR), 0.0005, 0.03) * 1.2
    return soft_clip(out + stone, 1.3)


def night_start():
    out = np.zeros(int(2.6 * SR))
    place(out, horn(note("D3"), 0.9, 0.55), 0.0, 1.0)
    place(out, horn(note("A3"), 1.6, 0.7), 0.75, 1.0)
    place(out, drum(60, 1.0, 0.4), 0.75, 0.6)
    return out


def dawn():
    out = np.zeros(int(3.2 * SR))
    place(out, pad([note("D4"), note("F#4"), note("A4")], 3.0, 0.6, 1.4, 1800), 0.0, 1.0)
    for i, n_ in enumerate(("A5", "D6", "F#6")):
        place(out, bell(note(n_), 1.8, 0.5), 0.3 + i * 0.35, 0.35)
    return out


def golem():
    s = 3.4
    out = np.zeros(int(s * SR))
    place(out, horn(note("D2"), 2.4, 0.4), 0.0, 1.0)
    place(out, horn(note("Ab2"), 2.0, 0.45), 0.9, 0.8)
    rumble = lp(noise(s), 120, 2) * env_adsr(int(s * SR), 0.6, 0.5, 0.7, 1.2) * 5
    out += rumble
    for i in range(3):
        place(out, drum(45, 1.0, 0.5), 0.2 + i * 0.9, 0.9)
    return soft_clip(out, 1.1)


def victory():
    out = np.zeros(int(4.2 * SR))
    for i, (n_, d) in enumerate((("D4", 0.35), ("F#4", 0.35), ("A4", 0.35), ("D5", 1.8))):
        place(out, horn(note(n_), d + 0.2, 0.75), i * 0.33, 0.8)
    place(out, pad([note("D3"), note("A3"), note("F#4")], 3.0, 0.3, 1.5, 1600), 1.0, 0.6)
    for i in range(4):
        place(out, drum(80, 0.6, 0.3), i * 0.33, 0.5)
    return out


def defeat():
    out = np.zeros(int(4.5 * SR))
    for i, (n_, d) in enumerate((("A3", 0.7), ("F3", 0.7), ("D3", 2.2))):
        place(out, horn(note(n_), d + 0.3, 0.4), i * 0.7, 0.8)
    place(out, pad([note("D2"), note("F2"), note("A2")], 3.5, 0.8, 2.0, 700), 0.6, 0.8)
    place(out, drum(50, 1.5, 0.3), 1.4, 0.7)
    return out


# ---------------------------------------------------------------- plan 008 release threats
# Each draws from its own seed (see `seeded`), so adding them leaves the shared stream, and every 007 clip, unchanged.

def seeded(seed, fn):
    def run():
        global rng
        saved, rng = rng, np.random.default_rng(seed)
        try:
            return fn()
        finally:
            rng = saved
    return run


def cannon_shot():
    """Black-powder boom: muzzle crack, deep chest thump, short iron barrel ring, rolling smoke tail."""
    s = 1.1
    n = int(s * SR)
    crack = hp(noise(s), 1800) * env_ad(n, 0.0003, 0.008) * 1.4
    t = t_axis(s)
    boom = sine(42 * (1 + 2.5 * np.exp(-t / 0.015)), s) * env_ad(n, 0.001, 0.22) * 1.6
    blast = lp(noise(s), 900, 2) * env_ad(n, 0.001, 0.07) * 4.0
    tail = lp(noise(s), 180, 2) * env_adsr(n, 0.02, 0.2, 0.35, 0.7) * 5.0
    ring = modal([310, 523, 870], [1, 0.5, 0.25], [0.12, 0.08, 0.05], s) * 0.25
    return soft_clip(crack + boom + blast + tail + ring, 1.6)


def raider_shot():
    """Enemy short bow: slacker and lower than the tower's, a rattle of loose string and a darker whoosh."""
    s = 0.24
    n = int(s * SR)
    t = t_axis(s)
    twang = sine(285 * (1 - 0.4 * t / s), s) * env_ad(n, 0.001, 0.03)
    rattle = biquad_bp(noise(s), 650, 3.0) * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 55 * t))) * env_ad(n, 0.001, 0.025) * 0.6
    whoosh = biquad_bp(noise(s), 1500, 1.6) * env_adsr(n, 0.015, 0.05, 0.35, 0.12) * 0.55
    return twang * 0.8 + rattle + whoosh


def fuse_lit():
    """Struck spark, then a hissing fuse with crackling pops: a warning that should cut through combat."""
    s = 0.85
    n = int(s * SR)
    strike = biquad_bp(noise(s), 3200, 1.5) * env_ad(n, 0.0005, 0.02) * 1.6
    hiss_env = np.clip(t_axis(s) / 0.06, 0, 1) * env_adsr(n, 0.001, 0.1, 0.8, 0.3)
    hiss = biquad_bp(noise(s), 5200, 0.9) * hiss_env * 0.5
    out = strike + hiss
    for at in np.sort(rng.uniform(0.05, 0.7, 11)):
        pop = hp(noise(0.012), 2500) * env_ad(int(0.012 * SR), 0.0002, 0.002)
        place(out, pop, at, rng.uniform(0.6, 1.2))
    return out


def bomber_blast():
    """Powder keg: sharp detonation crack, hollow boom, gravel and debris rain. Stone and grit, not splintering wood."""
    s = 1.5
    n = int(s * SR)
    t = t_axis(s)
    crack = noise(s) * env_ad(n, 0.0002, 0.006) * 1.5
    boom = sine(50 * (1 + 2 * np.exp(-t / 0.02)), s) * env_ad(n, 0.001, 0.18) * 1.5
    body = biquad_bp(noise(s), 420, 0.8) * env_ad(n, 0.002, 0.12) * 2.0
    rumble = lp(noise(s), 220, 2) * env_adsr(n, 0.01, 0.25, 0.4, 0.9) * 5.0
    out = crack + boom + body + rumble
    for at in np.sort(rng.uniform(0.12, 1.1, 22)):
        grit = biquad_bp(noise(0.03), rng.uniform(2200, 4800), 4.0) * env_ad(int(0.03 * SR), 0.0003, 0.006)
        place(out, grit, at, 0.9 * (1.2 - at))
    return soft_clip(out, 1.5)


def commander_arrival():
    """War-horn call rising a fifth over a quickening frame-drum roll: an army arrives (the Golem stinger is a slow, low tread)."""
    s = 3.2
    out = np.zeros(int(s * SR))
    place(out, horn(note("A2"), 0.7, 0.75), 0.0, 0.9)
    place(out, horn(note("E3"), 0.7, 0.8), 0.55, 0.9)
    place(out, horn(note("A3"), 1.9, 0.9), 1.1, 1.0)
    place(out, horn(note("E3"), 1.9, 0.6), 1.1, 0.5)
    at, gap = 0.0, 0.22
    while at < 1.05:
        place(out, frame_drum_hit(0.2), at, 0.35 + 0.4 * at)
        at += gap
        gap = max(0.07, gap * 0.85)
    place(out, drum(55, 1.4, 0.6), 1.1, 1.0)
    return soft_clip(out, 1.2)


# ---------------------------------------------------------------- loops

def loopify(x, seconds, xfade=2.0):
    """Equal-power crossfade of the rendered overrun onto the start: a seamless loop of exactly `seconds`."""
    n, m = int(seconds * SR), int(xfade * SR)
    y = x[:n].copy()
    k = np.linspace(0, np.pi / 2, m)
    y[:m] = x[:m] * np.sin(k) + x[n:n + m] * np.cos(k)
    return y


def ambience_day():
    return loopify(_ambience_day(34.0), 32.0)


def ambience_night():
    return loopify(_ambience_night(34.0), 32.0)


def _ambience_day(s):
    n = int(s * SR)
    t = t_axis(s)
    gust = 0.55 + 0.45 * np.sin(2 * np.pi * t / s * 3) * np.sin(2 * np.pi * t / s * 2 + 1)
    wind = lp(pink(s), 700, 2) * gust * 0.5
    out = wind.copy()
    # sparse songbird chirps (FM sweeps), distant
    for at in np.sort(rng.uniform(0, s, 16)):
        chirps = int(rng.integers(2, 5))
        base = rng.uniform(2800, 4200)
        for c in range(chirps):
            d = rng.uniform(0.05, 0.11)
            tt = t_axis(d)
            f = base * (1 + 0.25 * np.sin(np.pi * tt / d)) * rng.uniform(0.95, 1.05)
            chirp = sine(f, d) * np.sin(np.pi * tt / d) ** 2
            wrap_place(out, chirp, at + c * (d + 0.04), 0.05)
    return out


def _ambience_night(s):
    t = t_axis(s)
    gust = 0.6 + 0.4 * np.sin(2 * np.pi * t / s * 2 + 0.5)
    wind = lp(pink(s), 380, 2) * gust * 0.7
    out = wind.copy()
    # crickets: pulsed narrow tone, two insects slightly detuned and out of phase
    for f, rate, phase, g in ((4300, 2.1, 0.0, 0.035), (4650, 1.7, 1.3, 0.025)):
        trill = sine(f, s) * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 28 * t))) * 0.5
        gate = np.clip(np.sin(2 * np.pi * rate / 8 * t + phase) * 3, 0, 1) ** 2
        out += lp(trill * gate, 6000, 1) * g * 4
    return out


BPM_PLAN, BPM_COMBAT, BPM_FINALE = 84, 108, 126


def music_planning():
    beat = 60 / BPM_PLAN
    bars = 16
    s = bars * 4 * beat
    out = np.zeros(int(s * SR))
    chords = [("D3", "A3", "D4", "F4"), ("C3", "G3", "C4", "E4"), ("Bb2", "F3", "Bb3", "D4"), ("C3", "G3", "C4", "E4")]
    for bar in range(bars):
        ch = chords[bar % 4]
        t0 = bar * 4 * beat
        wrap_place(out, pad([note(x) for x in ch[:3]], 4 * beat + 0.8, 0.9, 0.9, 1100), t0, 0.35)
        pattern = [0, 2, 3, 1, 2, 3, 1, 2]
        for k, idx in enumerate(pattern):
            if bar % 8 == 7 and k > 5:
                continue
            f = note(ch[idx]) * 2
            wrap_place(out, pluck(f, 1.2, 0.45, seed=bar * 16 + k), t0 + k * beat / 2, 0.32 if k % 2 == 0 else 0.24)
    # simple melody over the second half
    melody = [("A4", 2), ("F4", 1), ("G4", 1), ("A4", 3), ("D5", 1), ("C5", 2), ("Bb4", 1), ("A4", 1), ("G4", 4),
              ("A4", 2), ("C5", 2), ("D5", 3), ("E5", 1), ("F5", 2), ("E5", 1), ("C5", 1), ("D5", 4)]
    at = 8 * 4 * beat
    for name, beats in melody:
        wrap_place(out, pluck(note(name), beats * beat + 0.6, 0.6, seed=500 + int(at * 10)), at, 0.3)
        at += beats * beat
    return out


def music_combat(bpm=BPM_COMBAT, intensity=1.0, bars=16):
    beat = 60 / bpm
    s = bars * 4 * beat
    out = np.zeros(int(s * SR))
    roots = ["D2", "D2", "Bb1", "C2"]
    for bar in range(bars):
        t0 = bar * 4 * beat
        root = note(roots[bar % 4])
        # driving low ostinato on eighths
        for k in range(8):
            f = root * (2 if k in (3, 7) else 1)
            n_ = int(beat / 2 * SR)
            tone = lp(saw(f * 2, beat / 2), 500 + 400 * intensity, 2) * env_ad(n_, 0.004, beat / 3)
            wrap_place(out, tone, t0 + k * beat / 2, 0.42)
        # war drums
        for k in range(4):
            wrap_place(out, drum(58, 0.6, 0.35), t0 + k * beat, 0.9 if k % 2 == 0 else 0.6)
        if intensity > 1.0:
            for k in range(8):
                wrap_place(out, frame_drum_hit(0.2), t0 + k * beat / 2 + beat / 4, 0.25)
        else:
            for k in (1, 3):
                wrap_place(out, frame_drum_hit(0.2), t0 + k * beat + beat / 2, 0.3)
        # brass stab every two bars
        if bar % 2 == 0:
            for f in (root * 4, root * 4 * 1.5):
                wrap_place(out, horn(f, beat * 1.5, 0.5 * intensity), t0, 0.22)
    # high tension line
    line = ["A4", "Bb4", "A4", "G4", "F4", "G4", "A4", "D5"]
    for i in range(bars // 2):
        wrap_place(out, horn(note(line[i % len(line)]), 8 * beat * 0.9, 0.35), (i * 2 + 1) * 4 * beat, 0.12 * intensity)
    return soft_clip(out, 1.2)


def music_finale():
    return music_combat(BPM_FINALE, 1.35, 16)


def music_conclusion():
    beat = 60 / 72
    bars = 8
    s = bars * 4 * beat
    out = np.zeros(int(s * SR))
    chords = [("D3", "F#3", "A3"), ("G2", "D3", "B3"), ("B2", "F#3", "D4"), ("A2", "E3", "C#4")]
    for bar in range(bars):
        ch = chords[bar % 4]
        wrap_place(out, pad([note(x) for x in ch], 4 * beat + 1.5, 1.4, 1.5, 1000), bar * 4 * beat, 0.5)
        wrap_place(out, bell(note(ch[2]) * 2, 2.5, 0.4), bar * 4 * beat, 0.12)
    return out


# ---------------------------------------------------------------- plan 019 hero (release-rules-4)
# The hero's own families, so its swing, blade, hurt, fall and steps no longer borrow tower and Keep sounds. Each is
# seeded on its own (see CLIPS), so adding them changes no earlier clip.

def _bands(x, centres, weights, q=1.6):
    out = np.zeros_like(x)
    for f, w in zip(centres, weights):
        out += biquad_bp(x, f, q) * w
    return out


def hero_swing(variant):
    """Blade whoosh: air rushing past a moving edge, rising to the contact and trailing off; no impact."""
    s = 0.24
    n = int(s * SR)
    t = t_axis(s)
    x = noise(s)
    peak = [0.085, 0.095][variant]
    low = _bands(x, [450, 700], [1.0, 0.7])
    high = _bands(x, [1600, 2600, 3800], [0.8, 0.6, 0.3], 2.2)
    rise = np.clip(t / peak, 0, 1) ** 2
    fall = np.exp(-np.clip(t - peak, 0, None) / 0.045)
    env = rise * fall
    return (low * (1 - rise * 0.6) + high * rise) * env * 1.4


def hero_impact(variant):
    """The blade biting: a bright edge tick, a padded leather-and-body thump and a short iron ring."""
    s = 0.24
    n = int(s * SR)
    tick = hp(noise(s), 2600) * env_ad(n, 0.0002, 0.005) * 0.9
    body = thud([115, 100][variant], s, 0.7) * 0.8
    ring = modal([2050, 3320, 4710], [1.0, 0.55, 0.3], [0.05, 0.035, 0.02], s) * 0.22
    padded = lp(noise(s), 900, 2) * env_ad(n, 0.0008, 0.03) * 1.6
    return tick + body + ring + padded


def hero_hurt():
    """Taking a hit: a short voiced exhale (a low buzz through two vowel formants) over a dull body blow."""
    s = 0.32
    n = int(s * SR)
    t = t_axis(s)
    f0 = 118 * (1 - 0.18 * t / s)
    voice = np.sign(np.sin(2 * np.pi * np.cumsum(f0) / SR)) * 0.5
    voice = _bands(voice, [650, 1150, 2400], [1.0, 0.6, 0.15], 4.0)
    env = env_ad(n, 0.012, 0.09)
    return voice * env * 1.3 + thud(85, s, 0.5) * 0.6


def hero_down():
    """The hero falling: knees, then the body and shield on the ground, a rattle of gear and settling dust."""
    s = 0.8
    out = np.zeros(int(s * SR))
    place(out, thud(95, 0.4, 0.6), 0.0, 0.7)
    place(out, thud(70, 0.5, 0.8), 0.14, 1.0)
    place(out, modal([1300, 2150, 3400], [1.0, 0.6, 0.3], [0.08, 0.05, 0.03], 0.3) * 0.3, 0.16, 1.0)
    dust = lp(noise(s), 700, 2) * env_ad(len(out), 0.18, 0.2) * 0.35
    return out + dust


def footstep(variant):
    """A boot on packed earth and grass: a soft heel thump and a short gritty scuff."""
    s = 0.12
    n = int(s * SR)
    cut = [900, 750, 1050, 820][variant]
    scuff = lp(noise(s), cut, 2) * env_ad(n, 0.002, 0.018) * 1.6
    grit = hp(noise(s), 2500) * env_ad(n, 0.001, 0.006) * 0.12
    heel = thud([72, 66, 78, 69][variant], s, 0.25) * 0.5
    return scuff + grit + heel


CLIPS = {
    "ui_click": (ui_click, -6),
    "ui_confirm": (ui_confirm, -3),
    "ui_reject": (ui_reject, -3),
    "build": (build, -1),
    "sell": (sell, -3),
    "repair": (repair, -2),
    "reward": (reward, -2),
    "shot_0": (lambda: bow_shot(0), -4),
    "shot_1": (lambda: bow_shot(1), -4),
    "shot_2": (lambda: bow_shot(2), -4),
    "impact_0": (lambda: impact(0), -3),
    "impact_1": (lambda: impact(1), -3),
    "impact_2": (lambda: impact(2), -3),
    "trap": (trap, -2),
    "heavy_strike": (heavy_strike, -1),
    "destroy": (destroy, -1),
    "keep_hit": (keep_hit, -0.5),
    "night_start": (night_start, -1),
    "dawn": (dawn, -2),
    "golem": (golem, -0.5),
    "victory": (victory, -1),
    "defeat": (defeat, -1),
    "cannon_shot": (seeded(8001, cannon_shot), -1),
    "raider_shot": (seeded(8002, raider_shot), -4),
    "fuse_lit": (seeded(8003, fuse_lit), -2),
    "bomber_blast": (seeded(8004, bomber_blast), -1),
    "commander_arrival": (seeded(8005, commander_arrival), -0.5),
    "hero_swing_0": (seeded(9001, lambda: hero_swing(0)), -6),
    "hero_swing_1": (seeded(9002, lambda: hero_swing(1)), -6),
    "hero_impact_0": (seeded(9003, lambda: hero_impact(0)), -3),
    "hero_impact_1": (seeded(9004, lambda: hero_impact(1)), -3),
    "hero_hurt": (seeded(9005, hero_hurt), -4),
    "hero_down": (seeded(9006, hero_down), -3),
    "footstep_0": (seeded(9007, lambda: footstep(0)), -10),
    "footstep_1": (seeded(9008, lambda: footstep(1)), -10),
    "footstep_2": (seeded(9009, lambda: footstep(2)), -10),
    "footstep_3": (seeded(9010, lambda: footstep(3)), -10),
}

LOOPS = {
    "ambience_day": (ambience_day, -10),
    "ambience_night": (ambience_night, -9),
    "music_planning": (music_planning, -4),
    "music_combat": (music_combat, -3),
    "music_finale": (music_finale, -2.5),
    "music_conclusion": (music_conclusion, -5),
}


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else "."
    only = set(sys.argv[2:])
    os.makedirs(out_dir, exist_ok=True)
    for name, (fn, peak) in CLIPS.items():
        if not only or name in only:
            write(out_dir, name, fn(), peak)
    for name, (fn, peak) in LOOPS.items():
        if not only or name in only:
            write(out_dir, name, fn(), peak, loop=True)


if __name__ == "__main__":
    main()
