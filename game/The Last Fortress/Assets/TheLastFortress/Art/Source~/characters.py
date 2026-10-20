"""
Plan 013: every figure on one articulated part rig — the hero, the Grunt, the five other walking enemies and the Siege
Golem. Run headless:

    blender -b --factory-startup -P characters.py -- <output folder>

Replaces the boxy plan 011/012 figures (models.py grunt, models_ext.py humanoid, models_hero.py). Provenance and
conventions as models.py: authored as code by the agent, deterministic, flat FBX export with world pivots, joints rebuilt
by Editor/ArtSetup.cs from its parent map.

Rig (pivot of each part in brackets): Hips [hip] > Torso [waist] > Head [neck], ArmL/ArmR [shoulder] > Weapon [grip];
Hips > LegL/LegR [hip joint] > ShinL/ShinR [knee]. Knees are new in plan 013; the runtime bends them when present.
Facing +Y, feet on the ground at z = 0, right hand on +X. Heights match the plan 011 figures (base 0.56 at height 1.0),
so the runtime's enemy scale, the hero-view scale and every health-bar height stay valid.

Silhouette tells per kind are kept from the art bible: Grunt hood and spiked club, Runner forward lean with scarf tails and
dagger, Brute horned helm and huge club, Raider Archer pointed hood with bow and quiver, Bomber keg on the back (bigger and
brighter than plan 011, which read weakly) with the WickTip/TorchFlame contract, Commander great helm, cape and war banner,
Golem a stone mass with a glowing core, hero a wide-brimmed kettle hat with plume, round shield and straight sword.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from models import (Part, box_bm, cyl_bm, export, ico_bm, root, trs,  # noqa: E402
                    TIMBER, TIMBER_DARK, IRON, BANNER, PLAYER_CLOTH, FLAME, ENEMY_LEATHER, ENEMY_EYES)
from mathutils import Matrix, Vector  # noqa: E402

SKIN, STRING = "ArtSkin", "ArtString"
GRUNT_CLOTH, RUNNER_CLOTH, BRUTE_CLOTH, ARCHER_CLOTH = "ArtGruntCloth", "ArtRunnerCloth", "ArtBruteCloth", "ArtArcherCloth"
BOMBER_CLOTH, COMMANDER_CLOTH, PENNANT, KEG, HAZARD = "ArtBomberCloth", "ArtCommanderCloth", "ArtPennant", "ArtKeg", "ArtHazard"
GOLEM_STONE, GOLEM_GLOW, ENEMY_IRON, BONE = "ArtGolemStone", "ArtGolemGlow", "ArtEnemyIron", "ArtBone"


# ---------------------------------------------------------------- shape helpers

def limb(p, a, b, r0, r1, mat, seg=8, flat=1.0):
    """A tapered cylinder from point a (radius r0) to b (radius r1); flat < 1 squashes it front to back."""
    a, b = Vector(a), Vector(b)
    d = b - a
    rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    m = Matrix.Translation(a) @ rot @ Matrix.Diagonal((1, flat, 1, 1))
    p.add(cyl_bm(r0, d.length, seg, radius_top=r1), mat, m, grain=(0, 0, 1))


def blob(p, c, radii, mat, subdiv=2, rot=(0, 0, 0)):
    """An ellipsoid (icosphere scaled to radii) at c."""
    p.add(ico_bm(1.0, subdiv), mat, trs(c, rot, radii))


def jitter_blob(p, c, radii, mat, rng, amount=0.18, rot=(0, 0, 0)):
    """A faceted rock: a low icosphere with seeded vertex jitter (Golem masses)."""
    bm = ico_bm(1.0, 1)
    for v in bm.verts:
        v.co *= 1 + rng.uniform(-amount, amount)
    p.add(bm, mat, trs(c, rot, radii))


def panel(p, top, bottom, width, thick, mat, flare=1.0, bend=0.0):
    """A hanging cloth panel (tabard, skirt, cape) from a top edge centre to a bottom edge centre, wider by flare at the
    bottom; bend pushes the middle back (cape billow). Built from three stacked slabs so it reads as fabric, not a board."""
    top, bottom = Vector(top), Vector(bottom)
    for k in range(3):
        t0, t1 = k / 3, (k + 1) / 3
        a = top.lerp(bottom, t0) + Vector((0, -bend * math.sin(t0 * math.pi), 0))
        b = top.lerp(bottom, t1) + Vector((0, -bend * math.sin(t1 * math.pi), 0))
        wa = width * (1 + (flare - 1) * t0)
        wb = width * (1 + (flare - 1) * t1)
        # box_bm tapers its top face: stand each slab on its wider edge and point it at the narrower one.
        base, tip = (b, a) if wb >= wa else (a, b)
        d = tip - base
        rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
        bm = box_bm((max(wa, wb), thick, d.length + thick * 0.5), 0.004, taper=min(wa, wb) / max(wa, wb))
        p.add(bm, mat, Matrix.Translation(base) @ rot, grain=(0, 0, 1))


# ---------------------------------------------------------------- the articulated figure

GRIP_Y = 0.03  # hands are held a little forward of the body (fraction of height)


class Body:
    """Joint positions for a figure of height h (1.0 = 0.56 tall) and width w, with optional forward lean (degrees)."""

    def __init__(self, h, w):
        self.h, self.w = h, w
        self.ankle = 0.035 * h
        self.knee = 0.115 * h
        self.hip = 0.2 * h          # Hips pivot (rig contract)
        self.hip_joint = 0.185 * h  # legs hang from here
        self.waist = 0.22 * h       # Torso pivot
        self.chest = 0.33 * h
        self.shoulder = 0.39 * h
        self.neck = 0.42 * h        # Head pivot
        self.head = 0.475 * h
        self.leg_x = 0.05 * w
        self.arm_x = 0.108 * w
        self.hand = self.shoulder - 0.15 * h


def legs(r, hips_o, b, cloth, boot, rng, trousers=None, armour=None):
    """Thigh (LegL/LegR, from the hip joint) and shin with boot (ShinL/ShinR, from the knee)."""
    h, w = b.h, b.w
    for s, leg_name, shin_name in ((-1, "LegL", "ShinL"), (1, "LegR", "ShinR")):
        x = s * b.leg_x
        thigh = Part(leg_name)
        limb(thigh, (x, 0, b.hip_joint + 0.01 * h), (x, 0.004, b.knee), 0.034 * w, 0.025 * w, trousers or cloth, 8, 0.9)
        blob(thigh, (x, 0.006, b.knee + 0.004 * h), (0.025 * w, 0.025 * w, 0.022 * h), armour or trousers or cloth, 1)
        leg_o = thigh.build(hips_o, (x, 0, b.hip_joint))
        shin = Part(shin_name)
        limb(shin, (x, 0.004, b.knee), (x, 0, b.ankle + 0.02 * h), 0.024 * w, 0.019 * w, armour or trousers or cloth, 8, 0.9)
        # boot: shaft, rounded foot with toe, sole and a turned-down cuff
        limb(shin, (x, 0, b.ankle + 0.035 * h), (x, 0, 0.012), 0.024 * w, 0.026 * w, boot, 8)
        blob(shin, (x, 0.022 * w, 0.018 * h), (0.025 * w, 0.042 * w, 0.019 * h), boot, 2)
        shin.add(box_bm((0.05 * w, 0.085 * w, 0.008), 0.003), TIMBER_DARK, trs((x, 0.016 * w, 0.0)))
        limb(shin, (x, 0, b.ankle + 0.038 * h), (x, 0, b.ankle + 0.05 * h), 0.03 * w, 0.028 * w, boot, 8)
        shin.build(leg_o, (x, 0.004, b.knee))


def arm(r, torso_o, b, s, sleeve, hand_mat, bracer=None, pauldron=None, bare_forearm=False, forearm_mat=None):
    """One arm: shoulder ball, upper arm, a slightly bent forearm and a closed hand around the grip point."""
    h, w = b.h, b.w
    x = s * b.arm_x
    a = Part("ArmR" if s > 0 else "ArmL")
    sh = Vector((x, 0, b.shoulder))
    elbow = Vector((x + s * 0.008 * w, -0.006, b.shoulder - 0.08 * h))
    wrist = Vector((x + s * 0.002 * w, GRIP_Y * h, b.hand + 0.018 * h))
    blob(a, sh + Vector((0, 0, -0.006)), (0.03 * w, 0.03 * w, 0.032 * h), sleeve, 1)
    limb(a, sh, elbow, 0.025 * w, 0.021 * w, sleeve, 8)
    blob(a, elbow, (0.021 * w, 0.021 * w, 0.02 * h), sleeve, 1)
    limb(a, elbow, wrist, 0.021 * w, 0.017 * w, forearm_mat or sleeve, 8)
    if bracer:
        limb(a, elbow.lerp(wrist, 0.35), elbow.lerp(wrist, 0.95), 0.022 * w, 0.02 * w, bracer, 8)
    if pauldron:
        blob(a, sh + Vector((s * 0.006, 0, 0.012 * h)), (0.042 * w, 0.038 * w, 0.026 * h), pauldron, 1)
    # hand: a rounded fist with a thumb, wrapped around the grip
    blob(a, (x, GRIP_Y * h, b.hand), (0.021 * w, 0.023 * w, 0.025 * h), hand_mat, 1)
    blob(a, (x - s * 0.013 * w, (GRIP_Y + 0.01) * h, b.hand + 0.008 * h), (0.009, 0.013, 0.009), hand_mat, 1)
    return a.build(torso_o, sh)


def weapon_part(r, arm_o, b, build, angle):
    """The held weapon: built around the grip (right hand), rotated forward-up into the ready pose."""
    grip = Vector((b.arm_x, GRIP_Y * b.h, b.hand))
    wp = Part("Weapon")
    build(wp, grip, b.h, b.w)
    wo = wp.build(arm_o, grip)
    wo.rotation_euler = (math.radians(angle), 0, 0)
    return wo


def figure(name, *, h=1.0, w=1.0, lean=0.0, cloth, leather=ENEMY_LEATHER, skin=ENEMY_LEATHER, boot=TIMBER_DARK,
           head=None, weapon=None, weapon_angle=-50, offhand=None, torso_extra=None, top_extra=None, trousers=None,
           leg_armour=None, pauldron=None, bracer=TIMBER_DARK, bare_forearm=False, belt=TIMBER_DARK, glove=None, seed=1):
    """
    One articulated figure. head(part, body) builds headgear and face on the Head part; weapon(part, grip, h, w) the held
    weapon; offhand(part, body) adds to the left arm (shield, bow hand); torso_extra(part, body) adds to the torso (cape,
    keg, quiver, armour); top_extra(root, body) adds top-level parts the runtime finds by name.
    """
    rng = random.Random(seed)
    r = root(name)
    b = Body(h, w)
    # Hips: pelvis, belt with buckle and pouch, front and back skirt flaps
    hp = Part("Hips")
    limb(hp, (0, 0, b.hip - 0.04 * h), (0, 0, b.waist), 0.072 * w, 0.068 * w, trousers or leather, 10, 0.72)
    limb(hp, (0, 0, b.waist - 0.012 * h), (0, 0, b.waist + 0.008 * h), 0.073 * w, 0.073 * w, belt, 10, 0.75)
    hp.add(box_bm((0.026 * w, 0.01, 0.022 * h), 0.003), IRON, trs((0, 0.055 * w, b.waist - 0.012 * h)))
    hp.add(box_bm((0.03 * w, 0.022 * w, 0.03 * h), 0.006), belt, trs((-0.055 * w, 0.03 * w, b.waist - 0.04 * h), (0, 0, 25)))
    panel(hp, (0, 0.05 * w, b.waist - 0.01 * h), (0, 0.062 * w, b.knee + 0.045 * h), 0.085 * w, 0.012, cloth, 1.2)
    panel(hp, (0, -0.05 * w, b.waist - 0.01 * h), (0, -0.064 * w, b.knee + 0.04 * h), 0.09 * w, 0.012, cloth, 1.2)
    hips_o = hp.build(r, (0, 0, b.hip))
    legs(r, hips_o, b, leather, boot, rng, trousers, leg_armour)

    # Torso: a tapered barrel from waist to shoulders, a rounded top, a tabard over the chest, collar
    tp = Part("Torso")
    limb(tp, (0, 0, b.waist), (0, 0, b.chest), 0.066 * w, 0.09 * w, leather, 10, 0.7)
    limb(tp, (0, 0, b.chest), (0, 0, b.shoulder), 0.09 * w, 0.08 * w, leather, 10, 0.7)
    blob(tp, (0, 0, b.shoulder - 0.004 * h), (0.112 * w, 0.058 * w, 0.034 * h), leather, 2)
    panel(tp, (0, 0.062 * w, b.shoulder - 0.012 * h), (0, 0.06 * w, b.waist + 0.005 * h), 0.09 * w, 0.012, cloth, 0.9)
    limb(tp, (0, 0, b.shoulder - 0.005 * h), (0, 0, b.neck), 0.026 * w, 0.022 * w, skin, 8)
    if torso_extra:
        torso_extra(tp, b)
    torso_o = tp.build(hips_o, (0, 0, b.waist))
    torso_o.rotation_euler = (math.radians(lean), 0, 0)

    hd = Part("Head")
    blob(hd, (0, 0.004, b.head), (0.046 * w, 0.05 * w, 0.054 * h), skin, 2)
    if head:
        head(hd, b)
    hd.build(torso_o, (0, 0, b.neck))

    hand = glove or (skin if bare_forearm else leather)
    forearm = skin if bare_forearm else None
    left = arm(r, torso_o, b, -1, leather, hand, bracer, pauldron, bare_forearm, forearm)
    right = arm(r, torso_o, b, 1, leather, hand, bracer, pauldron, bare_forearm, forearm)
    if offhand:
        oh = Part("OffHand")
        offhand(oh, b)
        oh.build(left, (-b.arm_x, GRIP_Y * h, b.hand))
    if weapon:
        weapon_part(r, right, b, weapon, weapon_angle)
    if top_extra:
        top_extra(r, b)
    export("Art" + name)


# ---------------------------------------------------------------- heads

def eyes(hd, b, depth=0.046, z=0.0, spread=0.018):
    """Two small coral glints: the enemies' threat colour, readable at night under a hood or visor."""
    for s in (-1, 1):
        hd.add(box_bm((0.013 * b.w, 0.006, 0.008 * b.h), 0.0), ENEMY_EYES, trs((s * spread * b.w, depth * b.w, b.head + z * b.h)))


