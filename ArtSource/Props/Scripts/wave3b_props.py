"""Builds the second batch of Wave 3 props: OilCan (04), PumpVolute and GloveDispenser (05), IvPole (06), and Tote,
PickingCart and the six StockItem_* models (08).

Unity-local space, origin on the floor (or the surface a prop rests on) under its centre, front at -z.
"""
import bpy, bmesh, math, importlib, os, time
from mathutils import Vector, Matrix
import props_lib as P
importlib.reload(P)

PARK_Y = 30.0


def lin(c): return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def mat(name, c, rough, metal=0.0): return P.material(name, lin(c), rough, metal)


class Prop:
    """Authoring helpers in the prop's Unity-local coordinates."""

    def __init__(self, name, ox):
        self.col = P.collection(name)
        self.ox = ox

    def b(self, u): return Vector((self.ox + u[0], PARK_Y + u[2], u[1]))

    def box(self, name, c, s, m, bevel=0.003, segs=2):
        return P.box(self.col, name, self.b(c), (s[0], s[2], s[1]), m, bevel, segs)

    def cyl(self, name, c, r, d, axis, m, seg=24, bevel=0.0, r2=None):
        return P.cyl(self.col, name, self.b(c), r, d, {"x": "x", "y": "z", "z": "y"}[axis], m, seg, bevel, r2)

    def plate(self, name, outline, z0, z1, m, bevel=0.0):
        """Extrudes a closed Unity (x, y) outline along z."""
        return P.plate_xz(self.col, name, [(self.ox + x, y) for x, y in outline], PARK_Y + z0, PARK_Y + z1, m, bevel)

    def slab(self, name, outline, y0, y1, m, bevel=0.0):
        """Extrudes a closed Unity (x, z) outline along y, for things lying flat."""
        bm = bmesh.new()
        lo = [bm.verts.new(self.b((x, y0, z))) for x, z in outline]
        hi = [bm.verts.new(self.b((x, y1, z))) for x, z in outline]
        k = len(outline)
        for i in range(k):
            j = (i + 1) % k
            bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
        bm.faces.new(lo)
        bm.faces.new(list(reversed(hi)))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return P.finish(self.col, name, bm, m, bevel)

    def lathe(self, name, profile, m, centre, seg=32, scale=(1, 1)):
        """Spins an (r, y) profile around the vertical axis through centre; scale squashes it in x and z."""
        bm = bmesh.new()
        c = self.b(centre)
        verts = [bm.verts.new((c.x + r, c.y, c.z + h)) for r, h in profile]
        edges = [bm.edges.new((verts[i], verts[i + 1])) for i in range(len(verts) - 1)]
        bmesh.ops.spin(bm, geom=verts + edges, cent=c, axis=(0, 0, 1), angle=math.tau, steps=seg, use_duplicate=False)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        for v in bm.verts:
            v.co.x = c.x + (v.co.x - c.x) * scale[0]
            v.co.y = c.y + (v.co.y - c.y) * scale[1]
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return P.finish(self.col, name, bm, m)

    def tube(self, name, points, r, m, seg=12, closed=False):
        """A round tube along points; r is a radius or one radius per point."""
        pts = [self.b(p) for p in points]
        n = len(pts)
        radii = r if isinstance(r, (list, tuple)) else [r] * n
        tangents = [((pts[(i + 1) % n] - pts[i - 1]) if closed else (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)])).normalized() for i in range(n)]
        ref = Vector((0, 0, 1)) if abs(tangents[0].z) < 0.9 else Vector((1, 0, 0))
        normal = tangents[0].cross(ref).normalized()
        bm = bmesh.new()
        rings = []
        for i in range(n):
            if i:
                normal = (normal - tangents[i] * normal.dot(tangents[i])).normalized()
            binormal = tangents[i].cross(normal)
            rings.append([bm.verts.new(pts[i] + radii[i] * (math.cos(math.tau * k / seg) * normal + math.sin(math.tau * k / seg) * binormal)) for k in range(seg)])
        for i in range(n if closed else n - 1):
            a, c = rings[i], rings[(i + 1) % n]
            for k in range(seg):
                bm.faces.new((a[k], a[(k + 1) % seg], c[(k + 1) % seg], c[k]))
        if not closed:
            bm.faces.new(list(reversed(rings[0])))
            bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return P.finish(self.col, name, bm, m)

    def ring(self, name, c, r, tube_r, axis, m, seg=24, tube_seg=8):
        """A closed ring (torus) of radius r around axis through c."""
        def at(a):
            u, v = r * math.cos(a), r * math.sin(a)
            return {"y": (c[0] + u, c[1], c[2] + v), "x": (c[0], c[1] + u, c[2] + v), "z": (c[0] + u, c[1] + v, c[2])}[axis]
        return self.tube(name, [at(math.tau * k / seg) for k in range(seg)], tube_r, m, seg=tube_seg, closed=True)


