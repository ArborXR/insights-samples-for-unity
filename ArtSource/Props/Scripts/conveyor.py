"""Builds the belt conveyor (Conveyor.fbx). Run inside Blender with props_lib on sys.path.

Layout matches Conveyor.cs: belt along X, ModelLength 2.6 m, belt top 0.725 m, cartons ride on +X,
head (drive) end at +X, station front at -Y.
"""
import bpy, bmesh, math, importlib
from mathutils import Vector, Matrix, Euler
import props_lib as P
importlib.reload(P)

L = 2.6
BELT_TOP = 0.725
DRUM_R = 0.045
DRUM_Z = BELT_TOP - DRUM_R
BELT_W = 0.46
PLATE_IN, PLATE_OUT = 0.235, 0.265
PLATE_Z0, PLATE_Z1 = 0.585, 0.715
HEAD = L / 2

# Colours are the linear equivalents of the Unity materials (sRGB), so baked albedo matches the e-stop.
def lin(c): return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)
FRAME = P.material("Conveyor_FramePaint", lin((0.33, 0.35, 0.39)), 0.5)
STEEL = P.material("Conveyor_Steel", lin((0.78, 0.79, 0.81)), 0.36, 1.0)
YELLOW = P.material("Conveyor_GuardYellow", lin((0.95, 0.73, 0.09)), 0.5)
MOTOR = P.material("Conveyor_MotorPaint", lin((0.21, 0.22, 0.24)), 0.55)
RUBBER = P.material("Conveyor_Rubber", lin((0.06, 0.06, 0.06)), 0.85)
DARK = P.material("Conveyor_DarkPlastic", lin((0.10, 0.10, 0.11)), 0.6)
BELT = bpy.data.materials["Conveyor_Belt"]
LENS = bpy.data.materials["StackLight_Lens"]

# Perforated guard: yellow sheet with a dot grid in object space, baked into the albedo.
PERF = bpy.data.materials.get("Conveyor_GuardPerforated") or bpy.data.materials.new("Conveyor_GuardPerforated")
PERF.use_nodes = True
nt = PERF.node_tree
for n in list(nt.nodes):
    if n.type not in ("BSDF_PRINCIPLED", "OUTPUT_MATERIAL"):
        nt.nodes.remove(n)
b = P.principled(PERF)
b.inputs["Roughness"].default_value = 0.5
coord = nt.nodes.new("ShaderNodeTexCoord")
sep = nt.nodes.new("ShaderNodeSeparateXYZ")
nt.links.new(coord.outputs["Object"], sep.inputs[0])
comb = nt.nodes.new("ShaderNodeCombineXYZ")
nt.links.new(sep.outputs["X"], comb.inputs["X"])
nt.links.new(sep.outputs["Z"], comb.inputs["Y"])
scale = nt.nodes.new("ShaderNodeVectorMath"); scale.operation = "SCALE"; scale.inputs[3].default_value = 1 / 0.012
nt.links.new(comb.outputs[0], scale.inputs[0])
frac = nt.nodes.new("ShaderNodeVectorMath"); frac.operation = "FRACTION"
nt.links.new(scale.outputs[0], frac.inputs[0])
sub = nt.nodes.new("ShaderNodeVectorMath"); sub.operation = "SUBTRACT"; sub.inputs[1].default_value = (0.5, 0.5, 0)
nt.links.new(frac.outputs[0], sub.inputs[0])
length = nt.nodes.new("ShaderNodeVectorMath"); length.operation = "LENGTH"
nt.links.new(sub.outputs[0], length.inputs[0])
hole = nt.nodes.new("ShaderNodeMath"); hole.operation = "LESS_THAN"; hole.inputs[1].default_value = 0.3
nt.links.new(length.outputs["Value"], hole.inputs[0])
mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"
mix.inputs[6].default_value = (*lin((0.95, 0.73, 0.09)), 1)
mix.inputs[7].default_value = (0.01, 0.01, 0.01, 1)
nt.links.new(hole.outputs[0], mix.inputs[0])
nt.links.new(mix.outputs[2], b.inputs["Base Color"])

col = P.collection("Conveyor")


