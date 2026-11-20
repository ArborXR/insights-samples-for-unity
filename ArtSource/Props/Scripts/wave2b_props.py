"""Builds Wave 2 props for samples 06, 08, 13 and 14: HandWashStation, PickingBin, Shelving, MigTorch, WeldingTable,
CheckoutCounter, Gondola.

Coordinates are each station's Unity-local space (x right, y up, z away from the camera), mapped into Blender as
(x, z, y); each prop is parked at its own X offset along Blender Y = 14.
"""
import bpy, bmesh, math, importlib, random
from mathutils import Vector, Matrix
import props_lib as P
importlib.reload(P)

PARK_Y = 14.0


def lin(c): return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def hexc(h): return ((h >> 16 & 255) / 255, (h >> 8 & 255) / 255, (h & 255) / 255)


def mat(name, c, rough, metal=0.0): return P.material(name, lin(c), rough, metal)


def dot_material(name, base, hole, spacing, radius, axes=("X", "Y")):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        if n.type not in ("BSDF_PRINCIPLED", "OUTPUT_MATERIAL"):
            nt.nodes.remove(n)
    b = P.principled(m)
    b.inputs["Roughness"].default_value = 0.55
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(tc.outputs["Object"], sep.inputs[0])
    comb = nt.nodes.new("ShaderNodeCombineXYZ")
    nt.links.new(sep.outputs[axes[0]], comb.inputs["X"]); nt.links.new(sep.outputs[axes[1]], comb.inputs["Y"])
    sc = nt.nodes.new("ShaderNodeVectorMath"); sc.operation = "SCALE"; sc.inputs[3].default_value = 1 / spacing
    nt.links.new(comb.outputs[0], sc.inputs[0])
    fr = nt.nodes.new("ShaderNodeVectorMath"); fr.operation = "FRACTION"; nt.links.new(sc.outputs[0], fr.inputs[0])
    sub = nt.nodes.new("ShaderNodeVectorMath"); sub.operation = "SUBTRACT"; sub.inputs[1].default_value = (0.5, 0.5, 0)
    nt.links.new(fr.outputs[0], sub.inputs[0])
    ln = nt.nodes.new("ShaderNodeVectorMath"); ln.operation = "LENGTH"; nt.links.new(sub.outputs[0], ln.inputs[0])
    lt = nt.nodes.new("ShaderNodeMath"); lt.operation = "LESS_THAN"; lt.inputs[1].default_value = radius
    nt.links.new(ln.outputs["Value"], lt.inputs[0])
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"
    mix.inputs[6].default_value = (*lin(base), 1); mix.inputs[7].default_value = (*lin(hole), 1)
    nt.links.new(lt.outputs[0], mix.inputs[0]); nt.links.new(mix.outputs[2], b.inputs["Base Color"])
    return m


