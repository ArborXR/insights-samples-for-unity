"""Builds Wave 2 props for samples 03-06: Disconnect, PushButtonStation, BenchGrinder, ExitDoor, GloveBox,
PartsWasher, InfusionPump.

Coordinates are written in each station's Unity-local space (x right, y up, z away from the camera) and mapped
into Blender (x, z, y); each prop is parked at its own X offset (OX) along Blender Y = 10.
"""
import bpy, bmesh, math, importlib
from mathutils import Vector, Matrix, Euler
import props_lib as P
importlib.reload(P)

PARK_Y = 10.0


def lin(c): return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def mat(name, c, rough, metal=0.0): return P.material(name, lin(c), rough, metal)


GREY_PAINT = mat("Disconnect_Grey", (0.62, 0.64, 0.66), 0.5)
POST = mat("Post_Galvanised", (0.42, 0.43, 0.45), 0.45, 0.6)
STEEL = mat("W2_Steel", (0.80, 0.81, 0.83), 0.3, 1.0)
DARK = mat("W2_DarkPlastic", (0.10, 0.10, 0.11), 0.55)
YELLOW = mat("W2_Yellow", (0.95, 0.80, 0.10), 0.45)
RED = mat("W2_RedPaint", (0.78, 0.10, 0.08), 0.4)
WHITE_TINT = mat("Tint_White", (1, 1, 1), 0.4)          # parts the station tints at runtime
GRINDER_BLUE = mat("Grinder_Blue", (0.33, 0.43, 0.50), 0.5, 0.2)
GRIT_DARK = mat("Grinder_WheelDark", (0.18, 0.18, 0.19), 0.95)
GRIT_LIGHT = mat("Grinder_WheelLight", (0.72, 0.72, 0.70), 0.95)
SHIELD = mat("Grinder_Shield", (0.85, 0.92, 0.96), 0.05)
DOOR = mat("Door_Grey", (0.62, 0.64, 0.66), 0.5, 0.1)
FRAME = mat("Door_Frame", (0.30, 0.32, 0.34), 0.45, 0.2)
SIGN_WHITE = mat("Sign_Housing", (0.92, 0.93, 0.93), 0.5)
SIGN_GREEN = mat("Exit_Sign_Face", (0.10, 0.62, 0.36), 0.3)
CARD_WHITE = mat("GloveBox_Card", (0.96, 0.96, 0.95), 0.6)
OPENING = mat("GloveBox_Opening", (0.05, 0.05, 0.06), 0.8)
WASHER_RED = mat("Washer_Red", (0.72, 0.10, 0.08), 0.35, 0.2)
LID = mat("Washer_Lid", (0.78, 0.79, 0.80), 0.35, 1.0)
GRATE = mat("Washer_Grate", (0.22, 0.23, 0.24), 0.5, 0.6)
LABEL_WHITE = mat("Washer_Label", (0.95, 0.95, 0.94), 0.6)
PUMP_WHITE = mat("Pump_Shell", (0.93, 0.94, 0.95), 0.45)
PUMP_GREY = mat("Pump_Faceplate", (0.80, 0.82, 0.84), 0.5)
PUMP_NAVY = mat("Pump_Accent", (0.10, 0.18, 0.40), 0.45)
TUBE = mat("Pump_Tubing", (0.88, 0.92, 0.95), 0.15)


