"""Builds Carton.fbx (clean shipping carton, three print variants in one atlas) and CartonCrushed.fbx (the
Poly Haven cardboard_box_01 scan, CC0, reduced for Quest). Both are normalised to a 0.3 m cube; Conveyor.cs
scales them to the size asked for. Run headless: it bakes with Cycles.
"""
import bpy, bmesh, math, os, importlib, shutil, sys
import numpy as np
from mathutils import Vector, Matrix
import props_lib as P
importlib.reload(P)

HERE = os.path.dirname(P.__file__)
DECALS = os.path.join(HERE, "decals")
SCAN = os.path.join(HERE, "ph-box", "gltf", "cardboard_box_01_1k.gltf")
MARKER = os.path.join(HERE, "cartons.done")
if os.path.exists(MARKER):
    os.remove(MARKER)

S = 0.3
h = S / 2
OX, OZ = 0.0, 3.0  # parked above the other props in the .blend
VARIANTS = 3
COL_PX, ATLAS_H = 512, 512


def lin(c): return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


col = P.collection("Carton")

# ---- Clean carton geometry: softly bulging panels, rounded edges, raised tape over the flap seam. ----
bm = bmesh.new()
bmesh.ops.create_cube(bm, size=S)
bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=7, use_grid_fill=True)
for v in bm.verts:
    c = v.co
    for ax in range(3):
        if abs(abs(c[ax]) - h) < 1e-5:
            o = [i for i in range(3) if i != ax]
            bulge = 0.0035 * (1 - (c[o[0]] / h) ** 2) * (1 - (c[o[1]] / h) ** 2)
            c[ax] += math.copysign(bulge, c[ax])
bmesh.ops.translate(bm, verts=bm.verts, vec=Vector((OX, 0, OZ)))
kraft = bpy.data.materials.get("Carton_Kraft") or bpy.data.materials.new("Carton_Kraft")
body = P.finish(col, "Carton Body", bm, kraft, bevel=0.007, segs=3, angle=30)

tape_mat = P.material("Carton_Tape", lin((0.84, 0.72, 0.5)), 0.22)
# The tape follows the panel bulge and wraps the rounded edges, sitting just proud of the cardboard.
BEVEL, TW, WRAP, LIFT = 0.007, 0.026, 0.075, 0.0007
bulge = lambda a, b: 0.0035 * (1 - (a / h) ** 2) * (1 - (b / h) ** 2)
def tape_path():
    pts = []
    for i in range(6):  # down the -X side
        z = h - WRAP + (WRAP - BEVEL) * i / 5
        pts.append((-h - bulge(0, z) - LIFT, z))
    for i in range(1, 6):  # around the rounded edge
        a = math.pi - (math.pi / 2) * i / 5
        r = BEVEL + LIFT
        pts.append((-h + BEVEL + r * math.cos(a), h - BEVEL + r * math.sin(a)))
    for i in range(1, 24):  # across the top
        x = -h + BEVEL + (2 * (h - BEVEL)) * i / 24
        pts.append((x, h + bulge(x, 0) + LIFT))
    for i in range(0, 5):
        a = math.pi / 2 - (math.pi / 2) * i / 5
        r = BEVEL + LIFT
        pts.append((h - BEVEL + r * math.cos(a), h - BEVEL + r * math.sin(a)))
    for i in range(6):
        z = h - BEVEL - (WRAP - BEVEL) * i / 5
        pts.append((h + bulge(0, z) + LIFT, z))
    return pts
bm = bmesh.new()
rows = [[bm.verts.new((OX + x, y, OZ + z)) for y in (-TW, TW)] for x, z in tape_path()]
for a, b in zip(rows, rows[1:]):
    bm.faces.new((a[0], b[0], b[1], a[1]))
tape = P.finish(col, "Carton Tape", bm, tape_mat)
# Point every tape face outward from the box centre.
for poly in tape.data.polygons:
    if poly.normal.dot(poly.center - Vector((OX, 0, OZ))) < 0:
        poly.flip()

