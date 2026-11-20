"""Builds the Wave 1 props other than the forklift: Workbench, HeadsetCart, Headset, Badge, Padlock.

Each sits in its own collection, parked along Y = 6 in the .blend. Origins: on the floor at the prop's centre
(Workbench, HeadsetCart); the headset's base; the card centre (Badge); the padlock body centre (Padlock).
Sizes follow the samples' existing station code.
"""
import bpy, bmesh, math, importlib
from mathutils import Vector, Matrix, Euler
import props_lib as P
importlib.reload(P)


def lin(c): return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


STEEL_DARK = P.material("Bench_SteelGrey", lin((0.30, 0.32, 0.35)), 0.45, 0.2)
WOOD = bpy.data.materials.get("Bench_Butcherblock") or bpy.data.materials.new("Bench_Butcherblock")
PEG = bpy.data.materials.get("Bench_Pegboard") or bpy.data.materials.new("Bench_Pegboard")
TOOL_STEEL = P.material("Bench_ToolSteel", lin((0.80, 0.81, 0.83)), 0.25, 1.0)
GRIP_RED = P.material("Bench_GripRed", lin((0.75, 0.08, 0.06)), 0.45)
GRIP_BLACK = P.material("Bench_GripBlack", lin((0.06, 0.06, 0.06)), 0.6)
HANDLE_WOOD = P.material("Bench_HandleWood", lin((0.80, 0.62, 0.38)), 0.55)
TOOLBOX = P.material("Bench_ToolboxRed", lin((0.78, 0.11, 0.09)), 0.35, 0.2)
CART_GREY = P.material("Cart_Grey", lin((0.80, 0.81, 0.79)), 0.5)
CART_PERF = bpy.data.materials.get("Cart_Perforated") or bpy.data.materials.new("Cart_Perforated")
CART_TRAY = P.material("Cart_TrayGrey", lin((0.30, 0.32, 0.35)), 0.6)
FOAM = P.material("Cart_Foam", lin((0.09, 0.09, 0.10)), 0.9)
CASTER = P.material("Cart_Caster", lin((0.35, 0.36, 0.38)), 0.6)
CHROME = bpy.data.materials["Forklift_Chrome"]
LENS = bpy.data.materials["StackLight_Lens"]
HS_SHELL = P.material("Headset_Shell", lin((0.24, 0.25, 0.28)), 0.35)
HS_FACE = P.material("Headset_Fascia", lin((0.70, 0.72, 0.75)), 0.4)
HS_FOAM = P.material("Headset_Foam", lin((0.42, 0.43, 0.45)), 0.9)
HS_STRAP = P.material("Headset_Strap", lin((0.08, 0.08, 0.09)), 0.7)
HS_CAM = P.material("Headset_Camera", lin((0.02, 0.02, 0.02)), 0.15)
CARD = P.material("Badge_Card", (1, 1, 1), 0.5)
STRIPE = P.material("Badge_Stripe", (1, 1, 1), 0.5)
HOLDER = P.material("Badge_Holder", lin((0.86, 0.9, 0.93)), 0.08)
LANYARD = P.material("Badge_Lanyard", lin((0.09, 0.14, 0.36)), 0.8)
LOCK_BODY = P.material("Padlock_Body", (1, 1, 1), 0.3)
TAG = P.material("Padlock_Tag", lin((0.96, 0.96, 0.95)), 0.6)
TAG_RED = P.material("Padlock_TagHeader", lin((0.83, 0.18, 0.18)), 0.6)
BRASS = P.material("Padlock_Brass", lin((0.78, 0.6, 0.28)), 0.3, 1.0)
TIE = P.material("Padlock_CableTie", lin((0.04, 0.04, 0.04)), 0.5)


def dot_material(m, base, hole, spacing, radius, axes=("X", "Z")):
    """A sheet with a grid of round holes in object space (pegboard, perforated doors)."""
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        if n.type not in ("BSDF_PRINCIPLED", "OUTPUT_MATERIAL"):
            nt.nodes.remove(n)
    b = P.principled(m)
    b.inputs["Roughness"].default_value = 0.6
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
    mix.inputs[6].default_value = (*base, 1); mix.inputs[7].default_value = (*hole, 1)
    nt.links.new(lt.outputs[0], mix.inputs[0]); nt.links.new(mix.outputs[2], b.inputs["Base Color"])


