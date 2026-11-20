"""Builds the first batch of Wave 3 props: Monitor and ReceptionCounter (01), PpeBoard (02), LockoutBoard and
RadioCharger (03), and ExtensionCord (04).

Unity-local space, origin on the floor (or desk surface for the monitor) under each prop's centre, front at -z.
"""
import bpy, bmesh, math, importlib, os, time
from mathutils import Vector, Matrix
import props_lib as P
importlib.reload(P)

PARK_Y = 26.0


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
        """Extrudes a closed Unity (x, y) outline along z from z0 to z1."""
        return P.plate_xz(self.col, name, [(self.ox + x, y) for x, y in outline], PARK_Y + z0, PARK_Y + z1, m, bevel)

    def lathe(self, name, profile, m, centre, facing_front=False, seg=32):
        """Spins an (r, h) profile around the vertical axis through centre, or around z with h toward the front (-z)."""
        bm = bmesh.new()
        c = self.b(centre)
        verts = [bm.verts.new((c.x + r, c.y, c.z + h)) for r, h in profile]
        edges = [bm.edges.new((verts[i], verts[i + 1])) for i in range(len(verts) - 1)]
        bmesh.ops.spin(bm, geom=verts + edges, cent=c, axis=(0, 0, 1), angle=math.tau, steps=seg, use_duplicate=False)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        if facing_front:
            bmesh.ops.rotate(bm, verts=bm.verts, cent=c, matrix=Matrix.Rotation(math.pi / 2, 3, "X"))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return P.finish(self.col, name, bm, m)

    def tube(self, name, points, r, m, seg=12, closed=False):
        pts = [self.b(p) for p in points]
        n = len(pts)
        tangents = [((pts[(i + 1) % n] - pts[i - 1]) if closed else (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)])).normalized() for i in range(n)]
        ref = Vector((0, 0, 1)) if abs(tangents[0].z) < 0.9 else Vector((1, 0, 0))
        normal = tangents[0].cross(ref).normalized()
        bm = bmesh.new()
        rings = []
        for i in range(n):
            if i:
                normal = (normal - tangents[i] * normal.dot(tangents[i])).normalized()
            binormal = tangents[i].cross(normal)
            rings.append([bm.verts.new(pts[i] + r * (math.cos(math.tau * k / seg) * normal + math.sin(math.tau * k / seg) * binormal)) for k in range(seg)])
        for i in range(n if closed else n - 1):
            a, c = rings[i], rings[(i + 1) % n]
            for k in range(seg):
                bm.faces.new((a[k], a[(k + 1) % seg], c[(k + 1) % seg], c[k]))
        if not closed:
            bm.faces.new(list(reversed(rings[0])))
            bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return P.finish(self.col, name, bm, m)


def smooth_path(points, steps=6):
    """Catmull-Rom through (x, y, z) points."""
    pts = [Vector(p) for p in points]
    out = []
    for i in range(len(pts) - 1):
        p0, p1, p2, p3 = pts[max(i - 1, 0)], pts[i], pts[i + 1], pts[min(i + 2, len(pts) - 1)]
        for s in range(steps):
            t = s / steps
            out.append(tuple(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3)))
    out.append(tuple(pts[-1]))
    return out