def belt():
    bm = bmesh.new()
    r, seg, w = DRUM_R + 0.004, 12, BELT_W
    pts = []
    for i in range(seg + 1):
        a = math.pi / 2 - math.pi * i / seg
        pts.append((HEAD + r * math.cos(a), DRUM_Z + r * math.sin(a)))
    for i in range(seg + 1):
        a = -math.pi / 2 - math.pi * i / seg
        pts.append((-HEAD + r * math.cos(a), DRUM_Z + r * math.sin(a)))
    front = [bm.verts.new((x, -w / 2, z)) for x, z in pts]
    back = [bm.verts.new((x, w / 2, z)) for x, z in pts]
    n = len(pts)
    lengths = [0.0]
    for i in range(n):
        lengths.append(lengths[-1] + (Vector(pts[(i + 1) % n]) - Vector(pts[i])).length)
    uv = bm.loops.layers.uv.new()
    for i in range(n):
        j = (i + 1) % n
        f = bm.faces.new((front[i], front[j], back[j], back[i]))
        for lp, idx in zip(f.loops, (i, i + 1, i + 1, i)):
            lp[uv].uv = (lengths[idx] / 0.5, (lp.vert.co.y + w / 2) / 0.5)
    for cap in (list(reversed(front)), back):
        f = bm.faces.new(cap)
        for lp in f.loops:
            lp[uv].uv = (lp.vert.co.x / 0.5, lp.vert.co.z / 0.5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = P.finish(col, "Belt", bm, BELT)
    for p in ob.data.polygons:
        p.use_smooth = abs(p.normal.z) < 0.99 and abs(p.normal.y) < 0.99
    return ob


def side_plate(sign):
    out, inn = PLATE_OUT * sign, PLATE_IN * sign
    g = 0.006 * sign
    z0, z1 = PLATE_Z0, PLATE_Z1
    prof = [(inn, z0), (out, z0), (out, 0.611), (out - g, 0.611), (out - g, 0.619), (out, 0.619),
            (out, 0.676), (out - g, 0.676), (out - g, 0.684), (out, 0.684), (out, z1), (inn, z1)]
    return P.profile_extrude(col, "Frame Plate", prof, -HEAD, HEAD, FRAME, bevel=0.002, angle=50)


def fin_ring(name, cx, cy, cz, radius, length, count, size, mat):
    """Axial cooling fins around an X axis."""
    for i in range(count):
        a = 2 * math.pi * i / count
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        for v in bm.verts:
            v.co = Vector((v.co.x * length, v.co.y * size[0], v.co.z * size[1] + radius))
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(a, 3, "X"))
        bmesh.ops.translate(bm, verts=bm.verts, vec=Vector((cx, cy, cz)))
        P.finish(col, name, bm, mat, bevel=0.0012)


belt()
for s in (-1, 1):
    side_plate(s)
    y_face = (PLATE_OUT + 0.006) * s
    for x in (-HEAD, HEAD):
        P.cyl(col, "Frame End Cap", (x, y_face, DRUM_Z), 0.055, 0.012, "y", FRAME, seg=32, bevel=0.003)
        P.cyl(col, "Frame Bearing", (x, (PLATE_OUT + 0.016) * s, DRUM_Z), 0.024, 0.012, "y", STEEL, seg=24, bevel=0.002)
        P.cyl(col, "Frame Shaft End", (x, (PLATE_OUT + 0.025) * s, DRUM_Z), 0.011, 0.01, "y", STEEL, seg=12)
    # Tail tensioner: slotted plate and take-up bolt.
    P.box(col, "Frame Tensioner Plate", (-HEAD + 0.1, (PLATE_OUT + 0.004) * s, DRUM_Z), (0.13, 0.006, 0.034), STEEL, bevel=0.0015)
    P.cyl(col, "Frame Tensioner Rod", (-HEAD + 0.19, (PLATE_OUT + 0.012) * s, DRUM_Z), 0.005, 0.1, "x", STEEL, seg=8)
    P.cyl(col, "Frame Tensioner Nut", (-HEAD + 0.235, (PLATE_OUT + 0.012) * s, DRUM_Z), 0.011, 0.012, "x", STEEL, seg=6, bevel=0.001)
    # Guide rail: flat bar on bolted posts.
    P.box(col, "Frame Guide Rail", (0, 0.252 * s, 0.805), (L + 0.08, 0.008, 0.032), STEEL, bevel=0.0015)
    for x in (-1.05, -0.35, 0.35, 1.05):
        P.box(col, "Frame Rail Post", (x, (PLATE_OUT + 0.003) * s, 0.715), (0.024, 0.006, 0.17), STEEL, bevel=0.001)
        P.box(col, "Frame Rail Clamp", (x, 0.26 * s, 0.792), (0.032, 0.022, 0.018), STEEL, bevel=0.0015)
        P.cyl(col, "Frame Bolt", (x, (PLATE_OUT + 0.007) * s, 0.648), 0.007, 0.005, "y", STEEL, seg=6)

# Legs: square tube, top plate, levelling foot, H brace.
LEG_X, LEG_Y = HEAD - 0.16, 0.205
for x in (-LEG_X, LEG_X):
    for y in (-LEG_Y, LEG_Y):
        P.box(col, "Frame Leg", (x, y, (0.07 + PLATE_Z0) / 2), (0.05, 0.05, PLATE_Z0 - 0.07), FRAME, bevel=0.004)
        P.box(col, "Frame Leg Plate", (x, y, PLATE_Z0 - 0.004), (0.09, 0.075, 0.008), FRAME, bevel=0.002)
        P.box(col, "Frame Leg Cap", (x, y, 0.066), (0.052, 0.052, 0.008), DARK, bevel=0.002)
        P.cyl(col, "Frame Foot Nut", (x, y, 0.052), 0.016, 0.012, "z", STEEL, seg=6, bevel=0.001)
        P.cyl(col, "Frame Foot Stem", (x, y, 0.032), 0.009, 0.04, "z", STEEL, seg=10)
        P.cyl(col, "Frame Foot Base", (x, y, 0.016), 0.032, 0.008, "z", STEEL, seg=24, bevel=0.002)
        P.cyl(col, "Frame Foot Pad", (x, y, 0.006), 0.034, 0.012, "z", RUBBER, seg=24, bevel=0.003)
    P.box(col, "Frame Cross Brace", (x, 0, 0.17), (0.04, 2 * LEG_Y, 0.04), FRAME, bevel=0.003)