def hood(cloth, peak=0.0, tail=0.0):
    """A deep hood: a cowl over the head, a dark face opening, a mantle over the shoulders; peak points it, tail drapes it."""
    def build(hd, b):
        h, w = b.h, b.w
        blob(hd, (0, -0.006, b.head + 0.008 * h), (0.058 * w, 0.06 * w, 0.064 * h), cloth, 2)
        if peak:
            limb(hd, (0, -0.02 * w, b.head + 0.04 * h), (0, -0.05 * w, b.head + (0.04 + peak) * h), 0.03 * w, 0.004, cloth, 8)
        if tail:
            limb(hd, (0, -0.05 * w, b.head + 0.02 * h), (0, -0.09 * w, b.head - tail * h), 0.022 * w, 0.006, cloth, 6)
        hd.add(box_bm((0.07 * w, 0.03, 0.058 * h), 0.012), ENEMY_LEATHER, trs((0, 0.038 * w, b.head - 0.03 * h)))  # shadowed face
        limb(hd, (0, 0, b.neck - 0.012 * h), (0, 0, b.neck + 0.018 * h), 0.09 * w, 0.055 * w, cloth, 10, 0.8)  # mantle
        eyes(hd, b, depth=0.069, z=0.006)
    return build


def horned_helm(hd, b):
    h, w = b.h, b.w
    limb(hd, (0, 0, b.head - 0.01 * h), (0, 0, b.head + 0.045 * h), 0.056 * w, 0.044 * w, ENEMY_IRON, 10)
    blob(hd, (0, 0, b.head + 0.045 * h), (0.044 * w, 0.044 * w, 0.025 * h), ENEMY_IRON, 2)
    hd.add(box_bm((0.012, 0.016, 0.05 * h), 0.003), ENEMY_IRON, trs((0, 0.054 * w, b.head - 0.03 * h)))  # nasal
    for s in (-1, 1):
        # curved horn: three tapering segments sweeping out and up
        pts = [Vector((s * 0.05 * w, 0, b.head + 0.03 * h)), Vector((s * 0.1 * w, 0.01, b.head + 0.05 * h)),
               Vector((s * 0.13 * w, 0.03, b.head + 0.1 * h)), Vector((s * 0.12 * w, 0.05, b.head + 0.15 * h))]
        for k in range(3):
            limb(hd, pts[k], pts[k + 1], (0.022 - k * 0.007) * w, (0.015 - k * 0.006) * w, BONE, 6)
    hd.add(box_bm((0.06 * w, 0.02, 0.03 * h), 0.006), ENEMY_LEATHER, trs((0, 0.04 * w, b.head - 0.045 * h)))  # beard
    eyes(hd, b, depth=0.05, z=0.0)