BLACK = mat("W3_Black", (0.05, 0.05, 0.055), 0.45)
SOFT_BLACK = mat("W3_Soft_Black", (0.09, 0.09, 0.1), 0.6)
SILVER = mat("W3_Silver", (0.72, 0.73, 0.75), 0.35, 0.9)
CHROME = mat("W3_Chrome", (0.84, 0.85, 0.87), 0.2, 1.0)
SCREEN = mat("W3_Screen", (0.06, 0.08, 0.12), 0.1)
PANEL = mat("W3_Panel", (0.84, 0.85, 0.86), 0.5)
WHITE = mat("W3_White", (0.95, 0.95, 0.95), 0.45)
WOOD = mat("W3_Wood", (0.86, 0.74, 0.55), 0.55)
NAVY = mat("W3_Navy", (0.12, 0.17, 0.42), 0.45)
RED = mat("W3_Red", (0.8, 0.1, 0.09), 0.4)
HIVIS = mat("W3_Hivis", (0.78, 0.95, 0.12), 0.6)
REFLECT = mat("W3_Reflective", (0.78, 0.8, 0.8), 0.3, 0.3)
LENS = mat("W3_Lens", (0.7, 0.74, 0.76), 0.1)
GLASS = mat("W3_Acrylic", (0.9, 0.93, 0.95), 0.05)
STEEL = mat("W3_Steel", (0.3, 0.32, 0.34), 0.45, 0.3)
LCD = mat("W3_Lcd", (0.55, 0.62, 0.55), 0.3)
GREEN_LED = mat("W3_Green_Led", (0.2, 0.9, 0.3), 0.3)
ORANGE = mat("W3_Orange", (0.95, 0.4, 0.08), 0.5)
COPPER = mat("W3_Copper", (0.85, 0.5, 0.25), 0.3, 1.0)
WIRE_WHITE = mat("W3_Wire_White", (0.9, 0.9, 0.88), 0.4)
WIRE_GREEN = mat("W3_Wire_Green", (0.1, 0.55, 0.25), 0.4)

# ---- 01 Monitor (origin on the desk surface under its base) ----
mon = Prop("Monitor", 40.0)
SC, SW, SH = 0.67, 1.0, 0.6  # screen centre height above the desk, housing width and height
mon.box("Monitor Base", (0, 0.007, 0.04), (0.3, 0.014, 0.22), SILVER, bevel=0.005)
mon.box("Monitor Neck", (0, SC / 2 - 0.05, 0.07), (0.075, SC - 0.06, 0.03), SILVER, bevel=0.008)
mon.box("Monitor Cable Hole", (0, SC / 2 - 0.12, 0.0545), (0.022, 0.07, 0.002), SOFT_BLACK, bevel=0.0)
mon.box("Monitor Housing", (0, SC, 0), (SW, SH, 0.035), BLACK, bevel=0.004)
mon.box("Monitor Back", (0, SC - 0.02, 0.03), (0.5, 0.32, 0.03), BLACK, bevel=0.01)
mon.box("Monitor Logo Strip", (0, SC - SH / 2 + 0.008, -0.018), (SW - 0.04, 0.012, 0.001), SOFT_BLACK, bevel=0.0)
mon.box("Screen", (0, SC + 0.006, -0.0185), (SW - 0.03, SH - 0.042, 0.001), SCREEN, bevel=0.0)

# ---- 01 Reception counter (origin on the floor, front run along x) ----
rec = Prop("Reception Counter", 42.0)
RW, RD, LEDGE = 1.9, 0.7, 1.1
rec.box("Counter Plinth", (0, 0.03, -RD / 2 + 0.06), (RW - 0.1, 0.06, 0.04), BLACK, bevel=0.002)
rec.box("Counter Front", (0, (0.06 + LEDGE - 0.015) / 2, -RD / 2 + 0.02), (RW - 0.06, LEDGE - 0.075, 0.03), PANEL, bevel=0.004)
rec.box("Counter Stripe", (0, 0.075, -RD / 2 + 0.004), (RW - 0.07, 0.016, 0.002), NAVY, bevel=0.0)
for x in (-RW / 2 + 0.02, RW / 2 - 0.02):
    rec.box("Counter Side", (x, (LEDGE - 0.015) / 2, 0), (0.04, LEDGE - 0.015, RD), PANEL, bevel=0.004)
rec.box("Counter Ledge", (0, LEDGE, -RD / 2 + 0.13), (RW + 0.04, 0.03, 0.3), WOOD, bevel=0.004)
rec.box("Counter Desk", (0, 0.75, 0.05), (RW - 0.08, 0.03, RD - 0.12), WHITE, bevel=0.003)
rec.box("Counter Modesty", (0, 0.4, -RD / 2 + 0.05), (RW - 0.08, 0.7, 0.01), PANEL, bevel=0.0)