# Tail guard: yellow perforated sheet over the drum end, on stand-offs.
outline = P.stadium(-HEAD + 0.03, DRUM_Z, 0.07, 0.06, seg=12)
P.plate_xz(col, "Frame Tail Guard", outline, -(PLATE_OUT + 0.032), -(PLATE_OUT + 0.042), PERF, bevel=0.003)
for x in (-HEAD - 0.02, -HEAD + 0.08):
    P.cyl(col, "Frame Guard Standoff", (x, -(PLATE_OUT + 0.018), DRUM_Z), 0.008, 0.03, "y", STEEL, seg=8)

# Drive at the head end, front side: worm gearbox on the drum shaft, motor running back along the frame.
GY = -(PLATE_OUT + 0.06)
P.box(col, "Drive Gearbox", (HEAD, GY, DRUM_Z), (0.13, 0.095, 0.14), YELLOW, bevel=0.012, segs=3)
P.cyl(col, "Drive Output Boss", (HEAD, GY, DRUM_Z), 0.036, 0.115, "y", YELLOW, seg=32, bevel=0.004)
P.cyl(col, "Drive Output Cover", (HEAD, GY - 0.06, DRUM_Z), 0.028, 0.008, "y", DARK, seg=32, bevel=0.002)
for i in range(5):
    P.box(col, "Drive Gearbox Rib", (HEAD - 0.04 + i * 0.02, GY, DRUM_Z + 0.074), (0.007, 0.085, 0.012), YELLOW, bevel=0.0015)
for z in (DRUM_Z + 0.05, DRUM_Z - 0.05):
    for x in (HEAD - 0.045, HEAD + 0.045):
        P.cyl(col, "Drive Gearbox Bolt", (x, GY - 0.049, z), 0.006, 0.005, "y", STEEL, seg=6)
P.box(col, "Drive Torque Arm", (HEAD - 0.03, -(PLATE_OUT + 0.008), DRUM_Z - 0.085), (0.03, 0.012, 0.09), STEEL, bevel=0.0015)
MX0 = HEAD - 0.066
MZ = DRUM_Z - 0.012
P.cyl(col, "Drive Motor Flange", (MX0 - 0.01, GY, MZ), 0.064, 0.02, "x", YELLOW, seg=36, bevel=0.003)
P.cyl(col, "Drive Motor", (MX0 - 0.12, GY, MZ), 0.058, 0.2, "x", MOTOR, seg=36, bevel=0.004)
fin_ring("Drive Motor Fin", MX0 - 0.12, GY, MZ, 0.058, 0.18, 16, (0.005, 0.012), MOTOR)
P.cyl(col, "Drive Fan Cover", (MX0 - 0.245, GY, MZ), 0.062, 0.05, "x", MOTOR, seg=36, bevel=0.006)
P.cyl(col, "Drive Fan Grille", (MX0 - 0.271, GY, MZ), 0.05, 0.004, "x", DARK, seg=36)
P.box(col, "Drive Terminal Box", (MX0 - 0.1, GY, MZ + 0.083), (0.075, 0.075, 0.045), MOTOR, bevel=0.005)
P.cyl(col, "Drive Terminal Gland", (MX0 - 0.1, GY - 0.042, MZ + 0.08), 0.009, 0.012, "y", DARK, seg=12)
P.box(col, "Drive Nameplate", (MX0 - 0.13, GY - 0.0585, MZ), (0.05, 0.002, 0.026), STEEL, bevel=0)
# Local control box with the running lamp and a start button, on the front plate.
CX = HEAD - 0.55
P.box(col, "Drive Control Box", (CX, -(PLATE_OUT + 0.024), 0.66), (0.075, 0.044, 0.1), MOTOR, bevel=0.005)
P.cyl(col, "Drive Lamp Bezel", (CX, -(PLATE_OUT + 0.048), 0.68), 0.017, 0.006, "y", DARK, seg=24, bevel=0.001)
P.cyl(col, "Drive Start Button", (CX, -(PLATE_OUT + 0.05), 0.637), 0.013, 0.01, "y", DARK, seg=24, bevel=0.002)
P.cyl(col, "Running Lamp", (CX, -(PLATE_OUT + 0.055), 0.68), 0.013, 0.008, "y", LENS, seg=24, bevel=0.002)

print(len(col.objects), "objects,", P.triangles([o for o in col.objects]), "tris")
