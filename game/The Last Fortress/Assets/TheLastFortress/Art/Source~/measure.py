# Plan 014: measures every FBX in a folder (world bounds per model and per part) into a JSON file.
# blender -b --factory-startup -P measure.py -- <Art/Models folder> <out.json>
import bpy, sys, os, json
from mathutils import Vector
args = sys.argv[sys.argv.index("--") + 1:]
folder, out = args[0], args[1]
res = {}
for f in sorted(os.listdir(folder)):
    if not f.endswith(".fbx"): continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(folder, f))
    parts = {}
    allp = []
    for o in bpy.data.objects:
        if o.type != 'MESH': continue
        pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
        allp += pts
        lo = [min(p[i] for p in pts) for i in range(3)]; hi = [max(p[i] for p in pts) for i in range(3)]
        parts[o.name] = [round(v, 3) for v in lo + hi]
    lo = [min(p[i] for p in allp) for i in range(3)]; hi = [max(p[i] for p in allp) for i in range(3)]
    res[f[:-4]] = {"size": [round(hi[i] - lo[i], 3) for i in range(3)], "min": [round(v, 3) for v in lo], "max": [round(v, 3) for v in hi], "parts": parts}
json.dump(res, open(out, "w"), indent=1)
