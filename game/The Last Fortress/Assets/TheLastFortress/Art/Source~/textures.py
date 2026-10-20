"""
Procedural, tileable material textures for The Last Fortress art slice (plan 011).

Provenance: authored as code by the agent for this project; no external images, fonts or paid assets. Deterministic
(fixed seeds), so rerunning reproduces the same PNGs byte-for-byte with the same numpy/Pillow versions.

Usage (from anywhere):  python textures.py  -> writes ../Textures/*.png next to this folder.

Every albedo keeps low contrast on purpose: one tile is ~50 px at 1080p default zoom, so detail must read as material
(grain, mortar, shingle rows) without turning into noise. Colour comes mostly from the material's base colour; cloth and
iron maps are near-neutral so the palette stays in the materials.
"""
import os

import numpy as np
from PIL import Image

SIZE = 512
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Textures")


def periodic_noise(rng, size, scale, octaves=4, persistence=0.5):
    """Tileable fBm: white noise low-passed in the frequency domain (periodic by construction). Returns 0..1."""
    total = np.zeros((size, size))
    amp, norm = 1.0, 0.0
    fy = np.fft.fftfreq(size)[:, None]
    fx = np.fft.fftfreq(size)[None, :]
    f = np.sqrt(fx * fx + fy * fy)
    for o in range(octaves):
        cutoff = scale * (2 ** o) / size
        white = rng.standard_normal((size, size))
        spec = np.fft.fft2(white) * np.exp(-(f / max(cutoff, 1e-6)) ** 2)
        layer = np.real(np.fft.ifft2(spec))
        layer = (layer - layer.mean()) / (layer.std() + 1e-9)
        total += layer * amp
        norm += amp
        amp *= persistence
    total /= norm
    return np.clip(0.5 + total * 0.18, 0, 1)