BLUE_PAINT = mat("W3b_Blue_Paint", (0.26, 0.47, 0.72), 0.45, 0.2)
TIN = mat("W3b_Tin", (0.7, 0.71, 0.72), 0.3, 1.0)
LABEL = mat("W3b_Label", (0.82, 0.83, 0.84), 0.6)
OIL = mat("W3b_Oil", (0.04, 0.03, 0.02), 0.12)
CAST = mat("W3b_Cast_Iron", (0.36, 0.42, 0.46), 0.75, 0.2)
RUST = mat("W3b_Rust", (0.45, 0.25, 0.12), 0.85)
BORE = mat("W3b_Bore", (0.12, 0.1, 0.09), 0.8)
STAINLESS = mat("W3b_Stainless", (0.74, 0.75, 0.77), 0.3, 1.0)
CHROME = mat("W3b_Chrome", (0.84, 0.85, 0.87), 0.2, 1.0)
RUBBER = mat("W3b_Rubber", (0.08, 0.08, 0.085), 0.6)
GREY_PLASTIC = mat("W3b_Grey_Plastic", (0.42, 0.44, 0.46), 0.55)
DARK_STEEL = mat("W3b_Dark_Steel", (0.27, 0.29, 0.31), 0.45, 0.4)
CLEAR = mat("W3b_Clear", (0.86, 0.9, 0.93), 0.08)
YELLOW = mat("W3b_Yellow", (0.98, 0.78, 0.05), 0.4)
HARNESS = mat("W3b_Harness", (0.55, 0.56, 0.57), 0.6)
RED = mat("W3b_Red", (0.82, 0.12, 0.1), 0.35)
BLACK = mat("W3b_Black", (0.06, 0.06, 0.065), 0.5)
LENS = mat("W3b_Lens", (0.78, 0.82, 0.84), 0.1)
TEMPLE = mat("W3b_Temple", (0.55, 0.62, 0.7), 0.3)
HIVIS = mat("W3b_Hivis", (0.78, 0.95, 0.12), 0.5)
REFLECT = mat("W3b_Reflective", (0.62, 0.64, 0.64), 0.3, 0.3)
LEATHER = mat("W3b_Leather", (0.64, 0.5, 0.33), 0.75)
CANVAS = mat("W3b_Canvas", (0.55, 0.46, 0.34), 0.85)
CARD = mat("W3b_Card", (0.95, 0.95, 0.94), 0.6)
MASK = mat("W3b_Mask", (0.93, 0.92, 0.88), 0.85)

# ---- 04 Oil can, lying tipped over with its spout low ----
can = Prop("Oil Can", 60.0)
can.box("Can Body", (0, 0.1, 0), (0.3, 0.2, 0.22), BLUE_PAINT, bevel=0.02, segs=3)
can.box("Can Seam", (0.1, 0.1, 0), (0.012, 0.206, 0.226), BLUE_PAINT, bevel=0.004)
can.box("Can Rim", (-0.151, 0.1, 0), (0.004, 0.19, 0.21), TIN, bevel=0.002)
can.box("Can Label", (-0.02, 0.1, -0.111), (0.14, 0.13, 0.002), LABEL, bevel=0.0)
for z in (-0.04, 0.04):
    can.box("Can Handle Post", (0.0 + (0.06 if z > 0 else -0.06), 0.215, 0), (0.016, 0.03, 0.03), TIN, bevel=0.003)
