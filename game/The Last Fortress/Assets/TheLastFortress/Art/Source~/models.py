"""
The Last Fortress art slice (plan 011): Keep, six connected palisade Wall pieces, Archer Tower with an animated archer.
The Grunt and the other figures moved to characters.py in plan 013. Run headless:

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

    def add_scaled(self, scale_matrix, geom_bm, material, matrix, grain=None):
        """add() with an extra transform applied after the part's own (plan 014: rescales authored pieces to real size)."""
        self.add(geom_bm, material, scale_matrix @ matrix, grain)

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
    """Iron bracket, a 0.6 m haft and a small flame at real size (plan 014); returns the flame-tip position for the Torch empty."""
    fx, fy = facing
    base = Vector(at)
    p.add(box_bm((0.012, 0.012, 0.018), 0.002), IRON, trs(base))
    haft = base + Vector((fx * 0.012, fy * 0.012, 0.0))
    p.add(cyl_bm(0.005, 0.072, 6), TIMBER_DARK, trs(haft, (-fy * 18, fx * 18, 0)))
    tip = haft + Vector((fx * 0.022, fy * 0.022, 0.068))
    p.add(cyl_bm(0.009, 0.012, 6), IRON, trs(tip - Vector((0, 0, 0.004))))
    p.add(cyl_bm(0.008, 0.028, 6, radius_top=0.0), FLAME, trs(tip + Vector((0, 0, 0.006))))
    return tip + Vector((0, 0, 0.022))


# ---------------------------------------------------------------- Keep

M = 0.125  # one metre in tiles (plan 014: a 1.75 m person is 0.22 tile, so a tile is about 8 m)


