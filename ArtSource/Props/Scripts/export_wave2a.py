"""Bakes and exports the Wave 2 props built by wave2a_props.py (samples 03-06)."""
import bpy, importlib, os, time
import props_lib as P
importlib.reload(P)

MARKER = os.path.join(os.path.dirname(P.__file__), "export_wave2a.done")
if os.path.exists(MARKER):
    os.remove(MARKER)
t0 = time.time()
PARK_Y = 10.0
report = {}


def objs(collection, prefix, exclude=()):
    return [o for o in bpy.data.collections[collection].objects
            if o.name.startswith(prefix) and not any(o.name.startswith(e) for e in exclude)]


def floor_under(x, exp, size=3):
    bpy.ops.mesh.primitive_plane_add(size=size, location=(x, PARK_Y, 0))
    f = bpy.context.active_object
    for c in list(f.users_collection): c.objects.unlink(f)
    exp.objects.link(f)
    return f


def run(filename, ox, parts, pivots, bake=None, floor=True):
    """pivots are Unity-local points; bake = (prefix, names, size)."""
    exp = bpy.data.collections.new("Export"); bpy.context.scene.collection.children.link(exp)
    to_b = lambda u: (ox + u[0], PARK_Y + u[2], u[1])
    made = P.join_parts(parts, {k: to_b(v) for k, v in pivots.items()}, exp)
    if bake:
        prefix, names, size = bake
        occ = [o for k, o in made.items() if k not in names]
        f = floor_under(ox, exp) if floor else None
        P.bake([made[n] for n in names], prefix, occluders=occ + ([f] if f else []), size=size, samples=32)
        if f: bpy.data.objects.remove(f, do_unlink=True)
        m = bpy.data.materials.get(prefix.capitalize() + "_Baked") or bpy.data.materials.new(prefix.capitalize() + "_Baked")
        for n in names: P.single_material(made[n], m)
    P.export(filename, made, to_b((0, 0, 0)))
    report[filename] = {k: P.triangles([v]) for k, v in made.items()}
    P.cleanup(exp)


run("Disconnect.fbx", 7.0,
    {"Disconnect Body": objs("Disconnect", "Body"), "Disconnect Lever": objs("Disconnect", "Lever")},
    {"Disconnect Body": (0, 0, 0), "Disconnect Lever": (0.2, 1.23, -0.04)},
    bake=("disconnect", ["Disconnect Body"], 1024))

run("PushButtonStation.fbx", 7.8,
    {"Push Button Body": objs("Push Button Station", "Body"),
     "Stop Button": objs("Push Button Station", "Stop Button"), "Start Button": objs("Push Button Station", "Start Button")},
    {"Push Button Body": (0, 0, 0), "Stop Button": (0.06, 1.28, -0.09), "Start Button": (0.06, 1.10, -0.09)},
    bake=("pushbutton", ["Push Button Body"], 1024))

run("BenchGrinder.fbx", 8.6,
    {"Bench Grinder": objs("Bench Grinder", "Grinder"), "Eye Shields": objs("Bench Grinder", "Eye Shield")},
    {"Bench Grinder": (0, 0, 0), "Eye Shields": (0, 0, 0)},
    bake=("grinder", ["Bench Grinder"], 1024))

run("ExitDoor.fbx", 9.6,
    {"Exit Door": objs("Exit Door", "Door"), "Exit Sign Face": objs("Exit Door", "Exit Sign Face")},
    {"Exit Door": (0, 0, 0), "Exit Sign Face": (0, 0, 0)},
    bake=("exitdoor", ["Exit Door"], 1024))

run("GloveBox.fbx", 10.6,
    {"Glove Box": objs("Glove Box", "Glove Box Body"), "Glove Box Band": objs("Glove Box", "Glove Box Band"),
     "Glove": objs("Glove Box", "Glove C") + objs("Glove Box", "Glove F")},
    {"Glove Box": (0, 0, 0), "Glove Box Band": (0, 0, 0), "Glove": (0, 0, 0)})

run("PartsWasher.fbx", 11.4,
    {"Parts Washer": objs("Parts Washer", "Washer")}, {"Parts Washer": (0, 0, 0)},
    bake=("washer", ["Parts Washer"], 1024))

run("InfusionPump.fbx", 12.4,
    {"Infusion Pump": objs("Infusion Pump", "Pump", exclude=("Pump Tubing",)), "Pump Tubing": objs("Infusion Pump", "Pump Tubing")},
    {"Infusion Pump": (0, 0, 0), "Pump Tubing": (0, 0, 0)},
    bake=("pump", ["Infusion Pump"], 1024), floor=False)

bpy.ops.wm.save_mainfile()
with open(MARKER, "w") as f:
    f.write(f"{report} in {round(time.time() - t0, 1)}s\n")