can.box("Can Handle Grip", (0, 0.235, 0), (0.15, 0.014, 0.028), TIN, bevel=0.005)
can.cyl("Can Spout Collar", (-0.16, 0.05, -0.06), 0.022, 0.016, "x", TIN, seg=20, bevel=0.002)
can.cyl("Can Spout", (-0.185, 0.05, -0.06), 0.016, 0.04, "x", TIN, seg=20, r2=0.008)
can.cyl("Can Spout Lip", (-0.207, 0.05, -0.06), 0.009, 0.006, "x", BORE, seg=12)
can.cyl("Can Drip", (-0.215, 0.002, -0.06), 0.012, 0.002, "y", OIL, seg=12)

# ---- 05 Pump volute casing, greasy, resting on the washer grate ----
vol = Prop("Pump Volute", 62.0)
spiral = [(0.085 + 0.025 * t, 0.075, 0) for t in [k / 24 for k in range(25)]]
path = [(r * math.cos(math.tau * t * 0.92), y, r * math.sin(math.tau * t * 0.92)) for (r, y, _), t in zip(spiral, [k / 24 for k in range(25)])]
radii = [0.045 + 0.02 * k / 24 for k in range(25)]
# Continue the last turn into a straight discharge throat along +x.
end = Vector(path[-1])
path += [(end.x + 0.03, end.y, end.z - 0.01), (end.x + 0.08, end.y, end.z - 0.02), (end.x + 0.12, end.y, end.z - 0.025)]
radii += [0.06, 0.055, 0.05]
vol.tube("Volute Casing", path, radii, CAST, seg=18)
vol.cyl("Volute Core", (0, 0.075, 0), 0.09, 0.1, "y", CAST, seg=28, bevel=0.01)
vol.cyl("Volute Base Flange", (0, 0.012, 0), 0.115, 0.024, "y", CAST, seg=32, bevel=0.003)
vol.cyl("Volute Inlet Neck", (0, 0.135, 0), 0.06, 0.04, "y", CAST, seg=28)
vol.cyl("Volute Inlet Flange", (0, 0.162, 0), 0.085, 0.016, "y", CAST, seg=32, bevel=0.003)
vol.cyl("Volute Inlet Bore", (0, 0.1705, 0), 0.042, 0.002, "y", BORE, seg=24)
for k in range(8):
    a = math.tau * k / 8
    vol.cyl("Volute Inlet Bolt Hole", (0.068 * math.cos(a), 0.1705, 0.068 * math.sin(a)), 0.006, 0.002, "y", BORE, seg=8)
dx = end.x + 0.13
vol.cyl("Volute Outlet Flange", (dx, end.y, end.z - 0.026), 0.075, 0.016, "x", CAST, seg=32, bevel=0.003)
vol.cyl("Volute Outlet Bore", (dx + 0.0085, end.y, end.z - 0.026), 0.036, 0.002, "x", BORE, seg=24)
for k in range(8):
    a = math.tau * k / 8
    vol.cyl("Volute Outlet Bolt Hole", (dx + 0.0085, end.y + 0.06 * math.cos(a), end.z - 0.026 + 0.06 * math.sin(a)), 0.005, 0.002, "x", BORE, seg=8)
for x, z, r in ((0.03, -0.06, 0.03), (-0.05, -0.05, 0.022), (0.07, 0.04, 0.026), (-0.02, 0.07, 0.018)):
    vol.cyl("Volute Grease", (x, 0.127, z), r, 0.004, "y", OIL, seg=16)
for x, z in ((0.06, -0.08), (-0.08, 0.06), (0.0, -0.1)):
    vol.cyl("Volute Rust", (x, 0.025, z), 0.018, 0.003, "y", RUST, seg=12)

# ---- 05 Triple glove dispenser (origin at the middle compartment's box centre) ----
gd = Prop("Glove Dispenser", 64.0)
CW, CH, CD = 0.33, 0.19, 0.14  # compartment width, height, depth around a 0.28 x 0.16 x 0.12 box
x0, x1 = -1.5 * CW - 0.01, 1.5 * CW + 0.01
FLOOR = -0.084
gd.box("Dispenser Back", (0, FLOOR + CH / 2 + 0.02, CD / 2 + 0.002), (x1 - x0, CH + 0.04, 0.004), STAINLESS, bevel=0.002)
gd.box("Dispenser Floor", (0, FLOOR - 0.002, 0), (x1 - x0, 0.004, CD + 0.004), STAINLESS, bevel=0.001)
for x in (x0, -CW / 2, CW / 2, x1):
    gd.box("Dispenser Divider", (x, FLOOR + CH / 2, 0), (0.004, CH, CD), STAINLESS, bevel=0.001)
