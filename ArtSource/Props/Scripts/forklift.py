"""Builds the electric counterbalance forklift (Forklift.fbx) used by samples 11 and 16.

Real size: about 1.1 m wide, 2.1 m to the top of the overhead guard, 3 m long with the forks. Forks point to -Y
(the station front, -Z in Unity). Origin: on the floor, midway between the axles. Parts that code moves or swaps
are separate objects with their own pivots.
"""
import bpy, bmesh, math, importlib
from mathutils import Vector, Matrix, Euler
import props_lib as P
importlib.reload(P)

OX = 5.0  # parked to the side in the .blend


def lin(c): return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


YELLOW = P.material("Forklift_Yellow", lin((0.96, 0.71, 0.08)), 0.42)
BLACK = P.material("Forklift_Black", lin((0.09, 0.09, 0.10)), 0.55)
GREY = P.material("Forklift_ForkGrey", lin((0.30, 0.31, 0.33)), 0.5, 0.6)
RUBBER = P.material("Forklift_Rubber", lin((0.05, 0.05, 0.05)), 0.9)
RIM = P.material("Forklift_Rim", lin((0.16, 0.16, 0.17)), 0.5, 0.3)
CHROME = P.material("Forklift_Chrome", lin((0.85, 0.86, 0.88)), 0.18, 1.0)
ORANGE = P.material("Forklift_Orange", lin((0.95, 0.42, 0.05)), 0.5)
GLASS = P.material("Forklift_LampGlass", lin((0.85, 0.88, 0.9)), 0.1)
LENS = bpy.data.materials["StackLight_Lens"]

col = P.collection("Forklift")


def at(x, y, z): return (OX + x, y, z)


def box(name, c, s, mat, bevel=0.01, segs=2):
    return P.box(col, name, at(*c), s, mat, bevel, segs)


def cyl(name, c, r, d, axis, mat, seg=24, bevel=0.0, r2=None):
    return P.cyl(col, name, at(*c), r, d, axis, mat, seg, bevel, r2)


def beam(name, a, b, w, d, mat, bevel=0.006):
    """A box beam from point a to point b with a w x d cross-section."""
    a, b = Vector(at(*a)), Vector(at(*b))
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    length = (b - a).length
    for v in bm.verts:
        v.co = Vector((v.co.x * w, v.co.y * d, v.co.z * length))
    rot = Vector((0, 0, 1)).rotation_difference((b - a).normalized()).to_matrix().to_4x4()
    bmesh.ops.transform(bm, verts=bm.verts, matrix=rot)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(a + b) / 2)
    return P.finish(col, name, bm, mat, bevel, 2)