dot_material(PEG, lin((0.93, 0.93, 0.92)), lin((0.18, 0.18, 0.19)), 0.025, 0.18)
dot_material(CART_PERF, lin((0.80, 0.81, 0.79)), lin((0.22, 0.23, 0.24)), 0.018, 0.22)

# Butcher-block: staves along X with grain and colour variation.
WOOD.use_nodes = True
nt = WOOD.node_tree
for n in list(nt.nodes):
    if n.type not in ("BSDF_PRINCIPLED", "OUTPUT_MATERIAL"):
        nt.nodes.remove(n)
b = P.principled(WOOD); b.inputs["Roughness"].default_value = 0.55
tc = nt.nodes.new("ShaderNodeTexCoord")
mp = nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (2.0, 22.0, 2.0)
nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
stave = nt.nodes.new("ShaderNodeTexVoronoi"); stave.feature = "F1"; stave.inputs["Scale"].default_value = 1.0
nt.links.new(mp.outputs[0], stave.inputs["Vector"])
grain = nt.nodes.new("ShaderNodeTexWave"); grain.wave_type = "BANDS"; grain.bands_direction = "Y"
grain.inputs["Scale"].default_value = 14; grain.inputs["Distortion"].default_value = 6
nt.links.new(tc.outputs["Object"], grain.inputs["Vector"])
ramp = nt.nodes.new("ShaderNodeValToRGB")
ramp.color_ramp.elements[0].color = (*lin((0.70, 0.48, 0.26)), 1)
ramp.color_ramp.elements[1].color = (*lin((0.86, 0.66, 0.40)), 1)
nt.links.new(stave.outputs["Color"], ramp.inputs["Fac"])
mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"; mix.inputs[0].default_value = 0.18
nt.links.new(ramp.outputs["Color"], mix.inputs[6]); nt.links.new(grain.outputs["Color"], mix.inputs[7])
nt.links.new(mix.outputs[2], b.inputs["Base Color"])


# ================= Workbench (04): top 1.6 x 0.65 at 0.91, pegboard behind (+Y), toolbox separate =================
col = P.collection("Workbench")
OY = 6.0
W, D, TOP = 1.6, 0.65, 0.91
P.box(col, "Bench Top", (0, OY, TOP - 0.03), (W, D, 0.06), WOOD, bevel=0.004)
for x in (-0.74, 0.74):
    for y in (-0.28, 0.28):
        P.box(col, "Bench Leg", (x, OY + y, (TOP - 0.06) / 2), (0.05, 0.05, TOP - 0.06), STEEL_DARK, bevel=0.003)
        P.cyl(col, "Bench Foot", (x, OY + y, 0.012), 0.02, 0.024, "z", GRIP_BLACK, seg=12)
    for z0, z1 in ((0.18, 0.78), (0.78, 0.18)):
        a, b2 = Vector((x, OY - 0.27, z0)), Vector((x, OY + 0.27, z1))
        bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
        L = (b2 - a).length
        for v in bm.verts: v.co = Vector((v.co.x * 0.025, v.co.y * L, v.co.z * 0.035))
        ang = math.atan2((b2 - a).z, (b2 - a).y)
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(ang, 3, "X"))
        bmesh.ops.translate(bm, verts=bm.verts, vec=(a + b2) / 2)
        P.finish(col, "Bench Brace", bm, STEEL_DARK, bevel=0.002)
    P.box(col, "Bench Rail", (x, OY, TOP - 0.1), (0.04, 0.56, 0.05), STEEL_DARK, bevel=0.003)
for y in (-0.28, 0.28):
    P.box(col, "Bench Rail", (0, OY + y, TOP - 0.1), (1.44, 0.04, 0.06), STEEL_DARK, bevel=0.003)
P.box(col, "Bench Shelf", (0, OY, 0.2), (1.52, 0.6, 0.025), STEEL_DARK, bevel=0.003)
P.box(col, "Bench Pegboard", (0, OY + 0.3, TOP + 0.4), (W, 0.02, 0.8), PEG, bevel=0.002)
for x in (-0.79, 0.79):
    P.box(col, "Bench Pegboard Post", (x, OY + 0.31, TOP + 0.4), (0.03, 0.03, 0.84), STEEL_DARK, bevel=0.003)
# Tools on the pegboard: five wrenches, a hammer, five screwdrivers.
for i in range(5):
    x = -0.66 + i * 0.07
    L = 0.16 + i * 0.025
    P.box(col, "Bench Wrench", (x, OY + 0.28, TOP + 0.62 - L / 2), (0.016, 0.006, L), TOOL_STEEL, bevel=0.002)
    P.cyl(col, "Bench Wrench Head", (x, OY + 0.28, TOP + 0.63), 0.02, 0.006, "y", TOOL_STEEL, seg=16)
