"""Builds and exports the post-mounted caution sign (CautionSign.fbx). Pivot: the centre of the sign face."""
import bpy, bmesh, math, importlib, os
from mathutils import Vector
import props_lib as P
importlib.reload(P)

OX, OZ = 3.6, 1.32
W, H = 0.56, 0.40
FRONT, BACK = -0.010, -0.007
STEEL = bpy.data.materials["Conveyor_Steel"]
face = bpy.data.materials.get("Sign_Face") or bpy.data.materials.new("Sign_Face")
b = P.principled(face)
tex = next((n for n in face.node_tree.nodes if n.type == "TEX_IMAGE"), None) or face.node_tree.nodes.new("ShaderNodeTexImage")
tex.image = bpy.data.images.load(os.path.join(P.MODELS, "caution_sign.png"), check_existing=True)
face.node_tree.links.new(tex.outputs["Color"], b.inputs["Base Color"])
b.inputs["Roughness"].default_value = 0.45
col = P.collection("Caution Sign")


def rounded_rect(w, h, r, seg=6):
    pts = []
    for cx, cz, a0 in ((w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)):
        for i in range(seg + 1):
            a = math.radians(a0 + 90 * i / seg)
            pts.append((OX + cx + r * math.cos(a), OZ + cz + r * math.sin(a)))
    return pts


plate = P.plate_xz(col, "Sign Plate", rounded_rect(W, H, 0.02), FRONT, BACK, STEEL, bevel=0.0008, segs=1)
me = plate.data
me.materials.append(face)
uv = me.uv_layers.new(name="UVMap")
for poly in me.polygons:
    front = poly.normal.y < -0.99
    poly.material_index = 1 if front else 0
    for li in poly.loop_indices:
        v = me.vertices[me.loops[li].vertex_index].co
        uv.data[li].uv = ((v.x - OX + W / 2) / W, (v.z - OZ + H / 2) / H) if front else (0.0, 0.0)

for x in (-W / 2 + 0.025, W / 2 - 0.025):
    for z in (-H / 2 + 0.025, H / 2 - 0.025):
        P.cyl(col, "Sign Bolt", (OX + x, FRONT - 0.0015, OZ + z), 0.0085, 0.003, "y", STEEL, seg=16, bevel=0.0012, r2=0.006)

# Post behind the sign, clamped at two heights, on a bolted floor flange.
PY, PR = 0.024, 0.025
top = OZ + 0.17
P.cyl(col, "Sign Post", (OX, PY, (0.012 + top) / 2), PR, top - 0.012, "z", STEEL, seg=24)
P.cyl(col, "Sign Post Cap", (OX, PY, top + 0.004), PR + 0.002, 0.008, "z", STEEL, seg=24, bevel=0.002)
for z in (OZ - 0.12, OZ + 0.12):
    P.box(col, "Sign Clamp Plate", (OX, (BACK + PY - PR) / 2, z), (0.09, PY - PR - BACK + 0.004, 0.03), STEEL, bevel=0.0015)
    P.cyl(col, "Sign Clamp Band", (OX, PY, z), PR + 0.004, 0.022, "z", STEEL, seg=24, bevel=0.0015)
P.cyl(col, "Sign Flange", (OX, PY, 0.006), 0.08, 0.012, "z", STEEL, seg=36, bevel=0.002)
for i in range(4):
    a = math.pi / 4 + i * math.pi / 2
    P.cyl(col, "Sign Flange Bolt", (OX + 0.058 * math.cos(a), PY + 0.058 * math.sin(a), 0.015), 0.009, 0.007, "z", STEEL, seg=6, bevel=0.001)

exp = bpy.data.collections.new("Export")
bpy.context.scene.collection.children.link(exp)
made = P.join_parts({"Caution Sign": list(col.objects)}, {"Caution Sign": (OX, 0, OZ)}, exp)
P.export("CautionSign.fbx", made, (OX, 0, OZ))
P.cleanup(exp)
bpy.ops.wm.save_mainfile()
print("tris", P.triangles(list(col.objects)))
