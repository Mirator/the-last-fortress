"""
Plan 011 extension: the established slice style applied to the remaining release content — House, Farm, Spike Trap,
Cannon, Watchtower, Repair Station, the entry gate, and the six other enemy kinds on the Grunt's part rig. Run headless:

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
from mathutils import Vector  # noqa: E402

PLASTER, CROP, SOIL_DARK, HAY, PENNANT, KEG, HAZARD = "ArtPlaster", "ArtCrop", "ArtSoilTilled", "ArtHay", "ArtPennant", "ArtKeg", "ArtHazard"
RUNNER_CLOTH, BRUTE_CLOTH, ARCHER_CLOTH, BOMBER_CLOTH, COMMANDER_CLOTH = "ArtRunnerCloth", "ArtBruteCloth", "ArtArcherCloth", "ArtBomberCloth", "ArtCommanderCloth"
GOLEM_STONE, GOLEM_GLOW, ENEMY_IRON, SKULL = "ArtGolemStone", "ArtGolemGlow", "ArtEnemyIron", "ArtBone"


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
    """Cottage, ~1.1: stone footing, whitewashed timber-framed walls, steep gabled shingle roof with eaves, stone chimney,
    plank door, two warm windows (glow at night through the window material)."""
    r = root("House")
    p = Part("Body")
    p.add(box_bm((0.76, 0.66, 0.1), 0.015, taper=0.95), STONE_DARK, trs((0, 0, 0)))
    p.add(box_bm((0.7, 0.6, 0.5), 0.01), PLASTER, trs((0, 0, 0.09)))
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.add(box_bm((0.05, 0.05, 0.52), 0.006), TIMBER_DARK, trs((sx * 0.335, sy * 0.285, 0.085)), grain=(0, 0, 1))
    for sy in (-1, 1):
        p.add(box_bm((0.72, 0.03, 0.04), 0.004), TIMBER_DARK, trs((0, sy * 0.302, 0.33)), grain=(1, 0, 0))
        p.add(box_bm((0.72, 0.03, 0.04), 0.004), TIMBER_DARK, trs((0, sy * 0.302, 0.56)), grain=(1, 0, 0))
        for sx in (-1, 1):
            beam(p, (sx * 0.3, sy * 0.305, 0.36), (sx * 0.14, sy * 0.305, 0.56), 0.03, 0.02, TIMBER_DARK, 0.003)
    for sx in (-1, 1):
        p.add(box_bm((0.03, 0.62, 0.04), 0.004), TIMBER_DARK, trs((sx * 0.352, 0, 0.56)), grain=(0, 1, 0))
    # gable ends (plaster triangles) and the roof
    p.add(prism_bm(0.7, 0.6, 0.36), PLASTER, trs((0, 0, 0.58)))
    p.add(prism_bm(0.86, 0.78, 0.44), SHINGLE, trs((0, 0, 0.54)))
    p.add(box_bm((0.88, 0.05, 0.05), 0.008), TIMBER_DARK, trs((0, 0, 0.965)), grain=(1, 0, 0))
    p.add(box_bm((0.13, 0.13, 0.5), 0.012, taper=0.92), STONE, trs((0.2, 0.17, 0.62)))
    p.add(box_bm((0.16, 0.16, 0.035), 0.006), STONE_DARK, trs((0.2, 0.17, 1.11)))
    p.add(box_bm((0.15, 0.025, 0.27), 0.004), DOOR, trs((-0.12, -0.31, 0.1)), grain=(0, 0, 1))
    p.add(box_bm((0.17, 0.02, 0.03), 0.003), TIMBER_DARK, trs((-0.12, -0.315, 0.37)))
    for x in (0.15,):
        p.add(box_bm((0.12, 0.02, 0.1), 0.0), WINDOW, trs((x, -0.305, 0.36)))
        p.add(box_bm((0.15, 0.03, 0.02), 0.003), TIMBER_DARK, trs((x, -0.315, 0.345)))
    p.add(box_bm((0.02, 0.12, 0.1), 0.0), WINDOW, trs((0.355, 0.0, 0.36)))
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
            h = rng.uniform(0.12, 0.2)
            p.add(cyl_bm(0.05, h, 5, radius_top=0.0), CROP, trs((x + rng.uniform(-0.02, 0.02), y, 0.07), (0, 0, rng.uniform(0, 360))))
    for k in range(9):
        t = -0.84 + k * 0.21
        p.add(cyl_bm(0.018, 0.22, 5), TIMBER_DARK, trs((t, -0.86, 0.04)), grain=(0, 0, 1))
        p.add(cyl_bm(0.018, 0.22, 5), TIMBER_DARK, trs((-0.86, t, 0.04)), grain=(0, 0, 1))
    for z in (0.12, 0.2):
        p.add(box_bm((1.72, 0.025, 0.035), 0.005), TIMBER, trs((0, -0.86, z)), grain=(1, 0, 0))
        p.add(box_bm((0.025, 1.72, 0.035), 0.005), TIMBER, trs((-0.86, 0, z)), grain=(0, 1, 0))
    p.add(box_bm((0.36, 0.32, 0.3), 0.01), TIMBER, trs((0.62, -0.62, 0.04)), grain=(0, 0, 1))
    p.add(prism_bm(0.46, 0.42, 0.2), SHINGLE, trs((0.62, -0.62, 0.34)))
    p.add(box_bm((0.1, 0.02, 0.18), 0.003), DOOR, trs((0.62, -0.785, 0.05)))
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
    for side in range(4):
        rot = math.radians(90 * side)
        c = Vector((math.cos(rot), math.sin(rot), 0)) * 0.37
        p.add(box_bm((0.06 if abs(c.x) > 0.1 else 0.8, 0.8 if abs(c.x) > 0.1 else 0.06, 0.05), 0.008), TIMBER, trs((c.x, c.y, 0.0)), grain=(0, 1, 0) if abs(c.x) > 0.1 else (1, 0, 0))
    for x in range(3):
        for y in range(3):
            p.add(cyl_bm(0.022, 0.1, 5, radius_top=0.0), IRON, trs((-0.2 + x * 0.2, -0.2 + y * 0.2, 0.035)))
    p.build(r)
    export("ArtSpikeTrap")


def cannon():
    """2 x 2 gun emplacement, ~1.1: crenellated stone platform and timber deck (static), and the gun that traverses:
    Carriage, Barrel, Muzzle (pivot at the muzzle mouth), Wheel (both wheels) and Shot (a stack of balls)."""
    r = root("Cannon")
    p = Part("Body")
    p.add(box_bm((1.74, 1.74, 0.26), 0.025, taper=0.95), STONE_DARK, trs((0, 0, 0)))
    p.add(box_bm((1.5, 1.5, 0.08), 0.01), TIMBER, trs((0, 0, 0.25)), grain=(1, 0, 0))
    for side in range(4):
        for k in (-2, -1, 1, 2):
            rot = math.radians(90 * side)
            off = Vector((k * 0.3, -0.78, 0))
            c = Vector((off.x * math.cos(rot) - off.y * math.sin(rot), off.x * math.sin(rot) + off.y * math.cos(rot), 0.25))
            p.add(box_bm((0.22, 0.14, 0.24), 0.015), STONE, trs(c, (0, 0, 90 * side)))
        rot = math.radians(90 * side)
        c = Vector((0.78 * math.sin(rot), -0.78 * math.cos(rot), 0.25))
        p.add(box_bm((1.7 if side % 2 == 0 else 0.14, 0.14 if side % 2 == 0 else 1.7, 0.1), 0.012), STONE, trs(c))
    p.build(r)
    carriage = Part("Carriage")
    for sx in (-1, 1):
        carriage.add(box_bm((0.07, 0.62, 0.26), 0.012), TIMBER_DARK, trs((sx * 0.17, -0.05, 0.33)), grain=(0, 1, 0))
    carriage.add(box_bm((0.36, 0.5, 0.06), 0.01), TIMBER_DARK, trs((0, -0.05, 0.36)), grain=(1, 0, 0))
    carriage.add(box_bm((0.3, 0.06, 0.06), 0.008), IRON, trs((0, -0.32, 0.36)))
    carriage.build(r)
    barrel = Part("Barrel")
    barrel.add(cyl_bm(0.11, 0.78, 12, radius_top=0.085, bevel=0.008), IRON, trs((0, -0.25, 0.62), (-70, 0, 0)))
    for t in (0.0, 0.3, 0.62):
        d = Vector((0, math.cos(math.radians(20)), math.sin(math.radians(20))))
        barrel.add(cyl_bm(0.12 - t * 0.05, 0.035, 12), IRON, trs(Vector((0, -0.25, 0.62)) + d * t * 0.78 - Vector((0, 0, 0.01)), (-70, 0, 0)))
    barrel.add(ico_bm(0.07, 1), IRON, trs((0, -0.29, 0.6)))
    barrel.build(r)
    d = Vector((0, math.cos(math.radians(20)), math.sin(math.radians(20))))
    mouth = Vector((0, -0.25, 0.62)) + d * 0.8
    muzzle = Part("Muzzle")
    muzzle.add(cyl_bm(0.105, 0.06, 12), IRON, trs(mouth - d * 0.06, (-70, 0, 0)))
    muzzle.build(r, mouth)
    wheel = Part("Wheel")
    for sx in (-1, 1):
        wheel.add(cyl_bm(0.17, 0.05, 12, bevel=0.008), TIMBER_DARK, trs((sx * 0.235, -0.05, 0.42), (0, 90, 0)))
        wheel.add(cyl_bm(0.05, 0.06, 8), IRON, trs((sx * 0.24, -0.05, 0.42), (0, 90, 0)))
    wheel.build(r)
    shot = Part("Shot")
    for (x, y, z) in ((0, 0, 0), (0.1, 0, 0), (0.05, 0.085, 0), (0.05, 0.03, 0.08)):
        shot.add(ico_bm(0.05, 1), IRON, trs((-0.55 + x, -0.55 + y, 0.38 + z)))
    shot.build(r)
    export("ArtCannon")


def watchtower():
    """Very tall thin lookout, 2.5: small stone footing, four stilts with braces, a lookout box with a shingle cap and an
    amber pennant on a pole (support, not a weapon)."""
    r = root("Watchtower")
    p = Part("Body")
    p.add(box_bm((0.58, 0.58, 0.14), 0.015, taper=0.88), STONE_DARK, trs((0, 0, 0)))
    for cx, cy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        beam(p, (cx * 0.2, cy * 0.2, 0.12), (cx * 0.16, cy * 0.16, 2.02), 0.055, 0.055, TIMBER, 0.008)
    for z0, z1 in ((0.2, 0.95), (0.95, 1.7)):
        for side in range(4):
            rot = math.radians(90 * side)
            def R(v):
                return Vector((v[0] * math.cos(rot) - v[1] * math.sin(rot), v[0] * math.sin(rot) + v[1] * math.cos(rot), v[2]))
            beam(p, R((-0.19, -0.21, z0)), R((0.17, -0.19, z1)), 0.025, 0.02, TIMBER_DARK, 0.003)
    for z in (0.95, 1.7):
        p.add(box_bm((0.44, 0.44, 0.03), 0.004), TIMBER_DARK, trs((0, 0, z)))
    p.add(box_bm((0.58, 0.58, 0.05), 0.008), TIMBER, trs((0, 0, 2.0)), grain=(1, 0, 0))
    for side in range(4):
        rot = math.radians(90 * side)
        c = Vector((0.27 * math.sin(rot), -0.27 * math.cos(rot), 2.05))
        p.add(box_bm((0.58 if side % 2 == 0 else 0.035, 0.035 if side % 2 == 0 else 0.58, 0.13), 0.005), TIMBER, trs(c), grain=(0, 0, 1))
    for cx, cy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        p.add(box_bm((0.035, 0.035, 0.18), 0.004), TIMBER_DARK, trs((cx * 0.26, cy * 0.26, 2.05)))
    p.add(pyramid_bm(0.68, 0.2), SHINGLE, trs((0, 0, 2.22)))
    p.add(cyl_bm(0.012, 0.42, 6), TIMBER_DARK, trs((0.1, 0.0, 2.3)))
    p.add(box_bm((0.25, 0.008, 0.12), 0.002), PENNANT, trs((0.23, 0.0, 2.58)))
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
    beam(p, (0.55, -0.45, 1.4), (-0.05, -0.45, 1.36), 0.06, 0.06, TIMBER, 0.008)
    beam(p, (0.55, -0.45, 0.95), (0.25, -0.45, 1.37), 0.04, 0.04, TIMBER_DARK, 0.005)
    p.add(cyl_bm(0.006, 0.42, 4), TIMBER_DARK, trs((0.0, -0.45, 0.94)))
    p.add(cyl_bm(0.03, 0.05, 8, bevel=0.005), IRON, trs((0.0, -0.45, 0.89)))
    p.add(box_bm((0.015, 0.05, 0.06), 0.003), IRON, trs((0.0, -0.45, 0.84)))
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


def gate():
    """Enemy entry gate (stands on the beach, faces the board along +Y): rough log posts with iron caps and a skull, a
    lashed crossbeam and a hanging coral war banner (part "Banner", pulsed by the runtime for a new side)."""
    r = root("Gate")
    p = Part("Body")
    for sx in (-1, 1):
        p.add(cyl_bm(0.11, 1.62, 7, radius_top=0.09), TIMBER_DARK, trs((sx * 0.5, 0, 0)), grain=(0, 0, 1))
        p.add(cyl_bm(0.1, 0.16, 7, radius_top=0.0), ENEMY_IRON, trs((sx * 0.5, 0, 1.6)))
        p.add(cyl_bm(0.115, 0.05, 7), ENEMY_IRON, trs((sx * 0.5, 0, 1.2)))
        p.add(box_bm((0.26, 0.26, 0.12), 0.02, taper=0.8), STONE_DARK, trs((sx * 0.5, 0, 0)))
    p.add(box_bm((1.36, 0.14, 0.14), 0.015), TIMBER_DARK, trs((0, 0, 1.42)), grain=(1, 0, 0))
    p.add(ico_bm(0.07, 1), SKULL, trs((0, 0.08, 1.56), scale=(1, 0.9, 1.05)))
    p.add(box_bm((0.05, 0.02, 0.02), 0.0), "ArtEnemyEyes", trs((0, 0.145, 1.57)))
    p.build(r)
    banner = Part("Banner")
    banner.add(box_bm((0.74, 0.03, 0.6), 0.006), "ArtWarBanner", trs((0, 0.02, 0.8)))
    banner.add(box_bm((0.74, 0.035, 0.05), 0.004), ENEMY_IRON, trs((0, 0.02, 1.38)))
    banner.add(cyl_bm(0.06, 0.035, 3), ENEMY_LEATHER, trs((0, 0.04, 1.0), (90, 0, 180)))
    banner.build(r, (0, 0.02, 1.4))
    export("ArtGate")


# ---------------------------------------------------------------- enemies on the shared part rig

def humanoid(name, cloth, *, height=1.0, width=1.0, lean=0.0, head="hood", weapon=None, torso_extra=None, top_extra=None,
             leather=ENEMY_LEATHER, legs_cloth=None):
    """
    Part rig shared by every walking enemy (same joints as the Grunt). height/width scale the body; lean tilts the torso
    forward in its rest pose (Runner); head picks the silhouette (hood, helm, horned, stone); weapon(part, grip) builds the
    held weapon in the right hand; torso_extra(part) adds to the torso (keg, cape, quiver); top_extra(root) adds top-level
    parts that must stay direct children (Bomber WickTip/TorchFlame).
    """
    r = root(name)
    h, w = height, width
    hip_z = 0.2 * h
    hips = Part("Hips")
    hips.add(box_bm((0.16 * w, 0.1 * w, 0.07 * h), 0.015), leather, trs((0, 0, hip_z - 0.03 * h)))
    hips.add(box_bm((0.17 * w, 0.11 * w, 0.025 * h), 0.006), TIMBER_DARK, trs((0, 0, hip_z + 0.025 * h)))
    hips.build(r, (0, 0, hip_z))
    torso = Part("Torso")
    chest_z = hip_z + 0.04 * h
    torso.add(box_bm((0.18 * w, 0.12 * w, 0.16 * h), 0.03, taper=1.12), leather, trs((0, 0, chest_z)))
    torso.add(box_bm((0.13 * w, 0.02, 0.21 * h), 0.006, taper=0.85), cloth, trs((0, 0.066 * w, hip_z - 0.02 * h)), grain=(0, 0, 1))
    torso.add(box_bm((0.13 * w, 0.02, 0.17 * h), 0.006, taper=0.9), cloth, trs((0, -0.066 * w, hip_z)), grain=(0, 0, 1))
    for s in (-1, 1):
        torso.add(ico_bm(0.05 * w, 1), leather if head != "helm" else ENEMY_IRON, trs((s * 0.1 * w, 0, hip_z + 0.19 * h), scale=(1.1, 1, 0.8)))
    if head == "hood":
        torso.add(cyl_bm(0.115 * w, 0.05 * h, 8, radius_top=0.08 * w), cloth, trs((0, -0.005, hip_z + 0.18 * h)))
    if torso_extra:
        torso_extra(torso, h, w)
    torso_o = torso.build(r, (0, 0, hip_z + 0.02 * h))
    torso_o.rotation_euler = (math.radians(lean), 0, 0)
    head_z = hip_z + 0.22 * h
    hd = Part("Head")
    if head == "stone":
        hd.add(box_bm((0.16 * w, 0.15 * w, 0.13 * h), 0.02, taper=0.85), GOLEM_STONE, trs((0, 0.01, head_z)))
        for s in (-1, 1):
            hd.add(box_bm((0.03, 0.01, 0.02), 0.0), GOLEM_GLOW, trs((s * 0.035 * w, 0.087 * w, head_z + 0.075 * h)))
    else:
        hd.add(ico_bm(0.062 * w, 1), leather, trs((0, 0.008, head_z + 0.06 * h)))
        if head == "hood":
            hd.add(cyl_bm(0.074 * w, 0.11 * h, 8, radius_top=0.02), cloth, trs((0, -0.012, head_z + 0.02 * h), (-12, 0, 0)))
            hd.add(box_bm((0.09 * w, 0.03, 0.06 * h), 0.008), leather, trs((0, 0.05 * w, head_z + 0.035 * h)))
        else:
            hd.add(cyl_bm(0.072 * w, 0.09 * h, 8, radius_top=0.062 * w, bevel=0.006), ENEMY_IRON, trs((0, 0.0, head_z + 0.035 * h)))
            hd.add(box_bm((0.1 * w, 0.02, 0.015), 0.0), leather, trs((0, 0.07 * w, head_z + 0.075 * h)))
            if head == "horned":
                for s in (-1, 1):
                    beam(hd, (s * 0.06 * w, 0, head_z + 0.1 * h), (s * 0.13 * w, 0.02, head_z + 0.17 * h), 0.022, 0.022, SKULL, 0.004)
        for s in (-1, 1):
            hd.add(box_bm((0.016, 0.006, 0.01), 0.0), ENEMY_EYES, trs((s * 0.022 * w, 0.066 * w, head_z + 0.062 * h)))
    hd.build(r, (0, 0, head_z))
    shoulder_z = hip_z + 0.19 * h
    for s, arm_name in ((-1, "ArmL"), (1, "ArmR")):
        arm = Part(arm_name)
        sx = s * 0.125 * w
        arm.add(box_bm((0.05 * w, 0.05 * w, 0.13 * h), 0.012), leather, trs((sx, 0, shoulder_z - 0.13 * h)))
        arm.add(box_bm((0.055 * w, 0.055 * w, 0.03 * h), 0.008), TIMBER_DARK, trs((sx, 0, shoulder_z - 0.1 * h)))
        arm.add(ico_bm(0.03 * w, 1), leather, trs((sx, 0.005, shoulder_z - 0.145 * h)))
        arm.build(r, (sx, 0, shoulder_z))
        if s == 1 and weapon:
            wp = Part("Weapon")
            grip = Vector((sx, 0.01, shoulder_z - 0.145 * h))
            angle = weapon(wp, grip, h, w)
            wo = wp.build(r, grip)
            wo.rotation_euler = (math.radians(angle), 0, 0)
    for s, leg_name in ((-1, "LegL"), (1, "LegR")):
        leg = Part(leg_name)
        lx = s * 0.05 * w
        leg.add(box_bm((0.06 * w, 0.065 * w, 0.13 * h), 0.012, taper=0.85), legs_cloth or leather, trs((lx, 0, hip_z - 0.16 * h)))
        leg.add(box_bm((0.065 * w, 0.1 * w, 0.045 * h), 0.012), TIMBER_DARK, trs((lx, 0.012, 0.0)))
        leg.build(r, (lx, 0, hip_z - 0.02 * h))
    if top_extra:
        top_extra(r, h, w)
    export("Art" + name)


def dagger(p, grip, h, w):
    p.add(box_bm((0.012, 0.012, 0.04), 0.002), TIMBER_DARK, trs(grip - Vector((0, 0, 0.02))))
    p.add(box_bm((0.04, 0.012, 0.01), 0.002), IRON, trs(grip + Vector((0, 0, 0.02))))
    p.add(cyl_bm(0.012, 0.1, 4, radius_top=0.0), IRON, trs(grip + Vector((0, 0, 0.03))))
    return -70


def big_club(p, grip, h, w):
    p.add(cyl_bm(0.02 * w, 0.26 * h, 6, radius_top=0.026 * w), TIMBER_DARK, trs(grip - Vector((0, 0, 0.04))), grain=(0, 0, 1))
    p.add(cyl_bm(0.06 * w, 0.14 * h, 7, radius_top=0.045 * w, bevel=0.01), TIMBER_DARK, trs(grip + Vector((0, 0, 0.18 * h))), grain=(0, 0, 1))
    for k in range(6):
        a = math.radians(k * 60)
        p.add(cyl_bm(0.012, 0.04, 4, radius_top=0.0), IRON, trs(grip + Vector((math.cos(a) * 0.055 * w, math.sin(a) * 0.055 * w, 0.26 * h)), (0, 90, math.degrees(a))))
    return -50


def bow(p, grip, h, w):
    for k in range(-2, 3):
        a = math.radians(k * 17)
        p.add(box_bm((0.014, 0.016, 0.07 * h), 0.003), TIMBER, trs(grip + Vector((0, 0.01 + (1 - math.cos(a)) * -0.12, math.sin(a) * 0.32 * h)), (math.degrees(a), 0, 0)))
    p.add(box_bm((0.004, 0.004, 0.32 * h), 0.0), "ArtString", trs(grip + Vector((0, -0.05, -0.16 * h))))
    return -5


def torch_hand(p, grip, h, w):
    p.add(cyl_bm(0.014, 0.18, 6), TIMBER_DARK, trs(grip - Vector((0, 0, 0.03))), grain=(0, 0, 1))
    p.add(cyl_bm(0.024, 0.03, 6), IRON, trs(grip + Vector((0, 0, 0.14))))
    return -35


def war_banner(p, grip, h, w):
    p.add(cyl_bm(0.018, 1.35, 6), TIMBER_DARK, trs(grip - Vector((0, 0, 0.35))), grain=(0, 0, 1))
    p.add(cyl_bm(0.03, 0.08, 6, radius_top=0.0), ENEMY_IRON, trs(grip + Vector((0, 0, 1.0))))
    p.add(box_bm((0.02, 0.26, 0.36), 0.004), COMMANDER_CLOTH, trs(grip + Vector((0, 0.14, 0.58))))
    p.add(box_bm((0.025, 0.1, 0.1), 0.004), "ArtPennant", trs(grip + Vector((0, 0.14, 0.76))))
    p.add(box_bm((0.03, 0.27, 0.03), 0.004), ENEMY_IRON, trs(grip + Vector((0, 0.14, 0.94))))
    return 0


def stone_fist(p, grip, h, w):
    p.add(box_bm((0.13, 0.13, 0.12), 0.02), GOLEM_STONE, trs(grip - Vector((0, 0, 0.08))))
    p.add(box_bm((0.06, 0.01, 0.06), 0.0), GOLEM_GLOW, trs(grip + Vector((0, 0.066, -0.03))))
    return 0


def quiver(t, h, w):
    t.add(cyl_bm(0.03, 0.17 * h, 6), TIMBER_DARK, trs((0.04, -0.08 * w, 0.22 * h), (25, 0, 10)))
    for k in range(3):
        t.add(cyl_bm(0.004, 0.06, 4), "ArtString", trs((0.04 + k * 0.01 - 0.01, -0.12 * w, 0.37 * h), (25, 0, 10)))


def cape(t, h, w):
    t.add(box_bm((0.24 * w, 0.03, 0.42 * h), 0.008, taper=1.2), COMMANDER_CLOTH, trs((0, -0.085 * w, 0.06 * h), (-8, 0, 0)), grain=(0, 0, 1))
    t.add(box_bm((0.26 * w, 0.16 * w, 0.05 * h), 0.01), ENEMY_IRON, trs((0, 0, 0.4 * h)))


def armour(t, h, w):
    t.add(box_bm((0.2 * w, 0.14 * w, 0.14 * h), 0.02, taper=1.1), ENEMY_IRON, trs((0, 0, 0.27 * h)))


def keg(t, h, w):
    c = Vector((0, -0.13 * w, 0.27 * h))
    t.add(cyl_bm(0.11, 0.22, 10, radius_top=0.11, bevel=0.012), KEG, trs(c), grain=(0, 0, 1))
    for z in (0.03, 0.19):
        t.add(cyl_bm(0.115, 0.018, 10), IRON, trs(c + Vector((0, 0, z))))
    t.add(cyl_bm(0.116, 0.04, 10), HAZARD, trs(c + Vector((0, 0, 0.09))))
    for s in (-1, 1):
        beam(t, (s * 0.08, 0.07, 0.38 * h), (s * 0.08, -0.06, 0.4 * h), 0.02, 0.012, TIMBER_DARK, 0.002)


def bomber_top(r, h, w):
    # Direct children the runtime finds by name: the wick tip (fuse sparks) and the hand torch flame.
    tip = Part("WickTip")
    c = Vector((0.03, -0.13 * w, 0.2 * h + 0.02 + 0.27 * h + 0.22))
    tip.add(cyl_bm(0.008, 0.07, 4), TIMBER_DARK, trs(c - Vector((0.02, 0, 0.06)), (0, 20, 0)))
    tip.add(ico_bm(0.014, 0), FLAME, trs(c))
    tip.build(r, c)
    flame = Part("TorchFlame")
    f = Vector((0.125 * w, 0.17, 0.43 * h))
    flame.add(cyl_bm(0.03, 0.07, 6, radius_top=0.0), FLAME, trs(f))
    flame.build(r, f)


def golem_body(t, h, w):
    t.add(box_bm((0.24 * w, 0.17 * w, 0.2 * h), 0.03, taper=1.15), GOLEM_STONE, trs((0, 0, 0.04 * h)))
    t.add(box_bm((0.07 * w, 0.01, 0.07 * h), 0.0), GOLEM_GLOW, trs((0, 0.09 * w, 0.14 * h)))
    for s in (-1, 1):
        t.add(box_bm((0.11 * w, 0.12 * w, 0.08 * h), 0.02), GOLEM_STONE, trs((s * 0.15 * w, 0, 0.19 * h)))


def enemies():
    humanoid("Runner", RUNNER_CLOTH, height=0.95, width=0.85, lean=18, weapon=dagger)
    humanoid("Brute", BRUTE_CLOTH, height=1.3, width=1.55, head="horned", weapon=big_club, torso_extra=armour)
    humanoid("RaiderArcher", ARCHER_CLOTH, height=1.0, width=0.9, weapon=bow, torso_extra=quiver)
    humanoid("Bomber", BOMBER_CLOTH, height=0.9, width=1.0, lean=10, weapon=torch_hand, torso_extra=keg, top_extra=bomber_top)
    humanoid("SiegeCommander", COMMANDER_CLOTH, height=2.0, width=1.5, head="helm", weapon=war_banner, torso_extra=cape, leather=ENEMY_IRON,
             legs_cloth=ENEMY_IRON)
    humanoid("SiegeGolem", GOLEM_STONE, height=2.1, width=2.0, head="stone", weapon=stone_fist, torso_extra=golem_body, leather=GOLEM_STONE)


if __name__ == "__main__":
    house()
    farm()
    spike_trap()
    cannon()
    watchtower()
    repair_station()
    gate()
    enemies()