# ---- 02 PPE board on a freestanding frame ----
ppe = Prop("PPE Board", 44.0)
BW, BH, BC = 1.3, 0.9, 1.45
for x in (-BW / 2 + 0.03, BW / 2 - 0.03):
    ppe.box("PPE Post", (x, 0.95, 0.03), (0.04, 1.9, 0.04), STEEL, bevel=0.003)
    ppe.box("PPE Foot", (x, 0.01, 0.03), (0.06, 0.02, 0.45), STEEL, bevel=0.003)
ppe.box("PPE Panel", (0, BC, 0), (BW, BH, 0.02), WHITE, bevel=0.004)
ppe.box("PPE Header", (0, BC + BH / 2 - 0.05, -0.0105), (BW - 0.06, 0.08, 0.002), NAVY, bevel=0.0)
for x, y in ((-0.42, 1.735), (-0.02, 1.775), (0.43, 1.775)):
    ppe.box("PPE Hook Plate", (x, y, -0.011), (0.025, 0.035, 0.003), CHROME, bevel=0.0)
    ppe.box("PPE Hook", (x, y - 0.012, -0.025), (0.008, 0.008, 0.03), CHROME, bevel=0.002)
# Hard hat hanging crown-out.
ppe.lathe("PPE Hard Hat", [(0, 0.15), (0.05, 0.145), (0.09, 0.125), (0.112, 0.085), (0.12, 0.03), (0.12, 0.012)], WHITE, (-0.42, 1.58, -0.012), facing_front=True)
ppe.lathe("PPE Hard Hat Brim", [(0.118, 0.016), (0.15, 0.008), (0.152, 0.002), (0.118, 0.006)], WHITE, (-0.42, 1.58, -0.012), facing_front=True)
ppe.box("PPE Hard Hat Ridge", (-0.42, 1.58, -0.14), (0.03, 0.18, 0.03), WHITE, bevel=0.012)
# Two hi-vis vests with reflective tape and a dark front zip.
for vx, hook in ((-0.02, 0.0), (0.43, 0.4)):
    top = 1.75
    outline = [(-0.1, 0), (-0.045, 0), (0, -0.1), (0.045, 0), (0.1, 0), (0.12, -0.03), (0.13, -0.1), (0.17, -0.2), (0.175, -0.58),
               (-0.175, -0.58), (-0.17, -0.2), (-0.13, -0.1), (-0.12, -0.03)]
    ppe.plate("PPE Vest", [(vx + x, top + y) for x, y in outline], -0.035, -0.015, HIVIS, bevel=0.004)
    for y in (top - 0.38, top - 0.5):
        ppe.box("PPE Vest Tape", (vx, y, -0.036), (0.345, 0.028, 0.002), REFLECT, bevel=0.0)
    for x in (-0.07, 0.07):
        ppe.box("PPE Vest Tape", (vx + x, top - 0.18, -0.036), (0.026, 0.34, 0.002), REFLECT, bevel=0.0)
    ppe.box("PPE Vest Zip", (vx, top - 0.34, -0.0365), (0.007, 0.48, 0.002), SOFT_BLACK, bevel=0.0)
# Safety glasses in a clear acrylic box.
GB = (-0.42, 1.12, -0.075)
for i in range(3):
    y = GB[1] - 0.04 + i * 0.022
    z = GB[2] - 0.01 + i * 0.008
    ppe.box("PPE Glasses Frame", (GB[0], y + 0.012, z - 0.02), (0.15, 0.008, 0.008), SOFT_BLACK, bevel=0.002)
    for x in (-0.035, 0.035):
        ppe.box("PPE Glasses Lens", (GB[0] + x, y, z - 0.021), (0.06, 0.03, 0.003), LENS, bevel=0.002)
    for x in (-0.072, 0.072):
        ppe.box("PPE Glasses Arm", (GB[0] + x, y + 0.01, z + 0.02), (0.006, 0.006, 0.08), SOFT_BLACK, bevel=0.001)
for x, y, z, sx, sy, sz in ((0, -0.07, 0, 0.22, 0.004, 0.12), (0, 0, -0.06, 0.22, 0.14, 0.004), (-0.11, 0, 0, 0.004, 0.14, 0.12),
                            (0.11, 0, 0, 0.004, 0.14, 0.12)):
    ppe.box("PPE Acrylic", (GB[0] + x, GB[1] + y, GB[2] + z), (sx, sy, sz), GLASS, bevel=0.0)