def normal_from_height(h, strength):
    """Tangent-space normal map (OpenGL/Unity convention, +Y up in the green channel) from a tileable height field."""
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * strength
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * strength
    n = np.dstack([-dx, dy, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return ((n * 0.5 + 0.5) * 255).astype(np.uint8)


def save(name, rgb):
    os.makedirs(OUT, exist_ok=True)
    arr = np.clip(rgb, 0, 255).astype(np.uint8)
    Image.fromarray(arr).save(os.path.join(OUT, name + ".png"), optimize=True)


def tint(gray, color, variation):
    """Grey detail (0..1) times a base colour, with +-variation brightness swing around 1."""
    c = np.array(color, dtype=float)
    k = 1.0 + (gray[..., None] - 0.5) * 2 * variation
    return c[None, None, :] * k


def stone():
    """Ashlar courses: four rows of irregular blocks, recessed mortar, per-block tint, chipped edges, faint grain."""
    rng = np.random.default_rng(11)
    h = np.zeros((SIZE, SIZE))
    tintmap = np.zeros((SIZE, SIZE))
    rows = 4
    rh = SIZE // rows
    yy, xx = np.mgrid[0:SIZE, 0:SIZE]
    edge = np.zeros((SIZE, SIZE))
    for r in range(rows):
        x = rng.integers(0, SIZE)
        widths = []
        while sum(widths) < SIZE:
            widths.append(int(rng.integers(rh * 1, rh * 2)))
        widths[-1] -= sum(widths) - SIZE  # close the row exactly so it tiles
        for w in widths:
            y0, y1 = r * rh, (r + 1) * rh
            xs = (np.arange(x, x + w) % SIZE)
            block = np.zeros((rh, SIZE), dtype=bool)
            block[:, xs] = True
            sub_x = (xx[y0:y1] - x) % SIZE
            inside = block
            # distance to the block border, for bevel and mortar
            dxl = sub_x
            dxr = w - 1 - sub_x
            dyt = yy[y0:y1] - y0
            dyb = y1 - 1 - yy[y0:y1]
            d = np.minimum(np.minimum(dxl, dxr), np.minimum(dyt, dyb)).astype(float)
            bevel = np.clip(d / 9.0, 0, 1)
            region = h[y0:y1]
            region[inside] = np.where(d < 3, 0.1, 0.35 + 0.65 * bevel)[inside]  # 3 px recessed mortar joint
            tintmap[y0:y1][inside] = rng.uniform(-1, 1)
            edge[y0:y1][inside] = (1 - bevel)[inside]
            x = (x + w) % SIZE
    grain = periodic_noise(rng, SIZE, 40, 4)
    chips = periodic_noise(rng, SIZE, 18, 3)
    h = np.where(h > 0, h * (0.85 + 0.3 * grain) - np.clip(chips - 0.62, 0, 1) * edge * 2.0, 0.08 + grain * 0.1)
    base = np.array([196, 186, 165], dtype=float)
    albedo = base[None, None, :] * (1 + tintmap[..., None] * 0.06) * (0.9 + 0.2 * grain[..., None])
    mortar = h < 0.22
    albedo[mortar] = albedo[mortar] * 0.62
    albedo *= (1 - edge[..., None] * 0.08)
    # a little grime collects low in each course
    course = (yy % rh) / rh
    albedo *= (1 - 0.06 * np.clip(course - 0.6, 0, 1)[..., None] / 0.4)
    save("stone_albedo", albedo)
    save("stone_normal", normal_from_height(h, 9.0))


def wood():
    """Weathered planks along V: grain streaks, ring flecks, dark seams. Neutral-warm; tinted per material."""
    rng = np.random.default_rng(23)
    planks = 4
    pw = SIZE // planks
    yy, xx = np.mgrid[0:SIZE, 0:SIZE]
    plank = xx // pw
    local = (xx % pw) / pw
    offsets = rng.uniform(0, SIZE, planks)
    streak = np.zeros((SIZE, SIZE))
    for p in range(planks):
        n = periodic_noise(np.random.default_rng(100 + p), SIZE, 6, 3)
        streak = np.where(plank == p, np.roll(n, int(offsets[p]), axis=0), streak)
    fine = periodic_noise(rng, SIZE, 90, 2)
    # grain: stretch noise along V by sampling sinusoidal bands distorted by low-frequency noise
    bands = 0.5 + 0.5 * np.sin((local * 9 + streak * 5) * np.pi)
    seam = np.clip(np.minimum(local, 1 - local) * pw / 3.0, 0, 1)
    h = (0.55 + 0.25 * bands * 0.5 + 0.2 * fine) * (0.4 + 0.6 * seam)
    shade = np.array([1.0, 1.0, 1.0])
    gray = 0.8 + 0.07 * bands + 0.1 * (fine - 0.5) + 0.06 * (rng.uniform(-1, 1, planks)[plank])
    gray *= 0.55 + 0.45 * seam
    albedo = np.dstack([gray, gray * 0.93, gray * 0.85]) * 210 * shade
    save("wood_albedo", albedo)
    save("wood_normal", normal_from_height(h, 5.0))


def shingle():
    """Roof shingles: staggered rows with rounded bottoms and a shadow under each overlap. Near-neutral."""
    rng = np.random.default_rng(31)
    rows, cols = 8, 8
    rh, cw = SIZE // rows, SIZE // cols
    yy, xx = np.mgrid[0:SIZE, 0:SIZE]
    row = yy // rh
    shift = (row % 2) * (cw // 2)
    col = ((xx + shift) // cw) % cols
    lx = ((xx + shift) % cw) / cw
    ly = (yy % rh) / rh
    vary = rng.uniform(-1, 1, (rows, cols))
    v = vary[row, col]
    side = np.clip(np.minimum(lx, 1 - lx) * 10, 0, 1)
    slope = 1 - ly  # thick at the bottom edge of each shingle (overlap reads in the normal map)
    h = (0.3 + 0.7 * slope) * (0.6 + 0.4 * side)
    grain = periodic_noise(rng, SIZE, 50, 3)
    gray = (0.84 + 0.08 * v + 0.12 * (grain - 0.5)) * (0.55 + 0.45 * side) * (0.75 + 0.25 * slope)
    albedo = np.dstack([gray, gray, gray]) * 220
    save("shingle_albedo", albedo)
    save("shingle_normal", normal_from_height(h, 7.0))


def grass():
    """Board ground: broad soft value patches (no checkerboard) + fine blades. Muted meadow green."""
    rng = np.random.default_rng(47)
    broad = periodic_noise(rng, SIZE, 3, 3)
    mid = periodic_noise(rng, SIZE, 14, 3)
    fine = periodic_noise(rng, SIZE, 120, 2)
    base = np.array([128, 140, 88], dtype=float)
    dry = np.array([150, 148, 96], dtype=float)
    t = np.clip((broad - 0.5) * 2.4 + 0.5, 0, 1)[..., None]
    albedo = base * (1 - t) + dry * t
    albedo *= (0.92 + 0.16 * mid[..., None]) * (0.9 + 0.2 * fine[..., None])
    h = 0.5 * mid + 0.5 * fine
    save("grass_albedo", albedo)
    save("grass_normal", normal_from_height(h, 3.0))


def soil():
    """Bank and path soil: packed earth with pebbles. Used for river banks and the beach ring (tinted)."""
    rng = np.random.default_rng(53)
    mid = periodic_noise(rng, SIZE, 10, 4)
    fine = periodic_noise(rng, SIZE, 100, 2)
    pebbles = periodic_noise(rng, SIZE, 36, 1)
    peb = np.clip((pebbles - 0.66) * 8, 0, 1)
    gray = 0.8 + 0.14 * (mid - 0.5) + 0.12 * (fine - 0.5) + 0.12 * peb
    albedo = np.dstack([gray, gray * 0.95, gray * 0.86]) * 215
    h = 0.4 * mid + 0.3 * fine + 0.5 * peb
    save("soil_albedo", albedo)
    save("soil_normal", normal_from_height(h, 4.0))


def cloth():
    """Coarse woven cloth/leather detail, neutral: tinted by faction and kind materials."""
    rng = np.random.default_rng(61)
    yy, xx = np.mgrid[0:SIZE, 0:SIZE]
    weave = 0.5 + 0.25 * np.sin(xx / SIZE * 2 * np.pi * 64) * np.sin(yy / SIZE * 2 * np.pi * 64)
    blot = periodic_noise(rng, SIZE, 8, 3)
    gray = 0.86 + 0.06 * (weave - 0.5) + 0.14 * (blot - 0.5)
    albedo = np.dstack([gray, gray, gray]) * 230
    save("cloth_albedo", albedo)
    save("cloth_normal", normal_from_height(weave * 0.5 + blot * 0.5, 2.0))


def water():
    """Gentle tileable ripple normal for rivers (scrolled by the runtime on the presentation clock)."""
    rng = np.random.default_rng(71)
    a = periodic_noise(rng, SIZE, 10, 3)
    b = periodic_noise(rng, SIZE, 26, 2)
    save("water_normal", normal_from_height(0.6 * a + 0.4 * b, 6.0))


def grass_detail():
    """Plan 013: close-range grass for the ground shader, multiplied (x2) over the macro map, so it averages to ~0.5 grey.
    Short blade strokes in random directions, clover-dark clumps and pale dry tips; tiled about every 2.4 tiles, so one
    tile is ~210 px and the strokes read at the hero's feet while averaging out to a soft texture from the strategic view."""
    rng = np.random.default_rng(83)
    h = periodic_noise(rng, SIZE, 60, 2) * 0.3
    tips = np.zeros((SIZE, SIZE))
    for _ in range(5200):
        x, y = rng.uniform(0, SIZE, 2)
        a = rng.uniform(-0.6, 0.6) + (np.pi / 2 if rng.random() < 0.5 else 0)
        length = rng.uniform(4, 11)
        val = rng.uniform(0.55, 1.0)
        for k in range(int(length)):
            px = int(x + np.cos(a) * k) % SIZE
            py = int(y + np.sin(a) * k) % SIZE
            fade = 1 - k / length
            h[py, px] = max(h[py, px], val * (0.5 + 0.5 * fade))
            if k > length * 0.7:
                tips[py, px] = max(tips[py, px], 0.6)
    clumps = periodic_noise(rng, SIZE, 22, 2)
    dark = np.clip((0.4 - clumps) * 4, 0, 1)
    gray = 0.42 + 0.16 * h - 0.08 * dark
    r = gray * (0.98 + 0.06 * tips)
    g = gray * (1.02 - 0.02 * tips)
    b = gray * (0.92 + 0.02 * tips)
    rgb = np.dstack([r, g, b])
    rgb = rgb / rgb.mean() * 0.5
    save("grass_detail", rgb * 255)
    save("grass_detail_normal", normal_from_height(h, 2.0))  # blades only: broad clumps read as crumpled paper


def sand():
    """Plan 013: beach and bank sand: fine grain, faint wind ripples and scattered shell/pebble flecks."""
    rng = np.random.default_rng(89)
    yy, xx = np.mgrid[0:SIZE, 0:SIZE]
    warp = periodic_noise(rng, SIZE, 4, 2)
    ripple = 0.5 + 0.5 * np.sin((yy / SIZE * 2 * np.pi * 18) + warp * 9)
    fine = periodic_noise(rng, SIZE, 160, 2)
    flecks = np.clip((periodic_noise(rng, SIZE, 70, 1) - 0.72) * 10, 0, 1)
    gray = 0.84 + 0.05 * (ripple - 0.5) + 0.12 * (fine - 0.5) + 0.1 * flecks
    save("sand_albedo", np.dstack([gray, gray * 0.95, gray * 0.84]) * 220)


def rock():
    """Plan 013: island cliffs and rock faces: stepped horizontal strata (side projection) split by vertical joints that
    shift from layer to layer, with lichen-dark blotches. Reads as layered sandstone from the hero view."""
    rng = np.random.default_rng(97)
    yy, xx = np.mgrid[0:SIZE, 0:SIZE]
    warp = periodic_noise(rng, SIZE, 3, 3)
    layer_f = (yy / SIZE * 6 + (warp - 0.5) * 1.2) % 6
    layer = np.floor(layer_f).astype(int)
    within = layer_f - layer
    shade = rng.uniform(-1, 1, 6)[layer]
    lip = np.clip((within - 0.82) * 6, 0, 1)  # the overhanging top of each layer catches light
    joints = np.zeros((SIZE, SIZE))
    for l in range(6):
        for x0 in rng.uniform(0, SIZE, 5):
            d = np.abs(((xx - x0 + SIZE / 2) % SIZE) - SIZE / 2)
            joints = np.maximum(joints, np.where(layer == l, np.clip(1 - d / 2.5, 0, 1), 0))
    blotch = periodic_noise(rng, SIZE, 8, 3)
    fine = periodic_noise(rng, SIZE, 120, 2)
    gray = 0.66 + 0.07 * shade + 0.12 * lip - 0.06 * (1 - within) + 0.1 * (fine - 0.5) - 0.3 * joints         - 0.1 * np.clip(blotch - 0.55, 0, 1) * 3
    save("rock_albedo", np.dstack([gray, gray * 0.97, gray * 0.92]) * 230)
    save("rock_normal", normal_from_height(within * 0.5 + lip * 0.3 + fine * 0.2 - joints * 0.5, 7.0))


if __name__ == "__main__":
    for f in (stone, wood, shingle, grass, soil, cloth, water, grass_detail, sand, rock):
        f()
        print("wrote", f.__name__)