def keep():
    """
    Square limestone tower keep at real scale (plan 014): about 6 m of wall inside a 7 m battered plinth, 13 m to the
    walkway and about 17.5 m to the turret cones, so it is the tallest building on the board. Four corner turrets with
    crenellated walks and shingle cones, a machicolated parapet with 0.6 m merlons, a steep roof with finial and the
    settlement banner, slit windows by floor, and a raised entrance: a 2.4 m arched door at 1.6 m reached by a steep stone
    stair, with two torches. Everything stays inside the 1 x 1 cell.
    """
    r = root("Keep")
    p = Part("Body")
    top = 1.62  # wall head (13 m)
    p.add(box_bm((0.86, 0.86, 0.15), 0.01, taper=0.92), STONE_DARK, trs((0, 0, 0)))
    p.add(box_bm((0.74, 0.74, top - 0.14), 0.008), STONE, trs((0, 0, 0.14)))
    for z in (0.62, 1.12):  # string courses mark the floors
        p.add(box_bm((0.765, 0.765, 0.022), 0.004), STONE_DARK, trs((0, 0, z)))
    p.add(box_bm((0.8, 0.8, 0.04), 0.006, taper=1.04), STONE_DARK, trs((0, 0, top)))
    p.add(box_bm((0.82, 0.82, 0.045), 0.006), STONE, trs((0, 0, top + 0.04)))
    for side in range(4):
        a = math.radians(90 * side)
        for x in (-0.165, -0.055, 0.055, 0.165):
            pos = Matrix.Rotation(a, 3, "Z") @ Vector((x, -0.395, 0))
            p.add(box_bm((0.075, 0.03, 0.1), 0.006), STONE, trs((pos.x, pos.y, top + 0.085), (0, 0, 90 * side)))
            corbel = Matrix.Rotation(a, 3, "Z") @ Vector((x, -0.385, 0))
            p.add(box_bm((0.03, 0.035, 0.05), 0.004, taper=0.6), STONE_DARK, trs((corbel.x, corbel.y, top - 0.05), (0, 0, 90 * side)))
        for z in (0.42, 0.85, 1.32):  # slit windows by floor, two per face
            for x in (-0.12, 0.12):
                if side == 0 and z < 0.5:
                    continue  # the entrance is there
                w = Matrix.Rotation(a, 3, "Z") @ Vector((x, -0.372, 0))
                p.add(box_bm((0.02, 0.012, 0.12), 0.0), WINDOW, trs((w.x, w.y, z), (0, 0, 90 * side)))
                sill = Matrix.Rotation(a, 3, "Z") @ Vector((x, -0.375, 0))
                p.add(box_bm((0.045, 0.02, 0.012), 0.002), STONE_DARK, trs((sill.x, sill.y, z - 0.01), (0, 0, 90 * side)))
    for cx, cy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        c = Vector((cx * 0.345, cy * 0.345, 0))
        p.add(cyl_bm(0.145, 0.16, 12, radius_top=0.13), STONE_DARK, trs(c))
        p.add(cyl_bm(0.125, top + 0.14, 12, bevel=0.004), STONE, trs(c + Vector((0, 0, 0.15))))
        p.add(cyl_bm(0.14, 0.04, 12, bevel=0.005), STONE_DARK, trs(c + Vector((0, 0, top + 0.27))))
        for k in range(6):
            a = math.radians(k * 60 + 15)
            p.add(box_bm((0.06, 0.028, 0.08), 0.004), STONE, trs(c + Vector((math.cos(a) * 0.12, math.sin(a) * 0.12, top + 0.31)), (0, 0, math.degrees(a) + 90)))
        p.add(pyramid_bm(0.19, 0.3, sides=12, rot=15), SHINGLE, trs(c + Vector((0, 0, top + 0.31))))
        p.add(cyl_bm(0.008, 0.07, 6, radius_top=0.0), IRON, trs(c + Vector((0, 0, top + 0.6))))
        for (wx, wy) in ((cx, 0), (0, cy)):
            d = Vector((wx, wy, 0)).normalized()
            for z in (0.7, 1.2):
                p.add(box_bm((0.016, 0.01, 0.1), 0.0), WINDOW, trs(c + d * 0.125 + Vector((0, 0, z)), (0, 0, 90 if wx else 0)))
    # main roof: steep pyramid over the walkway, ridge finial with the settlement banner
    p.add(box_bm((0.56, 0.56, 0.05), 0.008), TIMBER_DARK, trs((0, 0, top + 0.06)))
    p.add(pyramid_bm(0.62, 0.42), SHINGLE, trs((0, 0, top + 0.1)))
    p.add(cyl_bm(0.006, 0.26, 6), IRON, trs((0, 0, top + 0.5)))
    p.add(box_bm((0.1, 0.006, 0.07), 0.002), BANNER, trs((0.053, 0, top + 0.68)))
    # raised entrance on the south (-Y) face: landing at 1.6 m, a steep stone stair down to the east, arched studded door
    sill = 0.2
    dx = -0.1
    p.add(box_bm((0.2, 0.1, sill), 0.006), STONE_DARK, trs((dx, -0.42, 0)))
    for k in range(8):  # 0.2 m rises, 0.2 m treads
        h = sill * (8 - k) / 9
        p.add(box_bm((0.026, 0.1, h), 0.003), STONE_DARK, trs((dx + 0.113 + k * 0.025, -0.42, 0)))
    p.add(box_bm((0.19, 0.03, 0.34), 0.006), STONE_DARK, trs((dx, -0.375, sill)))
    p.add(cyl_bm(0.095, 0.03, 16), STONE_DARK, trs((dx, -0.36, sill + 0.3), (90, 0, 0)))
    p.add(box_bm((0.14, 0.02, 0.27), 0.003), DOOR, trs((dx, -0.39, sill)), grain=(0, 0, 1))
    p.add(cyl_bm(0.07, 0.02, 16), DOOR, trs((dx, -0.38, sill + 0.27), (90, 0, 0)))
    for z in (0.08, 0.2):
        p.add(box_bm((0.15, 0.008, 0.012), 0.001), IRON, trs((dx, -0.402, sill + z)))
    tips = [wall_torch(p, (dx + sx * 0.13, -0.375, sill + 0.24), (0, -1)) for sx in (-1, 1)]
    p.build(r)
    for i, t in enumerate(tips):
        empty(f"Torch.{i}", t, r)
    export("ArtKeep")


# ---------------------------------------------------------------- Walls

WALL_SHAPES = {  # canonical masks (WallTopology): N=1, E=2, S=4, W=8; facing +Y = north
    "Isolated": 0, "End": 1, "Straight": 1 | 4, "Corner": 1 | 2, "T": 1 | 2 | 8, "Cross": 15,
}
SPACING = 0.042  # logs about 0.3 m thick, set side by side (plan 014: real size)


def log_column(p, at, height, rng, radius=0.019):
    lean = (rng.uniform(-2, 2), rng.uniform(-2, 2), rng.uniform(0, 360))
    m = trs(at, lean)
    p.add(cyl_bm(radius, height, 6, radius_top=radius * 0.9), TIMBER if rng.random() < 0.6 else TIMBER_DARK, m, grain=(0, 0, 1))
    p.add(cyl_bm(radius * 0.9, 0.05, 6, radius_top=0.002, cap=True), TIMBER, m @ trs((0, 0, height)), grain=(0, 0, 1))


