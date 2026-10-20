"""
Board environment for The Last Fortress art slice (plan 011): trees, bush, rocks, reeds, grass tuft, ford stepping stone and
distant shore islands. Run headless:

    blender -b --factory-startup -P environment.py -- <output folder>

Provenance and conventions as models.py (shared helpers): authored as code by the agent, deterministic seeds, one tile = one
unit, pivot on the ground. Forest/rock decoration must stay inside its own cell; ground detail stays below 0.08 so it never
reads as an obstacle on cells where building and movement are allowed.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from models import (Part, box_bm, cyl_bm, export, ico_bm, root, trs, TIMBER_DARK, STONE_DARK)  # noqa: E402
from mathutils import Vector  # noqa: E402

FOLIAGE, FOLIAGE_DARK, ROCK, REED, FLOWER = "ArtFoliage", "ArtFoliageDark", "ArtRock", "ArtReed", "ArtFlower"
# Distant scenery has its own hazed, desaturated materials so it recedes behind the board (aerial perspective).


def jittered_ico(radius, subdiv, rng, amount, squash=1.0):
    bm = ico_bm(radius, subdiv)
    for v in bm.verts:
        k = 1 + rng.uniform(-amount, amount)
        v.co.x *= k
        v.co.y *= k
        v.co.z = v.co.z * k * squash
    return bm


def pine_into(p, at, s, rng):
    """
    Conifer at real scale (plan 014): about 14 m at s = 1 (1.75 tiles), slender: a tall trunk and six drooping tiers, each
    a skirt cone over a darker underside cone so the branch layers keep depth from the hero view. Crown radius 0.24 tile,
    so it stays inside its forest cell.
    """
    trunk = TIMBER_DARK
    base = Vector(at)
    p.add(cyl_bm(0.04 * s, 0.06 * s, 7, radius_top=0.028 * s), trunk, trs(base), grain=(0, 0, 1))
    p.add(cyl_bm(0.028 * s, 0.75 * s, 7, radius_top=0.012 * s), trunk, trs(base + Vector((0, 0, 0.05 * s))), grain=(0, 0, 1))
    tiers = 6
    z = 0.42 * s
    for k in range(tiers):
        t = k / (tiers - 1)
        r = (0.24 - 0.17 * t) * s
        h = (0.36 - 0.12 * t) * s
        off = Vector((rng.uniform(-0.01, 0.01), rng.uniform(-0.01, 0.01), 0)) * s
        spin = rng.uniform(0, 360)
        at_k = base + off + Vector((0, 0, z))
        lit = FOLIAGE if k % 2 else FOLIAGE_DARK
        p.add(cyl_bm(r, h, 8, radius_top=0.0), lit, trs(at_k, (0, 0, spin)))
        p.add(cyl_bm(r * 0.9, 0.04 * s, 8, radius_top=r * 0.5), FOLIAGE_DARK, trs(at_k - Vector((0, 0, 0.035 * s)), (0, 0, spin + 22)))
        z += h * 0.6
    p.add(cyl_bm(0.025 * s, 0.1 * s, 6, radius_top=0.0), FOLIAGE_DARK, trs(base + Vector((0, 0, z + 0.04 * s))))


def broadleaf_into(p, at, s, rng):
    """Deciduous tree at real scale (plan 014): about 11 m at s = 1, a trunk forking into branches under a full crown of
    lumpy masses in two tones; crown radius about 0.24 tile, inside its cell."""
    trunk = TIMBER_DARK
    base = Vector(at)
    p.add(cyl_bm(0.045 * s, 0.06 * s, 7, radius_top=0.032 * s), trunk, trs(base), grain=(0, 0, 1))
    p.add(cyl_bm(0.032 * s, 0.55 * s, 7, radius_top=0.022 * s), trunk, trs(base + Vector((0, 0, 0.05 * s))), grain=(0, 0, 1))
    for k in range(3):
        a = math.radians(k * 120 + rng.uniform(-25, 25))
        p.add(cyl_bm(0.015 * s, 0.28 * s, 5, radius_top=0.008 * s), trunk, trs(base + Vector((0, 0, 0.5 * s)), (math.cos(a) * 35, math.sin(a) * 35, 0)), grain=(0, 0, 1))
    masses = [((0, 0, 1.05), 0.2)]
    for k in range(5):
        a = math.radians(k * 72 + rng.uniform(-15, 15))
        masses.append(((math.cos(a) * 0.14, math.sin(a) * 0.14, 0.85 + rng.uniform(-0.05, 0.08)), 0.15))
    masses.append(((rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05), 1.25), 0.14))
    for k, (c, r) in enumerate(masses):
        tone = FOLIAGE if k in (0, 6) or c[2] > 0.9 else FOLIAGE_DARK
        p.add(jittered_ico(r * s, 1, rng, 0.14, 0.82), tone, trs(base + Vector(c) * s, (0, 0, rng.uniform(0, 360))))


def pine():
    rng = random.Random(1)
    r = root("Pine")
    p = Part("Body")
    pine_into(p, (0, 0, 0), 1.0, rng)
    p.build(r)
    export("ArtPine")


def broadleaf():
    rng = random.Random(2)
    r = root("Broadleaf")
    p = Part("Body")
    broadleaf_into(p, (0, 0, 0), 1.0, rng)
    p.build(r)
    export("ArtBroadleaf")


def bush():
    rng = random.Random(3)
    r = root("Bush")
    p = Part("Body")
    for k in range(3):
        a = math.radians(k * 120)
        p.add(jittered_ico(0.085, 1, rng, 0.15, 0.75), FOLIAGE, trs((math.cos(a) * 0.06, math.sin(a) * 0.06, 0.05), (0, 0, k * 40)))
    p.build(r)
    export("ArtBush")


def boulder(radius, rng, squash=0.6, cut=0.35):
    """A stylized boulder: a jittered icosphere sliced flat underneath and slightly bevelled on top, so it sits on the
    ground (no floating underside from the hero view) and catches a clean highlight."""
    bm = jittered_ico(radius, 2, rng, 0.16, squash)
    floor = -radius * squash * cut
    for v in bm.verts:
        if v.co.z < floor:
            v.co.z = floor
        if v.co.z > radius * squash * 0.75:
            v.co.z = radius * squash * 0.75 + (v.co.z - radius * squash * 0.75) * 0.4
    for v in bm.verts:
        v.co.z -= floor
    return bm


def rock_large():
    """A weathered boulder with two companions, inside one cell; ~0.36 tall."""
    rng = random.Random(4)
    r = root("RockLarge")
    p = Part("Body")
    p.add(boulder(0.27, rng, 0.66), ROCK, trs((-0.06, 0.02, 0.0), (0, 0, 30)))
    p.add(boulder(0.15, rng, 0.7), ROCK, trs((0.22, -0.18, 0.0), (0, 0, 70)))
    p.add(boulder(0.085, rng, 0.7), ROCK, trs((-0.24, -0.2, 0.0), (0, 0, 10)))
    p.add(boulder(0.06, rng, 0.6), ROCK, trs((0.05, 0.27, 0.0), (0, 0, 140)))
    p.build(r)
    export("ArtRockLarge")


def rock_small():
    rng = random.Random(5)
    r = root("RockSmall")
    p = Part("Body")
    p.add(boulder(0.09, rng, 0.6), ROCK, trs((0, 0, 0.0)))
    p.add(boulder(0.05, rng, 0.6), ROCK, trs((0.09, 0.05, 0.0)))
    p.build(r)
    export("ArtRockSmall")


def reeds():
    rng = random.Random(6)
    r = root("Reeds")
    p = Part("Body")
    for k in range(7):
        x, y = rng.uniform(-0.08, 0.08), rng.uniform(-0.05, 0.05)
        h = rng.uniform(0.16, 0.28)
        p.add(cyl_bm(0.008, h, 4, radius_top=0.0), REED, trs((x, y, 0), (rng.uniform(-10, 10), rng.uniform(-10, 10), 0)))
    p.add(cyl_bm(0.013, 0.05, 5), TIMBER_DARK, trs((0.03, 0.0, 0.2), (8, 0, 0)))  # a cattail head
    p.build(r)
    export("ArtReeds")


def tuft():
    """Ground detail: a low tuft with two tiny flowers. Height <= 0.07, so it never reads as a blocker."""
    rng = random.Random(7)
    r = root("Tuft")
    p = Part("Body")
    for k in range(6):
        a = math.radians(k * 60 + rng.uniform(-15, 15))
        p.add(cyl_bm(0.008, rng.uniform(0.04, 0.065), 3, radius_top=0.0), FOLIAGE, trs((math.cos(a) * 0.02, math.sin(a) * 0.02, 0), (math.cos(a) * 25, math.sin(a) * 25, 0)))
    for k in range(2):
        p.add(ico_bm(0.009, 0), FLOWER, trs((rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03), 0.05)))
    p.build(r)
    export("ArtTuft")


def ford_stone():
    rng = random.Random(8)
    r = root("FordStone")
    p = Part("Body")
    p.add(jittered_ico(0.085, 1, rng, 0.15, 0.35), ROCK, trs((0, 0, 0.0)))
    p.build(r)
    export("ArtFordStone")


if __name__ == "__main__":
    pine()
    broadleaf()
    bush()
    rock_large()
    rock_small()
    reeds()
    tuft()
    ford_stone()