def great_helm(hd, b):
    h, w = b.h, b.w
    limb(hd, (0, 0, b.head - 0.045 * h), (0, 0, b.head + 0.05 * h), 0.056 * w, 0.054 * w, ENEMY_IRON, 12)
    blob(hd, (0, 0, b.head + 0.05 * h), (0.054 * w, 0.054 * w, 0.02 * h), ENEMY_IRON, 2)
    hd.add(box_bm((0.07 * w, 0.006, 0.008 * h), 0.0), ENEMY_EYES, trs((0, 0.056 * w, b.head + 0.006 * h)))  # glowing visor slit
    hd.add(box_bm((0.008, 0.012, 0.07 * h), 0.002), ENEMY_IRON, trs((0, 0.058 * w, b.head - 0.04 * h)))
    # crest: a tall fan of plates front to back
    for k in range(5):
        y = (k - 2) * 0.02 * w
        hd.add(box_bm((0.008, 0.02 * w, (0.05 + 0.02 * (2 - abs(k - 2))) * h), 0.002), COMMANDER_CLOTH, trs((0, y, b.head + 0.06 * h)))


def bandana(hd, b):
    h, w = b.h, b.w
    blob(hd, (0, -0.004, b.head + 0.02 * h), (0.049 * w, 0.053 * w, 0.04 * h), RUNNER_CLOTH, 2)
    hd.add(box_bm((0.075 * w, 0.03, 0.026 * h), 0.008), ENEMY_LEATHER, trs((0, 0.036 * w, b.head - 0.03 * h)))  # face wrap
    for k, s in enumerate((-1, 1)):  # knot tails streaming back: they flutter with the run (rest pose swept back)
        limb(hd, (s * 0.01, -0.05 * w, b.head + 0.02 * h), (s * 0.03, -0.12 * w, b.head - 0.01 * h - k * 0.01), 0.01, 0.004, RUNNER_CLOTH, 4, 0.4)
    eyes(hd, b, depth=0.05, z=0.002)