class Prop:
    def __init__(self, name, ox):
        self.col = P.collection(name)
        self.ox = ox

    def b(self, u):
        return Vector((self.ox + u[0], PARK_Y + u[2], u[1]))

    def box(self, name, c, s, m, bevel=0.003, segs=2):
        return P.box(self.col, name, self.b(c), (s[0], s[2], s[1]), m, bevel, segs)

    def cyl(self, name, c, r, depth, axis, m, seg=24, bevel=0.0, r2=None):
        return P.cyl(self.col, name, self.b(c), r, depth, {"x": "x", "y": "z", "z": "y"}[axis], m, seg, bevel, r2)

    def beam(self, name, a, b, w, d, m, bevel=0.002, segs=2):
        a, b = self.b(a), self.b(b)
        bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
        L = (b - a).length
        for v in bm.verts: v.co = Vector((v.co.x * w, v.co.y * d, v.co.z * L))
        rot = Vector((0, 0, 1)).rotation_difference((b - a).normalized()).to_matrix().to_4x4()
        bmesh.ops.transform(bm, verts=bm.verts, matrix=rot)
        bmesh.ops.translate(bm, verts=bm.verts, vec=(a + b) / 2)
        return P.finish(self.col, name, bm, m, bevel, segs)

    def tube(self, name, a, b, r, m, seg=12):
        a, b = self.b(a), self.b(b)
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=(b - a).length)
        rot = Vector((0, 0, 1)).rotation_difference((b - a).normalized()).to_matrix().to_4x4()
        bmesh.ops.transform(bm, verts=bm.verts, matrix=rot)
        bmesh.ops.translate(bm, verts=bm.verts, vec=(a + b) / 2)
        return P.finish(self.col, name, bm, m)

    def side_profile(self, name, pts_zy, x0, x1, m, bevel=0.002):
        """Extrudes a closed (z, y) outline along x."""
        return P.profile_extrude(self.col, name, [(PARK_Y + z, y) for z, y in pts_zy], self.ox + x0, self.ox + x1, m, bevel)

    def ellipse_ring(self, name, c, ro, ri, h, m, seg=40):
        """A flat elliptical ring in the x-z plane; ro and ri are (rx, rz)."""
        bm = bmesh.new()
        rings = []
        for rx, rz in (ro, ri):
            for dy in (0, h):
                rings.append([bm.verts.new(self.b((c[0] + rx * math.cos(2 * math.pi * i / seg), c[1] + dy, c[2] + rz * math.sin(2 * math.pi * i / seg)))) for i in range(seg)])
        ob, ot, ib, it = rings
        for i in range(seg):
            j = (i + 1) % seg
            for a, b2 in ((ob, ot), (ot, it), (it, ib), (ib, ob)):
                bm.faces.new((a[i], a[j], b2[j], b2[i]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return P.finish(self.col, name, bm, m, 0.0015, 1)


# ================================ 06 Hand-wash station (sink root) ================================
CERAMIC = mat("Sink_Ceramic", (0.97, 0.97, 0.96), 0.12)
CABINET = mat("Sink_Cabinet", (0.82, 0.84, 0.83), 0.5)
COUNTER = mat("Sink_Counter", (0.78, 0.80, 0.80), 0.35)
SPLASH = mat("Sink_Splashback", (0.96, 0.96, 0.96), 0.25)
DISPENSER = mat("Sink_Dispenser", (0.97, 0.97, 0.97), 0.3)
TAP = mat("Sink_Tap", (0.82, 0.83, 0.85), 0.2, 1.0)
GAP = mat("Sink_Gap", (0.15, 0.15, 0.16), 0.6)
s = Prop("Hand Wash Station", 14.0)
s.box("Sink Cabinet", (0, 0.58, -0.02), (1.0, 0.52, 0.5), CABINET, bevel=0.006)
s.box("Sink Drawer Front", (0, 0.565, -0.272), (0.97, 0.46, 0.008), CABINET, bevel=0.003)
s.box("Sink Drawer Gap", (0, 0.8, -0.271), (0.97, 0.006, 0.006), GAP, bevel=0)
s.box("Sink Counter", (0, 0.865, 0), (1.05, 0.05, 0.6), COUNTER, bevel=0.008)
s.ellipse_ring("Sink Basin Rim", (0, 0.889, -0.03), (0.25, 0.18), (0.215, 0.145), 0.007, CERAMIC)
bm = bmesh.new()
ret = bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=16, radius=1.0)
for v in list(bm.verts):
    if v.co.z > 0.0001:
        bm.verts.remove(v)
for v in bm.verts:
    v.co = Vector((v.co.x * 0.215, v.co.y * 0.145, v.co.z * 0.11)) + s.b((0, 0.894, -0.03))
bmesh.ops.reverse_faces(bm, faces=bm.faces)
bowl = P.finish(s.col, "Sink Bowl", bm, CERAMIC)
s.cyl("Sink Drain", (0, 0.787, -0.03), 0.02, 0.004, "y", TAP, seg=20)
# Long-lever mixer tap at the back of the basin.
s.cyl("Sink Tap Base", (0, 0.9, 0.18), 0.022, 0.02, "y", TAP, seg=24, bevel=0.003)
s.cyl("Sink Tap Body", (0, 0.99, 0.18), 0.014, 0.18, "y", TAP, seg=20)
s.tube("Sink Tap Spout", (0, 1.08, 0.18), (0, 1.1, 0.05), 0.011, TAP)
s.cyl("Sink Tap Nozzle", (0, 1.085, 0.05), 0.012, 0.03, "y", TAP, seg=16)
s.tube("Sink Tap Lever", (0.012, 0.96, 0.18), (0.13, 0.99, 0.18), 0.007, TAP)
s.box("Sink Splashback", (0, 1.33, 0.29), (1.6, 0.86, 0.03), SPLASH, bevel=0.004)
# Soap dispenser and paper towel dispenser, below the step cards.
s.box("Sink Soap Body", (-0.42, 1.08, 0.235), (0.1, 0.17, 0.08), DISPENSER, bevel=0.02, segs=3)
s.box("Sink Soap Push", (-0.42, 1.02, 0.191), (0.07, 0.04, 0.012), DISPENSER, bevel=0.006)
s.cyl("Sink Soap Window", (-0.42, 1.11, 0.194), 0.01, 0.003, "z", GAP, seg=16)
s.box("Sink Towel Body", (0.45, 1.1, 0.225), (0.24, 0.22, 0.11), DISPENSER, bevel=0.025, segs=3)
s.box("Sink Towel Slot", (0.45, 0.989, 0.2), (0.17, 0.004, 0.02), GAP, bevel=0)
s.box("Sink Towel Sheet", (0.45, 0.975, 0.198), (0.15, 0.03, 0.002), CERAMIC, bevel=0)
s.box("Sink Towel Window", (0.45, 1.12, 0.169), (0.006, 0.05, 0.002), GAP, bevel=0)

# ================================ 08 Picking bin (origin at bin centre; 0.4 x 0.22 x 0.36) ================================
BIN = mat("Bin_Plastic", (0.93, 0.93, 0.93), 0.45)          # baked light; the station tints it per bin
bn = Prop("Picking Bin", 15.4)
W, H, D, T = 0.4, 0.22, 0.36, 0.01
bn.box("Bin Floor", (0, -H / 2 + T / 2, 0), (W, T, D), BIN, bevel=0.002)
bn.box("Bin Back", (0, 0, D / 2 - T / 2), (W, H, T), BIN, bevel=0.002)
bn.box("Bin Hang Lip", (0, H / 2 - 0.005, D / 2 + 0.01), (W - 0.04, 0.012, 0.02), BIN, bevel=0.002)
side = [(D / 2, H / 2), (-0.06, H / 2), (-D / 2, 0.0), (-D / 2, -H / 2), (D / 2, -H / 2)]
for x in (-W / 2, W / 2 - T):
    bn.side_profile("Bin Side", side, x, x + T, BIN)
    for z in (-0.08, 0.06):
        bn.box("Bin Rib", (x + (T if x > 0 else 0), -0.01, z), (0.006, 0.17, 0.012), BIN, bevel=0.001)
    bn.box("Bin Rail", (x + (T if x > 0 else 0), 0.06, 0.06), (0.006, 0.012, 0.2), BIN, bevel=0.001)
bn.box("Bin Front", (0, -H / 2 + 0.05, -D / 2 + T / 2), (W, 0.1, T), BIN, bevel=0.002)
bn.box("Bin Label Holder", (0, -H / 2 + 0.05, -D / 2 - 0.002), (0.32, 0.085, 0.004), BIN, bevel=0.002)
bn.side_profile("Bin Toe", [(-D / 2, -H / 2), (-D / 2 + 0.03, -H / 2 - 0.012), (D / 2 - 0.03, -H / 2 - 0.012), (D / 2, -H / 2)], -W / 2 + 0.01, W / 2 - 0.01, BIN)

# ================================ 08 Shelving (shelving root) ================================
YELLOW = mat("Rack_Yellow", (0.96, 0.80, 0.08), 0.4)
GALV = mat("Rack_Galvanised", (0.72, 0.74, 0.76), 0.35, 0.8)
HOLES = mat("Rack_Holes", (0.08, 0.08, 0.08), 0.8)
sh = Prop("Shelving", 16.2)
TOP = 1.6
for x in (-0.72, 0.72):
    for z in (-0.2, 0.2):
        sh.box("Rack Post", (x, TOP / 2, z), (0.05, TOP, 0.05), YELLOW, bevel=0.004)
        sh.box("Rack Foot", (x, 0.004, z), (0.09, 0.008, 0.09), YELLOW, bevel=0.002)
        for i in range(24):
            sh.box("Rack Hole", (x, 0.08 + i * 0.062, z - 0.0255), (0.012, 0.028, 0.002), HOLES, bevel=0)
    for y0, y1 in ((0.1, 0.55), (0.65, 1.05), (1.15, 1.55)):
        sh.beam("Rack Brace", (x, y0, -0.18), (x, y1, 0.18), 0.03, 0.02, YELLOW)
    for y in (0.1, 0.6, 1.1, 1.55):
        sh.box("Rack Spacer", (x, y, 0), (0.03, 0.025, 0.36), YELLOW, bevel=0.002)
for y in (0.12, 0.62, 1.12, 1.58):
    for z in (-0.2, 0.2):
        sh.box("Rack Beam", (0, y - 0.02, z), (1.39, 0.06, 0.035), GALV, bevel=0.003)
    sh.box("Rack Deck", (0, y + 0.0, 0), (1.39, 0.012, 0.42), GALV, bevel=0.002)
    for i in range(7):
        sh.box("Rack Deck Rib", (-0.6 + i * 0.2, y + 0.007, 0), (0.008, 0.004, 0.4), GALV, bevel=0)

# ================================ 13 MIG torch (origin at the nozzle tip, nozzle pointing down) ================================
COPPER = mat("Torch_Copper", (0.80, 0.48, 0.30), 0.35, 1.0)
CHROME = mat("Torch_Chrome", (0.80, 0.81, 0.83), 0.2, 1.0)
GRIP = mat("Torch_Grip", (0.07, 0.07, 0.08), 0.65)
BRASS = mat("Torch_Brass", (0.82, 0.66, 0.32), 0.3, 1.0)
t = Prop("MIG Torch", 17.6)
t.cyl("Torch Contact Tip", (0, 0.004, 0), 0.0045, 0.012, "y", BRASS, seg=12)
t.cyl("Torch Nozzle", (0, 0.04, 0), 0.0135, 0.07, "y", COPPER, seg=24, r2=0.015)
t.cyl("Torch Nozzle Heat", (0, 0.006, 0), 0.0122, 0.012, "y", mat("Torch_Heat", (0.35, 0.28, 0.32), 0.4, 1.0), seg=24, r2=0.0127)
# Gooseneck: a 40-degree bend away from the camera, then the handle along that line.
R, A = 0.08, math.radians(40)
pts = [(0, 0.075 + R * math.sin(A * i / 8), R * (1 - math.cos(A * i / 8))) for i in range(9)]
for a, b2 in zip(pts, pts[1:]):
    t.tube("Torch Gooseneck", a, b2, 0.0085, CHROME)
d = Vector((0, math.cos(A), math.sin(A)))
start = Vector(pts[-1])
p = lambda k: tuple(start + d * k)
t.tube("Torch Gooseneck", p(0), p(0.035), 0.0085, CHROME)
t.cyl("Torch Neck Nut", (0, 0, 0), 0.001, 0.001, "y", CHROME)  # placeholder removed below
bpy.data.objects.remove(bpy.data.objects["Torch Neck Nut"], do_unlink=True)
t.tube("Torch Neck Nut", p(0.03), p(0.045), 0.013, CHROME, seg=6)
t.beam("Torch Handle", p(0.045), p(0.2), 0.04, 0.046, GRIP, bevel=0.016, segs=3)
t.beam("Torch Trigger", tuple(Vector(p(0.07)) - Vector((0, -0.02, 0.026)).normalized() * 0.0), p(0.11), 0.016, 0.012, GRIP, bevel=0.003)
trig = bpy.data.objects["Torch Trigger"]
trig.data.transform(Matrix.Translation(Vector((0, -0.026, 0)) * 1.0))  # shifted toward the camera, under the grip
for k in (0.2, 0.215, 0.23, 0.245):
    t.tube("Torch Boot", p(k), p(k + 0.01), 0.019 - (k - 0.2) * 0.12, GRIP, seg=16)
t.tube("Torch Cable", p(0.25), tuple(Vector(p(0.25)) + Vector((0, 0.03, 0.12))), 0.012, GRIP, seg=12)

# ================================ 13 Welding table (table root; top surface at 0.865) ================================
TOPSTEEL = dot_material("Weld_TableTop", (0.20, 0.21, 0.22), (0.03, 0.03, 0.03), 0.1, 0.12, axes=("X", "Y"))
LEGGREY = mat("Weld_Legs", (0.42, 0.44, 0.46), 0.5, 0.2)
CLAMP = mat("Weld_Clamp", (0.70, 0.42, 0.25), 0.4, 1.0)
CABLE = mat("Weld_Cable", (0.05, 0.05, 0.05), 0.7)
wt = Prop("Welding Table", 18.4)
wt.box("Table Top", (0, 0.84, 0), (1.5, 0.05, 0.75), TOPSTEEL, bevel=0.004)
for x in (-0.68, 0.68):
    for z in (-0.31, 0.31):
        wt.box("Table Leg", (x, 0.43, z), (0.06, 0.77, 0.06), LEGGREY, bevel=0.004)
        wt.cyl("Table Foot", (x, 0.02, z), 0.018, 0.04, "y", LEGGREY, seg=6)
        wt.cyl("Table Foot Pad", (x, 0.004, z), 0.03, 0.008, "y", LEGGREY, seg=16)
for z in (-0.31, 0.31):
    wt.box("Table Rail", (0, 0.2, z), (1.3, 0.04, 0.04), LEGGREY, bevel=0.003)
for x in (-0.68, 0.68):
    wt.box("Table Rail", (x, 0.2, 0), (0.04, 0.04, 0.58), LEGGREY, bevel=0.003)
for i in range(11):
    wt.box("Table Slat", (-0.6 + i * 0.12, 0.228, 0), (0.05, 0.012, 0.62), LEGGREY, bevel=0.002)
# Ground clamp on the front edge, and its cable hanging down.
wt.box("Table Clamp Jaw", (-0.25, 0.82, -0.385), (0.03, 0.06, 0.02), CLAMP, bevel=0.003)
wt.beam("Table Clamp Handle", (-0.25, 0.8, -0.395), (-0.25, 0.68, -0.405), 0.02, 0.012, CLAMP)
pts = [(-0.25 + 0.06 * math.sin(i / 10 * math.pi), 0.68 - 0.3 * math.sin(i / 10 * math.pi / 2), -0.41) for i in range(11)]
for a, b2 in zip(pts, pts[1:]):
    wt.tube("Table Clamp Cable", a, b2, 0.006, CABLE, seg=8)

# ================================ 14 Checkout counter (counter root; top surface at 1.0) ================================
LAMINATE = mat("Counter_Laminate", (0.80, 0.82, 0.83), 0.5)
KICK = mat("Counter_Kick", (0.75, 0.76, 0.78), 0.3, 1.0)
OAK = bpy.data.materials["Bench_Butcherblock"]
cc = Prop("Checkout Counter", 19.8)
cc.box("Counter Body", (0, 0.5, 0), (1.8, 0.9, 0.55), LAMINATE, bevel=0.006)
cc.box("Counter Kick", (0, 0.05, 0), (1.81, 0.1, 0.56), KICK, bevel=0.002)
cc.box("Counter Top", (-0.15, 0.98, 0), (1.56, 0.04, 0.6), OAK, bevel=0.012, segs=3)
cc.box("Counter Top Nose", (-0.15, 0.975, -0.29), (1.56, 0.05, 0.04), OAK, bevel=0.02, segs=4)
# Bagging well at the right end: a lower shelf with a raised back lip.
cc.box("Counter Bag Shelf", (0.78, 0.86, 0.02), (0.28, 0.025, 0.52), LAMINATE, bevel=0.004)
cc.box("Counter Bag Lip", (0.78, 0.93, 0.27), (0.28, 0.12, 0.03), OAK, bevel=0.008)
cc.box("Counter Bag Wall", (0.925, 0.93, 0.02), (0.03, 0.14, 0.56), OAK, bevel=0.008)
cc.cyl("Counter Grommet", (0.2, 1.0005, 0.18), 0.03, 0.002, "y", GAP, seg=24)

# ================================ 14 Gondola shelf (shelf root; turned -25 degrees by the station) ================================
STEEL = mat("Gondola_Steel", (0.80, 0.82, 0.84), 0.45, 0.3)
PEG = dot_material("Gondola_Pegboard", (0.85, 0.86, 0.87), (0.25, 0.25, 0.27), 0.025, 0.15, axes=("X", "Z"))
RAIL = mat("Gondola_PriceRail", (0.92, 0.95, 0.97), 0.15)
COLORS = [mat("Product_Orange", hexc(0xE8761E), 0.4), mat("Product_Green", hexc(0x2E9E5B), 0.4),
          mat("Product_Violet", hexc(0x5A58EB), 0.4), mat("Product_Yellow", hexc(0xF2C200), 0.4)]
go = Prop("Gondola", 21.0)
for x in (-0.41, 0.41):
    go.box("Gondola Upright", (x, 0.9, 0.16), (0.04, 1.8, 0.04), STEEL, bevel=0.003)
    go.box("Gondola Foot", (x, 0.04, 0.0), (0.04, 0.08, 0.38), STEEL, bevel=0.003)
go.box("Gondola Back", (0, 0.95, 0.175), (0.8, 1.7, 0.01), PEG, bevel=0)
go.box("Gondola Base", (0, 0.11, -0.005), (0.84, 0.03, 0.37), STEEL, bevel=0.003)
go.box("Gondola Kick", (0, 0.055, -0.185), (0.84, 0.09, 0.01), STEEL, bevel=0.002)
random.seed(4)
levels = [0.125] + [y + 0.015 for y in (0.5, 1.0, 1.5)]
for li, base in enumerate(levels):
    if li > 0:
        go.box("Gondola Shelf", (0, base - 0.015, -0.005), (0.82, 0.02, 0.34), STEEL, bevel=0.003)
        for x in (-0.38, 0.38):
            go.beam("Gondola Bracket", (x, base - 0.025, 0.15), (x, base - 0.1, 0.13), 0.012, 0.02, STEEL)
    go.box("Gondola Price Rail", (0, base + 0.004, -0.18), (0.82, 0.035, 0.004), RAIL, bevel=0)
    x = -0.38
    while x < 0.36:
        kind = random.choice(["box", "box", "bottle", "can"])
        m = COLORS[random.randrange(4)]
        if kind == "box":
            w = random.choice([0.1, 0.12])
            hgt = random.choice([0.18, 0.22])
            for z in (-0.07, 0.06):
                go.box("Product Box", (x + w / 2, base + hgt / 2, z), (w - 0.006, hgt, 0.12), m, bevel=0.003)
            x += w
        elif kind == "bottle":
            for z in (-0.09, 0.0, 0.09):
                go.cyl("Product Bottle", (x + 0.035, base + 0.08, z), 0.032, 0.16, "y", m, seg=16, bevel=0.006)
                go.cyl("Product Bottle Neck", (x + 0.035, base + 0.18, z), 0.014, 0.04, "y", m, seg=12)
            x += 0.07
        else:
            for z in (-0.1, -0.03, 0.04, 0.11):
                go.cyl("Product Can", (x + 0.032, base + 0.056, z), 0.03, 0.11, "y", m, seg=16, bevel=0.003)
            x += 0.064

print({c: len(bpy.data.collections[c].objects) for c in
       ("Hand Wash Station", "Picking Bin", "Shelving", "MIG Torch", "Welding Table", "Checkout Counter", "Gondola")})
