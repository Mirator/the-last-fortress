"""
Plan 011 extension: the established slice style applied to the remaining release content — House, Farm, Spike Trap,
Cannon, Watchtower, Repair Station and the entry gate. The enemy figures moved to characters.py in plan 013. Run headless:

    blender -b --factory-startup -P models_ext.py -- <output folder>

Provenance and conventions: see models.py (authored as code by the agent, deterministic, flat export with world pivots,
joints rebuilt by ArtSetup). Runtime name contracts kept here: Cannon parts Carriage/Barrel/Muzzle/Wheel/Shot (the gun
traverses), Bomber WickTip/TorchFlame (fuse sparks, torch flame), Torch* empties (night torch particles), and the rig joints
Hips/Torso/Head/ArmL/ArmR/Weapon/LegL/LegR. Footprints and height bands follow plans/008-art-and-audio-bible.md.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from models import (Part, beam, box_bm, cyl_bm, empty, export, ico_bm, pyramid_bm, root, trs, wall_torch,  # noqa: E402
                    STONE, STONE_DARK, TIMBER, TIMBER_DARK, SHINGLE, IRON, DOOR, FLAME, WINDOW, BANNER, ENEMY_LEATHER, ENEMY_EYES)
import bmesh  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

PLASTER, CROP, SOIL_DARK, HAY, PENNANT, KEG, HAZARD = "ArtPlaster", "ArtCrop", "ArtSoilTilled", "ArtHay", "ArtPennant", "ArtKeg", "ArtHazard"
RUNNER_CLOTH, BRUTE_CLOTH, ARCHER_CLOTH, BOMBER_CLOTH, COMMANDER_CLOTH = "ArtRunnerCloth", "ArtBruteCloth", "ArtArcherCloth", "ArtBomberCloth", "ArtCommanderCloth"
GOLEM_STONE, GOLEM_GLOW, ENEMY_IRON, SKULL = "ArtGolemStone", "ArtGolemGlow", "ArtEnemyIron", "ArtBone"
BONE_TIP = "ArtBone"


def prism_bm(length, width, height):
    """Gable roof along X: a triangular prism, origin at the eave-line centre."""
    bm = bmesh.new()
    hl, hw = length / 2, width / 2
    a = [bm.verts.new(v) for v in ((-hl, -hw, 0), (hl, -hw, 0), (hl, hw, 0), (-hl, hw, 0), (-hl, 0, height), (hl, 0, height))]
    for f in ((a[0], a[1], a[5], a[4]), (a[2], a[3], a[4], a[5]), (a[0], a[4], a[3]), (a[1], a[2], a[5]), (a[0], a[3], a[2], a[1])):
        bm.faces.new(f)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


# ---------------------------------------------------------------- buildings

def house():
    """Cottage at real scale (plan 014): 6 x 5 m on a 0.3 m stone footing, 2.9 m whitewashed timber-framed walls, a steep
    gabled shingle roof to about 6 m, stone chimney, a 2 m plank door at ground level with a step, and 0.9 m windows that
    glow at night through the window material."""
    r = root("House")
    p = Part("Body")
    wall = 0.4
    p.add(box_bm((0.76, 0.66, 0.04), 0.008, taper=0.97), STONE_DARK, trs((0, 0, 0)))
    p.add(box_bm((0.7, 0.6, wall - 0.035), 0.008), PLASTER, trs((0, 0, 0.035)))
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.add(box_bm((0.035, 0.035, wall - 0.03), 0.004), TIMBER_DARK, trs((sx * 0.338, sy * 0.288, 0.03)), grain=(0, 0, 1))
    for sy in (-1, 1):
        p.add(box_bm((0.72, 0.022, 0.028), 0.003), TIMBER_DARK, trs((0, sy * 0.302, 0.2)), grain=(1, 0, 0))
        p.add(box_bm((0.72, 0.022, 0.03), 0.003), TIMBER_DARK, trs((0, sy * 0.302, wall - 0.01)), grain=(1, 0, 0))
        for sx in (-1, 1):
            beam(p, (sx * 0.3, sy * 0.305, 0.22), (sx * 0.17, sy * 0.305, wall - 0.01), 0.022, 0.015, TIMBER_DARK, 0.002)
    for sx in (-1, 1):
        p.add(box_bm((0.022, 0.62, 0.03), 0.003), TIMBER_DARK, trs((sx * 0.352, 0, wall - 0.01)), grain=(0, 1, 0))
    p.add(prism_bm(0.7, 0.6, 0.32), PLASTER, trs((0, 0, wall + 0.01)))
    p.add(prism_bm(0.84, 0.76, 0.38), SHINGLE, trs((0, 0, wall - 0.02)))
    p.add(box_bm((0.86, 0.035, 0.035), 0.005), TIMBER_DARK, trs((0, 0, wall + 0.35)), grain=(1, 0, 0))
    p.add(box_bm((0.1, 0.1, 0.46), 0.008, taper=0.94), STONE, trs((0.2, 0.17, wall + 0.02)))
    p.add(box_bm((0.12, 0.12, 0.025), 0.004), STONE_DARK, trs((0.2, 0.17, wall + 0.47)))
    # door at ground level: 1 m x 2 m, lintel, one stone step
    p.add(box_bm((0.15, 0.06, 0.03), 0.004), STONE_DARK, trs((-0.12, -0.33, 0)))
    p.add(box_bm((0.12, 0.02, 0.25), 0.003), DOOR, trs((-0.12, -0.31, 0.035)), grain=(0, 0, 1))
    p.add(box_bm((0.15, 0.022, 0.02), 0.003), TIMBER_DARK, trs((-0.12, -0.314, 0.285)))
    for x in (0.15,):
        p.add(box_bm((0.11, 0.02, 0.11), 0.0), WINDOW, trs((x, -0.305, 0.17)))
        p.add(box_bm((0.13, 0.03, 0.015), 0.003), TIMBER_DARK, trs((x, -0.315, 0.16)))
    p.add(box_bm((0.02, 0.11, 0.11), 0.0), WINDOW, trs((0.355, 0.0, 0.17)))
    p.build(r)
    export("ArtHouse")


def farm():
    """2 x 2 field (pivot at the footprint centre), ~0.35 (shed ~0.55): tilled furrows with crop rows, a wattle fence on
    two sides, a small shingled shed and hay bales. Low, so it never reads as a wall."""
    rng = random.Random(41)
    r = root("Farm")
    p = Part("Body")
    p.add(box_bm((1.82, 1.82, 0.05), 0.02), SOIL_DARK, trs((0, 0, 0)))
    for i in range(6):
        x = -0.72 + i * 0.27
        p.add(box_bm((0.15, 1.5, 0.04), 0.02), SOIL_DARK, trs((x, 0.05, 0.04)), grain=(0, 1, 0))
        for k in range(9):
            y = -0.62 + k * 0.165
            for _ in range(4):  # a clump of thin stalks about 1 m tall (plan 014: real size)
                h = rng.uniform(0.1, 0.135)
                p.add(cyl_bm(0.007, h, 4, radius_top=0.0), CROP, trs((x + rng.uniform(-0.035, 0.035), y + rng.uniform(-0.04, 0.04), 0.07),
                                                                     (rng.uniform(-8, 8), rng.uniform(-8, 8), rng.uniform(0, 360))))
    for k in range(9):
        t = -0.84 + k * 0.21
        p.add(cyl_bm(0.012, 0.15, 5), TIMBER_DARK, trs((t, -0.86, 0.04)), grain=(0, 0, 1))
        p.add(cyl_bm(0.012, 0.15, 5), TIMBER_DARK, trs((-0.86, t, 0.04)), grain=(0, 0, 1))
    for z in (0.09, 0.15):
        p.add(box_bm((1.72, 0.016, 0.02), 0.003), TIMBER, trs((0, -0.86, z)), grain=(1, 0, 0))
        p.add(box_bm((0.016, 1.72, 0.02), 0.003), TIMBER, trs((-0.86, 0, z)), grain=(0, 1, 0))
    p.add(box_bm((0.36, 0.32, 0.3), 0.01), TIMBER, trs((0.62, -0.62, 0.04)), grain=(0, 0, 1))
    p.add(prism_bm(0.46, 0.42, 0.2), SHINGLE, trs((0.62, -0.62, 0.34)))
    p.add(box_bm((0.11, 0.02, 0.25), 0.003), DOOR, trs((0.62, -0.785, 0.04)))
    for (x, y) in ((0.3, -0.75), (0.42, -0.42)):
        p.add(cyl_bm(0.07, 0.12, 8, bevel=0.01), HAY, trs((x, y, 0.05), (90, 0, rng.uniform(0, 180))))
    p.build(r)
    export("ArtFarm")


def spike_trap():
    """Walkable pit frame with iron spikes; never taller than 0.15 so it does not read as a blocker."""
    r = root("SpikeTrap")
    p = Part("Body")
    p.add(box_bm((0.8, 0.8, 0.03), 0.01), TIMBER_DARK, trs((0, 0, 0)), grain=(1, 0, 0))
    p.add(box_bm((0.66, 0.66, 0.012), 0.0), SOIL_DARK, trs((0, 0, 0.03)))
    for side in range(4):  # a hazard-red band along the frame's inner edge: reads as a trap from the strategic view
        rot = math.radians(90 * side)
        cc = Vector((math.cos(rot), math.sin(rot), 0)) * 0.335
        p.add(box_bm((0.012 if abs(cc.x) > 0.1 else 0.66, 0.66 if abs(cc.x) > 0.1 else 0.012, 0.012), 0.0), HAZARD, trs((cc.x, cc.y, 0.05)))
    for side in range(4):
        rot = math.radians(90 * side)
        c = Vector((math.cos(rot), math.sin(rot), 0)) * 0.37
        p.add(box_bm((0.06 if abs(c.x) > 0.1 else 0.8, 0.8 if abs(c.x) > 0.1 else 0.06, 0.05), 0.008), TIMBER, trs((c.x, c.y, 0.0)), grain=(0, 1, 0) if abs(c.x) > 0.1 else (1, 0, 0))
    for x in range(5):
        for y in range(5):  # 0.6 m iron stakes (plan 014: real size)
            c = (-0.24 + x * 0.12 + (y % 2) * 0.03, -0.24 + y * 0.12, 0.035)
            p.add(cyl_bm(0.011, 0.05, 5, radius_top=0.007), TIMBER_DARK, trs(c))
            p.add(cyl_bm(0.007, 0.03, 5, radius_top=0.0), BONE_TIP, trs((c[0], c[1], c[2] + 0.05)))  # pale fire-hardened point
    p.build(r)
    export("ArtSpikeTrap")


def cannon():
    """2 x 2 gun emplacement, ~1.1: crenellated stone platform and timber deck (static), and the gun that traverses:
    Carriage, Barrel, Muzzle (pivot at the muzzle mouth), Wheel (both wheels) and Shot (a stack of balls)."""
    r = root("Cannon")
    p = Part("Body")
    p.add(box_bm((1.74, 1.74, 0.12), 0.02, taper=0.97), STONE_DARK, trs((0, 0, 0)))
    p.add(box_bm((1.5, 1.5, 0.03), 0.006), TIMBER, trs((0, 0, 0.115)), grain=(1, 0, 0))
    for side in range(4):
        for k in (-2, -1, 1, 2):
            rot = math.radians(90 * side)
            off = Vector((k * 0.3, -0.78, 0))
            c = Vector((off.x * math.cos(rot) - off.y * math.sin(rot), off.x * math.sin(rot) + off.y * math.cos(rot), 0.21))
            p.add(box_bm((0.16, 0.1, 0.1), 0.008), STONE, trs(c, (0, 0, 90 * side)))
        rot = math.radians(90 * side)
        c = Vector((0.78 * math.sin(rot), -0.78 * math.cos(rot), 0.11))
        p.add(box_bm((1.7 if side % 2 == 0 else 0.12, 0.12 if side % 2 == 0 else 1.7, 0.1), 0.008), STONE, trs(c))
    p.build(r)
    G = Matrix.Translation((0, -0.05, 0.145)) @ Matrix.Scale(0.42, 4) @ Matrix.Translation((0, 0.05, -0.33))
    carriage = Part("Carriage")
    for sx in (-1, 1):
        carriage.add_scaled(G, box_bm((0.07, 0.62, 0.26), 0.012), TIMBER_DARK, trs((sx * 0.17, -0.05, 0.33)), grain=(0, 1, 0))
    carriage.add_scaled(G, box_bm((0.36, 0.5, 0.06), 0.01), TIMBER_DARK, trs((0, -0.05, 0.36)), grain=(1, 0, 0))
    carriage.add_scaled(G, box_bm((0.3, 0.06, 0.06), 0.008), IRON, trs((0, -0.32, 0.36)))
    carriage.build(r)
    barrel = Part("Barrel")
    barrel.add_scaled(G, cyl_bm(0.11, 0.78, 12, radius_top=0.085, bevel=0.008), IRON, trs((0, -0.25, 0.62), (-70, 0, 0)))
    for t in (0.0, 0.3, 0.62):
        d = Vector((0, math.cos(math.radians(20)), math.sin(math.radians(20))))
        barrel.add_scaled(G, cyl_bm(0.12 - t * 0.05, 0.035, 12), IRON, trs(Vector((0, -0.25, 0.62)) + d * t * 0.78 - Vector((0, 0, 0.01)), (-70, 0, 0)))
    barrel.add_scaled(G, ico_bm(0.07, 1), IRON, trs((0, -0.29, 0.6)))
    barrel.build(r)
    d = Vector((0, math.cos(math.radians(20)), math.sin(math.radians(20))))
    mouth = Vector((0, -0.25, 0.62)) + d * 0.8
    muzzle = Part("Muzzle")
    muzzle.add_scaled(G, cyl_bm(0.105, 0.06, 12), IRON, trs(mouth - d * 0.06, (-70, 0, 0)))
    muzzle.build(r, G @ mouth)
    wheel = Part("Wheel")
    for sx in (-1, 1):
        wheel.add_scaled(G, cyl_bm(0.17, 0.05, 12, bevel=0.008), TIMBER_DARK, trs((sx * 0.235, -0.05, 0.42), (0, 90, 0)))
        wheel.add_scaled(G, cyl_bm(0.05, 0.06, 8), IRON, trs((sx * 0.24, -0.05, 0.42), (0, 90, 0)))
    wheel.build(r)
    shot = Part("Shot")
    for (x, y, z) in ((0, 0, 0), (0.1, 0, 0), (0.05, 0.085, 0), (0.05, 0.03, 0.08)):
        shot.add_scaled(G, ico_bm(0.05, 1), IRON, trs((-0.55 + x, -0.55 + y, 0.38 + z)))
    shot.build(r)
    export("ArtCannon")


def watchtower():
    """Tall thin lookout at real scale (plan 014), about 14 m: a small stone footing, four 0.3 m stilts with braces and
    two landings, a lookout box at 11 m with a shingle cap and an amber pennant on a pole (support, not a weapon)."""
    r = root("Watchtower")
    p = Part("Body")
    deck = 1.38
    p.add(box_bm((0.5, 0.5, 0.1), 0.01, taper=0.88), STONE_DARK, trs((0, 0, 0)))
    for cx, cy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        beam(p, (cx * 0.19, cy * 0.19, 0.09), (cx * 0.15, cy * 0.15, deck), 0.036, 0.036, TIMBER, 0.004)
    for z0, z1 in ((0.15, 0.62), (0.62, 1.1)):
        for side in range(4):
            rot = math.radians(90 * side)

            def R(v):
                return Vector((v[0] * math.cos(rot) - v[1] * math.sin(rot), v[0] * math.sin(rot) + v[1] * math.cos(rot), v[2]))

            beam(p, R((-0.18, -0.2, z0)), R((0.16, -0.18, z1)), 0.016, 0.013, TIMBER_DARK, 0.002)
    for z in (0.62, 1.1):
        p.add(box_bm((0.4, 0.4, 0.02), 0.003), TIMBER_DARK, trs((0, 0, z)))
    p.add(box_bm((0.5, 0.5, 0.03), 0.005), TIMBER, trs((0, 0, deck)), grain=(1, 0, 0))
    for side in range(4):
        rot = math.radians(90 * side)
        c = Vector((0.235 * math.sin(rot), -0.235 * math.cos(rot), deck + 0.03))
        p.add(box_bm((0.5 if side % 2 == 0 else 0.022, 0.022 if side % 2 == 0 else 0.5, 0.13), 0.003), TIMBER, trs(c), grain=(0, 0, 1))
    for cx, cy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        p.add(box_bm((0.025, 0.025, 0.27), 0.003), TIMBER_DARK, trs((cx * 0.225, cy * 0.225, deck + 0.03)))
    p.add(pyramid_bm(0.62, 0.18), SHINGLE, trs((0, 0, deck + 0.3)))
    p.add(cyl_bm(0.006, 0.3, 6), TIMBER_DARK, trs((0.08, 0.0, deck + 0.36)))
    p.add(box_bm((0.15, 0.005, 0.08), 0.001), PENNANT, trs((0.155, 0.0, deck + 0.58)))
    p.build(r)
    export("ArtWatchtower")


def repair_station():
    """2 x 2 workshop, ~1.4 (pivot at the footprint centre): plastered workshop with a shingle roof and big door, a timber
    crane with jib, rope and hook, stacked planks, a barrel and an anvil on a packed-earth yard."""
    r = root("RepairStation")
    p = Part("Body")
    p.add(box_bm((1.76, 1.76, 0.04), 0.015), SOIL_DARK, trs((0, 0, 0)))
    p.add(box_bm((0.92, 0.82, 0.08), 0.012), STONE_DARK, trs((-0.35, 0.3, 0.02)))
    p.add(box_bm((0.86, 0.76, 0.56), 0.01), PLASTER, trs((-0.35, 0.3, 0.09)))
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.add(box_bm((0.05, 0.05, 0.58), 0.006), TIMBER_DARK, trs((-0.35 + sx * 0.41, 0.3 + sy * 0.36, 0.08)), grain=(0, 0, 1))
    p.add(prism_bm(0.86, 0.76, 0.34), PLASTER, trs((-0.35, 0.3, 0.64)))
    p.add(prism_bm(1.02, 0.96, 0.42), SHINGLE, trs((-0.35, 0.3, 0.6)))
    p.add(box_bm((0.34, 0.03, 0.4), 0.004), DOOR, trs((-0.35, -0.095, 0.1)), grain=(0, 0, 1))
    p.add(box_bm((0.1, 0.02, 0.08), 0.0), WINDOW, trs((-0.08, -0.09, 0.4)))
    # crane
    p.add(box_bm((0.22, 0.22, 0.1), 0.012), STONE_DARK, trs((0.55, -0.45, 0.02)))
    p.add(box_bm((0.08, 0.08, 1.36), 0.01), TIMBER, trs((0.55, -0.45, 0.1)), grain=(0, 0, 1))
    beam(p, (0.55, -0.45, 1.0), (-0.05, -0.45, 1.36), 0.06, 0.06, TIMBER, 0.008)  # angled jib (plan 015: it read as a gallows)
    beam(p, (0.55, -0.45, 1.38), (-0.03, -0.45, 1.36), 0.012, 0.012, TIMBER_DARK, 0.0)  # stay rope from the mast head
    # Plan 013: a hoisted stone in a rope sling and a windlass at the foot, so the jib reads as a crane at default zoom
    # (with only a rope and hook it read as a gallows).
    p.add(cyl_bm(0.007, 0.5, 4), TIMBER_DARK, trs((0.0, -0.45, 0.86)))
    p.add(cyl_bm(0.032, 0.05, 8, bevel=0.005), IRON, trs((0.0, -0.45, 0.82)))
    for s in (-1, 1):
        beam(p, (0.0, -0.45, 0.82), (s * 0.08, -0.45, 0.66), 0.008, 0.008, TIMBER_DARK, 0.0)
    p.add(box_bm((0.2, 0.17, 0.15), 0.02), STONE, trs((0.0, -0.45, 0.52), (0, 0, 12)))
    p.add(box_bm((0.21, 0.02, 0.012), 0.0), TIMBER_DARK, trs((0.0, -0.45, 0.6), (0, 0, 12)))
    p.add(cyl_bm(0.07, 0.2, 10, bevel=0.006), TIMBER_DARK, trs((0.55, -0.34, 0.26), (0, 90, 0)))
    for s in (-1, 1):
        p.add(box_bm((0.03, 0.03, 0.26), 0.004), TIMBER, trs((0.55 + s * 0.11, -0.34, 0.02)), grain=(0, 0, 1))
    beam(p, (0.68, -0.34, 0.26), (0.72, -0.25, 0.3), 0.015, 0.015, IRON, 0.0)
    for i in range(4):
        p.add(box_bm((0.62, 0.15, 0.06), 0.008), TIMBER, trs((0.42, 0.45, 0.04 + i * 0.062), (0, 0, i * 9 - 10)), grain=(1, 0, 0))
    p.add(cyl_bm(0.1, 0.24, 10, radius_top=0.09, bevel=0.01), TIMBER_DARK, trs((0.15, -0.7, 0.04)), grain=(0, 0, 1))
    p.add(cyl_bm(0.105, 0.025, 10), IRON, trs((0.15, -0.7, 0.2)))
    p.add(box_bm((0.1, 0.1, 0.14), 0.01), TIMBER_DARK, trs((-0.15, -0.55, 0.04)))
    p.add(box_bm((0.18, 0.08, 0.06), 0.01, taper=0.8), IRON, trs((-0.15, -0.55, 0.18)))
    tip = wall_torch(p, (-0.62, -0.09, 0.42), (0, -1))
    p.build(r)
    empty("Torch", tip, r)
    export("ArtRepairStation")


if __name__ == "__main__":
    house()
    farm()
    spike_trap()
    cannon()
    watchtower()
    repair_station()
    # the six other enemy kinds: characters.py (plan 013)