# ---- 03 Lockout board on its post ----
lob = Prop("Lockout Board", 46.0)
LC = 1.22
lob.box("Lockout Post", (0, 0.5, 0.05), (0.07, 1.0, 0.07), STEEL, bevel=0.004)
lob.box("Lockout Foot", (0, 0.006, 0.05), (0.2, 0.012, 0.2), STEEL, bevel=0.003)
lob.box("Lockout Panel", (0, LC, 0.01), (0.5, 0.52, 0.02), WHITE, bevel=0.006)
lob.box("Lockout Header", (0, LC + 0.215, -0.0005), (0.5 - 0.02, 0.07, 0.002), RED, bevel=0.0)
for x, y in ((-0.06, LC + 0.03), (0.07, LC + 0.03), (-0.17, LC + 0.1), (-0.17, LC - 0.1), (0.17, LC + 0.1), (0.17, LC - 0.08)):
    lob.cyl("Lockout Peg", (x, y, -0.02), 0.005, 0.04, "z", CHROME, seg=10)


def padlock(prop, name, c):
    prop.box(name + " Body", c, (0.045, 0.05, 0.022), RED, bevel=0.006)
    prop.tube(name + " Shackle", [(c[0] - 0.014, c[1] + 0.024, c[2]), (c[0] - 0.014, c[1] + 0.045, c[2]), (c[0] - 0.008, c[1] + 0.056, c[2]),
                                  (c[0] + 0.008, c[1] + 0.056, c[2]), (c[0] + 0.014, c[1] + 0.045, c[2]), (c[0] + 0.014, c[1] + 0.024, c[2])],
              0.0035, CHROME, seg=8)
    prop.box(name + " Keyway", (c[0], c[1] - 0.012, c[2] - 0.0115), (0.008, 0.012, 0.001), SOFT_BLACK, bevel=0.0)


padlock(lob, "Lockout Spare Lock", (-0.17, LC + 0.055, -0.028))
padlock(lob, "Lockout Spare Lock", (-0.17, LC - 0.145, -0.028))
# A lockout hasp: steel jaws with red-coated handles, hung by its ring.
hx, hy = 0.17, LC + 0.08
lob.tube("Lockout Hasp Ring", [(hx + 0.028 * math.cos(a), hy + 0.028 * math.sin(a), -0.03) for a in [math.tau * k / 18 for k in range(18)]],
         0.004, CHROME, seg=8, closed=True)
for sx in (-1, 1):
    lob.tube("Lockout Hasp Handle", [(hx + sx * 0.012, hy - 0.026, -0.03), (hx + sx * 0.018, hy - 0.07, -0.03), (hx + sx * 0.022, hy - 0.13, -0.03)],
             0.007, RED, seg=10)
# A pad of DANGER tags on the lower right peg.
lob.box("Lockout Tags", (0.17, LC - 0.135, -0.03), (0.075, 0.12, 0.012), WHITE, bevel=0.002)
lob.box("Lockout Tags Header", (0.17, LC - 0.09, -0.0365), (0.075, 0.03, 0.001), RED, bevel=0.0)
lob.tube("Lockout Tags String", [(0.17, LC - 0.075, -0.03), (0.175, LC - 0.06, -0.03), (0.17, LC - 0.08 + 0.0, -0.02)], 0.0015, WHITE, seg=6)