gd.box("Dispenser Lip", (0, FLOOR + 0.025, -CD / 2), (x1 - x0, 0.05, 0.004), STAINLESS, bevel=0.002)
for cx in (-CW, 0, CW):
    for x in (cx - 0.1, cx + 0.1):
        gd.cyl("Dispenser Retainer", (x, FLOOR + 0.11, -CD / 2 - 0.004), 0.0025, 0.12, "y", STAINLESS, seg=8)
    gd.cyl("Dispenser Retainer", (cx, FLOOR + 0.17, -CD / 2 - 0.004), 0.0025, 0.2, "x", STAINLESS, seg=8)
gd.cyl("Dispenser Top Rail", (0, FLOOR + CH, -CD / 2), 0.004, x1 - x0, "x", STAINLESS, seg=10)

# ---- 06 IV pole: five-castor base, telescoping pole, two-hook top with the saline bag ----
iv = Prop("IV Pole", 66.0)
PZ = 0.08
for k in range(5):
    a = math.tau * k / 5 + math.pi / 2
    tip = (0.3 * math.cos(a), 0.085, PZ + 0.3 * math.sin(a))
    iv.tube("Pole Leg", [(0, 0.1, PZ), tip], 0.016, STAINLESS, seg=10)
    iv.box("Pole Castor Fork", (tip[0], 0.06, tip[2]), (0.03, 0.04, 0.03), STAINLESS, bevel=0.003)
    iv.cyl("Pole Castor", (tip[0], 0.028, tip[2]), 0.028, 0.018, "x", RUBBER, seg=16, bevel=0.003)
iv.cyl("Pole Hub", (0, 0.1, PZ), 0.03, 0.05, "y", STAINLESS, seg=20, bevel=0.004)
iv.cyl("Pole Lower", (0, 0.58, PZ), 0.014, 0.95, "y", STAINLESS, seg=16)
iv.cyl("Pole Collar", (0, 1.06, PZ), 0.019, 0.04, "y", STAINLESS, seg=16, bevel=0.003)
iv.cyl("Pole Knob", (0.026, 1.06, PZ), 0.009, 0.02, "x", DARK_STEEL, seg=12)
iv.cyl("Pole Upper", (0, 1.53, PZ), 0.011, 0.94, "y", STAINLESS, seg=16)
for sx in (-1, 1):
    iv.tube("Pole Hook", [(0, 1.99, PZ), (sx * 0.08, 1.99, PZ), (sx * 0.13, 1.99, PZ), (sx * 0.145, 2.0, PZ), (sx * 0.15, 2.03, PZ)], 0.005, STAINLESS, seg=8)
BX, BY = -0.13, 1.86
iv.tube("Bag Port", [(BX, BY - 0.13, PZ), (BX, BY - 0.165, PZ)], 0.006, CLEAR, seg=8)
iv.cyl("Bag Drip Chamber", (BX, BY - 0.195, PZ), 0.009, 0.05, "y", CLEAR, seg=12)
iv.cyl("Bag Clamp", (BX, BY - 0.235, PZ), 0.008, 0.015, "y", TEMPLE, seg=10)
iv.tube("Bag Line", [(BX, BY - 0.24, PZ), (BX - 0.005, BY - 0.27, PZ - 0.02), (BX - 0.005, BY - 0.29, PZ - 0.06)], 0.0025, CLEAR, seg=6)
iv.tube("Bag Hanger", [(BX, BY + 0.12, PZ), (BX, BY + 0.14, PZ), (BX + 0.003, 2.0, PZ)], 0.003, CLEAR, seg=6)
iv.box("Saline Bag", (BX, BY, PZ), (0.15, 0.24, 0.035), CLEAR, bevel=0.015, segs=3)

# ---- 08 Tote: a grey stacking tote, origin at its base ----
tote = Prop("Tote", 68.0)
TW, TD, TH, WALL = 0.5, 0.37, 0.2, 0.012
tote.box("Tote Floor", (0, 0.008, 0), (TW - 0.03, 0.012, TD - 0.03), GREY_PLASTIC, bevel=0.003)
for z in (-TD / 2 + WALL / 2, TD / 2 - WALL / 2):
    tote.box("Tote Wall", (0, TH / 2, z), (TW, TH, WALL), GREY_PLASTIC, bevel=0.003)