def kettle_hat(hd, b):
    """The hero's tell: a wide-brimmed kettle hat with an amber plume; a weathered face and short beard underneath."""
    h, w = b.h, b.w
    blob(hd, (0, 0.046 * w, b.head - 0.002 * h), (0.012, 0.012, 0.014), SKIN, 1)  # nose
    for s in (-1, 1):
        hd.add(box_bm((0.009, 0.004, 0.006), 0.0), ENEMY_LEATHER, trs((s * 0.017 * w, 0.046 * w, b.head + 0.012 * h)))
    blob(hd, (0, 0.03 * w, b.head - 0.035 * h), (0.038 * w, 0.03 * w, 0.025 * h), TIMBER_DARK, 2)  # beard
    limb(hd, (0, 0, b.head + 0.02 * h), (0, 0, b.head + 0.065 * h), 0.05 * w, 0.04 * w, IRON, 12)
    blob(hd, (0, 0, b.head + 0.064 * h), (0.04 * w, 0.04 * w, 0.02 * h), IRON, 2)
    limb(hd, (0, 0, b.head + 0.016 * h), (0, 0, b.head + 0.026 * h), 0.1 * w, 0.094 * w, IRON, 16)  # brim
    limb(hd, (0, 0, b.head + 0.026 * h), (0, 0, b.head + 0.034 * h), 0.052 * w, 0.051 * w, BANNER, 12)  # hat band
    # plume: one long feather sweeping up and back from the band, curling at the tip
    pts = [Vector((0.03 * w, -0.03 * w, b.head + 0.05 * h)), Vector((0.034 * w, -0.05 * w, b.head + 0.095 * h)),
           Vector((0.03 * w, -0.085 * w, b.head + 0.115 * h)), Vector((0.024 * w, -0.12 * w, b.head + 0.1 * h))]
    for k in range(3):
        # soft lobes along the curve, each turned to follow it: a fluffy plume, not a stick
        a, z = pts[k], pts[k + 1]
        d = z - a
        pitch = math.degrees(math.atan2(d.z, d.y))
        blob(hd, (a + z) / 2, (0.009 * w * (1 - 0.15 * k), d.length * 0.55, 0.011 * h * (1 - 0.2 * k)), PENNANT if k < 2 else BANNER, 1, (pitch, 0, 0))