# ---- Kraft shader: fibre noise, faint flutes, printed decals switchable per variant. ----
nt = kraft.node_tree if kraft.use_nodes else None
kraft.use_nodes = True
nt = kraft.node_tree
for n in list(nt.nodes):
    if n.type not in ("BSDF_PRINCIPLED", "OUTPUT_MATERIAL"):
        nt.nodes.remove(n)
bsdf = P.principled(kraft)
bsdf.inputs["Roughness"].default_value = 0.88
N = nt.nodes.new
L = nt.links.new
tc = N("ShaderNodeTexCoord")
# Measured from the carton's centre, so decals land the same on the parked source and the baked copy.
local = N("ShaderNodeVectorMath"); local.operation = "SUBTRACT"; local.inputs[1].default_value = (OX, 0, OZ)
L(tc.outputs["Object"], local.inputs[0])
obj = N("ShaderNodeSeparateXYZ"); L(local.outputs[0], obj.inputs[0])
geo = N("ShaderNodeNewGeometry")
nrm = N("ShaderNodeSeparateXYZ"); L(geo.outputs["Normal"], nrm.inputs[0])

noise = N("ShaderNodeTexNoise"); noise.inputs["Scale"].default_value = 9; noise.inputs["Detail"].default_value = 4
L(tc.outputs["Object"], noise.inputs["Vector"])
ramp = N("ShaderNodeValToRGB")
ramp.color_ramp.elements[0].color = (*lin((0.70, 0.53, 0.35)), 1)
ramp.color_ramp.elements[1].color = (*lin((0.80, 0.62, 0.42)), 1)
L(noise.outputs["Fac"], ramp.inputs["Fac"])
fibre = N("ShaderNodeTexNoise"); fibre.inputs["Scale"].default_value = 160; fibre.inputs["Detail"].default_value = 2
L(tc.outputs["Object"], fibre.inputs["Vector"])
flute = N("ShaderNodeTexWave"); flute.wave_type = "BANDS"; flute.bands_direction = "X"; flute.inputs["Scale"].default_value = 70
L(tc.outputs["Object"], flute.inputs["Vector"])
mix_fibre = N("ShaderNodeMix"); mix_fibre.data_type = "RGBA"; mix_fibre.blend_type = "OVERLAY"; mix_fibre.inputs[0].default_value = 0.18
L(ramp.outputs["Color"], mix_fibre.inputs[6]); L(fibre.outputs["Color"], mix_fibre.inputs[7])
mix_flute = N("ShaderNodeMix"); mix_flute.data_type = "RGBA"; mix_flute.blend_type = "MULTIPLY"; mix_flute.inputs[0].default_value = 0.035
L(mix_fibre.outputs[2], mix_flute.inputs[6]); L(flute.outputs["Color"], mix_flute.inputs[7])
current = mix_flute.outputs[2]

FACES = {  # plane coords (a, b) and the normal test for each face
    "front": (("X", 1), ("Z", 1), ("Y", -1)),
    "back": (("X", -1), ("Z", 1), ("Y", 1)),
    "right": (("Y", 1), ("Z", 1), ("X", 1)),
    "left": (("Y", -1), ("Z", 1), ("X", -1)),
    "top": (("X", 1), ("Y", 1), ("Z", 1)),
}
switches = {}