def wall(shape):
    """
    Palisade at real scale (plan 014): sharpened logs about 0.3 m thick and 3.6-4 m tall set side by side on a low stone
    sill, two binding rails on both faces, a 0.45 m joint post with an iron collar and cap. Each arm runs to the tile edge;
    log positions are spaced so neighbouring pieces continue at one spacing across the edge.
    """
    mask = WALL_SHAPES[shape]
    rng = random.Random(1000 + mask)
    r = root("Wall" + shape)
    p = Part("Body")
    dirs = [(1, (0, 1)), (2, (1, 0)), (4, (0, -1)), (8, (-1, 0))]
    p.add(box_bm((0.1, 0.1, 0.03), 0.006, taper=0.9), STONE_DARK, trs((0, 0, 0)))
    for bit, (dx, dy) in dirs:
        if not mask & bit:
            continue
        along = Vector((dx, dy, 0))
        for k in range(4):
            c = along * (0.04 + k * 0.115 + 0.0575)
            size = (0.12 if dx else 0.075, 0.075 if dx else 0.12, 0.022 + rng.uniform(0, 0.01))
            p.add(box_bm(size, 0.006, taper=0.88), STONE_DARK, trs(c, (0, 0, rng.uniform(-5, 5))))
        for k in range(1, 12):
            log_column(p, along * (0.021 + k * SPACING), rng.uniform(0.44, 0.5), rng)
        side = Vector((dy, dx, 0)) * 0.024
        for z in (0.14, 0.36):
            for s_ in (1, -1):
                c = along * 0.27 + side * s_ + Vector((0, 0, z))
                p.add(box_bm((0.46 if dx else 0.012, 0.012 if dx else 0.46, 0.016), 0.003), TIMBER_DARK, trs(c), grain=(dx, dy, 0))
    if mask == 0:
        for k in range(12):
            a = math.radians(k * 30)
            log_column(p, (math.cos(a) * 0.07, math.sin(a) * 0.07, 0), rng.uniform(0.42, 0.48), rng)
        for z in (0.14, 0.36):
            p.add(cyl_bm(0.092, 0.016, 16, cap=False), TIMBER_DARK, trs((0, 0, z)))
    p.add(cyl_bm(0.028, 0.53, 8, radius_top=0.026), TIMBER_DARK, trs((0, 0, 0.02)), grain=(0, 0, 1))
    p.add(cyl_bm(0.03, 0.02, 8), IRON, trs((0, 0, 0.42)))
    p.add(cyl_bm(0.027, 0.06, 8, radius_top=0.004), IRON, trs((0, 0, 0.55)))
    p.build(r)
    export("ArtWall" + shape)


# ---------------------------------------------------------------- Archer Tower

def archer(parent, at):
    """Player archer on the platform (pivot at the feet, facing +Y): Body turns to aim, Bow draws on each shot."""
    T = Matrix.Translation(at)  # authored at the feet, placed on the platform (it used to stand on the ground)
    body = Part("Archer")
    body.add_scaled(T, cyl_bm(0.045, 0.1, 8, radius_top=0.05), PLAYER_CLOTH, trs((0, 0, 0)))
    body.add_scaled(T, cyl_bm(0.05, 0.075, 8, radius_top=0.035), PLAYER_CLOTH, trs((0, 0, 0.1)))
    body.add_scaled(T, box_bm((0.075, 0.06, 0.015), 0.004), TIMBER_DARK, trs((0, 0, 0.1)))
    body.add_scaled(T, ico_bm(0.032, 1), "ArtSkin", trs((0, 0.004, 0.205)))
    body.add_scaled(T, cyl_bm(0.04, 0.05, 8, radius_top=0.0), PLAYER_CLOTH, trs((0, -0.006, 0.21)))
    body.add_scaled(T, box_bm((0.03, 0.025, 0.08), 0.004), TIMBER_DARK, trs((-0.03, -0.045, 0.1), (15, 0, 0)))  # quiver
    o = body.build(parent, at)
    bow = Part("Bow")
    for k in range(-2, 3):  # a bent stave: five short segments along an arc
        a = math.radians(k * 16)
        bow.add_scaled(T, box_bm((0.012, 0.014, 0.05), 0.003), TIMBER_DARK, trs((0.045 + math.cos(a) * 0.06 - 0.06, 0.06, 0.16 + math.sin(a) * 0.12), (0, -math.degrees(a), 0)))
    bow.add_scaled(T, box_bm((0.003, 0.003, 0.22), 0.0), "ArtString", trs((0.0, 0.06, 0.05)))
    bow.add_scaled(T, box_bm((0.025, 0.06, 0.02), 0.004), PLAYER_CLOTH, trs((0.03, 0.03, 0.14)))  # forward arm
    bow.build(o, (Vector(at) + Vector((0.02, 0.0, 0.15))))
    return o