# ---- 03 Radio in its charger, on a post with a shelf ----
rad = Prop("Radio Charger", 48.0)
rad.box("Stand Post", (0, 0.44, 0.03), (0.06, 0.88, 0.06), STEEL, bevel=0.004)
rad.box("Stand Foot", (0, 0.006, 0.03), (0.22, 0.012, 0.22), STEEL, bevel=0.003)
rad.box("Stand Shelf", (0, 0.888, 0), (0.2, 0.015, 0.16), STEEL, bevel=0.003)
rad.box("Stand Charger", (0, 0.913, 0), (0.11, 0.035, 0.09), SOFT_BLACK, bevel=0.012, segs=3)
rad.box("Stand Charger Pocket", (0, 0.931, 0), (0.07, 0.002, 0.045), BLACK, bevel=0.0)
rad.cyl("Stand Charger Led", (0, 0.913, -0.0455), 0.004, 0.002, "z", GREEN_LED, seg=10)
rad.tube("Stand Charger Cable", [(0, 0.905, 0.045), (0.0, 0.9, 0.07), (0.02, 0.89, 0.09), (0.03, 0.86, 0.1), (0.032, 0.6, 0.1)], 0.003, BLACK, seg=6)
RY = 0.99
rad.box("Radio Body", (0, RY, 0), (0.058, 0.15, 0.034), SOFT_BLACK, bevel=0.008, segs=3)
rad.box("Radio Grip", (0, RY - 0.045, -0.0172), (0.05, 0.05, 0.002), BLACK, bevel=0.0)
rad.box("Radio Screen Bezel", (0, RY + 0.035, -0.0175), (0.044, 0.036, 0.002), BLACK, bevel=0.001)
rad.box("Radio Screen", (0, RY + 0.035, -0.0186), (0.036, 0.028, 0.001), LCD, bevel=0.0)
rad.cyl("Radio Antenna", (-0.014, RY + 0.11, 0), 0.0075, 0.075, "y", BLACK, seg=12, r2=0.006)
rad.cyl("Radio Antenna Tip", (-0.014, RY + 0.148, 0), 0.006, 0.004, "y", BLACK, seg=12)
rad.cyl("Radio Knob", (0.015, RY + 0.084, 0), 0.0095, 0.018, "y", BLACK, seg=12, bevel=0.001)
rad.box("Radio Talk Button", (-0.031, RY + 0.015, 0), (0.006, 0.04, 0.018), BLACK, bevel=0.002)

# ---- 04 Damaged extension cord, lying along z ----
cord = Prop("Extension Cord", 50.0)
R = 0.016
seg1 = smooth_path([(0.0, R, -0.5), (0.05, R, -0.4), (0.06, R, -0.32), (0.045, R, -0.26)])
seg2 = smooth_path([(0.0, R, -0.14), (-0.03, R, -0.07), (-0.065, R, 0.04), (-0.045, R, 0.19), (0.025, R, 0.32), (0.06, R, 0.43), (0.055, R, 0.5)])
cord.tube("Cord Jacket", seg1, R, ORANGE, seg=14)
cord.tube("Cord Jacket", seg2, R, ORANGE, seg=14)
# The split: torn jacket flaps around three exposed conductors, the green one stripped to copper.
# The split: a long tear in the jacket, three conductors bowed out of it, and the green one stripped to bare copper.
for dx, dy, bulge, m in ((-0.008, 0.0, -0.018, BLACK), (0.006, 0.006, 0.012, WIRE_WHITE), (0.0, -0.002, 0.032, WIRE_GREEN)):
    cord.tube("Cord Wire", smooth_path([(0.045 + dx, R + dy, -0.27), (0.03 + dx + bulge, R + dy + 0.01, -0.2), (0.0 + dx, R + dy, -0.13)], 6), 0.0055, m, seg=8)
for k, (ex, ey, ez) in enumerate(((0.1, 0.03, -0.215), (0.11, 0.022, -0.19), (0.095, 0.04, -0.18), (0.105, 0.012, -0.205))):
    cord.tube("Cord Copper", [(0.062, R + 0.008, -0.2), (ex, R + ey, ez)], 0.0016, COPPER, seg=5)
cord.tube("Cord Torn Jacket", smooth_path([(0.045, R - 0.011, -0.265), (0.03, R - 0.012, -0.2), (0.0, R - 0.011, -0.135)], 5), 0.006, ORANGE, seg=8)
# Male plug at the -z end, female connector at +z.
cord.cyl("Cord Plug Relief", (0.0, R, -0.52), 0.019, 0.04, "z", BLACK, seg=16, r2=0.016)
cord.box("Cord Plug", (0.0, 0.024, -0.565), (0.045, 0.045, 0.055), BLACK, bevel=0.008, segs=3)
for x in (-0.012, 0.012):
    cord.box("Cord Plug Blade", (x, 0.024, -0.605), (0.002, 0.012, 0.03), CHROME, bevel=0.0)
