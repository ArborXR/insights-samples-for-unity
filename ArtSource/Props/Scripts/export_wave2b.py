"""Bakes and exports the Wave 2 props built by wave2b_props.py (samples 06, 08, 13, 14)."""
import bpy, importlib, os, time
import props_lib as P
importlib.reload(P)

MARKER = os.path.join(os.path.dirname(P.__file__), "export_wave2b.done")
if os.path.exists(MARKER):
    os.remove(MARKER)
t0 = time.time()
PARK_Y = 14.0
report = {}
for c in ("Welding Table", "Shelving"):
    for o in bpy.data.collections[c].objects:
        o.hide_viewport = False


def objs(collection, prefix="", exclude=()):
    return [o for o in bpy.data.collections[collection].objects
            if o.name.startswith(prefix) and not any(o.name.startswith(e) for e in exclude)]


def run(filename, ox, parts, prefix, floor=True, size=1024):
    exp = bpy.data.collections.new("Export"); bpy.context.scene.collection.children.link(exp)
    origin = (ox, PARK_Y, 0)
    made = P.join_parts(parts, {k: origin for k in parts}, exp)
    occ = []
    if floor:
        bpy.ops.mesh.primitive_plane_add(size=4, location=origin)
        f = bpy.context.active_object
        for c in list(f.users_collection): c.objects.unlink(f)
        exp.objects.link(f)
        occ.append(f)
    P.bake(list(made.values()), prefix, occluders=occ, size=size, samples=32)
    for f in occ: bpy.data.objects.remove(f, do_unlink=True)
    m = bpy.data.materials.get(prefix.capitalize() + "_Baked") or bpy.data.materials.new(prefix.capitalize() + "_Baked")
    for o in made.values(): P.single_material(o, m)
    P.export(filename, made, origin)
    report[filename] = {k: P.triangles([v]) for k, v in made.items()}
    P.cleanup(exp)


run("HandWashStation.fbx", 14.0, {"Hand Wash Station": objs("Hand Wash Station")}, "handwash")
run("PickingBin.fbx", 15.4, {"Picking Bin": objs("Picking Bin")}, "bin", floor=False, size=512)
run("Shelving.fbx", 16.2, {"Shelving": objs("Shelving")}, "shelving")
run("MigTorch.fbx", 17.6, {"Torch Body": objs("MIG Torch", exclude=("Torch Handle", "Torch Trigger")),
                           "Torch Handle": objs("MIG Torch", "Torch Handle") + objs("MIG Torch", "Torch Trigger")},
    "torch", floor=False, size=512)
run("WeldingTable.fbx", 18.4, {"Welding Table": objs("Welding Table")}, "weldtable")
run("CheckoutCounter.fbx", 19.8, {"Checkout Counter": objs("Checkout Counter")}, "counter")
run("Gondola.fbx", 21.0, {"Gondola": objs("Gondola")}, "gondola")

bpy.ops.wm.save_mainfile()
with open(MARKER, "w") as f:
    f.write(f"{report} in {round(time.time() - t0, 1)}s\n")