def tilted_box(name, c, s, mat, rx, bevel=0.0):
    """A box rotated about X around its own centre."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * s[0], v.co.y * s[1], v.co.z * s[2]))
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(rx, 3, "X"))
    bmesh.ops.translate(bm, verts=bm.verts, vec=Vector(at(*c)))
    return P.finish(col, name, bm, mat, bevel, 2)


def boolean_cut(target, cutter):
    mod = target.modifiers.new("Cut", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.object = cutter
    mod.solver = "EXACT"
    cutter.hide_render = True
    cutter.hide_viewport = True


# ---- Chassis and bodywork ----
FRONT_AXLE, REAR_AXLE = -0.62, 0.66
FR, RR = 0.25, 0.21          # front and rear tyre radius
body = box("Body Chassis", (0, 0.02, 0.40), (0.92, 1.56, 0.46), YELLOW, bevel=0.04, segs=3)
cutters = []
for x in (-0.47, 0.47):
    for y, r in ((FRONT_AXLE, FR), (REAR_AXLE, RR)):
        c = cyl("Cutter Arch", (x, y, r), r + 0.04, 0.4, "x", BLACK, seg=32)
        boolean_cut(body, c)
        cutters.append(c)
box("Body Cowl", (0, -0.58, 0.86), (0.84, 0.36, 0.34), YELLOW, bevel=0.05, segs=3)
box("Body Dash", (0, -0.44, 1.02), (0.7, 0.14, 0.06), BLACK, bevel=0.015)
box("Body Floor Mat", (0, -0.24, 0.635), (0.66, 0.36, 0.012), BLACK, bevel=0.003)
box("Body Step", (0.47, -0.24, 0.34), (0.06, 0.32, 0.03), BLACK, bevel=0.004)
box("Body Side Panel", (0.462, 0.32, 0.45), (0.012, 0.5, 0.26), BLACK, bevel=0.006)
box("Body Side Panel", (-0.462, 0.32, 0.45), (0.012, 0.5, 0.26), BLACK, bevel=0.006)
cw = box("Body Counterweight", (0, 0.86, 0.55), (0.96, 0.32, 0.78), BLACK, bevel=0.09, segs=4)
for x in (-0.47, 0.47):
    c = cyl("Cutter Arch", (x, REAR_AXLE, RR), RR + 0.04, 0.4, "x", BLACK, seg=32)
    boolean_cut(cw, c)
    cutters.append(c)
box("Body Tail Light", (0.3, 1.025, 0.78), (0.1, 0.012, 0.05), ORANGE, bevel=0.004)
box("Body Tail Light", (-0.3, 1.025, 0.78), (0.1, 0.012, 0.05), ORANGE, bevel=0.004)

# ---- Overhead guard ----
GUARD_Z = 2.08
for x in (-0.42, 0.42):
    beam("Body Guard Post", (x, -0.66, 0.95), (x, -0.50, GUARD_Z), 0.05, 0.07, BLACK)
    beam("Body Guard Post", (x, 0.72, 0.75), (x, 0.74, GUARD_Z), 0.05, 0.07, BLACK)
    beam("Body Guard Rail", (x, -0.52, GUARD_Z), (x, 0.78, GUARD_Z), 0.06, 0.05, BLACK)
for y in (-0.52, 0.76):
    beam("Body Guard Rail", (-0.45, y, GUARD_Z), (0.45, y, GUARD_Z), 0.05, 0.06, BLACK)
for i in range(6):
    y = -0.36 + i * 0.2
    beam("Body Guard Slat", (-0.42, y, GUARD_Z + 0.01), (0.42, y, GUARD_Z + 0.01), 0.04, 0.025, BLACK, bevel=0.004)
cyl("Body Beacon Base", (0.28, 0.62, GUARD_Z + 0.045), 0.04, 0.03, "z", BLACK, seg=20, bevel=0.003)
cyl("Beacon Lens", (0.28, 0.62, GUARD_Z + 0.1), 0.035, 0.08, "z", LENS, seg=24, bevel=0.006, r2=0.03)
box("Body Work Light", (-0.42, -0.52, GUARD_Z - 0.06), (0.08, 0.07, 0.07), BLACK, bevel=0.008)
box("Body Work Light Glass", (-0.42, -0.556, GUARD_Z - 0.06), (0.06, 0.004, 0.05), GLASS, bevel=0.002)

# ---- Mast, cylinders, chains ----
MAST_Y = -0.92
for x in (-0.36, 0.36):
    box("Body Mast Outer", (x, MAST_Y, 1.06), (0.07, 0.13, 1.96), BLACK, bevel=0.008)
    box("Body Mast Inner", (x * 0.8, MAST_Y - 0.02, 1.1), (0.06, 0.1, 1.9), BLACK, bevel=0.008)
    beam("Body Tilt Cylinder", (x * 0.75, -0.62, 0.72), (x * 0.75, MAST_Y + 0.05, 0.86), 0.06, 0.06, BLACK, bevel=0.01)
    cyl("Body Tilt Rod", (x * 0.75, MAST_Y + 0.07, 0.86), 0.015, 0.1, "y", CHROME, seg=12)
    box("Body Chain", (x * 0.32, MAST_Y + 0.07, 1.25), (0.025, 0.012, 1.6), RIM, bevel=0)
for z in (0.12, 1.02, 2.02):
    box("Body Mast Tie", (0, MAST_Y + 0.03, z), (0.66, 0.06, 0.08), BLACK, bevel=0.008)
cyl("Body Lift Cylinder", (0, MAST_Y + 0.08, 0.95), 0.045, 1.6, "z", BLACK, seg=20, bevel=0.004)
cyl("Body Lift Rod", (0, MAST_Y + 0.08, 1.85), 0.025, 0.25, "z", CHROME, seg=16)

# ---- Carriage and forks (separate so they can lift) ----
CAR_Y = MAST_Y - 0.12
box("Carriage Plate", (0, CAR_Y, 0.3), (0.94, 0.06, 0.38), BLACK, bevel=0.01)
for x in (-0.44, -0.22, 0.0, 0.22, 0.44):
    box("Carriage Backrest Bar", (x, CAR_Y + 0.005, 0.85), (0.035, 0.035, 0.72), BLACK, bevel=0.005)
box("Carriage Backrest Top", (0, CAR_Y + 0.005, 1.2), (0.94, 0.045, 0.045), BLACK, bevel=0.006)
box("Carriage Backrest Mid", (0, CAR_Y + 0.005, 0.85), (0.94, 0.035, 0.035), BLACK, bevel=0.005)
for side, x in (("Left", -0.26), ("Right", 0.26)):
    box(f"Fork {side} Shank", (x, CAR_Y - 0.05, 0.3), (0.1, 0.045, 0.46), GREY, bevel=0.006)
    bm = bmesh.new()
    L, T, W = 1.07, 0.045, 0.1
    pts = [(0, 0), (0, T), (-L + 0.12, T), (-L, 0.012), (-L, 0)]  # (y offset, z) taper at the tip
    y0 = CAR_Y - 0.07
    a = [bm.verts.new(at(x - W / 2, y0 + py, 0.035 + pz)) for py, pz in pts]
    b = [bm.verts.new(at(x + W / 2, y0 + py, 0.035 + pz)) for py, pz in pts]
    for i in range(len(pts)):
        j = (i + 1) % len(pts)
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(a); bm.faces.new(list(reversed(b)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    P.finish(col, f"Fork {side} Tine", bm, GREY, bevel=0.004)

# ---- Operator: seat on the hinged battery hood, steering ----
HINGE = (0, -0.05, 0.78)
box("Hood Panel", (0, 0.33, 0.70), (0.84, 0.76, 0.16), YELLOW, bevel=0.04, segs=3)
box("Hood Seat Base", (0, 0.32, 0.81), (0.42, 0.4, 0.06), BLACK, bevel=0.015)
box("Hood Seat Cushion", (0, 0.3, 0.87), (0.48, 0.44, 0.09), BLACK, bevel=0.03, segs=3)
back = box("Hood Seat Back", (0, 0.52, 1.13), (0.46, 0.09, 0.46), BLACK, bevel=0.03, segs=3)
back.data.transform(Matrix.Translation(Vector(at(0, 0.52, 1.13))) @ Matrix.Rotation(math.radians(-12), 4, "X") @ Matrix.Translation(-Vector(at(0, 0.52, 1.13))))
box("Hood Belt", (0.25, 0.28, 0.9), (0.03, 0.06, 0.04), ORANGE, bevel=0.005)
box("Hood Belt Strap", (0.24, 0.42, 1.0), (0.012, 0.04, 0.3), ORANGE, bevel=0.002)
for x in (-0.27, 0.27):
    box("Hood Armrest", (x, 0.36, 0.98), (0.05, 0.3, 0.05), BLACK, bevel=0.01)

beam("Body Steering Column", (0, -0.46, 1.0), (0, -0.33, 1.24), 0.06, 0.06, BLACK, bevel=0.01)
SW = (0, -0.31, 1.27)
bm = bmesh.new()
segs, tube = 36, 10
for i in range(segs):
    a = 2 * math.pi * i / segs
    for j in range(tube):
        b = 2 * math.pi * j / tube
        r = 0.15 + 0.016 * math.cos(b)
        bm.verts.new((r * math.cos(a), r * math.sin(a), 0.016 * math.sin(b)))
bm.verts.ensure_lookup_table()
for i in range(segs):
    for j in range(tube):
        a = i * tube + j; b = i * tube + (j + 1) % tube
        c = ((i + 1) % segs) * tube + (j + 1) % tube; d = ((i + 1) % segs) * tube + j
        bm.faces.new((bm.verts[a], bm.verts[d], bm.verts[c], bm.verts[b]))
tilt = Matrix.Translation(Vector(at(*SW))) @ Matrix.Rotation(math.radians(-55), 4, "X")
bmesh.ops.transform(bm, verts=bm.verts, matrix=tilt)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
P.finish(col, "Steering Wheel Rim", bm, BLACK)
for a in (90, 210, 330):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * 0.14, v.co.y * 0.025, v.co.z * 0.012))
    bmesh.ops.translate(bm, verts=bm.verts, vec=Vector((0.07, 0, 0)))
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(a), 3, "Z"))
    bmesh.ops.transform(bm, verts=bm.verts, matrix=tilt)
    P.finish(col, "Steering Wheel Spoke", bm, BLACK, bevel=0.003)
bm = bmesh.new()
bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=0.04, radius2=0.036, depth=0.03)
bmesh.ops.transform(bm, verts=bm.verts, matrix=tilt @ Matrix.Translation((0, 0, 0.012)))
P.finish(col, "Horn Button", bm, RIM, bevel=0.004)

# ---- Wheels: each its own object at its axle; plus a front-left with a gouged tread ----
def wheel(name, x, y, r, width, damaged=False):
    side = 1 if x > 0 else -1
    parts = []
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=40, radius1=r, radius2=r, depth=width)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Euler((0, math.pi / 2, 0)).to_matrix())
    bmesh.ops.translate(bm, verts=bm.verts, vec=Vector(at(x, y, r)))
    tyre = P.finish(col, name + " Tyre", bm, RUBBER, bevel=0.03, segs=2, angle=30)
    parts.append(tyre)
    lugs = 18
    for i in range(lugs):
        a = 2 * math.pi * i / lugs
        if damaged and 0.4 < a < 1.3:
            continue
        for half in (-1, 1):
            bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
            for v in bm.verts: v.co = Vector((v.co.x * width * 0.42, v.co.y * 0.045, v.co.z * 0.014))
            bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(18 * half), 3, "Z"))
            bmesh.ops.translate(bm, verts=bm.verts, vec=Vector((half * width * 0.22, 0, r + 0.004)))
            bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(a, 3, "X"))
            bmesh.ops.translate(bm, verts=bm.verts, vec=Vector(at(x, y, r)))
            parts.append(P.finish(col, name + " Lug", bm, RUBBER))
    cyl(name + " Rim", (x + side * (width / 2 - 0.02), y, r), r * 0.56, 0.06, "x", RIM, seg=32, bevel=0.01)
    cyl(name + " Hub", (x + side * (width / 2 + 0.005), y, r), r * 0.22, 0.03, "x", BLACK, seg=20, bevel=0.006)
    for i in range(5):
        a = 2 * math.pi * i / 5
        cyl(name + " Nut", (x + side * (width / 2 + 0.016), y + math.cos(a) * r * 0.3, r + math.sin(a) * r * 0.3), 0.011, 0.012, "x", CHROME, seg=6)
    if damaged:
        # A torn chunk out of the tread, showing the paler casing.
        c = tilted_box("Cutter Gouge", (x, y - r * 0.58, r + r * 0.74), (width + 0.02, 0.11, 0.09), BLACK, math.radians(35))
        boolean_cut(tyre, c)
        cutters.append(c)
        tilted_box(name + " Casing", (x, y - r * 0.53, r + r * 0.69), (width * 0.8, 0.07, 0.004), RIM, math.radians(35))


wheel("Wheel FL", -0.45, FRONT_AXLE, FR, 0.2)
wheel("Wheel FR", 0.45, FRONT_AXLE, FR, 0.2)
wheel("Wheel RL", -0.44, REAR_AXLE, RR, 0.17)
wheel("Wheel RR", 0.44, REAR_AXLE, RR, 0.17)
wheel("Wheel FL Damaged", -0.45, FRONT_AXLE, FR, 0.2, damaged=True)
print(len(col.objects), "objects")