cord.cyl("Cord Plug Ground", (0.0, 0.012, -0.602), 0.003, 0.026, "z", CHROME, seg=8)
cord.cyl("Cord Socket Relief", (0.055, R, 0.52), 0.019, 0.04, "z", BLACK, seg=16, r2=0.016)
cord.box("Cord Socket", (0.055, 0.026, 0.57), (0.055, 0.05, 0.065), BLACK, bevel=0.01, segs=3)
cord.box("Cord Socket Face", (0.055, 0.026, 0.6025), (0.04, 0.036, 0.001), SOFT_BLACK, bevel=0.0)

# ---- Bake and export ----
MARKER = os.path.join(os.path.dirname(P.__file__), "wave3a_props.done")
if os.path.exists(MARKER):
    os.remove(MARKER)
if bpy.app.background:
    t0 = time.time()
    report = {}

    def objs(prop, *prefixes, exclude=()):
        return [o for o in prop.col.objects
                if (not prefixes or o.name.startswith(prefixes)) and not (exclude and o.name.startswith(exclude))]

    jobs = [
        (mon, "Monitor.fbx", "monitor", {"Monitor": (objs(mon, exclude=("Screen",)), (0, 0, 0)), "Monitor Screen": (objs(mon, "Screen"), (0, 0, 0))}, ("Monitor Screen",)),
        (rec, "ReceptionCounter.fbx", "reception", {"Reception Counter": (objs(rec), (0, 0, 0))}, ()),
        (ppe, "PpeBoard.fbx", "ppeboard", {"PPE Board": (objs(ppe, exclude=("PPE Acrylic",)), (0, 0, 0)), "PPE Board Acrylic": (objs(ppe, "PPE Acrylic"), (0, 0, 0))}, ("PPE Board Acrylic",)),
        (lob, "LockoutBoard.fbx", "lockoutboard", {"Lockout Board": (objs(lob), (0, 0, 0))}, ()),
        (rad, "RadioCharger.fbx", "radio", {"Radio Stand": (objs(rad, "Stand"), (0, 0, 0)), "Radio": (objs(rad, "Radio"), (0, RY, 0))}, ()),
        (cord, "ExtensionCord.fbx", "cord", {"Extension Cord": (objs(cord), (0, 0, 0))}, ()),
    ]
    for prop, filename, prefix, parts, unbaked in jobs:
        exp = bpy.data.collections.new("Export"); bpy.context.scene.collection.children.link(exp)
        origin = tuple(prop.b((0, 0, 0)))
        made = P.join_parts({k: v[0] for k, v in parts.items()}, {k: tuple(prop.b(v[1])) for k, v in parts.items()}, exp)
        bpy.ops.mesh.primitive_plane_add(size=4, location=origin)
        floor = bpy.context.active_object
        for c in list(floor.users_collection): c.objects.unlink(floor)
        exp.objects.link(floor)
        baked_parts = [o for k, o in made.items() if k not in unbaked]
        P.bake(baked_parts, prefix, occluders=[floor] + [made[k] for k in unbaked], size=1024, samples=32)
        bpy.data.objects.remove(floor, do_unlink=True)
        name = prefix.capitalize() + "_Baked"
        baked = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        for o in baked_parts: P.single_material(o, baked)
        for k in unbaked:
            P.single_material(made[k], bpy.data.materials.get(k.replace(" ", "_")) or bpy.data.materials.new(k.replace(" ", "_")))
        P.export(filename, made, origin)
        report[filename] = {k: P.triangles([v]) for k, v in made.items()}
        P.cleanup(exp)
    bpy.ops.wm.save_mainfile()
    with open(MARKER, "w") as f:
        f.write(f"{report} in {round(time.time() - t0, 1)}s\n")
print({p.col.name: len(p.col.objects) for p in (mon, rec, ppe, lob, rad, cord)})