for x in (-TW / 2 + WALL / 2, TW / 2 - WALL / 2):
    tote.box("Tote Wall", (x, TH / 2, 0), (WALL, TH, TD), GREY_PLASTIC, bevel=0.003)
    tote.box("Tote Handle", (x * 1.001, TH - 0.04, 0), (WALL + 0.002, 0.03, 0.1), BLACK, bevel=0.004)
for s in (-1, 1):
    tote.box("Tote Rim", (0, TH - 0.006, s * (TD / 2 + 0.004)), (TW + 0.012, 0.012, 0.012), GREY_PLASTIC, bevel=0.003)
    tote.box("Tote Rim", (s * (TW / 2 + 0.004), TH - 0.006, 0), (0.012, 0.012, TD + 0.012), GREY_PLASTIC, bevel=0.003)
    for k in (-0.17, -0.06, 0.06, 0.17):
        tote.box("Tote Rib", (k, TH / 2, s * (TD / 2 + 0.006)), (0.012, TH - 0.02, 0.008), GREY_PLASTIC, bevel=0.002)
    tote.box("Tote Band", (0, TH - 0.035, s * (TD / 2 + 0.005)), (TW, 0.01, 0.006), GREY_PLASTIC, bevel=0.002)

# ---- 08 Picking cart: two shelves, push handle on +x, four castors ----
cart = Prop("Picking Cart", 70.0)
CW2, CD2, TOP, LOW = 0.75, 0.45, 0.5, 0.18
for y in (TOP, LOW):
    cart.box("Cart Shelf", (0, y, 0), (CW2, 0.02, CD2), DARK_STEEL, bevel=0.003)
    for s in (-1, 1):
        cart.box("Cart Lip", (0, y + 0.02, s * (CD2 / 2 - 0.004)), (CW2, 0.02, 0.008), DARK_STEEL, bevel=0.002)
        cart.box("Cart Lip", (s * (CW2 / 2 - 0.004), y + 0.02, 0), (0.008, 0.02, CD2), DARK_STEEL, bevel=0.002)
for x in (-CW2 / 2 + 0.02, CW2 / 2 - 0.02):
    for z in (-CD2 / 2 + 0.02, CD2 / 2 - 0.02):
        cart.box("Cart Post", (x, (LOW + TOP) / 2 - 0.02, z), (0.025, TOP - LOW + 0.04, 0.025), DARK_STEEL, bevel=0.003)
        cart.box("Cart Castor Plate", (x, LOW - 0.02, z), (0.05, 0.008, 0.05), DARK_STEEL, bevel=0.002)
        cart.box("Cart Castor Fork", (x, LOW - 0.06, z), (0.02, 0.07, 0.035), CHROME, bevel=0.003)
        cart.cyl("Cart Castor", (x, 0.058, z), 0.058, 0.03, "z", RUBBER, seg=20, bevel=0.004)
        cart.cyl("Cart Castor Hub", (x, 0.058, z), 0.02, 0.034, "z", CHROME, seg=12)
HX = CW2 / 2 + 0.015
cart.tube("Cart Handle", [(HX, LOW, -CD2 / 2 + 0.03), (HX, 0.9, -CD2 / 2 + 0.03), (HX, 0.94, -CD2 / 2 + 0.07), (HX, 0.94, CD2 / 2 - 0.07),
                          (HX, 0.9, CD2 / 2 - 0.03), (HX, LOW, CD2 / 2 - 0.03)], 0.014, DARK_STEEL, seg=12)
for y in (LOW + 0.02, TOP + 0.02):
    for z in (-CD2 / 2 + 0.03, CD2 / 2 - 0.03):
        cart.box("Cart Handle Bracket", (HX - 0.012, y, z), (0.03, 0.04, 0.035), DARK_STEEL, bevel=0.003)

