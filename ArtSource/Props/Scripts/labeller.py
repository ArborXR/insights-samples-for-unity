"""Builds the post-mounted print-and-apply labeller (Labeller.fbx) for sample 09.

Unity-local space: origin at the centre of the post's floor plate; the applicator reaches forward (-z) 0.45 m to
sit over the belt. The tamp pad is its own object (it moves down onto the carton), and so is the clear window.
"""
import bpy, bmesh, math, importlib, os, time
from mathutils import Vector
import props_lib as P
importlib.reload(P)

PARK_Y = 18.0
OX = 23.0


def lin(c): return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def mat(name, c, rough, metal=0.0): return P.material(name, lin(c), rough, metal)


col = P.collection("Labeller")


def b(u): return Vector((OX + u[0], PARK_Y + u[2], u[1]))
def box(name, c, s, m, bevel=0.003, segs=2): return P.box(col, name, b(c), (s[0], s[2], s[1]), m, bevel, segs)
def cyl(name, c, r, d, axis, m, seg=24, bevel=0.0, r2=None):
    return P.cyl(col, name, b(c), r, d, {"x": "x", "y": "z", "z": "y"}[axis], m, seg, bevel, r2)


STAND = mat("Labeller_Stand", (0.30, 0.32, 0.34), 0.45, 0.2)
HOUSING = mat("Labeller_Housing", (0.24, 0.25, 0.27), 0.45)
DOOR = mat("Labeller_Door", (0.92, 0.93, 0.93), 0.4)
CHROME = mat("Labeller_Chrome", (0.82, 0.83, 0.85), 0.2, 1.0)
BLACK = mat("Labeller_Black", (0.06, 0.06, 0.07), 0.55)
YELLOW = mat("Labeller_Yellow", (0.96, 0.78, 0.06), 0.45)
SCREEN = mat("Labeller_Screen", (0.55, 0.62, 0.68), 0.15)
ROLL = mat("Labeller_Roll", (0.96, 0.96, 0.94), 0.5)
PAD = mat("Labeller_Pad", (0.55, 0.36, 0.22), 0.6)
GLASS = mat("Labeller_Window", (0.85, 0.9, 0.95), 0.05)
BRASS = mat("Labeller_Brass", (0.8, 0.62, 0.3), 0.3, 1.0)
RED = mat("Labeller_Red", (0.78, 0.12, 0.1), 0.4)

# Floor stand.
box("Stand Base", (0, 0.008, 0), (0.32, 0.016, 0.32), STAND, bevel=0.003)
for dx in (-0.12, 0.12):
    for dz in (-0.12, 0.12):
        cyl("Stand Bolt", (dx, 0.022, dz), 0.012, 0.012, "y", CHROME, seg=6, bevel=0.001)
for sx, sz in ((1, 0), (-1, 0), (0, 1), (0, -1)):
    box("Stand Gusset", (sx * 0.07, 0.07, sz * 0.07), (0.04 if sx else 0.008, 0.11, 0.008 if sx else 0.04), STAND, bevel=0.001)
box("Stand Post", (0, 0.53, 0), (0.1, 1.06, 0.1), STAND, bevel=0.005)
box("Stand Clamp", (0, 0.98, 0), (0.14, 0.12, 0.14), STAND, bevel=0.008)
cyl("Stand Clamp Knob", (0.09, 0.98, 0), 0.025, 0.03, "x", BLACK, seg=8, bevel=0.004)
# Air regulator and filter bowl on the post.
box("Stand Regulator", (0, 0.42, -0.065), (0.05, 0.05, 0.03), BLACK, bevel=0.004)
cyl("Stand Regulator Knob", (0, 0.455, -0.065), 0.015, 0.02, "y", RED, seg=16, bevel=0.003)
cyl("Stand Filter Bowl", (0, 0.37, -0.065), 0.016, 0.05, "y", GLASS, seg=16, r2=0.012)

