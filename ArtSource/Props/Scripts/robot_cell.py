"""Builds sample 12's robot cell: RobotArm.fbx (jointed), RobotController.fbx, and SafetyFence.fbx (one module).

Unity-local space, origin on the floor, front at -z. The arm is authored in its zero pose: lower arm vertical,
upper arm and wrist reaching along +x. Each moving part is its own object with its pivot on its joint:
  Robot Waist (J1, about y) at (0, 0.30, 0); Robot Lower Arm (J2, about z) at (0.08, 0.62, 0);
  Robot Upper Arm (J3) at (0.08, 1.22, 0); Robot Wrist (J5) at (0.63, 1.22, 0); fingers slide along z.
"""
import bpy, bmesh, math, importlib, os, time
from mathutils import Vector
import props_lib as P
importlib.reload(P)

PARK_Y = 22.0
ARM_X, CAB_X, FENCE_X = 30.0, 32.0, 34.0
J2, J3, J5 = (0.08, 0.62, 0), (0.08, 1.22, 0), (0.63, 1.22, 0)


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

    def lathe(self, name, profile, m, centre=(0, 0), seg=40):
        bm = bmesh.new()
        c = self.b((centre[0], 0, centre[1]))
        verts = [bm.verts.new((c.x + r, c.y, y)) for r, y in profile]
        edges = [bm.edges.new((verts[i], verts[i + 1])) for i in range(len(verts) - 1)]
        bmesh.ops.spin(bm, geom=verts + edges, cent=(c.x, c.y, 0), axis=(0, 0, 1), angle=math.tau, steps=seg, use_duplicate=False)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return P.finish(self.col, name, bm, m)

    def tube(self, name, points, r, m, seg=12):
        pts = [self.b(p) for p in points]
        n = len(pts)
        tangents = [(pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized() for i in range(n)]
        ref = Vector((0, 0, 1)) if abs(tangents[0].z) < 0.9 else Vector((1, 0, 0))
        normal = tangents[0].cross(ref).normalized()
        bm = bmesh.new()
        rings = []
        for i in range(n):
            if i:
                normal = (normal - tangents[i] * normal.dot(tangents[i])).normalized()
            binormal = tangents[i].cross(normal)
            rings.append([bm.verts.new(pts[i] + r * (math.cos(math.tau * k / seg) * normal + math.sin(math.tau * k / seg) * binormal))
                          for k in range(seg)])
        for i in range(n - 1):
            for k in range(seg):
                bm.faces.new((rings[i][k], rings[i][(k + 1) % seg], rings[i + 1][(k + 1) % seg], rings[i + 1][k]))
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return P.finish(self.col, name, bm, m)


GREY = mat("Robot_Grey", (0.80, 0.81, 0.80), 0.4)
DARK = mat("Robot_Dark", (0.22, 0.23, 0.25), 0.45, 0.3)
BLACK = mat("Robot_Black", (0.05, 0.05, 0.055), 0.6)
CHROME = mat("Robot_Chrome", (0.82, 0.83, 0.85), 0.2, 1.0)
CAB = mat("Cell_Cabinet", (0.78, 0.79, 0.78), 0.45)
CAB_DARK = mat("Cell_Cabinet_Dark", (0.2, 0.21, 0.23), 0.5)
KEY_YELLOW = mat("Cell_Yellow", (0.95, 0.75, 0.05), 0.45)
EST_RED = mat("Cell_Red", (0.8, 0.1, 0.08), 0.35)
POST = mat("Fence_Post", (0.95, 0.75, 0.05), 0.45)
MESH = mat("Fence_Mesh", (0.08, 0.08, 0.09), 0.5, 0.4)

# ---- Robot arm ----
arm = Prop("Robot Arm", ARM_X)
# Base (fixed): floor plate with bolts, and the pedestal.
arm.cyl("Base Plate", (0, 0.015, 0), 0.24, 0.03, "y", DARK, seg=40, bevel=0.004)
for k in range(6):
    a = math.tau * k / 6 + 0.3
    arm.cyl("Base Bolt", (0.205 * math.cos(a), 0.036, 0.205 * math.sin(a)), 0.014, 0.012, "y", CHROME, seg=6, bevel=0.002)
arm.lathe("Base Pedestal", [(0.2, 0.03), (0.2, 0.14), (0.172, 0.165), (0.172, 0.3), (0, 0.3)], GREY, seg=40)
# Waist (J1): turntable, turret, shoulder cheeks, J1 and J2 motors.
arm.cyl("Waist Ring", (0, 0.315, 0), 0.18, 0.03, "y", DARK, seg=40, bevel=0.003)
arm.cyl("Waist Turret", (0, 0.42, 0), 0.15, 0.18, "y", GREY, seg=40, bevel=0.02)
for z in (-0.125, 0.125):
    arm.box("Waist Cheek", (0.04, 0.55, z), (0.2, 0.2, 0.05), GREY, bevel=0.02, segs=3)
    arm.cyl("Waist Cheek Cap", (J2[0], J2[1], z), 0.1, 0.05, "z", GREY, seg=32, bevel=0.012)
arm.cyl("Waist J2 Motor", (J2[0], J2[1], -0.19), 0.068, 0.085, "z", DARK, seg=28, bevel=0.01)
arm.cyl("Waist J2 Motor Cap", (J2[0], J2[1], -0.24), 0.05, 0.02, "z", BLACK, seg=24, bevel=0.004)
arm.cyl("Waist J1 Motor", (-0.14, 0.47, 0), 0.055, 0.12, "y", DARK, seg=24, bevel=0.01)
# Lower arm (J2 to J3).
arm.cyl("Lower Hub", J2, 0.095, 0.19, "z", GREY, seg=32, bevel=0.015)
arm.box("Lower Link", (J2[0], (J2[1] + J3[1]) / 2, 0), (0.15, J3[1] - J2[1], 0.17), GREY, bevel=0.035, segs=4)
arm.cyl("Lower Top", J3, 0.085, 0.17, "z", GREY, seg=32, bevel=0.015)
arm.tube("Lower Cable", [(-0.02, 0.68, -0.1), (-0.035, 0.8, -0.105), (-0.035, 1.05, -0.105), (-0.01, 1.18, -0.1)], 0.018, BLACK)
for y in (0.76, 0.96, 1.12):
    arm.box("Lower Cable Clamp", (-0.03, y, -0.1), (0.05, 0.02, 0.045), DARK, bevel=0.004)
# Upper arm (J3 to the wrist): elbow housing, J3 and J4 motors, forearm with a side cover, wrist yoke.
arm.cyl("Upper Elbow", J3, 0.09, 0.21, "z", GREY, seg=32, bevel=0.015)
arm.cyl("Upper J3 Motor", (J3[0], J3[1], -0.15), 0.058, 0.09, "z", DARK, seg=28, bevel=0.01)
arm.cyl("Upper J4 Motor", (-0.07, J3[1] + 0.02, 0), 0.055, 0.13, "x", DARK, seg=28, bevel=0.01)
arm.cyl("Upper Forearm", ((J3[0] + J5[0]) / 2 + 0.02, J3[1], 0), 0.072, 0.4, "x", GREY, seg=32, bevel=0.01, r2=0.058)
arm.box("Upper Cover", ((J3[0] + J5[0]) / 2 + 0.02, J3[1], -0.068), (0.24, 0.07, 0.012), DARK, bevel=0.005)
for z in (-0.052, 0.052):
    arm.box("Upper Wrist Yoke", (J5[0] - 0.03, J5[1], z), (0.09, 0.085, 0.018), GREY, bevel=0.008)
# Wrist (J5) with flange and gripper body; tool tip at x 0.82.
arm.cyl("Wrist Body", J5, 0.042, 0.085, "z", DARK, seg=24, bevel=0.006)
arm.cyl("Wrist Neck", (J5[0] + 0.045, J5[1], 0), 0.036, 0.06, "x", GREY, seg=24, bevel=0.004)
arm.cyl("Wrist Flange", (J5[0] + 0.082, J5[1], 0), 0.045, 0.014, "x", CHROME, seg=24, bevel=0.002)
arm.box("Wrist Gripper", (J5[0] + 0.115, J5[1], 0), (0.05, 0.065, 0.13), DARK, bevel=0.006)
arm.box("Wrist Gripper Rail", (J5[0] + 0.142, J5[1], 0), (0.006, 0.03, 0.12), CHROME, bevel=0.001)
for side, z in (("L", -0.045), ("R", 0.045)):
    arm.box(f"Finger {side}", (J5[0] + 0.17, J5[1], z), (0.06, 0.028, 0.012), BLACK, bevel=0.002)

# ---- Controller cabinet ----
cab = Prop("Robot Controller", CAB_X)
cab.box("Cabinet Plinth", (0, 0.05, 0), (0.58, 0.1, 0.38), CAB_DARK, bevel=0.004)
cab.box("Cabinet Body", (0, 0.68, 0), (0.6, 1.16, 0.4), CAB, bevel=0.012)
cab.box("Cabinet Door", (0, 0.68, -0.202), (0.54, 1.08, 0.006), CAB, bevel=0.004)
cab.box("Cabinet Handle", (0.22, 0.78, -0.212), (0.02, 0.14, 0.018), CAB_DARK, bevel=0.004)
for i in range(6):
    cab.box("Cabinet Louvre", (0, 0.2 + i * 0.03, -0.206), (0.3, 0.008, 0.004), CAB_DARK, bevel=0.001)
cab.box("Cabinet Panel", (-0.05, 1.0, -0.207), (0.3, 0.14, 0.004), CAB_DARK, bevel=0.003)
cab.cyl("Cabinet Key Switch", (-0.15, 1.0, -0.212), 0.018, 0.012, "z", KEY_YELLOW, seg=16, bevel=0.002)
cab.cyl("Cabinet Power Lamp", (-0.07, 1.0, -0.212), 0.014, 0.012, "z", CHROME, seg=16, bevel=0.002)
cab.cyl("Cabinet E-Stop Collar", (0.04, 1.0, -0.211), 0.03, 0.008, "z", KEY_YELLOW, seg=24)
cab.cyl("Cabinet E-Stop", (0.04, 1.0, -0.222), 0.024, 0.018, "z", EST_RED, seg=24, bevel=0.006)
cab.box("Cabinet Fan", (0.31, 0.45, 0), (0.012, 0.2, 0.2), CAB_DARK, bevel=0.003)
# Teach pendant on its hook on the side, with its coiled cable.
cab.box("Pendant Hook", (0.31, 1.02, 0.08), (0.02, 0.04, 0.08), CAB_DARK, bevel=0.003)
cab.box("Pendant", (0.335, 0.92, 0.08), (0.05, 0.26, 0.17), CAB_DARK, bevel=0.012)
cab.box("Pendant Screen", (0.361, 0.95, 0.08), (0.002, 0.12, 0.12), BLACK, bevel=0)
cab.cyl("Pendant E-Stop", (0.362, 1.04, 0.13), 0.012, 0.012, "x", EST_RED, seg=16, bevel=0.003)
cab.tube("Pendant Cable", [(0.335, 0.79, 0.08), (0.34, 0.6, 0.1), (0.32, 0.35, 0.14), (0.3, 0.25, 0.16)], 0.008, BLACK, seg=8)

# ---- Safety fence module: a post at x 0 and a 1.2 m mesh panel to +x ----
fence = Prop("Safety Fence", FENCE_X)
W, H, BOT = 1.2, 1.8, 0.12
fence.box("Post", (0, 1.0, 0), (0.06, 2.0, 0.06), POST, bevel=0.004)
fence.box("Post Cap", (0, 2.005, 0), (0.066, 0.01, 0.066), MESH, bevel=0.002)
fence.box("Post Foot", (0, 0.005, 0), (0.14, 0.01, 0.14), POST, bevel=0.002)
for dx in (-0.05, 0.05):
    for dz in (-0.05, 0.05):
        fence.cyl("Post Bolt", (dx, 0.014, dz), 0.008, 0.008, "y", MESH, seg=6)
x0, x1 = 0.045, W - 0.045
for y in (BOT, BOT + H):
    fence.box("Panel Frame", ((x0 + x1) / 2, y, 0), (x1 - x0, 0.025, 0.025), MESH, bevel=0.002)
for x in (x0, x1):
    fence.box("Panel Frame", (x, BOT + H / 2, 0), (0.025, H, 0.025), MESH, bevel=0.002)
    for y in (0.35, 1.0, 1.65):
        fence.box("Panel Clip", (x + (-0.022 if x < 0.5 else 0.022), y, 0), (0.02, 0.04, 0.03), MESH, bevel=0.002)
step = 0.075
nx = int((x1 - x0) / step)
for i in range(1, nx):
    fence.box("Panel Wire", (x0 + i * step, BOT + H / 2, 0.004), (0.004, H, 0.004), MESH, bevel=0)
ny = int(H / step)
for j in range(1, ny):
    fence.box("Panel Wire", ((x0 + x1) / 2, BOT + j * step, -0.004), (x1 - x0, 0.004, 0.004), MESH, bevel=0)

# ---- Bake and export ----
MARKER = os.path.join(os.path.dirname(P.__file__), "robot_cell.done")
if os.path.exists(MARKER):
    os.remove(MARKER)
if bpy.app.background:
    t0 = time.time()
    report = {}

    def objs(prop, *prefixes):
        return [o for o in prop.col.objects if o.name.startswith(prefixes)]

    jobs = [
        (arm, "RobotArm.fbx", "robot", {
            "Robot Base": (objs(arm, "Base"), (0, 0, 0)),
            "Robot Waist": (objs(arm, "Waist"), (0, 0.3, 0)),
            "Robot Lower Arm": (objs(arm, "Lower"), J2),
            "Robot Upper Arm": (objs(arm, "Upper"), J3),
            "Robot Wrist": (objs(arm, "Wrist"), J5),
            "Robot Finger L": (objs(arm, "Finger L"), (J5[0] + 0.14, J5[1], -0.045)),
            "Robot Finger R": (objs(arm, "Finger R"), (J5[0] + 0.14, J5[1], 0.045)),
        }),
        (cab, "RobotController.fbx", "controller", {"Robot Controller": (list(cab.col.objects), (0, 0, 0))}),
        (fence, "SafetyFence.fbx", "fence", {"Fence Post": (objs(fence, "Post"), (0, 0, 0)),
                                             "Fence Panel": (objs(fence, "Panel"), (0, 0, 0))}),
    ]
    for prop, filename, prefix, parts in jobs:
        exp = bpy.data.collections.new("Export"); bpy.context.scene.collection.children.link(exp)
        origin = tuple(prop.b((0, 0, 0)))
        made = P.join_parts({k: v[0] for k, v in parts.items()}, {k: tuple(prop.b(v[1])) for k, v in parts.items()}, exp)
        bpy.ops.mesh.primitive_plane_add(size=4, location=origin)
        floor = bpy.context.active_object
        for c in list(floor.users_collection): c.objects.unlink(floor)
        exp.objects.link(floor)
        P.bake(list(made.values()), prefix, occluders=[floor], size=1024, samples=32)
        bpy.data.objects.remove(floor, do_unlink=True)
        name = prefix.capitalize() + "_Baked"
        baked = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        for o in made.values(): P.single_material(o, baked)
        P.export(filename, made, origin)
        report[filename] = {k: P.triangles([v]) for k, v in made.items()}
        P.cleanup(exp)
    bpy.ops.wm.save_mainfile()
    with open(MARKER, "w") as f:
        f.write(f"{report} in {round(time.time() - t0, 1)}s\n")
print(len(arm.col.objects), len(cab.col.objects), len(fence.col.objects), "objects")