def stone_head(hd, b):
    rng = random.Random(77)
    jitter_blob(hd, (0, 0.01, b.head), (0.07 * b.w, 0.068 * b.w, 0.06 * b.h), GOLEM_STONE, rng)
    for s in (-1, 1):
        hd.add(box_bm((0.028, 0.01, 0.018), 0.0), GOLEM_GLOW, trs((s * 0.03 * b.w, 0.072 * b.w, b.head + 0.01 * b.h)))


# ---------------------------------------------------------------- weapons and gear

def spiked_club(p, grip, h, w):
    limb(p, grip - Vector((0, 0, 0.04)), grip + Vector((0, 0, 0.16 * h)), 0.012, 0.018, TIMBER_DARK, 7)
    limb(p, grip + Vector((0, 0, 0.14 * h)), grip + Vector((0, 0, 0.23 * h)), 0.034, 0.03, TIMBER_DARK, 8)
    blob(p, grip + Vector((0, 0, 0.235 * h)), (0.03, 0.03, 0.014), TIMBER_DARK, 1)
    for k in range(6):
        a = math.radians(k * 60 + 15)
        c = grip + Vector((math.cos(a) * 0.033, math.sin(a) * 0.033, (0.17 + 0.03 * (k % 2)) * h))
        limb(p, c, c + Vector((math.cos(a) * 0.03, math.sin(a) * 0.03, 0.006)), 0.008, 0.0, IRON, 4)
    limb(p, grip - Vector((0, 0, 0.02)), grip + Vector((0, 0, 0.03)), 0.015, 0.015, ENEMY_LEATHER, 6)  # grip wrap
    return -55


def big_club(p, grip, h, w):
    limb(p, grip - Vector((0, 0, 0.05)), grip + Vector((0, 0, 0.18 * h)), 0.018 * w, 0.026 * w, TIMBER_DARK, 8)
    limb(p, grip + Vector((0, 0, 0.16 * h)), grip + Vector((0, 0, 0.3 * h)), 0.055 * w, 0.045 * w, TIMBER_DARK, 9)
    for ring in (0.2, 0.27):
        limb(p, grip + Vector((0, 0, ring * h)), grip + Vector((0, 0, (ring + 0.015) * h)), 0.058 * w, 0.056 * w, IRON, 9)
    for k in range(8):
        a = math.radians(k * 45)
        c = grip + Vector((math.cos(a) * 0.05 * w, math.sin(a) * 0.05 * w, (0.22 + 0.04 * (k % 2)) * h))
        limb(p, c, c + Vector((math.cos(a) * 0.04, math.sin(a) * 0.04, 0.01)), 0.011, 0.0, IRON, 4)
    return -50


def dagger(p, grip, h, w):
    limb(p, grip - Vector((0, 0, 0.02)), grip + Vector((0, 0, 0.02)), 0.008, 0.008, ENEMY_LEATHER, 6)
    p.add(box_bm((0.04, 0.012, 0.008), 0.002), IRON, trs(grip + Vector((0, 0, 0.02))))
    p.add(box_bm((0.016, 0.004, 0.09), 0.001, taper=0.15), IRON, trs(grip + Vector((0, 0, 0.026))))
    return -70