# ---- 08 Stock items, each resting on its origin ----
items = {}
# Safety glasses: wraparound lens, frame bar, temples folded back.
gl = Prop("Stock Safety Glasses", 72.0)
gl.plate("Glasses Lens", [(-0.075, 0.012), (0.075, 0.012), (0.072, 0.045), (0.02, 0.05), (0, 0.044), (-0.02, 0.05), (-0.072, 0.045)], -0.032, -0.028, LENS, bevel=0.001)
gl.box("Glasses Brow", (0, 0.047, -0.03), (0.15, 0.008, 0.008), TEMPLE, bevel=0.002)
for x in (-0.074, 0.074):
    gl.box("Glasses Temple", (x * 0.97, 0.04, 0.02), (0.006, 0.01, 0.1), TEMPLE, bevel=0.002)
items["StockItem_SafetyGlasses"] = gl
# Hi-vis vest folded in its bag.
vv = Prop("Stock Hivis Vest", 74.0)
vv.box("Vest Folded", (0, 0.012, 0), (0.25, 0.022, 0.19), HIVIS, bevel=0.006)
for z in (-0.05, 0.03):
    vv.box("Vest Tape", (0, 0.0235, z), (0.25, 0.002, 0.025), REFLECT, bevel=0.0)
vv.box("Vest Trim", (0, 0.0237, 0.07), (0.008, 0.002, 0.1), BLACK, bevel=0.0)
items["StockItem_HivisVest"] = vv
# Ear defenders: red cups on a black headband.
ed = Prop("Stock Ear Defenders", 76.0)
for sx in (-1, 1):
    ed.cyl("Defender Cup", (sx * 0.06, 0.05, 0), 0.042, 0.04, "x", RED, seg=24, bevel=0.012)
    ed.cyl("Defender Cushion", (sx * 0.035, 0.05, 0), 0.036, 0.014, "x", BLACK, seg=20, bevel=0.005)
    ed.box("Defender Slider", (sx * 0.07, 0.105, 0), (0.008, 0.05, 0.016), BLACK, bevel=0.002)
ed.tube("Defender Band", [(-0.07, 0.13, 0), (-0.065, 0.16, 0), (-0.04, 0.185, 0), (0, 0.193, 0), (0.04, 0.185, 0), (0.065, 0.16, 0), (0.07, 0.13, 0)], 0.008, BLACK, seg=10)
items["StockItem_EarDefenders"] = ed
# Hard hat, crown up, with its front peak.
hh = Prop("Stock Hard Hat", 78.0)
hh.lathe("Hat Shell", [(0, 0.15), (0.05, 0.146), (0.09, 0.125), (0.112, 0.085), (0.118, 0.04), (0.12, 0.02)], YELLOW, (0, 0, 0), scale=(0.9, 1.08))
hh.lathe("Hat Brim", [(0.118, 0.024), (0.14, 0.016), (0.142, 0.008), (0.118, 0.014)], YELLOW, (0, 0, 0), scale=(0.9, 1.08))
hh.box("Hat Peak", (0, 0.022, -0.13), (0.13, 0.008, 0.04), YELLOW, bevel=0.003)
hh.box("Hat Ridge", (0, 0.15, 0), (0.025, 0.014, 0.16), YELLOW, bevel=0.006)
hh.box("Hat Harness", (0, 0.04, 0), (0.17, 0.02, 0.2), HARNESS, bevel=0.006)
items["StockItem_HardHat"] = hh
# Work gloves: a pair lying flat, leather palms and canvas cuffs.
wg = Prop("Stock Work Gloves", 80.0)
glove = [(-0.045, -0.09), (0.045, -0.09), (0.048, 0.0), (0.062, 0.02), (0.07, 0.035), (0.06, 0.045), (0.042, 0.03), (0.042, 0.1), (0.03, 0.108),
         (0.022, 0.1), (0.02, 0.118), (0.008, 0.124), (-0.004, 0.118), (-0.006, 0.12), (-0.018, 0.124), (-0.028, 0.114), (-0.032, 0.1), (-0.044, 0.096), (-0.048, 0.03)]
for ox, oz, flip in ((-0.035, 0.0, 1), (0.045, 0.02, -1)):
    wg.slab("Glove Palm", [(ox + flip * x, oz + z) for x, z in glove], 0.0 if flip > 0 else 0.022, 0.022 if flip > 0 else 0.044, LEATHER, bevel=0.004)
    wg.box("Glove Cuff", (ox, 0.011 if flip > 0 else 0.033, oz - 0.12), (0.1, 0.022, 0.07), CANVAS, bevel=0.005)