def decal(image, face, center, size, variant):
    img = bpy.data.images.load(os.path.join(DECALS, image), check_existing=True)
    (ax, sa), (bx, sb), (nx, sn) = FACES[face]
    def scaled(axis, sign):
        m = N("ShaderNodeMath"); m.operation = "MULTIPLY"; m.inputs[1].default_value = sign
        L(obj.outputs[axis], m.inputs[0]); return m.outputs[0]
    comb = N("ShaderNodeCombineXYZ")
    L(scaled(ax, sa), comb.inputs["X"]); L(scaled(bx, sb), comb.inputs["Y"])
    sub = N("ShaderNodeVectorMath"); sub.operation = "SUBTRACT"; sub.inputs[1].default_value = (center[0], center[1], 0)
    L(comb.outputs[0], sub.inputs[0])
    div = N("ShaderNodeVectorMath"); div.operation = "DIVIDE"; div.inputs[1].default_value = (size[0], size[1], 1)
    L(sub.outputs[0], div.inputs[0])
    add = N("ShaderNodeVectorMath"); add.operation = "ADD"; add.inputs[1].default_value = (0.5, 0.5, 0)
    L(div.outputs[0], add.inputs[0])
    tex = N("ShaderNodeTexImage"); tex.image = img; tex.extension = "CLIP"; tex.interpolation = "Cubic"
    L(add.outputs[0], tex.inputs["Vector"])
    facing = N("ShaderNodeMath"); facing.operation = "GREATER_THAN"; facing.inputs[1].default_value = 0.5
    sgn = N("ShaderNodeMath"); sgn.operation = "MULTIPLY"; sgn.inputs[1].default_value = sn
    L(nrm.outputs[nx], sgn.inputs[0]); L(sgn.outputs[0], facing.inputs[0])
    on = N("ShaderNodeValue"); on.outputs[0].default_value = 0.0
    switches.setdefault(variant, []).append(on)
    f1 = N("ShaderNodeMath"); f1.operation = "MULTIPLY"; L(tex.outputs["Alpha"], f1.inputs[0]); L(facing.outputs[0], f1.inputs[1])
    f2 = N("ShaderNodeMath"); f2.operation = "MULTIPLY"; L(f1.outputs[0], f2.inputs[0]); L(on.outputs[0], f2.inputs[1])
    mix = N("ShaderNodeMix"); mix.data_type = "RGBA"
    L(f2.outputs[0], mix.inputs[0]); L(current_ref[0], mix.inputs[6]); L(tex.outputs["Color"], mix.inputs[7])
    current_ref[0] = mix.outputs[2]


current_ref = [current]
# Variant 0: label front-left, arrows on the back and right.
decal("label_a.png", "front", (-0.05, -0.015), (0.1, 0.15), 0)
decal("arrows.png", "back", (0, 0.03), (0.12, 0.104), 0)
decal("arrows.png", "right", (0, 0.03), (0.12, 0.104), 0)
# Variant 1: label lower right, FRAGILE on the left and back, lot stamp on the right.
decal("label_b.png", "front", (0.055, -0.04), (0.1, 0.15), 1)
decal("fragile.png", "left", (0, 0.04), (0.2, 0.0615), 1)
decal("fragile.png", "back", (0, 0.04), (0.2, 0.0615), 1)
decal("lot.png", "right", (0, -0.07), (0.16, 0.042), 1)
# Variant 2: FRAGILE and a lot stamp on the front, label on the right side, arrows on the left.
decal("fragile.png", "front", (0, 0.06), (0.22, 0.068), 2)
decal("lot.png", "front", (-0.04, -0.09), (0.15, 0.039), 2)
decal("label_a.png", "right", (0.02, -0.02), (0.1, 0.15), 2)
decal("arrows.png", "left", (0, 0.03), (0.12, 0.104), 2)
L(current_ref[0], bsdf.inputs["Base Color"])

# ---- Bake into a three-column atlas (one 512 px column per variant). ----
scn = bpy.context.scene
scn.render.engine = "CYCLES"
scn.cycles.device = "CPU"
scn.cycles.samples = 32
scn.world.light_settings.distance = 0.05
exp = bpy.data.collections.new("Export"); scn.collection.children.link(exp)
made = P.join_parts({"Carton": [body, tape]}, {"Carton": (OX, 0, OZ)}, exp)
carton = made["Carton"]
others = [o for o in scn.objects if o is not carton and not o.hide_render]
for o in others:
    o.hide_render = True
me = carton.data
while me.uv_layers:
    me.uv_layers.remove(me.uv_layers[0])
uvl = me.uv_layers.new(name="UVMap")
P.select_only([carton])
with bpy.context.temp_override(**P.view3d(), active_object=carton, object=carton, selected_objects=[carton], selected_editable_objects=[carton]):
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(70), island_margin=0.02, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=0.02, rotate=True)
    bpy.ops.object.mode_set(mode="OBJECT")