def bow(p, grip, h, w):
    """A recurve stave held upright in the right hand, with its string."""
    pts = []
    for k in range(-4, 5):
        t = k / 4
        pts.append(grip + Vector((0, 0.02 - 0.045 * (1 - t * t) + 0.015 * abs(t) ** 3, t * 0.17 * h)))
    for k in range(8):
        limb(p, pts[k], pts[k + 1], 0.009 if 2 < k < 5 else 0.006, 0.009 if 2 < k < 5 else 0.005, TIMBER, 6)
    limb(p, pts[0], pts[-1], 0.0018, 0.0018, STRING, 4)
    return -5


def torch_hand(p, grip, h, w):
    limb(p, grip - Vector((0, 0, 0.04)), grip + Vector((0, 0, 0.14)), 0.011, 0.014, TIMBER_DARK, 6)
    limb(p, grip + Vector((0, 0, 0.13)), grip + Vector((0, 0, 0.16)), 0.022, 0.02, IRON, 6)
    return -35


def war_banner(p, grip, h, w):
    limb(p, grip - Vector((0, 0, 0.35)), grip + Vector((0, 0, 1.0)), 0.016, 0.014, TIMBER_DARK, 8)
    limb(p, grip + Vector((0, 0, 1.0)), grip + Vector((0, 0, 1.09)), 0.03, 0.0, ENEMY_IRON, 6)
    p.add(box_bm((0.03, 0.29, 0.03), 0.004), ENEMY_IRON, trs(grip + Vector((0, 0.14, 0.94))))
    panel(p, grip + Vector((0, 0.15, 0.955)), grip + Vector((0, 0.15, 0.5)), 0.27, 0.012, COMMANDER_CLOTH, 0.85)
    blob(p, grip + Vector((0, 0.15, 0.78)), (0.012, 0.06, 0.06), PENNANT, 1)  # sigil
    return 0


def sword(p, grip, h, w):
    limb(p, grip - Vector((0, 0, 0.035)), grip + Vector((0, 0, 0.03)), 0.01, 0.01, TIMBER_DARK, 6)
    blob(p, grip - Vector((0, 0, 0.04)), (0.014, 0.014, 0.014), IRON, 1)  # pommel
    p.add(box_bm((0.075, 0.014, 0.012), 0.003), IRON, trs(grip + Vector((0, 0, 0.03))))  # crossguard
    p.add(box_bm((0.02, 0.005, 0.25 * h), 0.0015, taper=0.55), IRON, trs(grip + Vector((0, 0, 0.042))))  # blade
    p.add(box_bm((0.005, 0.0065, 0.18 * h), 0.0), TIMBER_DARK, trs(grip + Vector((0, 0, 0.05))))  # fuller
    return -45


def stone_fist(p, grip, h, w):
    rng = random.Random(5)
    jitter_blob(p, grip - Vector((0, 0, 0.06)), (0.075, 0.075, 0.07), GOLEM_STONE, rng, 0.22)
    p.add(box_bm((0.05, 0.01, 0.05), 0.0), GOLEM_GLOW, trs(grip + Vector((0, 0.07, -0.05))))
    return 0


def round_shield(oh, b):
    c = Vector((-b.arm_x - 0.035 * b.w, 0.03 * b.h, b.hand + 0.03 * b.h))
    rot = (0, 90, 20)
    oh.add(cyl_bm(0.105 * b.w, 0.016, 16, bevel=0.004), TIMBER, trs(c, rot))
    oh.add(cyl_bm(0.11 * b.w, 0.009, 16, cap=False), IRON, trs(c + Vector((-0.003, 0, 0)), rot))
    oh.add(cyl_bm(0.04 * b.w, 0.018, 12), BANNER, trs(c + Vector((-0.004, 0, 0)), rot))
    blob(oh, c + Vector((-0.02, 0.006, 0)), (0.02, 0.02, 0.02), IRON, 1)  # boss
    for k in (-1, 1):  # painted quarters
        oh.add(box_bm((0.006, 0.018 * b.w, 0.17 * b.w), 0.0), BANNER, trs(c + Vector((-0.008, 0, 0)), (0, 0, 20 + k * 0)))


def hero_cape(t, b):
    h, w = b.h, b.w
    panel(t, (0, -0.06 * w, b.shoulder), (0, -0.075 * w, b.knee + 0.04 * h), 0.15 * w, 0.012, PLAYER_CLOTH, 1.35, 0.018)
    limb(t, (0, -0.004, b.shoulder - 0.012 * h), (0, -0.004, b.shoulder + 0.01 * h), 0.1 * w, 0.06 * w, PLAYER_CLOTH, 12, 0.75)  # mantle
    t.add(box_bm((0.03 * w, 0.014, 0.12 * h), 0.003), BANNER, trs((0, 0.069 * w, b.waist + 0.04 * h)))  # amber stripe
    limb(t, (-0.07 * w, 0.06 * w, b.shoulder - 0.01 * h), (0.07 * w, -0.06 * w, b.waist + 0.01 * h), 0.008, 0.008, TIMBER_DARK, 6)  # baldric