# Print engine on the post: dark case, white side door with a clear window onto the label roll, small screen.
HC, HS = (0.02, 1.24, 0.03), (0.4, 0.36, 0.42)
box("Engine Case", HC, HS, HOUSING, bevel=0.01)
box("Engine Door", (HC[0] + HS[0] / 2 + 0.004, HC[1], HC[2]), (0.008, HS[1] - 0.03, HS[2] - 0.03), DOOR, bevel=0.004)
cyl("Engine Roll", (HC[0] + 0.1, HC[1] + 0.01, HC[2] + 0.04), 0.11, 0.1, "x", ROLL, seg=40, bevel=0.003)
cyl("Engine Roll Core", (HC[0] + 0.155, HC[1] + 0.01, HC[2] + 0.04), 0.03, 0.006, "x", CHROME, seg=24)
cyl("Engine Ribbon", (HC[0] + 0.1, HC[1] - 0.1, HC[2] - 0.12), 0.035, 0.1, "x", BLACK, seg=24, bevel=0.002)
box("Engine Handle", (HC[0] + HS[0] / 2 + 0.012, HC[1], HC[2] - 0.15), (0.01, 0.08, 0.015), BLACK, bevel=0.003)
box("Engine Screen Bezel", (HC[0] - 0.1, HC[1] + 0.1, HC[2] - HS[2] / 2 - 0.003), (0.13, 0.09, 0.006), BLACK, bevel=0.003)
box("Engine Screen", (HC[0] - 0.1, HC[1] + 0.1, HC[2] - HS[2] / 2 - 0.0065), (0.11, 0.07, 0.002), SCREEN, bevel=0)
# Applicator arm forward over the belt, with the pneumatic cylinder above the tamp.
TZ = -0.45
box("Applicator Arm", (0, 1.12, (HC[2] - HS[2] / 2 + TZ - 0.08) / 2), (0.16, 0.04, abs(TZ - 0.08 - (HC[2] - HS[2] / 2))), STAND, bevel=0.004)
box("Applicator Head", (0, 1.18, TZ), (0.18, 0.08, 0.16), HOUSING, bevel=0.006)
cyl("Applicator Cylinder", (0, 1.28, TZ), 0.03, 0.14, "y", CHROME, seg=24, bevel=0.003)
cyl("Applicator Cylinder Cap", (0, 1.355, TZ), 0.034, 0.016, "y", BLACK, seg=24, bevel=0.003)
for dx in (-0.025, 0.025):
    P.cyl(col, "Applicator Air Line", b((dx, 1.33, TZ + 0.035)), 0.004, 0.16, "z", BLACK, seg=8)
cyl("Applicator Fitting", (0.04, 1.3, TZ), 0.008, 0.02, "x", BRASS, seg=8)
# Hazard-striped guard around the tamp: yellow and black segments on the rails, and four corner posts.
GX, GZ0, GZ1, GY = 0.11, TZ - 0.1, TZ + 0.1, 1.11
n = 9
for i in range(n):
    m = YELLOW if i % 2 == 0 else BLACK
    w = 2 * GX / n
    box("Guard Rail", (-GX + w * (i + 0.5), GY, GZ0), (w, 0.022, 0.014), m, bevel=0)
    d = (GZ1 - GZ0) / n
    box("Guard Rail", (-GX, GY, GZ0 + d * (i + 0.5)), (0.014, 0.022, d), m, bevel=0)
    box("Guard Rail", (GX, GY, GZ0 + d * (i + 0.5)), (0.014, 0.022, d), m, bevel=0)
for x in (-GX, GX):
    for z in (GZ0, GZ1):
        box("Guard Post", (x, (1.02 + GY) / 2, z), (0.014, GY - 1.02, 0.014), BLACK, bevel=0.001)
# Tamp pad: rod and pad, resting with its face at y 1.06; the station slides it down onto the carton.
cyl("Tamp Rod", (0, 1.11, TZ), 0.009, 0.1, "y", CHROME, seg=12)
box("Tamp Plate", (0, 1.07, TZ), (0.13, 0.012, 0.11), STAND, bevel=0.002)
box("Tamp Face", (0, 1.062, TZ), (0.12, 0.006, 0.1), PAD, bevel=0.001)
# Clear window in the door (its own object for a transparent material).
box("Window", (HC[0] + HS[0] / 2 + 0.0085, HC[1], HC[2] + 0.02), (0.002, HS[1] - 0.1, HS[2] - 0.12), GLASS, bevel=0)

# ---- Bake and export ----
MARKER = os.path.join(os.path.dirname(P.__file__), "labeller.done")
if os.path.exists(MARKER):
    os.remove(MARKER)
if bpy.app.background:
    t0 = time.time()
    exp = bpy.data.collections.new("Export"); bpy.context.scene.collection.children.link(exp)
    origin = (OX, PARK_Y, 0)
    pad_pivot = tuple(b((0, 1.06, TZ)))
    parts = {"Labeller": [o for o in col.objects if not o.name.startswith(("Tamp", "Window", "Stand Filter"))],
             "Tamp Pad": [o for o in col.objects if o.name.startswith("Tamp")],
             "Labeller Glass": [o for o in col.objects if o.name.startswith(("Window", "Stand Filter"))]}
    made = P.join_parts(parts, {"Labeller": origin, "Tamp Pad": pad_pivot, "Labeller Glass": origin}, exp)
    bpy.ops.mesh.primitive_plane_add(size=3, location=origin)
    floor = bpy.context.active_object
    for c in list(floor.users_collection): c.objects.unlink(floor)
    exp.objects.link(floor)
    P.bake([made["Labeller"], made["Tamp Pad"]], "labeller", occluders=[made["Labeller Glass"], floor], size=1024, samples=32)
    bpy.data.objects.remove(floor, do_unlink=True)
    baked = bpy.data.materials.get("Labeller_Baked") or bpy.data.materials.new("Labeller_Baked")
    for k in ("Labeller", "Tamp Pad"): P.single_material(made[k], baked)
    P.export("Labeller.fbx", made, origin)
    report = {k: P.triangles([v]) for k, v in made.items()}
    P.cleanup(exp)
    bpy.ops.wm.save_mainfile()
    with open(MARKER, "w") as f:
        f.write(f"{report} in {round(time.time() - t0, 1)}s\n")
print(len(col.objects), "objects")