me = carton.data
base_uv = np.empty(len(me.loops) * 2, dtype=np.float32)
me.uv_layers["UVMap"].data.foreach_get("uv", base_uv)
base_uv = base_uv.reshape(-1, 2)
base_uv[:, 0] /= VARIANTS


def set_column(k):
    uv = base_uv.copy(); uv[:, 0] += k / VARIANTS
    me.uv_layers["UVMap"].data.foreach_set("uv", uv.ravel())


W = COL_PX * VARIANTS


def image(name, noncolor):
    old = bpy.data.images.get(name)
    if old: bpy.data.images.remove(old)
    img = bpy.data.images.new(name, W, ATLAS_H, alpha=True)
    img.colorspace_settings.name = "Non-Color" if noncolor else "sRGB"
    return img


def bake_into(img, kind, clear, emit_value=None):
    mats = [m for m in me.materials if m]
    nodes, saved = [], []
    for m in mats:
        n = m.node_tree.nodes.new("ShaderNodeTexImage"); n.image = img; m.node_tree.nodes.active = n; nodes.append((m, n))
        b = P.principled(m)
        saved.append((b, b.inputs["Emission Strength"].default_value, tuple(b.inputs["Emission Color"].default_value)))
        if emit_value:
            v = emit_value(b); b.inputs["Emission Color"].default_value = (v, v, v, 1); b.inputs["Emission Strength"].default_value = 1
    P.select_only([carton])
    with bpy.context.temp_override(**P.view3d(), active_object=carton, object=carton, selected_objects=[carton], selected_editable_objects=[carton]):
        if kind == "DIFFUSE":
            bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, margin=4, use_clear=clear)
        else:
            bpy.ops.object.bake(type=kind, margin=4, use_clear=clear)
    for b, s, c in saved:
        b.inputs["Emission Strength"].default_value = s; b.inputs["Emission Color"].default_value = c
    for m, n in nodes:
        m.node_tree.nodes.remove(n)


albedo = image("carton_bake_albedo", False)
for k in range(VARIANTS):
    for v, sw in switches.items():
        for s in sw:
            s.outputs[0].default_value = 1.0 if v == k else 0.0
    set_column(k)
    bake_into(albedo, "DIFFUSE", clear=(k == 0))
set_column(0)
ao = image("carton_bake_ao", True); bake_into(ao, "AO", True)
rough = image("carton_bake_rough", True); bake_into(rough, "EMIT", True, lambda b: b.inputs["Roughness"].default_value)

px = lambda img: P._pixels(img)
A, O, R = px(albedo), px(ao)[..., 0], px(rough)[..., 0]
col0 = slice(0, COL_PX)
for k in range(1, VARIANTS):  # geometry-only maps are the same in every column
    O[:, k * COL_PX:(k + 1) * COL_PX] = O[:, col0]
    R[:, k * COL_PX:(k + 1) * COL_PX] = R[:, col0]
srgb = np.clip(A[..., :3], 0, 1)
linear = np.where(srgb <= 0.04045, srgb / 12.92, ((srgb + 0.055) / 1.055) ** 2.4)
linear *= 0.7 + 0.3 * O[..., None]
out = np.where(linear <= 0.0031308, linear * 12.92, 1.055 * np.power(np.clip(linear, 0, None), 1 / 2.4) - 0.055)
P._save("carton_albedo_out", np.concatenate([out, np.ones_like(O)[..., None]], -1), os.path.join(P.MODELS, "carton_albedo.png"), False)
P._save("carton_mask_out", np.stack([np.zeros_like(O), O, np.zeros_like(O), 1 - R], -1), os.path.join(P.MODELS, "carton_mask.png"), True)
for img in (albedo, ao, rough):
    bpy.data.images.remove(img)
P.single_material(carton, bpy.data.materials.get("Carton_Baked") or bpy.data.materials.new("Carton_Baked"))
carton["pivot"] = [OX, 0, OZ]
P.export("Carton.fbx", {"Carton": carton}, (OX, 0, OZ))
P.cleanup(exp)
for o in others:
    o.hide_render = False

