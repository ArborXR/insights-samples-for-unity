"""Bakes and exports Forklift.fbx from the Forklift collection built by forklift.py."""
import bpy, bmesh, importlib, os, time
from mathutils import Matrix, Vector
import props_lib as P
importlib.reload(P)

MARKER = os.path.join(os.path.dirname(P.__file__), "export_forklift.done")
if os.path.exists(MARKER):
    os.remove(MARKER)
t0 = time.time()
OX = 5.0
FRONT_AXLE, REAR_AXLE, FR, RR = -0.62, 0.66, 0.25, 0.21
col = bpy.data.collections["Forklift"]
objs = [o for o in col.objects if not o.name.startswith("Cutter")]
for o in objs:
    o.hide_viewport = False


def named(prefix, exclude=()):
    return [o for o in objs if o.name.startswith(prefix) and not any(o.name.startswith(e) for e in exclude)]


parts = {
    "Forklift Body": named("Body"),
    "Forklift Hood": named("Hood"),
    "Forklift Carriage": named("Carriage"),
    "Fork Left": named("Fork Left"),
    "Fork Right": named("Fork Right"),
    "Steering Wheel": named("Steering Wheel"),
    "Horn Button": named("Horn Button"),
    "Wheel FL": named("Wheel FL ", exclude=("Wheel FL Damaged",)),
    "Wheel FR": named("Wheel FR"),
    "Wheel RL": named("Wheel RL"),
    "Wheel RR": named("Wheel RR"),
    "Wheel FL Damaged": named("Wheel FL Damaged"),
    "Forklift Beacon": named("Beacon Lens"),
}
SW = (OX, -0.31, 1.27)
pivots = {
    "Forklift Body": (OX, 0, 0), "Forklift Hood": (OX, 0.70, 0.78), "Forklift Carriage": (OX, -1.04, 0.1),
    "Fork Left": (OX - 0.26, -1.04, 0.1), "Fork Right": (OX + 0.26, -1.04, 0.1),
    "Steering Wheel": SW, "Horn Button": SW,
    "Wheel FL": (OX - 0.45, FRONT_AXLE, FR), "Wheel FR": (OX + 0.45, FRONT_AXLE, FR),
    "Wheel RL": (OX - 0.44, REAR_AXLE, RR), "Wheel RR": (OX + 0.44, REAR_AXLE, RR),
    "Wheel FL Damaged": (OX - 0.45, FRONT_AXLE, FR), "Forklift Beacon": (OX + 0.28, 0.62, 2.18),
}
exp = bpy.data.collections.new("Export")
bpy.context.scene.collection.children.link(exp)
made = P.join_parts(parts, pivots, exp)

# A floor under the truck for contact shading, and the damaged wheel moved clear so it doesn't shade the good one.
bpy.ops.mesh.primitive_plane_add(size=6, location=(OX, -0.4, 0))
floor = bpy.context.active_object
for c in list(floor.users_collection): c.objects.unlink(floor)
exp.objects.link(floor)
shift = Matrix.Translation((3.0, 0, 0))
made["Wheel FL Damaged"].data.transform(shift)
baked = [o for k, o in made.items() if k != "Forklift Beacon"]
P.bake(baked, "forklift", occluders=[made["Forklift Beacon"], floor], size=2048, samples=32, ao_distance=0.35)
made["Wheel FL Damaged"].data.transform(shift.inverted())
bpy.data.objects.remove(floor, do_unlink=True)

mat = bpy.data.materials.get("Forklift_Baked") or bpy.data.materials.new("Forklift_Baked")
for o in baked:
    P.single_material(o, mat)
P.export("Forklift.fbx", made, (OX, 0, 0))
report = {k: P.triangles([v]) for k, v in made.items()}
P.cleanup(exp)
bpy.ops.wm.save_mainfile()
with open(MARKER, "w") as f:
    f.write(f"{report} total {sum(report.values())} in {round(time.time() - t0, 1)}s\n")