P.box(col, "Bench Hammer Handle", (-0.22, OY + 0.28, TOP + 0.47), (0.025, 0.02, 0.28), HANDLE_WOOD, bevel=0.004)
P.box(col, "Bench Hammer Head", (-0.22, OY + 0.27, TOP + 0.62), (0.11, 0.03, 0.03), TOOL_STEEL, bevel=0.004)
for i in range(5):
    x = 0.0 + i * 0.045
    P.cyl(col, "Bench Driver Grip", (x, OY + 0.27, TOP + 0.6), 0.012, 0.09, "z", GRIP_RED if i % 2 == 0 else GRIP_BLACK, seg=10, bevel=0.002)
    P.cyl(col, "Bench Driver Shaft", (x, OY + 0.27, TOP + 0.49), 0.004, 0.13, "z", TOOL_STEEL, seg=8)
# Toolbox (separate object so 05 can drop it).
TB = (0.45, OY + 0.05, TOP)
P.box(col, "Toolbox Body", (TB[0], TB[1], TB[2] + 0.07), (0.42, 0.22, 0.14), TOOLBOX, bevel=0.006)
P.box(col, "Toolbox Lid", (TB[0], TB[1], TB[2] + 0.155), (0.43, 0.225, 0.03), TOOLBOX, bevel=0.008)
P.box(col, "Toolbox Handle", (TB[0], TB[1], TB[2] + 0.2), (0.16, 0.02, 0.015), TOOLBOX, bevel=0.004)
for x in (-0.08, 0.08):
    P.box(col, "Toolbox Handle", (TB[0] + x, TB[1], TB[2] + 0.18), (0.012, 0.015, 0.04), TOOLBOX, bevel=0.003)
P.box(col, "Toolbox Latch", (TB[0], TB[1] - 0.114, TB[2] + 0.13), (0.04, 0.006, 0.03), TOOL_STEEL, bevel=0.002)