# ---- Jam carton: the scan, reduced and normalised to the same 0.3 m cube. ----
scol = P.collection("Carton Scan")
before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=SCAN)
imported = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
for o in bpy.data.objects:
    if o not in before:
        for c in list(o.users_collection): c.objects.unlink(o)
        scol.objects.link(o)
scan = imported[0]
scan.name = "Carton Scan"
scan.parent = None
dec = scan.modifiers.new("Decimate", "DECIMATE"); dec.ratio = 0.2
with bpy.context.temp_override(object=scan, active_object=scan, selected_objects=[scan], selected_editable_objects=[scan]):
    bpy.ops.object.modifier_apply(modifier=dec.name)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in [o for o in scol.objects if o is not scan]:
    bpy.data.objects.remove(o, do_unlink=True)
vs = np.array([v.co[:] for v in scan.data.vertices])
lo, hi = vs.min(0), vs.max(0)
size = hi - lo
# Long side along X, like the other cartons, then fit the bounds to a 0.3 m cube on (OX + 0.6, 0, OZ).
if size[1] > size[0]:
    scan.data.transform(Matrix.Rotation(math.pi / 2, 4, "Z"))
    vs = np.array([v.co[:] for v in scan.data.vertices]); lo, hi = vs.min(0), vs.max(0); size = hi - lo
centre = (lo + hi) / 2
scan.data.transform(Matrix.Translation(Vector((OX + 0.6, 0, OZ))) @ Matrix.Diagonal((*(S / size), 1)) @ Matrix.Translation(-Vector(centre)))
scan.data.update()
for p in scan.data.polygons:
    p.use_smooth = True
scan_mat = scan.data.materials[0]
stale = bpy.data.materials.get("Carton_Scan")
if stale and stale is not scan_mat:
    bpy.data.materials.remove(stale)
scan_mat.name = "Carton_Scan"
tex_dir = os.path.join(os.path.dirname(SCAN), "textures")
# Shift the scan's cardboard toward the baked kraft so the jammed carton matches the others. The gain is the ratio of
# the two textures' median cardboard colours, in linear light.
diff = bpy.data.images.load(os.path.join(tex_dir, "cardboard_box_01_diff_1k.jpg"))
d = P._pixels(diff)[..., :3]
d = np.where(d <= 0.04045, d / 12.92, ((d + 0.055) / 1.055) ** 2.4) * np.array([1.389, 1.047, 0.938])
d = np.clip(d, 0, 1)
d = np.where(d <= 0.0031308, d * 12.92, 1.055 * np.power(d, 1 / 2.4) - 0.055)
P._save("carton_scan_albedo_out", np.concatenate([d, np.ones_like(d[..., :1])], -1), os.path.join(P.MODELS, "carton_scan_albedo.png"), False)
shutil.copy(os.path.join(tex_dir, "cardboard_box_01_nor_gl_1k.jpg"), os.path.join(P.MODELS, "carton_scan_normal.jpg"))
arm = bpy.data.images.load(os.path.join(tex_dir, "cardboard_box_01_arm_1k.jpg"))
arm.colorspace_settings.name = "Non-Color"
a = P._pixels(arm)  # R ambient occlusion, G roughness, B metallic
P._save("carton_scan_mask_out", np.stack([a[..., 2], a[..., 0], np.zeros_like(a[..., 0]), 1 - a[..., 1]], -1),
        os.path.join(P.MODELS, "carton_scan_mask.png"), True)
exp = bpy.data.collections.new("Export"); scn.collection.children.link(exp)
made = P.join_parts({"Carton Crushed": [scan]}, {"Carton Crushed": (OX + 0.6, 0, OZ)}, exp)
P.export("CartonCrushed.fbx", made, (OX + 0.6, 0, OZ))
tris = P.triangles([made["Carton Crushed"]])
P.cleanup(exp)
bpy.ops.wm.save_mainfile()
with open(MARKER, "w") as f:
    f.write(f"carton tris {P.triangles([body, tape])} scan tris {tris}\n")
