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
GRASS_TOP, CLIFF, FAR_FOLIAGE, FAR_FOLIAGE_DARK, FAR_TRUNK = "ArtFarGround", "ArtFarCliff", "ArtFarFoliage", "ArtFarFoliageDark", "ArtFarTrunk"


def jittered_ico(radius, subdiv, rng, amount, squash=1.0):
    bm = ico_bm(radius, subdiv)
    for v in bm.verts:
        k = 1 + rng.uniform(-amount, amount)
        v.co.x *= k
        v.co.y *= k
        v.co.z = v.co.z * k * squash
    return bm


def pine_into(p, at, s, rng, far=False):
    """Conifer: short trunk and three stacked, slightly offset cones; ~0.65 tall at s = 1."""
    p.add(cyl_bm(0.035 * s, 0.16 * s, 6, radius_top=0.028 * s), FAR_TRUNK if far else TIMBER_DARK, trs(at), grain=(0, 0, 1))
    z = 0.1 * s
    for k, (r, h) in enumerate(((0.21, 0.26), (0.165, 0.23), (0.11, 0.2))):
        off = Vector((rng.uniform(-0.01, 0.01), rng.uniform(-0.01, 0.01), 0))
        p.add(cyl_bm(r * s, h * s, 7, radius_top=0.0), FAR_FOLIAGE_DARK if far else FOLIAGE_DARK, trs(Vector(at) + off + Vector((0, 0, z)), (0, 0, rng.uniform(0, 360))))
        z += h * s * 0.55


def broadleaf_into(p, at, s, rng, far=False):
    """Deciduous tree: forked trunk and a cluster of four lumpy crowns; ~0.62 tall at s = 1."""
    p.add(cyl_bm(0.04 * s, 0.24 * s, 6, radius_top=0.03 * s), FAR_TRUNK if far else TIMBER_DARK, trs(at), grain=(0, 0, 1))
    for k in range(4):
        a = math.radians(k * 90 + rng.uniform(-20, 20))
        r = 0.08 * s if k else 0.0
        c = Vector(at) + Vector((math.cos(a) * r, math.sin(a) * r, (0.38 + (0.08 if k == 0 else rng.uniform(-0.03, 0.03))) * s))
        p.add(jittered_ico((0.16 if k == 0 else 0.12) * s, 1, rng, 0.12, 0.85), FAR_FOLIAGE if far else FOLIAGE, trs(c, (0, 0, rng.uniform(0, 360))))


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


def rock_large():
    """A weathered boulder with two companions, inside one cell; ~0.36 tall."""
    rng = random.Random(4)
    r = root("RockLarge")
    p = Part("Body")
    p.add(jittered_ico(0.27, 1, rng, 0.18, 0.62), ROCK, trs((-0.06, 0.02, 0.1), (6, 4, 30)))
    p.add(jittered_ico(0.14, 1, rng, 0.2, 0.7), ROCK, trs((0.22, -0.18, 0.05), (0, 8, 70)))
    p.add(jittered_ico(0.08, 1, rng, 0.2, 0.7), ROCK, trs((-0.24, -0.2, 0.03), (0, 0, 10)))
    p.build(r)
    export("ArtRockLarge")


def rock_small():
    rng = random.Random(5)
    r = root("RockSmall")
    p = Part("Body")
    p.add(jittered_ico(0.09, 1, rng, 0.2, 0.6), ROCK, trs((0, 0, 0.03)))
    p.add(jittered_ico(0.05, 1, rng, 0.2, 0.6), ROCK, trs((0.09, 0.05, 0.015)))
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


def far_island(seed, name):
    """
    Distant shore seen past the sea at the frame corners: an irregular grassy plateau with stone cliffs, a strand and a
    stand of mixed trees, merged into one renderer. Pure scenery far outside the board; never near a legal cell.
    """
    rng = random.Random(seed)
    r = root(name)
    p = Part("Body")
    segs = 14
    radius = 3.0
    # cliff ring and grass top as one jagged prism
    import bmesh
    bm = bmesh.new()
    top, bot = [], []
    for k in range(segs):
        a = 2 * math.pi * k / segs
        rr = radius * rng.uniform(0.75, 1.15)
        top.append(bm.verts.new((math.cos(a) * rr, math.sin(a) * rr, rng.uniform(0.35, 0.6))))
        bot.append(bm.verts.new((math.cos(a) * rr * 1.08, math.sin(a) * rr * 1.08, -0.6)))
    bm.faces.new(top)
    for k in range(segs):
        bm.faces.new((bot[k], bot[(k + 1) % segs], top[(k + 1) % segs], top[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    cap = [f for f in bm.faces if len(f.verts) == segs][0]
    cap_bm = bmesh.new()
    # split: the top cap gets the grass material, the sides the cliff
    side_bm = bm
    verts = [Vector(v.co) for v in cap.verts]
    side_bm.faces.remove(cap)
    p.add(side_bm, CLIFF, trs())
    for i, v in enumerate(verts):
        cap_bm.verts.new(v)
    cap_bm.faces.new(cap_bm.verts)
    bmesh.ops.triangulate(cap_bm, faces=cap_bm.faces)
    p.add(cap_bm, GRASS_TOP, trs())
    for k in range(26):
        a = rng.uniform(0, 2 * math.pi)
        d = math.sqrt(rng.uniform(0.0, 1.0)) * radius * 0.7
        at = (math.cos(a) * d, math.sin(a) * d, 0.42)
        s = rng.uniform(1.6, 2.6)
        if rng.random() < 0.6:
            pine_into(p, at, s, rng, far=True)
        else:
            broadleaf_into(p, at, s, rng, far=True)
    p.build(r)
    export(name)


if __name__ == "__main__":
    pine()
    broadleaf()
    bush()
    rock_large()
    rock_small()
    reeds()
    tuft()
    ford_stone()
    far_island(21, "ArtFarIslandA")
    far_island(22, "ArtFarIslandB")