# ================= Headset cart (10, 15): cabinet 1.0 x 0.82 x 0.5, tray top at 0.90, four bays =================
col = P.collection("Headset Cart")
CX = 2.0
P.box(col, "Cart Cabinet", (CX, OY, 0.47), (1.0, 0.5, 0.78), CART_GREY, bevel=0.012)
P.box(col, "Cart Door Left", (CX - 0.245, OY - 0.252, 0.47), (0.47, 0.008, 0.7), CART_GREY, bevel=0.003)
P.box(col, "Cart Door Right", (CX + 0.245, OY - 0.252, 0.47), (0.47, 0.008, 0.7), CART_PERF, bevel=0.003)
P.cyl(col, "Cart Lock", (CX - 0.03, OY - 0.258, 0.6), 0.01, 0.01, "y", CHROME, seg=12)
P.box(col, "Cart Tray", (CX, OY, 0.885), (1.04, 0.54, 0.03), CART_TRAY, bevel=0.01)
P.box(col, "Cart Tray Rim", (CX, OY - 0.26, 0.905), (1.04, 0.02, 0.02), CART_TRAY, bevel=0.004)
P.box(col, "Cart Tray Rim", (CX, OY + 0.26, 0.905), (1.04, 0.02, 0.02), CART_TRAY, bevel=0.004)
for i in range(4):
    x = CX - 0.36 + i * 0.24
    bm = bmesh.new()
    # A foam cradle: a raised ring shaped like the headset's outline.
    outer, inner = [], []
    for k in range(28):
        a = 2 * math.pi * k / 28
        ox, oy = 0.105 * math.cos(a), 0.09 * math.sin(a) * (1.1 if math.sin(a) > 0 else 0.9)
        outer.append(bm.verts.new((x + ox, OY + oy, 0.9)))
        outer.append(None)
    outer = [v for v in outer if v]
    top_o = [bm.verts.new((v.co.x, v.co.y, 0.935)) for v in outer]
    top_i = [bm.verts.new((x + (v.co.x - x) * 0.72, OY + (v.co.y - OY) * 0.7, 0.935)) for v in outer]
    bot_i = [bm.verts.new((v.co.x, v.co.y, 0.912)) for v in top_i]
    n = len(outer)
    for k in range(n):
        j = (k + 1) % n
        bm.faces.new((outer[k], outer[j], top_o[j], top_o[k]))
        bm.faces.new((top_o[k], top_o[j], top_i[j], top_i[k]))
        bm.faces.new((top_i[k], top_i[j], bot_i[j], bot_i[k]))
    bm.faces.new(list(reversed(bot_i)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    P.finish(col, "Cart Cradle", bm, FOAM, bevel=0.004, angle=50)
    P.cyl(col, f"Bay Light {i + 1}", (x + 0.085, OY - 0.245, 0.888), 0.01, 0.008, "y", LENS, seg=16)
# Handle on the right end: two uprights and a top bar, as a tube loop. Then casters.
for y in (-0.2, 0.2):
    P.cyl(col, "Cart Handle Upright", (CX + 0.56, OY + y, 0.81), 0.016, 0.38, "z", CART_GREY, seg=16)
    P.box(col, "Cart Handle Bracket", (CX + 0.525, OY + y, 0.66), (0.06, 0.03, 0.04), CART_GREY, bevel=0.006)
P.cyl(col, "Cart Handle Bar", (CX + 0.56, OY, 1.0), 0.016, 0.43, "y", CART_GREY, seg=16)
for x in (-0.42, 0.42):
    for y in (-0.19, 0.19):
        P.box(col, "Cart Caster Fork", (CX + x, OY + y, 0.065), (0.05, 0.06, 0.05), CASTER, bevel=0.005)
        P.cyl(col, "Cart Caster Wheel", (CX + x, OY + y, 0.04), 0.04, 0.03, "x", HS_STRAP, seg=20, bevel=0.004)

# ================= Headset: generic halo-band design, about 0.19 wide; origin at its base, front -Y =================
col = P.collection("Headset")
HX = 3.4
P.box(col, "Headset Visor", (HX, OY, 0.055), (0.19, 0.105, 0.1), HS_SHELL, bevel=0.03, segs=4)
P.box(col, "Headset Fascia", (HX, OY - 0.051, 0.058), (0.16, 0.008, 0.07), HS_FACE, bevel=0.012, segs=3)
for x, z in ((-0.072, 0.09), (0.072, 0.09), (-0.072, 0.025), (0.072, 0.025)):
    P.cyl(col, "Headset Camera", (HX + x, OY - 0.053, z), 0.006, 0.004, "y", HS_CAM, seg=12)
P.box(col, "Headset Face Cushion", (HX, OY + 0.06, 0.05), (0.17, 0.02, 0.085), HS_FOAM, bevel=0.009)
# Halo: a rigid ring around the wearer's head, from the back of the face cushion to a rear cradle.
bm = bmesh.new()
seg, tube_w, tube_h = 32, 0.03, 0.012
rx, ry, cz = 0.085, 0.11, 0.1
cy = OY + 0.07 + ry
ring = []
for i in range(seg):
    a = 2 * math.pi * i / seg
    for dz in (-tube_w / 2, tube_w / 2):
        for dr in (0, tube_h):
            ring.append(bm.verts.new((HX + (rx + dr) * math.cos(a), cy + (ry + dr) * math.sin(a), cz + dz)))
for i in range(seg):
    j = (i + 1) % seg
    q = [i * 4 + k for k in range(4)]; r = [j * 4 + k for k in range(4)]
    for (a1, a2) in ((0, 1), (1, 3), (3, 2), (2, 0)):
        bm.faces.new((ring[q[a1]], ring[r[a1]], ring[r[a2]], ring[q[a2]]))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
P.finish(col, "Headset Halo", bm, HS_STRAP, bevel=0.003)
P.box(col, "Headset Rear Pad", (HX, OY + 0.3, 0.1), (0.1, 0.025, 0.06), HS_FOAM, bevel=0.01)
P.cyl(col, "Headset Dial", (HX, OY + 0.32, 0.1), 0.022, 0.018, "y", HS_SHELL, seg=24, bevel=0.003)
P.cyl(col, "Headset Status Light", (HX + 0.096, OY - 0.02, 0.04), 0.004, 0.003, "x", LENS, seg=10)

# ================= Badge (10, 18): card 0.24 x 0.17 in a clear holder, stripe separate, lanyard loop =================
col = P.collection("Badge")
BX = 4.2
BZ = 1.0
P.box(col, "Badge Card", (BX, OY, BZ), (0.24, 0.004, 0.17), CARD, bevel=0.002)
P.box(col, "Badge Stripe", (BX, OY - 0.0025, BZ + 0.06), (0.24, 0.001, 0.04), STRIPE, bevel=0)
for z in (-0.0905, 0.0905):
    P.box(col, "Badge Holder Rim", (BX, OY, BZ + z), (0.252, 0.008, 0.011), HOLDER, bevel=0.002)
for x in (-0.1265, 0.1265):
    P.box(col, "Badge Holder Rim", (BX + x, OY, BZ), (0.011, 0.008, 0.19), HOLDER, bevel=0.002)
P.box(col, "Badge Clip", (BX, OY, BZ + 0.105), (0.03, 0.01, 0.025), CHROME, bevel=0.003)
P.cyl(col, "Badge Clip Ring", (BX, OY, BZ + 0.125), 0.008, 0.004, "y", CHROME, seg=16)
for side in (-1, 1):
    P.box(col, "Badge Lanyard", (BX + side * 0.012, OY + 0.002, BZ + 0.185), (0.02, 0.002, 0.11), LANYARD, bevel=0)

# ================= Padlock and tag (03, 16): body 0.08 x 0.09 x 0.04, tag 0.11 x 0.18 to the right =================
col = P.collection("Padlock")
LX = 4.8
LZ = 1.0
P.box(col, "Padlock Body", (LX, OY, LZ), (0.08, 0.04, 0.09), LOCK_BODY, bevel=0.008, segs=3)
P.box(col, "Padlock Face", (LX, OY - 0.0205, LZ), (0.06, 0.002, 0.07), LOCK_BODY, bevel=0.003)
bm = bmesh.new()
for i in range(13):
    a = math.pi * i / 12
    for dx in (0,):
        pass
pts = [(0.022 * math.cos(math.pi * i / 12), 0.03 + 0.022 * math.sin(math.pi * i / 12)) for i in range(13)]
path = [(0.022, -0.015), (0.022, 0.03)] + pts[1:] + [(-0.022, 0.03), (-0.022, -0.015)]
P.box(col, "Padlock Shackle Left", (LX - 0.022, OY, LZ + 0.045 + 0.04), (0.008, 0.008, 0.08), CHROME, bevel=0.003)
P.box(col, "Padlock Shackle Right", (LX + 0.022, OY, LZ + 0.045 + 0.04), (0.008, 0.008, 0.08), CHROME, bevel=0.003)
bm = bmesh.new()
segs = 16
verts = []
for i in range(segs + 1):
    a = math.pi * i / segs
    for dr in (-0.004, 0.004):
        for dy in (-0.004, 0.004):
            verts.append(bm.verts.new((LX + (0.022 + dr) * math.cos(a), OY + dy, LZ + 0.125 + (0.022 + dr) * math.sin(a))))
for i in range(segs):
    q = [i * 4 + k for k in range(4)]; r = [(i + 1) * 4 + k for k in range(4)]
    for (a1, a2) in ((0, 1), (1, 3), (3, 2), (2, 0)):
        bm.faces.new((verts[q[a1]], verts[r[a1]], verts[r[a2]], verts[q[a2]]))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
P.finish(col, "Padlock Shackle Arc", bm, CHROME)
# Tag: blank white with a red header band; live text goes on its face (-Y).
TX, TZ = LX + 0.13, LZ - 0.04
P.box(col, "Padlock Tag", (TX, OY, TZ), (0.11, 0.003, 0.18), TAG, bevel=0.001)
P.box(col, "Padlock Tag Header", (TX, OY - 0.0018, TZ + 0.07), (0.11, 0.0006, 0.04), TAG_RED, bevel=0)
P.cyl(col, "Padlock Tag Grommet", (TX, OY - 0.002, TZ + 0.075), 0.007, 0.002, "y", BRASS, seg=16)
bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
a, b2 = Vector((TX, OY, TZ + 0.075)), Vector((LX + 0.022, OY, LZ + 0.12))
Lt = (b2 - a).length
for v in bm.verts: v.co = Vector((v.co.x * Lt, v.co.y * 0.003, v.co.z * 0.005))
bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.atan2((b2 - a).z, (b2 - a).x), 3, "Y").inverted())
bmesh.ops.translate(bm, verts=bm.verts, vec=(a + b2) / 2)
P.finish(col, "Padlock Cable Tie", bm, TIE)
print({c: len(bpy.data.collections[c].objects) for c in ("Workbench", "Headset Cart", "Headset", "Badge", "Padlock")})
