"""Bakes and exports the Wave 1 props built by wave1_props.py."""
import bpy, importlib, os, time
import props_lib as P
importlib.reload(P)

MARKER = os.path.join(os.path.dirname(P.__file__), "export_wave1.done")
if os.path.exists(MARKER):
    os.remove(MARKER)
t0 = time.time()
OY = 6.0
report = {}


def objects(collection, prefix, exclude=()):
    return [o for o in bpy.data.collections[collection].objects
            if o.name.startswith(prefix) and not any(o.name.startswith(e) for e in exclude)]


def floor_under(x, y, exp):
    bpy.ops.mesh.primitive_plane_add(size=4, location=(x, y, 0))
    f = bpy.context.active_object
    for c in list(f.users_collection): c.objects.unlink(f)
    exp.objects.link(f)
    return f


def run(filename, parts, pivots, origin, bake=None):
    exp = bpy.data.collections.new("Export"); bpy.context.scene.collection.children.link(exp)
    made = P.join_parts(parts, pivots, exp)
    if bake:
        prefix, names, floor_xy, size = bake
        floor = floor_under(*floor_xy, exp)
        targets = [made[n] for n in names]
        P.bake(targets, prefix, occluders=[o for k, o in made.items() if k not in names] + [floor], size=size, samples=32)
        bpy.data.objects.remove(floor, do_unlink=True)
        mat = bpy.data.materials.get(prefix.capitalize() + "_Baked") or bpy.data.materials.new(prefix.capitalize() + "_Baked")
        for o in targets:
            P.single_material(o, mat)
    P.export(filename, made, origin)
    report[filename] = {k: P.triangles([v]) for k, v in made.items()}
    P.cleanup(exp)


TOP = 0.91
run("Workbench.fbx",
    {"Workbench": objects("Workbench", "Bench"), "Toolbox": objects("Workbench", "Toolbox")},
    {"Workbench": (0, OY, 0), "Toolbox": (0.45, OY + 0.05, TOP)}, (0, OY, 0),
    bake=("workbench", ["Workbench", "Toolbox"], (0, OY), 1024))

CX = 2.0
cart = {"Headset Cart": objects("Headset Cart", "Cart")}
for i in range(4):
    cart[f"Bay Light {i + 1}"] = objects("Headset Cart", f"Bay Light {i + 1}")
run("HeadsetCart.fbx", cart, {k: (CX, OY, 0) for k in cart}, (CX, OY, 0),
    bake=("cart", ["Headset Cart"], (CX, OY), 1024))

HX = 3.4
run("Headset.fbx",
    {"Headset": objects("Headset", "Headset", exclude=("Headset Status Light",)), "Headset Status Light": objects("Headset", "Headset Status Light")},
    {"Headset": (HX, OY, 0), "Headset Status Light": (HX, OY, 0)}, (HX, OY, 0))

BX, BZ = 4.2, 1.0
run("Badge.fbx",
    {"Badge Card": objects("Badge", "Badge Card"), "Badge Stripe": objects("Badge", "Badge Stripe"),
     "Badge Holder": objects("Badge", "Badge Holder") + objects("Badge", "Badge Clip") + objects("Badge", "Badge Lanyard")},
    {k: (BX, OY, BZ) for k in ("Badge Card", "Badge Stripe", "Badge Holder")}, (BX, OY, BZ))

LX, LZ = 4.8, 1.0
run("Padlock.fbx",
    {"Padlock Body": objects("Padlock", "Padlock Body") + objects("Padlock", "Padlock Face"),
     "Padlock Shackle": objects("Padlock", "Padlock Shackle"),
     "Padlock Tag": objects("Padlock", "Padlock Tag") + objects("Padlock", "Padlock Cable Tie")},
    {k: (LX, OY, LZ) for k in ("Padlock Body", "Padlock Shackle", "Padlock Tag")}, (LX, OY, LZ))

bpy.ops.wm.save_mainfile()
with open(MARKER, "w") as f:
    f.write(f"{report} in {round(time.time() - t0, 1)}s\n")
