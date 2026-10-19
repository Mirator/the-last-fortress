"""
The Last Fortress art slice (plan 011): Keep, six connected palisade Wall pieces, Archer Tower with an animated archer,
and the Grunt as a part rig. Run headless:

    blender -b --factory-startup -P models.py -- <output folder>

Provenance: authored as code by the agent for this project (Blender 5.2, no add-ons, no external meshes or textures).
Deterministic: every random choice uses a seeded generator, so rerunning reproduces the same geometry.

Conventions (checked with an axis test, see plans/011): Blender X/Y/Z become Unity X/Z/Y with bakeAxisConversion, so
+Y here is the Unity forward (+Z, "north" for Walls) and Z is up. One tile = one unit; pivots on the ground at the tile
centre. Material slot names are matched to Assets/TheLastFortress/Art/Materials/<name>.mat by the Unity art setup.
Empties named Torch* mark flame tips (the runtime emits torch particles there); the setup renames them to "Torch".
Every part is exported top-level with its pivot in world space; ArtSetup re-parents rig joints (Grunt, archer).
UVs are planar per face in object space (1 unit = 1 UV), so every material tiles at one density set in Unity.
"""
import math
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "."


# ---------------------------------------------------------------- building blocks

class Part:
    """Accumulates primitives into one bmesh with material slots; becomes one mesh object (one renderer in Unity)."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.materials = []

    def slot(self, material):
        if material not in self.materials:
            self.materials.append(material)
        return self.materials.index(material)

    def add(self, geom_bm, material, matrix, grain=None):
        """Merges a primitive's bmesh, transformed, with planar UVs; grain = local direction the texture's V follows."""
        index = self.slot(material)
        geom_bm.transform(matrix)
        rot = matrix.to_3x3().normalized()
        g = (rot @ Vector(grain)).normalized() if grain else None
        mesh = bpy.data.meshes.new("tmp")
        geom_bm.to_mesh(mesh)
        geom_bm.free()
        start = len(self.bm.faces)
        self.bm.from_mesh(mesh)
        bpy.data.meshes.remove(mesh)
        self.bm.faces.ensure_lookup_table()
        for f in self.bm.faces[start:]:
            f.material_index = index
            n = f.normal
            v = g if g is not None else Vector((0, 0, 1))
            v = v - n * v.dot(n)
            if v.length < 1e-4:
                v = Vector((0, 1, 0)) - n * n.y
                if v.length < 1e-4:
                    v = Vector((1, 0, 0)) - n * n.x
            v.normalize()
            u = v.cross(n).normalized()
            for loop in f.loops:
                p = loop.vert.co
                loop[self.uv].uv = (p.dot(u), p.dot(v))

    def build(self, parent=None, origin=(0, 0, 0)):
        """Creates the object; geometry is authored in parent space and re-centred on origin (the joint/pivot)."""
        o = Vector(origin)
        for v in self.bm.verts:
            v.co -= o
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for m in self.materials:
            mesh.materials.append(material(m))
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        # Exported flat (top-level, world pivot): the FBX exporter only converts axes cleanly for top-level objects, so the
        # joint hierarchy is rebuilt in Unity by ArtSetup from its parent map. `parent` is documentation of that map here.
        obj.location = o
        return obj


def world_origin(obj):
    loc = Vector((0, 0, 0))
    while obj is not None:
        loc += obj.location
        obj = obj.parent
    return loc


def material(name):
    m = bpy.data.materials.get(name)
    return m if m is not None else bpy.data.materials.new(name)


def trs(loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    r = Matrix.Rotation(math.radians(rot[2]), 4, "Z") @ Matrix.Rotation(math.radians(rot[1]), 4, "Y") @ Matrix.Rotation(math.radians(rot[0]), 4, "X")
    s = Matrix.Diagonal((scale[0], scale[1], scale[2], 1))
    return Matrix.Translation(loc) @ r @ s


def bevel_all(bm, width, segments=1):
    if width <= 0:
        return
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=width, offset_type="OFFSET", segments=segments, profile=0.5, affect="EDGES", clamp_overlap=True)


def box_bm(size, bevel=0.012, taper=1.0, taper_y=None, segments=1):
    """Unit cube scaled to size, origin at the bottom centre; taper scales the top face (battered stone, merlons)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.z += 0.5
        if v.co.z > 0.5:
            v.co.x *= taper
            v.co.y *= taper if taper_y is None else taper_y
        v.co.x *= size[0]
        v.co.y *= size[1]
        v.co.z *= size[2]
    bevel_all(bm, min(bevel, min(size) * 0.3), segments)
    return bm