def grunt_gear(t, b):
    h, w = b.h, b.w
    limb(t, (-0.07 * w, 0.06 * w, b.shoulder - 0.01 * h), (0.07 * w, -0.06 * w, b.waist + 0.02 * h), 0.009, 0.009, TIMBER_DARK, 6)
    blob(t, (0.06 * w, -0.05 * w, b.chest - 0.02 * h), (0.025, 0.02, 0.03), TIMBER_DARK, 1)  # sack


def runner_gear(t, b):
    h, w = b.h, b.w
    limb(t, (0, 0.06 * w, b.chest), (0, 0.06 * w, b.chest + 0.012 * h), 0.03, 0.03, RUNNER_CLOTH, 8)
    for s in (-1, 1):
        limb(t, (s * 0.06 * w, -0.05 * w, b.waist + 0.03 * h), (s * 0.075 * w, -0.06 * w, b.waist - 0.06 * h), 0.008, 0.008, IRON, 4)  # spare blades


def brute_gear(t, b):
    h, w = b.h, b.w
    blob(t, (0, 0.012, b.chest), (0.095 * w, 0.065 * w, 0.06 * h), ENEMY_IRON, 2)  # plate
    for k in range(4):
        limb(t, (-0.08 * w, 0.064 * w, b.chest - (0.03 - 0.02 * k) * h), (0.08 * w, 0.064 * w, b.chest - (0.03 - 0.02 * k) * h), 0.004, 0.004, IRON, 4)
    blob(t, (0, -0.01, b.shoulder + 0.01 * h), (0.12 * w, 0.07 * w, 0.03 * h), ENEMY_LEATHER, 2)  # fur mantle
    for s in (-1, 1):
        for k in range(3):
            blob(t, (s * (0.05 + 0.025 * k) * w, -0.03 * w, b.shoulder + 0.022 * h), (0.02, 0.02, 0.016), BONE, 1)


def archer_gear(t, b):
    h, w = b.h, b.w
    c = Vector((0.04 * w, -0.07 * w, b.chest - 0.01 * h))
    top = c + Vector((-0.02, -0.03, 0.14 * h))
    limb(t, c - Vector((-0.01, -0.01, 0.07 * h)), top, 0.024, 0.026, TIMBER_DARK, 8)
    for k in range(4):
        tip = top + Vector((0.008 * (k - 1.5), -0.004 * k, 0.04))
        limb(t, top - Vector((0, 0, 0.02)), tip, 0.0025, 0.0025, TIMBER, 4)
        t.add(box_bm((0.012, 0.003, 0.018), 0.0), ARCHER_CLOTH, trs(tip))
    panel(t, (0, -0.06 * w, b.shoulder), (0, -0.07 * w, b.waist - 0.04 * h), 0.13 * w, 0.01, ARCHER_CLOTH, 1.3, 0.012)


def bomber_gear(t, b):
    """The keg on the back: plan 011 read it as too small and dark at default zoom, so it is bigger, lighter, banded."""
    h, w = b.h, b.w
    c = Vector((0, -0.12 * w, b.chest - 0.07 * h))
    limb(t, c, c + Vector((0, 0, 0.17)), 0.085, 0.085, KEG, 12)
    blob(t, c + Vector((0, 0, 0.085)), (0.094, 0.094, 0.07), KEG, 2)
    for z in (0.02, 0.15):
        limb(t, c + Vector((0, 0, z)), c + Vector((0, 0, z + 0.014)), 0.092, 0.092, IRON, 12)
    limb(t, c + Vector((0, 0, 0.07)), c + Vector((0, 0, 0.1)), 0.096, 0.096, HAZARD, 12)
    for s in (-1, 1):
        limb(t, (s * 0.06 * w, 0.055 * w, b.shoulder), (s * 0.06 * w, -0.05 * w, b.chest - 0.08 * h), 0.008, 0.008, TIMBER_DARK, 4)


def commander_gear(t, b):
    h, w = b.h, b.w
    blob(t, (0, 0.01, b.chest), (0.095 * w, 0.064 * w, 0.065 * h), ENEMY_IRON, 2)
    panel(t, (0, -0.06 * w, b.shoulder + 0.01 * h), (0, -0.09 * w, 0.02 * h), 0.2 * w, 0.014, COMMANDER_CLOTH, 1.4, 0.03)
    for s in (-1, 1):
        blob(t, (s * 0.1 * w, 0, b.shoulder + 0.01 * h), (0.05 * w, 0.045 * w, 0.03 * h), ENEMY_IRON, 2)


