"""Bakes and exports Conveyor.fbx from the Conveyor collection built by conveyor.py."""
import bpy, importlib, os, time
import props_lib as P
importlib.reload(P)

MARKER = os.path.join(os.path.dirname(P.__file__), "export_conveyor.done")
if os.path.exists(MARKER):
    os.remove(MARKER)
t0 = time.time()
HEAD = 1.3
col = bpy.data.collections["Conveyor"]
groups = {"Conveyor Frame": "Frame", "Conveyor Drive": "Drive", "Conveyor Belt": "Belt", "Conveyor Running Lamp": "Running Lamp"}
parts = {k: [o for o in col.objects if o.name.startswith(v)] for k, v in groups.items()}
pivots = {"Conveyor Frame": (0, 0, 0), "Conveyor Belt": (0, 0, 0), "Conveyor Drive": (HEAD, 0, 0), "Conveyor Running Lamp": (HEAD, 0, 0)}
exp = bpy.data.collections.new("Export")
bpy.context.scene.collection.children.link(exp)
made = P.join_parts(parts, pivots, exp)
P.bake([made["Conveyor Frame"], made["Conveyor Drive"]], "conveyor",
       occluders=[made["Conveyor Belt"], made["Conveyor Running Lamp"], bpy.data.objects["LookDev Floor"]], size=1024, samples=32)
baked = bpy.data.materials.get("Conveyor_Baked") or bpy.data.materials.new("Conveyor_Baked")
for k in ("Conveyor Frame", "Conveyor Drive"):
    P.single_material(made[k], baked)
P.export("Conveyor.fbx", made, (0, 0, 0))
report = {k: (len(v.data.materials), P.triangles([v])) for k, v in made.items()}
P.cleanup(exp)
bpy.ops.wm.save_mainfile()
with open(MARKER, "w") as f:
    f.write(f"{report} {round(time.time() - t0, 1)}s\n")