def cyl_bm(radius, depth, segments=8, radius_top=None, bevel=0.0, cap=True):
    """Cylinder/cone along +Z, origin at the bottom centre."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=segments, radius1=radius,
                          radius2=radius if radius_top is None else radius_top, depth=depth)
    for v in bm.verts:
        v.co.z += depth / 2
    if bevel > 0:
        rims = [e for e in bm.edges if not e.is_wire and len(e.link_faces) == 2 and abs(e.link_faces[0].normal.dot(e.link_faces[1].normal)) < 0.5]
        if rims:
            bmesh.ops.bevel(bm, geom=rims, offset=bevel, offset_type="OFFSET", segments=1, profile=0.5, affect="EDGES", clamp_overlap=True)
    return bm


def pyramid_bm(base, height, apex_scale=0.0, sides=4, rot=45.0):
    """Roof: a square pyramid (or truncated) with origin at the base centre; sides=4 rotated so faces align with axes."""
    bm = bmesh.new()
    r = base / math.sqrt(2)
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=sides, radius1=r, radius2=r * apex_scale, depth=height)
    for v in bm.verts:
        v.co.z += height / 2
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(rot), 3, "Z"))
    return bm


def ico_bm(radius, subdiv=1):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    return bm


def beam(p, a, b, w, h, mat, bevel=0.006):
    """A box from point a to point b (its length axis), w x h in section; grain follows the length."""
    a, b = Vector(a), Vector(b)
    d = b - a
    rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    p.add(box_bm((w, h, d.length), bevel), mat, Matrix.Translation(a) @ rot, grain=(0, 0, 1))


def empty(name, loc, parent):
    e = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(e)
    e.location = Vector(loc)
    return e


def root(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    r = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(r)
    return r


def export(name):
    path = f"{OUT}/{name}.fbx"
    bpy.ops.export_scene.fbx(filepath=path, use_selection=False, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, object_types={"EMPTY", "MESH"}, use_mesh_modifiers=True, add_leaf_bones=False,
                             mesh_smooth_type="FACE", use_tspace=False, use_custom_props=False)
    print("exported", path)


STONE, STONE_DARK, TIMBER, TIMBER_DARK, SHINGLE, IRON, DOOR = "ArtStone", "ArtStoneDark", "ArtTimber", "ArtTimberDark", "ArtShingle", "ArtIron", "ArtDoor"
FLAME, WINDOW, BANNER, PLAYER_CLOTH = "ArtFlame", "ArtWindow", "ArtBanner", "ArtPlayerCloth"
ENEMY_LEATHER, GRUNT_CLOTH, ENEMY_EYES = "ArtEnemyLeather", "ArtGruntCloth", "ArtEnemyEyes"


def wall_torch(p, at, facing):
    """Iron bracket, short timber haft and a flame cone; returns the flame-tip position for the Torch empty."""
    fx, fy = facing
    base = Vector(at)
    p.add(box_bm((0.03, 0.03, 0.03), 0.004), IRON, trs(base))
    haft = base + Vector((fx * 0.035, fy * 0.035, 0.0))
    p.add(cyl_bm(0.012, 0.09, 6), TIMBER_DARK, trs(haft, (-fy * 20, fx * 20, 0)))
    tip = haft + Vector((fx * 0.03, fy * 0.03, 0.085))
    p.add(cyl_bm(0.022, 0.025, 6), IRON, trs(tip - Vector((0, 0, 0.01))))
    p.add(cyl_bm(0.02, 0.06, 6, radius_top=0.0), FLAME, trs(tip + Vector((0, 0, 0.01))))
    return tip + Vector((0, 0, 0.05))


# ---------------------------------------------------------------- Keep

def keep():
    """
    Square limestone keep, height ~1.6: battered plinth, string courses, four corner turrets with crenellated caps, a
    machicolated parapet, a steep shingle roof with an iron finial and the settlement banner, an arched studded door
    with lantern torches, and warm slit windows (the window material glows at night).
    """
    r = root("Keep")
    p = Part("Body")
    p.add(box_bm((0.86, 0.86, 0.22), 0.02, taper=0.9), STONE_DARK, trs((0, 0, 0)))
    p.add(box_bm((0.7, 0.7, 0.92), 0.012), STONE, trs((0, 0, 0.2)))
    p.add(box_bm((0.74, 0.74, 0.035), 0.008), STONE_DARK, trs((0, 0, 0.6)))
    # corbelled parapet band and walkway
    p.add(box_bm((0.78, 0.78, 0.05), 0.01, taper=1.04), STONE_DARK, trs((0, 0, 1.06)))
    p.add(box_bm((0.8, 0.8, 0.07), 0.01), STONE, trs((0, 0, 1.11)))
    for i in range(-2, 3):
        for side in range(4):
            a = math.radians(90 * side)
            off = Vector((i * 0.12, -0.37, 0))
            pos = Matrix.Rotation(a, 3, "Z") @ off
            if abs(i) == 2:
                continue  # turrets take the corners
            p.add(box_bm((0.075, 0.06, 0.11), 0.008), STONE, trs((pos.x, pos.y, 1.18), (0, 0, 90 * side)))
            # machicolation corbels under the parapet
            p.add(box_bm((0.04, 0.05, 0.06), 0.005, taper=0.6), STONE_DARK, trs(Matrix.Rotation(a, 3, "Z") @ Vector((i * 0.12, -0.36, 0.99)), (0, 0, 90 * side)))
    for cx, cy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        c = Vector((cx * 0.35, cy * 0.35, 0))
        p.add(cyl_bm(0.135, 0.2, 10, radius_top=0.12), STONE_DARK, trs(c))
        p.add(cyl_bm(0.12, 1.02, 10, bevel=0.006), STONE, trs(c + Vector((0, 0, 0.18))))
        p.add(cyl_bm(0.14, 0.05, 10, bevel=0.008), STONE_DARK, trs(c + Vector((0, 0, 1.16))))
        for k in range(5):
            a = math.radians(k * 72 + 18)
            p.add(box_bm((0.05, 0.035, 0.07), 0.005), STONE, trs(c + Vector((math.cos(a) * 0.12, math.sin(a) * 0.12, 1.21)), (0, 0, math.degrees(a) + 90)))
        p.add(pyramid_bm(0.2, 0.17, sides=8, rot=22.5), SHINGLE, trs(c + Vector((0, 0, 1.2))))
        # slit windows on the two outer faces of each turret
        for (wx, wy) in ((cx, 0), (0, cy)):
            d = Vector((wx, wy, 0)).normalized()
            p.add(box_bm((0.022, 0.012, 0.09), 0.0), WINDOW, trs(c + d * 0.118 + Vector((0, 0, 0.78)), (0, 0, 90 if wx else 0)))
    # main roof: steep pyramid with eaves and a ridge cap
    p.add(box_bm((0.56, 0.56, 0.08), 0.01), TIMBER_DARK, trs((0, 0, 1.15)))
    p.add(pyramid_bm(0.66, 0.36), SHINGLE, trs((0, 0, 1.2)))
    p.add(cyl_bm(0.02, 0.05, 6), IRON, trs((0, 0, 1.54)))
    p.add(cyl_bm(0.008, 0.22, 6), IRON, trs((0, 0, 1.54)))
    p.add(box_bm((0.13, 0.008, 0.08), 0.002), BANNER, trs((0.068, 0, 1.66)))
    # arched door on the south (-Y) face: stone surround, studded planks, iron straps, a step
    p.add(box_bm((0.27, 0.05, 0.38), 0.01), STONE_DARK, trs((0, -0.35, 0.18)))
    p.add(cyl_bm(0.135, 0.05, 12, bevel=0.0), STONE_DARK, trs((0, -0.325, 0.56), (90, 0, 0)))
    p.add(box_bm((0.19, 0.03, 0.3), 0.004), DOOR, trs((0, -0.37, 0.2)), grain=(0, 0, 1))
    p.add(cyl_bm(0.095, 0.03, 12), DOOR, trs((0, -0.355, 0.5), (90, 0, 0)))
    for z in (0.28, 0.44):
        p.add(box_bm((0.2, 0.012, 0.02), 0.002), IRON, trs((0, -0.388, z)))
    p.add(box_bm((0.3, 0.1, 0.03), 0.008), STONE_DARK, trs((0, -0.44, 0.17)))
    # windows on the four faces of the body
    for side in range(4):
        a = math.radians(90 * side)
        for (x, z) in ((-0.13, 0.82), (0.13, 0.82), (0, 0.48)) if side != 0 else ((-0.13, 0.82), (0.13, 0.82)):
            pos = Matrix.Rotation(a, 3, "Z") @ Vector((x, -0.352, 0))
            p.add(box_bm((0.035, 0.012, 0.11), 0.0), WINDOW, trs((pos.x, pos.y, z), (0, 0, 90 * side)))
            sill = Matrix.Rotation(a, 3, "Z") @ Vector((x, -0.355, 0))
            p.add(box_bm((0.06, 0.02, 0.015), 0.003), STONE_DARK, trs((sill.x, sill.y, z - 0.012), (0, 0, 90 * side)))
    tips = [wall_torch(p, (sx * 0.2, -0.37, 0.42), (0, -1)) for sx in (-1, 1)]
    p.build(r)
    for i, t in enumerate(tips):
        empty(f"Torch.{i}", t, r)
    export("ArtKeep")


# ---------------------------------------------------------------- Walls

WALL_SHAPES = {  # canonical masks (WallTopology): N=1, E=2, S=4, W=8; facing +Y = north
    "Isolated": 0, "End": 1, "Straight": 1 | 4, "Corner": 1 | 2, "T": 1 | 2 | 8, "Cross": 15,
}
SPACING = 0.11
FIRST = (1 - 7 * SPACING) / 2  # logs at FIRST + k*SPACING meet the neighbour's at one spacing across the tile edge


def log_column(p, at, height, rng, radius=0.052):
    lean = (rng.uniform(-3, 3), rng.uniform(-3, 3), rng.uniform(0, 360))
    m = trs(at, lean)
    body = cyl_bm(radius, height, 7, radius_top=radius * 0.92)
    p.add(body, TIMBER if rng.random() < 0.6 else TIMBER_DARK, m, grain=(0, 0, 1))
    p.add(cyl_bm(radius * 0.92, 0.075, 7, radius_top=0.004, cap=True), TIMBER, m @ trs((0, 0, height)), grain=(0, 0, 1))


def wall(shape):
    """
    Palisade of sharpened logs (0.58-0.7) on a low stone sill, a thicker iron-capped post at the joint, twin binding rails
    on both faces. Arms run to the tile edge so neighbouring pieces join without a gap; height band 0.6-0.8.
    """
    mask = WALL_SHAPES[shape]
    rng = random.Random(1000 + mask)
    r = root("Wall" + shape)
    p = Part("Body")
    dirs = [(1, (0, 1)), (2, (1, 0)), (4, (0, -1)), (8, (-1, 0))]
    p.add(box_bm((0.24, 0.24, 0.07), 0.015, taper=0.92), STONE_DARK, trs((0, 0, 0)))
    for bit, (dx, dy) in dirs:
        if not mask & bit:
            continue
        along = Vector((dx, dy, 0))
        # sill: a run of irregular stones to the tile edge
        for k in range(3):
            c = along * (0.12 + k * 0.13 + 0.065)
            size = (0.13 if dx else 0.2, 0.2 if dx else 0.13, 0.06 + rng.uniform(0, 0.02))
            p.add(box_bm(size, 0.014, taper=0.9), STONE_DARK, trs(c, (0, 0, rng.uniform(-4, 4))))
        for k in range(4):
            log_column(p, along * (FIRST + k * SPACING), rng.uniform(0.56, 0.68), rng)
        side = Vector((dy, dx, 0)) * 0.06
        for z in (0.2, 0.43):
            for s in (1, -1):
                c = along * 0.25 + side * s + Vector((0, 0, z))
                p.add(box_bm((0.5 if dx else 0.028, 0.028 if dx else 0.5, 0.032), 0.006), TIMBER_DARK, trs(c), grain=(dx, dy, 0))
    if mask == 0:
        for k in range(5):
            a = math.radians(k * 72 + 10)
            log_column(p, (math.cos(a) * 0.12, math.sin(a) * 0.12, 0), rng.uniform(0.5, 0.62), rng)
        for z in (0.2, 0.4):
            p.add(cyl_bm(0.17, 0.03, 10, cap=False), TIMBER_DARK, trs((0, 0, z)))
    # joint post: thicker, taller, iron collar and spike cap
    p.add(cyl_bm(0.07, 0.7, 8, radius_top=0.064), TIMBER_DARK, trs((0, 0, 0.04)), grain=(0, 0, 1))
    p.add(cyl_bm(0.073, 0.03, 8), IRON, trs((0, 0, 0.5)))
    p.add(cyl_bm(0.066, 0.1, 8, radius_top=0.01), IRON, trs((0, 0, 0.74)))
    p.build(r)
    export("ArtWall" + shape)


# ---------------------------------------------------------------- Archer Tower

def archer(parent, at):
    """Player archer on the platform (pivot at the feet, facing +Y): Body turns to aim, Bow draws on each shot."""
    body = Part("Archer")
    body.add(cyl_bm(0.045, 0.1, 8, radius_top=0.05), PLAYER_CLOTH, trs((0, 0, 0)))
    body.add(cyl_bm(0.05, 0.075, 8, radius_top=0.035), PLAYER_CLOTH, trs((0, 0, 0.1)))
    body.add(box_bm((0.075, 0.06, 0.015), 0.004), TIMBER_DARK, trs((0, 0, 0.1)))
    body.add(ico_bm(0.032, 1), "ArtSkin", trs((0, 0.004, 0.205)))
    body.add(cyl_bm(0.04, 0.05, 8, radius_top=0.0), PLAYER_CLOTH, trs((0, -0.006, 0.21)))
    body.add(box_bm((0.03, 0.025, 0.08), 0.004), TIMBER_DARK, trs((-0.03, -0.045, 0.1), (15, 0, 0)))  # quiver
    o = body.build(parent, at)
    bow = Part("Bow")
    for k in range(-2, 3):  # a bent stave: five short segments along an arc
        a = math.radians(k * 16)
        bow.add(box_bm((0.012, 0.014, 0.05), 0.003), TIMBER_DARK, trs((0.045 + math.cos(a) * 0.06 - 0.06, 0.06, 0.16 + math.sin(a) * 0.12), (0, -math.degrees(a), 0)))
    bow.add(box_bm((0.003, 0.003, 0.22), 0.0), "ArtString", trs((0.0, 0.06, 0.05)))
    bow.add(box_bm((0.025, 0.06, 0.02), 0.004), PLAYER_CLOTH, trs((0.03, 0.03, 0.14)))  # forward arm
    bow.build(o, (Vector(at) + Vector((0.02, 0.0, 0.15))))
    return o


def tower():
    """
    Archer Tower, height 2.0: battered stone footing, four leaning timber posts with X-bracing and a mid ring beam, an
    overhanging plank platform with crenellated board parapet, a ladder, a shingle pyramid roof on corner posts with an iron
    finial, a torch, and an archer who aims and draws (runtime). Arrows leave at about 1.75.
    """
    r = root("ArcherTower")
    p = Part("Body")
    p.add(box_bm((0.72, 0.72, 0.32), 0.02, taper=0.86), STONE_DARK, trs((0, 0, 0)))
    p.add(box_bm((0.64, 0.64, 0.04), 0.008), STONE, trs((0, 0, 0.3)))
    lo, hi = 0.25, 0.22
    corners = [(1, 1), (1, -1), (-1, 1), (-1, -1)]
    def post(z):  # the four posts lean inward from lo at the footing to hi under the platform
        return lo - (lo - hi) * (z - 0.32) / 1.18

    for cx, cy in corners:
        beam(p, (cx * lo, cy * lo, 0.32), (cx * hi, cy * hi, 1.5), 0.075, 0.075, TIMBER, 0.01)
    for side in range(4):
        rot = Matrix.Rotation(math.radians(90 * side), 3, "Z")
        for z0, z1 in ((0.38, 0.92), (0.92, 1.44)):
            for sgn in (1, -1):  # an X of braces on each face, just outside the posts
                a = rot @ Vector((-sgn * post(z0), -post(z0) - 0.04, z0))
                b = rot @ Vector((sgn * post(z1), -post(z1) - 0.04, z1))
                beam(p, a, b, 0.032, 0.026, TIMBER_DARK, 0.005)
        wz = post(0.92)
        p.add(box_bm((2 * wz + 0.1, 0.045, 0.045), 0.006), TIMBER_DARK, trs(rot @ Vector((0, -wz - 0.02, 0.9)), (0, 0, 90 * side)), grain=(1, 0, 0))
    # platform: joists, planks, parapet boards with gaps
    for k in (-1, 0, 1):
        p.add(box_bm((0.86, 0.05, 0.05), 0.006), TIMBER_DARK, trs((0, k * 0.22, 1.44)), grain=(1, 0, 0))
    p.add(box_bm((0.8, 0.8, 0.045), 0.008), TIMBER, trs((0, 0, 1.49)), grain=(0, 1, 0))
    for side in range(4):
        rot = Matrix.Rotation(math.radians(90 * side), 3, "Z")
        for k in (-1, 0, 1):
            c = rot @ Vector((k * 0.25, -0.385, 1.535))
            p.add(box_bm((0.18, 0.035, 0.16), 0.006), TIMBER, trs(c, (0, 0, 90 * side)), grain=(0, 0, 1))
        c = rot @ Vector((0, -0.39, 1.6))
        p.add(box_bm((0.8, 0.045, 0.03), 0.006), TIMBER_DARK, trs(c, (0, 0, 90 * side)), grain=(1, 0, 0))
    for cx, cy in corners:
        p.add(box_bm((0.05, 0.05, 0.3), 0.006), TIMBER_DARK, trs((cx * 0.36, cy * 0.36, 1.53)), grain=(0, 0, 1))
    p.add(pyramid_bm(0.98, 0.28), SHINGLE, trs((0, 0, 1.8)))
    p.add(box_bm((0.88, 0.88, 0.03), 0.008), TIMBER_DARK, trs((0, 0, 1.79)))
    p.add(cyl_bm(0.018, 0.12, 6, radius_top=0.004), IRON, trs((0, 0, 2.05)))
    # ladder up the east face
    for s in (-1, 1):
        p.add(box_bm((0.025, 0.025, 1.18), 0.004), TIMBER_DARK, trs((0.33, s * 0.07, 0.32), (0, -6, 0)), grain=(0, 0, 1))
    for k in range(7):
        z = 0.42 + k * 0.155
        p.add(box_bm((0.02, 0.16, 0.018), 0.003), TIMBER, trs((0.33 + (z - 0.32) * 0.105, 0, z)), grain=(0, 1, 0))
    tip = wall_torch(p, (0.37, -0.37, 1.58), (0.7, -0.7))
    p.build(r)
    empty("Torch", tip, r)
    archer(r, (0, 0.05, 1.515))
    export("ArtArcherTower")


# ---------------------------------------------------------------- Grunt

def grunt():
    """
    Hooded raider, ~0.56 tall before the runtime's 1.35 enemy scale, facing +Y. A part rig the runtime animates:
    Hips (bob/sway) > Torso > Head, ArmL, ArmR > Weapon; LegL/LegR swing from the hip. Dark leather, coral-brown tabard
    and hood, iron-studded club, two faint coral eye glints under the hood (threat colour, readable at night).
    """
    r = root("Grunt")
    hip_z = 0.2
    hips = Part("Hips")
    hips.add(box_bm((0.16, 0.1, 0.07), 0.015), ENEMY_LEATHER, trs((0, 0, hip_z - 0.03)))
    hips.add(box_bm((0.17, 0.11, 0.025), 0.006), TIMBER_DARK, trs((0, 0, hip_z + 0.025)))
    hips.add(box_bm((0.03, 0.012, 0.03), 0.003), IRON, trs((0, 0.058, hip_z + 0.022)))
    hips_o = hips.build(r, (0, 0, hip_z))

    torso = Part("Torso")
    torso.add(box_bm((0.18, 0.12, 0.16), 0.03, taper=1.12, taper_y=1.0), ENEMY_LEATHER, trs((0, 0, hip_z + 0.04)))
    torso.add(box_bm((0.13, 0.02, 0.21), 0.006, taper=0.85), GRUNT_CLOTH, trs((0, 0.066, hip_z - 0.02)), grain=(0, 0, 1))
    torso.add(box_bm((0.13, 0.02, 0.17), 0.006, taper=0.9), GRUNT_CLOTH, trs((0, -0.066, hip_z + 0.0)), grain=(0, 0, 1))
    for s in (-1, 1):
        torso.add(ico_bm(0.05, 1), ENEMY_LEATHER, trs((s * 0.1, 0, hip_z + 0.19), scale=(1.1, 1, 0.8)))
    torso.add(cyl_bm(0.115, 0.05, 8, radius_top=0.08), GRUNT_CLOTH, trs((0, -0.005, hip_z + 0.18)))  # hood cowl over the shoulders
    torso_o = torso.build(hips_o, (0, 0, hip_z + 0.02))

    head_z = hip_z + 0.22
    head = Part("Head")
    head.add(ico_bm(0.062, 1), ENEMY_LEATHER, trs((0, 0.008, head_z + 0.06)))
    head.add(cyl_bm(0.074, 0.11, 8, radius_top=0.02), GRUNT_CLOTH, trs((0, -0.012, head_z + 0.02), (-12, 0, 0)))
    head.add(box_bm((0.09, 0.03, 0.06), 0.008), ENEMY_LEATHER, trs((0, 0.05, head_z + 0.035)))  # shadowed face opening
    for s in (-1, 1):
        head.add(box_bm((0.016, 0.006, 0.01), 0.0), ENEMY_EYES, trs((s * 0.022, 0.066, head_z + 0.062)))
    head.build(torso_o, (0, 0, head_z))

    shoulder_z = hip_z + 0.19
    for s, name in ((-1, "ArmL"), (1, "ArmR")):
        arm = Part(name)
        sx = s * 0.125
        arm.add(box_bm((0.05, 0.05, 0.13), 0.012), ENEMY_LEATHER, trs((sx, 0, shoulder_z - 0.13)))
        arm.add(box_bm((0.055, 0.055, 0.03), 0.008), TIMBER_DARK, trs((sx, 0, shoulder_z - 0.1)))  # bracer
        arm.add(ico_bm(0.03, 1), ENEMY_LEATHER, trs((sx, 0.005, shoulder_z - 0.145)))  # fist
        arm_o = arm.build(torso_o, (sx, 0, shoulder_z))
        if s == 1:
            weapon = Part("Weapon")
            grip = Vector((sx, 0.01, shoulder_z - 0.145))
            weapon.add(cyl_bm(0.014, 0.2, 6, radius_top=0.018), TIMBER_DARK, trs(grip + Vector((0, 0, -0.03))), grain=(0, 0, 1))
            weapon.add(cyl_bm(0.038, 0.08, 7, radius_top=0.03, bevel=0.006), TIMBER_DARK, trs(grip + Vector((0, 0, 0.15))), grain=(0, 0, 1))
            for k in range(4):
                a = math.radians(k * 90 + 20)
                weapon.add(cyl_bm(0.009, 0.03, 4, radius_top=0.0), IRON, trs(grip + Vector((math.cos(a) * 0.035, math.sin(a) * 0.035, 0.19)), (0, 90, math.degrees(a))))
            # held forward and up, ready to swing: the runtime rotates the arm, the club follows
            club = weapon.build(arm_o, grip)
            club.rotation_euler = (math.radians(-55), 0, 0)

    for s, name in ((-1, "LegL"), (1, "LegR")):
        leg = Part(name)
        lx = s * 0.05
        leg.add(box_bm((0.06, 0.065, 0.13), 0.012, taper=0.85), ENEMY_LEATHER, trs((lx, 0, hip_z - 0.16)))
        leg.add(box_bm((0.065, 0.1, 0.045), 0.012), TIMBER_DARK, trs((lx, 0.012, 0.0)))  # boot
        leg.add(box_bm((0.066, 0.07, 0.025), 0.005), GRUNT_CLOTH, trs((lx, 0, hip_z - 0.06)))  # wrap
        leg.build(hips_o, (lx, 0, hip_z - 0.02))
    export("ArtGrunt")


if __name__ == "__main__":
    keep()
    for s in WALL_SHAPES:
        wall(s)
    tower()
    grunt()