def golem_gear(t, b):
    rng = random.Random(31)
    h, w = b.h, b.w
    jitter_blob(t, (0, 0, b.chest), (0.13 * w, 0.09 * w, 0.11 * h), GOLEM_STONE, rng, 0.15)
    for s in (-1, 1):
        jitter_blob(t, (s * 0.12 * w, -0.01, b.shoulder), (0.07 * w, 0.065 * w, 0.06 * h), GOLEM_STONE, rng)
    t.add(box_bm((0.05 * w, 0.01, 0.05 * h), 0.0), GOLEM_GLOW, trs((0, 0.088 * w, b.chest)))  # core
    for k in range(5):
        a = rng.uniform(0, 6.28)
        jitter_blob(t, (math.cos(a) * 0.08 * w, -0.05 * w, b.chest + rng.uniform(-0.05, 0.08) * h), (0.03, 0.03, 0.025), GOLEM_STONE, rng)


BOMBER_LEAN = 10


def bomber_top(r, b):
    # Direct children the runtime finds by name: the wick tip (fuse sparks) and the hand torch flame. They are not on
    # the rig, so they sit where the leaning torso and the ready torch put the keg top and the flame in the rest pose.
    h, w = b.h, b.w
    waist = Vector((0, 0, b.waist))
    keg_top = Vector((0.02, -0.12 * w, b.chest - 0.07 * h + 0.24))
    c = waist + Matrix.Rotation(math.radians(BOMBER_LEAN), 3, "X") @ (keg_top - waist)
    tip = Part("WickTip")
    limb(tip, c - Vector((0.02, 0, 0.06)), c, 0.006, 0.005, TIMBER_DARK, 4)
    blob(tip, c, (0.014, 0.014, 0.014), FLAME, 0)
    tip.build(r, c)
    grip = Vector((b.arm_x, GRIP_Y * h, b.hand))
    f = waist + Matrix.Rotation(math.radians(BOMBER_LEAN), 3, "X") @ (grip + Matrix.Rotation(math.radians(-35), 3, "X") @ Vector((0, 0, 0.16)) - waist)
    flame = Part("TorchFlame")
    flame.add(cyl_bm(0.026, 0.07, 6, radius_top=0.0), FLAME, trs(f))
    flame.build(r, f)


# ---------------------------------------------------------------- the cast

HERO = dict(h=1.06, w=1.05, cloth=PLAYER_CLOTH, leather=PLAYER_CLOTH, skin=SKIN, boot=TIMBER_DARK, head=kettle_hat, weapon=sword,
            weapon_angle=-45, offhand=round_shield, torso_extra=hero_cape, trousers=TIMBER_DARK, pauldron=None, bracer=IRON,
            belt=TIMBER_DARK, glove=TIMBER_DARK, seed=8)


def cast():
    figure("Grunt", cloth=GRUNT_CLOTH, head=hood(GRUNT_CLOTH, peak=0.03, tail=0.02), weapon=spiked_club, weapon_angle=-55, torso_extra=grunt_gear, seed=1)
    figure("Runner", h=0.95, w=0.85, lean=18, cloth=RUNNER_CLOTH, head=bandana, weapon=dagger, weapon_angle=-70,
           torso_extra=runner_gear, bare_forearm=True, bracer=None, seed=2)
    figure("Brute", h=1.3, w=1.55, cloth=BRUTE_CLOTH, head=horned_helm, weapon=big_club, weapon_angle=-50, torso_extra=brute_gear,
           pauldron=ENEMY_IRON, bracer=ENEMY_IRON, seed=3)
    figure("RaiderArcher", h=1.0, w=0.9, cloth=ARCHER_CLOTH, head=hood(ARCHER_CLOTH, peak=0.05, tail=0.04), weapon=bow, weapon_angle=-5,
           torso_extra=archer_gear, seed=4)
    figure("Bomber", h=0.9, w=1.0, lean=BOMBER_LEAN, cloth=BOMBER_CLOTH, head=hood(BOMBER_CLOTH), weapon=torch_hand, weapon_angle=-35,
           torso_extra=bomber_gear, top_extra=bomber_top, seed=5)
    # plan 014: the Commander is a 2.1 m armoured man, not a giant
    figure("SiegeCommander", h=1.3, w=1.2, cloth=COMMANDER_CLOTH, leather=ENEMY_IRON, head=great_helm, weapon=war_banner, weapon_angle=0,
           torso_extra=commander_gear, leg_armour=ENEMY_IRON, pauldron=ENEMY_IRON, bracer=ENEMY_IRON, seed=6)
    figure("SiegeGolem", h=2.1, w=2.0, cloth=GOLEM_STONE, leather=GOLEM_STONE, skin=GOLEM_STONE, boot=GOLEM_STONE, belt=GOLEM_STONE,
           head=stone_head, weapon=stone_fist, weapon_angle=0, torso_extra=golem_gear, bracer=None, pauldron=GOLEM_STONE, seed=7)
    figure("Hero", **HERO)


if __name__ == "__main__":
    cast()