def tower():
    """
    Archer Tower at real scale (plan 014), about 11 m: a 1 m battered stone footing, four 0.3 m leaning posts with
    X-bracing and a ring beam, a plank platform at 7 m with a 1.1 m board parapet, a ladder with 0.3 m rungs, a shingle
    pyramid roof on corner posts, a torch, and the archer who aims and draws (runtime). Arrows leave at about 1.0.
    """
    r = root("ArcherTower")
    p = Part("Body")
    p.add(box_bm((0.72, 0.72, 0.125), 0.012, taper=0.9), STONE_DARK, trs((0, 0, 0)))
    p.add(box_bm((0.66, 0.66, 0.02), 0.005), STONE, trs((0, 0, 0.12)))
    lo, hi, z0, z1 = 0.28, 0.25, 0.14, 0.86
    corners = [(1, 1), (1, -1), (-1, 1), (-1, -1)]

    def post(z):  # the posts lean inward from lo at the footing to hi under the platform
        return lo - (lo - hi) * (z - z0) / (z1 - z0)

    for cx, cy in corners:
        beam(p, (cx * lo, cy * lo, z0), (cx * hi, cy * hi, z1), 0.038, 0.038, TIMBER, 0.005)
    for side in range(4):
        rot = Matrix.Rotation(math.radians(90 * side), 3, "Z")
        for a0, a1 in ((z0 + 0.02, 0.5), (0.5, z1 - 0.02)):
            for sgn in (1, -1):
                a = rot @ Vector((-sgn * post(a0), -post(a0) - 0.022, a0))
                b = rot @ Vector((sgn * post(a1), -post(a1) - 0.022, a1))
                beam(p, a, b, 0.018, 0.015, TIMBER_DARK, 0.003)
        w = post(0.5)
        p.add(box_bm((2 * w + 0.05, 0.022, 0.022), 0.003), TIMBER_DARK, trs(rot @ Vector((0, -w - 0.012, 0.49)), (0, 0, 90 * side)), grain=(1, 0, 0))
    for k in (-1, 0, 1):
        p.add(box_bm((0.72, 0.03, 0.03), 0.004), TIMBER_DARK, trs((0, k * 0.22, z1 - 0.03)), grain=(1, 0, 0))
    p.add(box_bm((0.7, 0.7, 0.025), 0.005), TIMBER, trs((0, 0, z1)), grain=(0, 1, 0))
    deck = z1 + 0.025
    for side in range(4):
        rot = Matrix.Rotation(math.radians(90 * side), 3, "Z")
        for k in range(-2, 3):
            c = rot @ Vector((k * 0.13, -0.335, deck))
            p.add(box_bm((0.115, 0.018, 0.13), 0.003), TIMBER, trs(c, (0, 0, 90 * side)), grain=(0, 0, 1))
        c = rot @ Vector((0, -0.335, deck + 0.13))
        p.add(box_bm((0.7, 0.024, 0.018), 0.003), TIMBER_DARK, trs(c, (0, 0, 90 * side)), grain=(1, 0, 0))
    for cx, cy in corners:
        p.add(box_bm((0.03, 0.03, 0.29), 0.004), TIMBER_DARK, trs((cx * 0.32, cy * 0.32, deck)), grain=(0, 0, 1))
    p.add(box_bm((0.78, 0.78, 0.02), 0.005), TIMBER_DARK, trs((0, 0, deck + 0.285)))
    p.add(pyramid_bm(0.86, 0.24), SHINGLE, trs((0, 0, deck + 0.3)))
    p.add(cyl_bm(0.008, 0.06, 6, radius_top=0.002), IRON, trs((0, 0, deck + 0.53)))
    # ladder up the east face: 0.45 m wide, rungs every 0.3 m
    for s_ in (-1, 1):
        beam(p, (0.31, s_ * 0.028, z0), (0.335, s_ * 0.028, deck), 0.008, 0.008, TIMBER_DARK, 0.0)
    k = 0
    while z0 + 0.03 + k * 0.0375 < deck - 0.01:
        z = z0 + 0.03 + k * 0.0375
        p.add(box_bm((0.006, 0.056, 0.006), 0.0), TIMBER, trs((0.31 + (z - z0) / (deck - z0) * 0.025, 0, z)), grain=(0, 1, 0))
        k += 1
    tip = wall_torch(p, (0.335, -0.335, deck + 0.04), (0.7, -0.7))
    p.build(r)
    empty("Torch", tip, r)
    archer(r, (0, 0.05, deck))
    export("ArtArcherTower")


if __name__ == "__main__":
    keep()
    for s in WALL_SHAPES:
        wall(s)
    tower()  # the Grunt and every other figure: characters.py (plan 013)