class Prop:
    """Collects one prop's parts; all shape helpers take Unity-local coordinates."""

    def __init__(self, name, ox):
        self.col = P.collection(name)
        self.ox = ox

    def b(self, u):  # Unity-local point -> Blender
        return Vector((self.ox + u[0], PARK_Y + u[2], u[1]))

    def box(self, name, c, s, m, bevel=0.003, segs=2):
        return P.box(self.col, name, self.b(c), (s[0], s[2], s[1]), m, bevel, segs)

    def cyl(self, name, c, r, depth, axis, m, seg=24, bevel=0.0, r2=None):
        return P.cyl(self.col, name, self.b(c), r, depth, {"x": "x", "y": "z", "z": "y"}[axis], m, seg, bevel, r2)

    def beam(self, name, a, b, w, d, m, bevel=0.002):
        a, b = self.b(a), self.b(b)
        bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
        L = (b - a).length
        for v in bm.verts: v.co = Vector((v.co.x * w, v.co.y * d, v.co.z * L))
        rot = Vector((0, 0, 1)).rotation_difference((b - a).normalized()).to_matrix().to_4x4()
        bmesh.ops.transform(bm, verts=bm.verts, matrix=rot)
        bmesh.ops.translate(bm, verts=bm.verts, vec=(a + b) / 2)
        return P.finish(self.col, name, bm, m, bevel, 2)

    def rotated_box(self, name, c, s, m, euler_unity, bevel=0.002):
        """A box rotated (degrees) about Unity x, y, z through its own centre."""
        ob = self.box(name, (0, 0, 0), s, m, bevel)
        rx, ry, rz = (math.radians(a) for a in euler_unity)
        # Unity x -> Blender x, Unity y -> Blender z, Unity z -> Blender y (a handedness flip, so angles negate).
        R = Matrix.Rotation(-ry, 4, "Z") @ Matrix.Rotation(-rx, 4, "X") @ Matrix.Rotation(-rz, 4, "Y")
        base = self.b((0, 0, 0))
        ob.data.transform(Matrix.Translation(-base))
        ob.data.transform(R)
        ob.data.transform(Matrix.Translation(self.b(c)))
        return ob

    def ring_band(self, name, centre, r_in, r_out, width, start_deg, end_deg, m, segs=32, axis="x"):
        """A partial thick ring around an axis through centre (angles measured in the plane, 0 = toward -z, up = 90)."""
        bm = bmesh.new()
        rows = []
        for i in range(segs + 1):
            a = math.radians(start_deg + (end_deg - start_deg) * i / segs)
            dz, dy = -math.cos(a), math.sin(a)
            row = []
            for r in (r_in, r_out):
                for w in (-width / 2, width / 2):
                    u = (centre[0] + w, centre[1] + dy * r, centre[2] + dz * r)
                    row.append(bm.verts.new(self.b(u)))
            rows.append(row)
        for i in range(segs):
            a, c = rows[i], rows[i + 1]
            for (p, q) in ((0, 1), (1, 3), (3, 2), (2, 0)):
                bm.faces.new((a[p], c[p], c[q], a[q]))
        bm.faces.new((rows[0][0], rows[0][1], rows[0][3], rows[0][2]))
        bm.faces.new((rows[-1][2], rows[-1][3], rows[-1][1], rows[-1][0]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return P.finish(self.col, name, bm, m, 0.0015, 1)


def post_with_plate(p, x, z, top, prefix="Body"):
    p.box(f"{prefix} Post", (x, top / 2, z), (0.075, top, 0.075), POST, bevel=0.004)
    p.box(f"{prefix} Post Cap", (x, top + 0.003, z), (0.08, 0.006, 0.08), POST, bevel=0.002)
    p.box(f"{prefix} Base Plate", (x, 0.006, z), (0.26, 0.012, 0.26), POST, bevel=0.003)
    for dx in (-0.1, 0.1):
        for dz in (-0.1, 0.1):
            p.cyl(f"{prefix} Base Bolt", (x + dx, 0.018, z + dz), 0.011, 0.012, "y", STEEL, seg=6, bevel=0.001)


# ======================= 03 Main disconnect (station root; box centre (0, 1.25, 0)) =======================
d = Prop("Disconnect", 7.0)
BY = 1.25
post_with_plate(d, 0, 0.12, 1.52)
d.box("Body Enclosure", (0, BY, 0), (0.32, 0.42, 0.16), GREY_PAINT, bevel=0.008)
d.box("Body Door", (0, BY, -0.0815), (0.30, 0.40, 0.006), GREY_PAINT, bevel=0.003)
for y in (BY + 0.14, BY - 0.14):
    d.cyl("Body Hinge", (-0.163, y, -0.08), 0.007, 0.05, "y", GREY_PAINT, seg=12)
for y in (BY + 0.17, BY - 0.17):
    d.cyl("Body Screw", (0.13, y, -0.0855), 0.006, 0.003, "z", STEEL, seg=12)
d.box("Body Label Plate", (0, BY + 0.14, -0.0855), (0.08, 0.025, 0.002), STEEL, bevel=0)
d.cyl("Body Conduit Hub", (-0.09, BY + 0.225, 0), 0.022, 0.03, "y", STEEL, seg=20, bevel=0.002)
d.cyl("Body Conduit Nut", (-0.09, BY + 0.212, 0), 0.026, 0.008, "y", STEEL, seg=6)
d.cyl("Body Knockout", (0.06, BY - 0.21, 0), 0.016, 0.004, "y", GREY_PAINT, seg=20)
# Operating mechanism on the right side: a bracket out of the side, a hub facing the front.
PIV = (0.2, BY - 0.02, -0.04)
d.box("Body Handle Bracket", (0.185, PIV[1], -0.005), (0.06, 0.14, 0.012), GREY_PAINT, bevel=0.003)
d.cyl("Body Handle Quadrant", (PIV[0], PIV[1], -0.015), 0.055, 0.006, "z", STEEL, seg=32, bevel=0.001)
d.cyl("Body Handle Hub", (PIV[0], PIV[1], -0.028), 0.028, 0.02, "z", DARK, seg=24, bevel=0.003)
# Lever (separate; points up in ON, the station rotates it about z to OFF), with a padlock hasp tab.
d.beam("Lever Arm", (PIV[0], PIV[1], PIV[2]), (PIV[0], PIV[1] + 0.2, PIV[2] - 0.01), 0.045, 0.025, WHITE_TINT, bevel=0.006)
d.cyl("Lever Grip", (PIV[0], PIV[1] + 0.2, PIV[2] - 0.015), 0.024, 0.045, "z", WHITE_TINT, seg=20, bevel=0.006)
d.box("Lever Hasp", (PIV[0] - 0.03, PIV[1] + 0.09, PIV[2] - 0.005), (0.035, 0.03, 0.004), WHITE_TINT, bevel=0.001)
d.cyl("Lever Hasp Hole", (PIV[0] - 0.035, PIV[1] + 0.09, PIV[2] - 0.0075), 0.006, 0.002, "z", DARK, seg=12)

# ======================= 03 STOP/START station (station root; box centre (0, 1.18, 0)) =======================
s = Prop("Push Button Station", 7.8)
SY = 1.18
post_with_plate(s, 0, 0.1, 1.42)
s.box("Body Enclosure", (0, SY, 0), (0.34, 0.44, 0.14), YELLOW, bevel=0.012, segs=3)
s.box("Body Cover", (0, SY, -0.0715), (0.32, 0.42, 0.006), YELLOW, bevel=0.004)
for x in (-0.145, 0.145):
    for y in (SY - 0.195, SY + 0.195):
        s.cyl("Body Screw", (x, y, -0.0755), 0.006, 0.003, "z", STEEL, seg=12)
for y, cap in ((SY + 0.1, "Stop Button"), (SY - 0.08, "Start Button")):
    s.cyl("Body Bezel", (0.06, y, -0.078), 0.05, 0.012, "z", DARK, seg=32, bevel=0.003)
    s.cyl(cap, (0.06, y, -0.09), 0.037, 0.018, "z", WHITE_TINT, seg=32, bevel=0.004)
    s.box("Body Legend Plate", (-0.09, y, -0.0755), (0.11, 0.045, 0.003), STEEL, bevel=0.001)
s.cyl("Body Gland", (0.06, SY - 0.235, 0), 0.02, 0.03, "y", STEEL, seg=6, bevel=0.002)
s.cyl("Body Gland Cap", (0.06, SY - 0.256, 0), 0.016, 0.012, "y", STEEL, seg=16, bevel=0.002)

# ======================= 04 Bench grinder (root on the bench top) =======================
g = Prop("Bench Grinder", 8.6)
g.box("Grinder Base", (0, 0.012, 0), (0.3, 0.024, 0.17), GRINDER_BLUE, bevel=0.006)
for x in (-0.12, 0.12):
    for z in (-0.06, 0.06):
        g.cyl("Grinder Base Hole", (x, 0.0245, z), 0.008, 0.002, "y", DARK, seg=12)
g.box("Grinder Pedestal", (0, 0.06, 0), (0.15, 0.075, 0.11), GRINDER_BLUE, bevel=0.012)
g.box("Grinder Switch Plate", (0, 0.06, -0.056), (0.04, 0.045, 0.004), DARK, bevel=0.002)
g.box("Grinder Switch", (0, 0.062, -0.0595), (0.024, 0.028, 0.006), RED, bevel=0.002)
MY = 0.15
g.cyl("Grinder Motor", (0, MY, 0), 0.07, 0.2, "x", GRINDER_BLUE, seg=40, bevel=0.01)
for i in range(6):
    g.box("Grinder Vent", (0.05 + i * 0.008, MY, -0.07), (0.003, 0.03, 0.004), DARK, bevel=0)
for side in (-1, 1):
    g.cyl("Grinder End Bell", (side * 0.11, MY, 0), 0.052, 0.03, "x", GRINDER_BLUE, seg=32, bevel=0.006)
    g.cyl("Grinder Shaft", (side * 0.145, MY, 0), 0.009, 0.05, "x", STEEL, seg=12)
    g.cyl("Grinder Wheel", (side * 0.175, MY, 0), 0.068, 0.026, "x", GRIT_DARK if side < 0 else GRIT_LIGHT, seg=40, bevel=0.002)
    # Guard: closed outer cover and a peripheral band, open at the front between -25 and 65 degrees.
    g.cyl("Grinder Guard Cover", (side * 0.2, MY, 0), 0.088, 0.008, "x", YELLOW, seg=40, bevel=0.003)
    g.cyl("Grinder Guard Dome", (side * 0.207, MY, 0), 0.03, 0.01, "x", YELLOW, seg=24, bevel=0.004, r2=0.022)
    g.ring_band("Grinder Guard Band", (side * 0.178, MY, 0), 0.078, 0.088, 0.052, 65, 335, YELLOW)
    g.ring_band("Grinder Guard Inner", (side * 0.151, MY, 0), 0.06, 0.088, 0.006, 65, 335, YELLOW)
    # Tongue guard at the top of the opening, tool rest at the bottom.
    g.rotated_box("Grinder Tongue Guard", (side * 0.178, MY + 0.083, -0.038), (0.046, 0.004, 0.03), STEEL, (25, 0, 0))
    g.box("Grinder Tool Rest", (side * 0.178, MY - 0.03, -0.085), (0.04, 0.005, 0.028), STEEL, bevel=0.001)
    g.box("Grinder Rest Bracket", (side * 0.155, MY - 0.045, -0.072), (0.006, 0.04, 0.012), STEEL, bevel=0.001)
    g.cyl("Grinder Rest Knob", (side * 0.147, MY - 0.045, -0.072), 0.009, 0.012, "x", DARK, seg=12)
    # Eye shield on an arm over the opening (the shield panes are their own object for a clear material).
    g.beam("Grinder Shield Arm", (side * 0.178, MY + 0.088, 0.0), (side * 0.178, MY + 0.12, -0.05), 0.01, 0.006, STEEL)
    g.rotated_box("Eye Shield", (side * 0.178, MY + 0.118, -0.085), (0.1, 0.004, 0.07), SHIELD, (-30, 0, 0), bevel=0.002)

# ======================= 04 Fire exit door (root at the door's floor centre) =======================
e = Prop("Exit Door", 9.6)
for x in (-0.5, 0.5):
    e.box("Door Jamb", (x, 1.05, 0.03), (0.05, 2.1, 0.06), FRAME, bevel=0.004)
e.box("Door Head", (0, 2.075, 0.03), (1.05, 0.05, 0.06), FRAME, bevel=0.004)
e.box("Door Slab", (0, 1.0, 0.0), (0.9, 1.99, 0.045), DOOR, bevel=0.003)
e.box("Door Kick Plate", (0, 0.12, -0.0235), (0.86, 0.2, 0.002), STEEL, bevel=0)
for y in (0.25, 1.0, 1.75):
    e.cyl("Door Hinge", (-0.455, y, -0.02), 0.009, 0.09, "y", STEEL, seg=12)
# Panic bar: mechanism case on the latch side, push bar, hinge-side end cap.
e.box("Door Bar Case", (0.3, 1.0, -0.045), (0.16, 0.09, 0.04), STEEL, bevel=0.006)
e.box("Door Push Bar", (-0.06, 1.0, -0.05), (0.56, 0.04, 0.03), STEEL, bevel=0.008)
e.box("Door Bar End Cap", (-0.36, 1.0, -0.045), (0.05, 0.07, 0.04), STEEL, bevel=0.006)
# Closer on the pull side of the door, arm to the head.
e.box("Door Closer", (0.22, 1.93, -0.055), (0.24, 0.06, 0.06), STEEL, bevel=0.008)
e.beam("Door Closer Arm", (0.12, 1.93, -0.09), (0.0, 2.04, -0.03), 0.02, 0.012, STEEL)
# Exit sign: housing above the frame; the face is separate so it can glow.
e.box("Door Sign Stem", (0, 2.13, 0.04), (0.04, 0.06, 0.03), SIGN_WHITE, bevel=0.003)
e.box("Door Sign Housing", (0, 2.25, 0.0), (0.45, 0.18, 0.05), SIGN_WHITE, bevel=0.008)
e.box("Exit Sign Face", (0, 2.25, -0.026), (0.41, 0.14, 0.002), SIGN_GREEN, bevel=0)

# ======================= 05 Glove box (origin at the box centre; 0.28 x 0.16 x 0.12) =======================
gb = Prop("Glove Box", 10.6)
BW, BH, BD = 0.28, 0.16, 0.12
gb.box("Glove Box Body", (0, 0, 0), (BW, BH, BD), CARD_WHITE, bevel=0.004)
bm = bmesh.new()
for i in range(32):
    a = 2 * math.pi * i / 32
    bm.verts.new(gb.b((0.08 * math.cos(a), BH / 2 + 0.0008, 0.03 * math.sin(a))))
bm.faces.new(bm.verts)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
gb_open = P.finish(gb.col, "Glove Box Body Opening", bm, OPENING)
for p in gb_open.data.polygons:
    if p.normal.z < 0: p.flip()
gb.box("Glove Box Body Label", (0, -0.02, -BD / 2 - 0.0016), (0.17, 0.065, 0.001), CARD_WHITE, bevel=0)
# Colour band around the box's middle (tinted per glove type).
bw = 0.1
gb.box("Glove Box Band Top", (0, BH / 2 + 0.0004, 0), (bw, 0.0008, BD + 0.0016), WHITE_TINT, bevel=0)
gb.box("Glove Box Band Front", (0, 0, -BD / 2 - 0.0004), (bw, BH + 0.0016, 0.0008), WHITE_TINT, bevel=0)
gb.box("Glove Box Band Back", (0, 0, BD / 2 + 0.0004), (bw, BH + 0.0016, 0.0008), WHITE_TINT, bevel=0)
gb.box("Glove Box Band Stripe", (0, -0.045, -BD / 2 - 0.0005), (BW + 0.0016, 0.04, 0.0008), WHITE_TINT, bevel=0)
# Glove cuff poking out of the opening (tinted per glove type).
cuff = gb.cyl("Glove Cuff", (0.0, BH / 2 + 0.03, 0.0), 0.024, 0.07, "y", WHITE_TINT, seg=16, r2=0.02)
for v in cuff.data.vertices:
    v.co.x += (v.co.z - BH / 2) * 0.5
    v.co.y *= 1.0
cuff.data.update()
for i, (dx, dy) in enumerate(((0.032, 0.08), (0.04, 0.072), (0.046, 0.062))):
    gb.cyl("Glove Finger", (dx - 0.01, BH / 2 + dy - 0.03, (i - 1) * 0.012), 0.008, 0.035, "x", WHITE_TINT, seg=10)

# ======================= 05 Parts washer (bench root; drum on the floor) =======================
w = Prop("Parts Washer", 11.4)
WZ = -0.12
DR, DH = 0.29, 0.74
w.cyl("Washer Drum", (0, DH / 2, WZ), DR, DH, "y", WASHER_RED, seg=48, bevel=0.006)
for y in (0.012, DH - 0.012, DH * 0.33, DH * 0.66):
    w.cyl("Washer Drum Hoop", (0, y, WZ), DR + 0.006, 0.014, "y", WASHER_RED, seg=48, bevel=0.003)
w.cyl("Washer Drum Bung", (0.15, DH + 0.004, WZ), 0.025, 0.012, "y", WASHER_RED, seg=16)
w.box("Washer Pump", (-0.05, DH + 0.04, WZ), (0.14, 0.07, 0.07), DARK, bevel=0.01)
w.cyl("Washer Pump Motor", (0.06, DH + 0.04, WZ), 0.03, 0.08, "x", DARK, seg=20, bevel=0.004)
# Diamond hazard label on the drum front (blank; the station prints the contents in live text).
w.rotated_box("Washer Label", (0, 0.52, WZ - DR - 0.002), (0.13, 0.13, 0.002), LABEL_WHITE, (0, 0, 45), bevel=0)
# Sink on four stand legs above the drum.
SB, ST = 0.82, 0.98
SW, SD, WALL = 0.88, 0.42, 0.014
for x in (-0.18, 0.18):
    for z in (-0.12, 0.12):
        w.box("Washer Stand", (x, (DH + SB) / 2, WZ + z), (0.025, SB - DH, 0.025), WASHER_RED, bevel=0.002)
w.box("Washer Sink Floor", (0, SB + WALL / 2, WZ), (SW, WALL, SD), WASHER_RED, bevel=0.01)
w.box("Washer Sink Wall", (0, (SB + ST) / 2, WZ - SD / 2 + WALL / 2), (SW, ST - SB, WALL), WASHER_RED, bevel=0.004)
w.box("Washer Sink Wall", (0, (SB + ST) / 2, WZ + SD / 2 - WALL / 2), (SW, ST - SB, WALL), WASHER_RED, bevel=0.004)
w.box("Washer Sink Wall", (-SW / 2 + WALL / 2, (SB + ST) / 2, WZ), (WALL, ST - SB, SD), WASHER_RED, bevel=0.004)
w.box("Washer Sink Wall", (SW / 2 - WALL / 2, (SB + ST) / 2, WZ), (WALL, ST - SB, SD), WASHER_RED, bevel=0.004)
w.box("Washer Sink Rim", (0, ST, WZ - SD / 2 - 0.006), (SW + 0.02, 0.012, 0.025), WASHER_RED, bevel=0.004)
for i in range(11):
    w.box("Washer Grate Bar", (0, SB + 0.03, WZ - SD / 2 + 0.04 + i * 0.034), (SW - 0.05, 0.008, 0.008), GRATE, bevel=0)
w.box("Washer Grate Rail", (0, SB + 0.025, WZ), (0.012, 0.01, SD - 0.04), GRATE, bevel=0)
# Hose coil and brush on the grate.
bm = bmesh.new()
segs, tube, R = 40, 8, 0.11
ring = []
for i in range(segs):
    a = 2 * math.pi * i / segs
    for j in range(tube):
        t = 2 * math.pi * j / tube
        r = R + 0.007 * math.cos(t)
        ring.append(bm.verts.new(w.b((0.18 + r * math.cos(a), SB + 0.043 + 0.007 * math.sin(t), WZ + r * math.sin(a)))))
for i in range(segs):
    for j in range(tube):
        a = ring[i * tube + j]; b2 = ring[i * tube + (j + 1) % tube]
        c = ring[((i + 1) % segs) * tube + (j + 1) % tube]; d2 = ring[((i + 1) % segs) * tube + j]
        bm.faces.new((a, d2, c, b2))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
P.finish(w.col, "Washer Hose", bm, STEEL)
w.box("Washer Brush", (0.05, SB + 0.045, WZ - 0.06), (0.1, 0.02, 0.025), mat("Washer_Brush", (0.78, 0.62, 0.32), 0.8), bevel=0.004)
# Lid hinged at the back edge, standing open, held by its fusible-link arm.
w.rotated_box("Washer Lid", (0, ST + SD / 2 - 0.01, WZ + SD / 2 + 0.012), (SW + 0.02, 0.012, SD), LID, (-88, 0, 0), bevel=0.006)
w.beam("Washer Lid Arm", (SW / 2 - 0.03, ST, WZ + 0.05), (SW / 2 - 0.03, ST + 0.2, WZ + SD / 2 + 0.01), 0.008, 0.008, STEEL)

# ======================= 06 Infusion pump (origin at the pump centre; 0.42 x 0.62 x 0.14) =======================
pp = Prop("Infusion Pump", 12.4)
PW, PH, PD = 0.42, 0.62, 0.14
body = pp.box("Pump Shell", (0, 0, 0.005), (PW, PH, PD - 0.01), PUMP_WHITE, bevel=0.045, segs=6)
pp.box("Pump Front", (0, 0, -PD / 2 + 0.006), (PW - 0.02, PH - 0.02, 0.012), PUMP_WHITE, bevel=0.03, segs=5)
pp.box("Pump Key Panel", (0, -0.09, -PD / 2 - 0.0005), (0.34, 0.4, 0.002), PUMP_GREY, bevel=0.015, segs=4)
pp.box("Pump Display Bezel", (0, 0.2, -PD / 2 - 0.0015), (0.37, 0.18, 0.004), PUMP_GREY, bevel=0.012, segs=3)
for x in (-PW / 2 + 0.004, PW / 2 - 0.004):
    pp.box("Pump Accent", (x, 0, 0.0), (0.008, PH - 0.08, PD - 0.03), PUMP_NAVY, bevel=0.003)
# Hinged tubing door on the left side, with tubing clips.
pp.box("Pump Side Door", (-PW / 2 - 0.004, -0.02, 0.0), (0.006, 0.42, 0.1), PUMP_GREY, bevel=0.004)
for y in (0.17, -0.21):
    pp.box("Pump Door Clip", (-PW / 2 - 0.01, y, -0.04), (0.012, 0.03, 0.02), PUMP_GREY, bevel=0.003)
# Pole clamp on the back with a screw knob (the IV pole runs behind the pump).
pp.box("Pump Clamp", (0, 0, PD / 2 + 0.025), (0.06, 0.12, 0.05), STEEL, bevel=0.004)
pp.cyl("Pump Clamp Screw", (0.05, 0, PD / 2 + 0.035), 0.007, 0.06, "x", STEEL, seg=12)
pp.cyl("Pump Clamp Knob", (0.085, 0, PD / 2 + 0.035), 0.02, 0.02, "x", DARK, seg=8, bevel=0.003)
# Tubing from the bag down into the side door (its own object for a clear material).
pp.beam("Pump Tubing", (-PW / 2 - 0.01, PH / 2 - 0.1, -0.02), (-0.05, PH / 2 + 0.25, 0.08), 0.006, 0.006, TUBE, bevel=0)

print({c: len(bpy.data.collections[c].objects) for c in
       ("Disconnect", "Push Button Station", "Bench Grinder", "Exit Door", "Glove Box", "Parts Washer", "Infusion Pump")})