items["StockItem_WorkGloves"] = wg
# Dust masks: a dispenser box with one mask showing from the top.
dm = Prop("Stock Dust Masks", 82.0)
dm.box("Masks Box", (0, 0.065, 0), (0.13, 0.13, 0.13), CARD, bevel=0.003)
dm.box("Masks Opening", (0, 0.07, -0.0655), (0.08, 0.06, 0.002), BLACK, bevel=0.0)
dm.lathe("Mask Cup", [(0, 0.05), (0.03, 0.045), (0.048, 0.025), (0.055, 0.0)], MASK, (0, 0.12, 0), scale=(1, 0.8))
dm.tube("Mask Strap", [(-0.05, 0.125, 0), (-0.03, 0.15, 0.01), (0.03, 0.15, 0.01), (0.05, 0.125, 0)], 0.002, YELLOW, seg=6)
items["StockItem_DustMasks"] = dm

# ---- Bake and export ----
MARKER = os.path.join(os.path.dirname(P.__file__), "wave3b_props.done")
if os.path.exists(MARKER):
    os.remove(MARKER)
if bpy.app.background:
    t0 = time.time()
    report = {}

    def objs(prop, *prefixes, exclude=()):
        return [o for o in prop.col.objects
                if (not prefixes or o.name.startswith(prefixes)) and not (exclude and o.name.startswith(exclude))]

    def run(jobs, prefix, unbaked=()):
        exp = bpy.data.collections.new("Export"); bpy.context.scene.collection.children.link(exp)
        made_all, files = {}, []
        for prop, filename, parts in jobs:
            made = P.join_parts({k: v[0] for k, v in parts.items()}, {k: tuple(prop.b(v[1])) for k, v in parts.items()}, exp)
            made_all.update(made)
            files.append((filename, made, tuple(prop.b((0, 0, 0)))))
        floors = []
        for _, _, origin in files:
            bpy.ops.mesh.primitive_plane_add(size=2, location=origin)
            f = bpy.context.active_object
            for c in list(f.users_collection): c.objects.unlink(f)
            exp.objects.link(f); floors.append(f)
        baked_parts = [o for k, o in made_all.items() if k not in unbaked]
        P.bake(baked_parts, prefix, occluders=floors + [made_all[k] for k in unbaked], size=1024, samples=32)
        for f in floors: bpy.data.objects.remove(f, do_unlink=True)
        name = prefix.capitalize() + "_Baked"
        baked = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        for o in baked_parts: P.single_material(o, baked)
        for k in unbaked:
            P.single_material(made_all[k], bpy.data.materials.get(k.replace(" ", "_")) or bpy.data.materials.new(k.replace(" ", "_")))
        for filename, made, origin in files:
            P.export(filename, made, origin)
            report[filename] = {k: P.triangles([v]) for k, v in made.items()}
        P.cleanup(exp)

    run([(can, "OilCan.fbx", {"Oil Can": (objs(can), (0, 0, 0))})], "oilcan")
    run([(vol, "PumpVolute.fbx", {"Pump Volute": (objs(vol), (0, 0, 0))})], "volute")
    run([(gd, "GloveDispenser.fbx", {"Glove Dispenser": (objs(gd), (0, 0, 0))})], "dispenser")
    run([(iv, "IvPole.fbx", {"IV Pole": (objs(iv, exclude=("Saline Bag",)), (0, 0, 0)), "Saline Bag": (objs(iv, "Saline Bag"), (BX, BY, PZ))})], "ivpole", unbaked=("Saline Bag",))
    run([(tote, "Tote.fbx", {"Tote": (objs(tote), (0, 0, 0))})], "tote")
    run([(cart, "PickingCart.fbx", {"Picking Cart": (objs(cart), (0, 0, 0))})], "pickcart")
    run([(p, f + ".fbx", {f.replace("StockItem_", "Stock "): (objs(p), (0, 0, 0))}) for f, p in items.items()], "stockitems")
    bpy.ops.wm.save_mainfile()
    with open(MARKER, "w") as f:
        f.write(f"{report} in {round(time.time() - t0, 1)}s\n")
print({p.col.name: len(p.col.objects) for p in [can, vol, gd, iv, tote, cart] + list(items.values())})
