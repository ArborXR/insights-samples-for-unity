"""Builds and exports the floor-standing stack light (StackLight.fbx). Pivot: centre of the base, on the floor."""
import bpy, bmesh, math, importlib
from mathutils import Vector
import props_lib as P
importlib.reload(P)

OX = 2.2  # parked beside the conveyor in the .blend
STEEL = bpy.data.materials["Conveyor_Steel"]
DARK = bpy.data.materials["Conveyor_DarkPlastic"]
LENS = bpy.data.materials["StackLight_Lens"]
col = P.collection("Stack Light")

R = 0.045           # lens radius; a little over real size so it reads from the station camera
LENS_H = 0.065
POLE_TOP = 1.06


def fluted(name, z0, h, r, mat, flutes=36):
    """A lens segment with vertical ribs, like a fresnel stack light lens."""
    bm = bmesh.new()
    n = flutes * 2
    ring = lambda z: [bm.verts.new((OX + (r if i % 2 == 0 else r * 0.955) * math.cos(2 * math.pi * i / n),
                                    (r if i % 2 == 0 else r * 0.955) * math.sin(2 * math.pi * i / n), z)) for i in range(n)]
    a, b = ring(z0), ring(z0 + h)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(list(reversed(a)))
    bm.faces.new(b)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # Flat-shaded ribs scatter the highlight along the lens instead of one glossy hot spot.
    ob = P.finish(col, name, bm, mat, smooth=False)
    return ob


# Base: flange with four bolts, then the pole.
P.cyl(col, "Body Flange", (OX, 0, 0.005), 0.07, 0.01, "z", STEEL, seg=40, bevel=0.002)
P.cyl(col, "Body Boss", (OX, 0, 0.022), 0.026, 0.026, "z", STEEL, seg=24, bevel=0.002)
for i in range(4):
    a = math.pi / 4 + i * math.pi / 2
    P.cyl(col, "Body Bolt", (OX + 0.05 * math.cos(a), 0.05 * math.sin(a), 0.013), 0.008, 0.007, "z", STEEL, seg=6, bevel=0.001)
P.cyl(col, "Body Pole", (OX, 0, (0.03 + POLE_TOP) / 2), 0.016, POLE_TOP - 0.03, "z", STEEL, seg=20)
# Mounting base, lens stack with separator rings, cap.
P.cyl(col, "Body Mount", (OX, 0, POLE_TOP + 0.02), R * 0.98, 0.04, "z", DARK, seg=40, bevel=0.004)
z = POLE_TOP + 0.04
for name in ("Lens Green", "Lens Amber", "Lens Red"):
    P.cyl(col, "Body Ring", (OX, 0, z + 0.003), R * 1.01, 0.006, "z", DARK, seg=40, bevel=0.0012)
    z += 0.006
    fluted(name, z, LENS_H, R, LENS)
    z += LENS_H
P.cyl(col, "Body Ring", (OX, 0, z + 0.003), R * 1.01, 0.006, "z", DARK, seg=40, bevel=0.0012)
z += 0.006
P.cyl(col, "Body Cap", (OX, 0, z + 0.014), R * 0.99, 0.028, "z", DARK, seg=40, bevel=0.008, r2=R * 0.9)
top = z + 0.028

# Export: one body mesh plus the three lenses, which the station tints and lights.
exp = bpy.data.collections.new("Export")
bpy.context.scene.collection.children.link(exp)
lenses = {n: bpy.data.objects[n] for n in ("Lens Green", "Lens Amber", "Lens Red")}
for n, o in lenses.items():
    o.name = n + " Source"
parts = {"Stack Light Body": [o for o in col.objects if o.name.startswith("Body")]}
parts.update({n: [o] for n, o in lenses.items()})
made = P.join_parts(parts, {k: (OX, 0, 0) for k in parts}, exp)
P.export("StackLight.fbx", made, (OX, 0, 0))
P.cleanup(exp)
for n, o in lenses.items():
    o.name = n
bpy.ops.wm.save_mainfile()
print("top", round(top, 3), "tris", P.triangles(list(col.objects)))
