"""Builds sample 17's fire-extinguisher practice props: FireExtinguisher.fbx and TrainingPan.fbx.

Unity-local space, origin on the floor under each prop's centre, front at -z. The pan is a propane practice pan
on legs with two perforated burner pipes, an inlet valve, and a supply post with a hose to the valve.
"""
import bpy, bmesh, math, importlib, os, time
from mathutils import Vector, Matrix
import props_lib as P
importlib.reload(P)

PARK_Y = 20.0
EX_X, PAN_X = 26.0, 28.0


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

    def lathe(self, name, profile, m, centre=(0, 0), seg=40, start=0.0, span=360.0, smooth=True):
        """Spins an (r, y) profile around the vertical axis through (x, z) = centre."""
        bm = bmesh.new()
        c = self.b((centre[0], 0, centre[1]))
        a0 = math.radians(start)
        verts = [bm.verts.new((c.x + r * math.cos(a0), c.y + r * math.sin(a0), y)) for r, y in profile]
        edges = [bm.edges.new((verts[i], verts[i + 1])) for i in range(len(verts) - 1)]
        full = span >= 360.0
        bmesh.ops.spin(bm, geom=verts + edges, cent=(c.x, c.y, 0), axis=(0, 0, 1), angle=math.radians(span),
                       steps=seg if full else max(4, int(seg * span / 360.0)), use_duplicate=False)
        if full:
            bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return P.finish(self.col, name, bm, m, smooth=smooth)

    def tube(self, name, points, r, m, seg=12, closed=False):
        """A round tube along Unity-local points, with parallel-transported rings."""
        pts = [self.b(p) for p in points]
        n = len(pts)
        tangents = []
        for i in range(n):
            if closed:
                t = pts[(i + 1) % n] - pts[i - 1]
            else:
                t = pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]
            tangents.append(t.normalized())
        ref = Vector((0, 0, 1)) if abs(tangents[0].z) < 0.9 else Vector((1, 0, 0))
        normal = tangents[0].cross(ref).normalized()
        bm = bmesh.new()
        rings = []
        for i in range(n):
            if i:
                normal = (normal - tangents[i] * normal.dot(tangents[i])).normalized()
            binormal = tangents[i].cross(normal)
            rings.append([bm.verts.new(pts[i] + r * (math.cos(2 * math.pi * k / seg) * normal +
                                                     math.sin(2 * math.pi * k / seg) * binormal)) for k in range(seg)])
        for i in range(n if closed else n - 1):
            a, b2 = rings[i], rings[(i + 1) % n]
            for k in range(seg):
                bm.faces.new((a[k], a[(k + 1) % seg], b2[(k + 1) % seg], b2[k]))
        if not closed:
            bm.faces.new(list(reversed(rings[0])))
            bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return P.finish(self.col, name, bm, m)

    def profile(self, name, points, x0, x1, m, bevel=0.0):
        """Extrudes a closed Unity (z, y) profile along x."""
        return P.profile_extrude(self.col, name, [(PARK_Y + z, y) for z, y in points], self.ox + x0, self.ox + x1, m, bevel)

    def wheel(self, name, c, r, axis, m, spokes=3):
        """A valve handwheel: rim, spokes and hub, in the plane normal to axis ("y" or "x")."""
        def at(a, rr):
            u, v = rr * math.cos(a), rr * math.sin(a)
            return (c[0] + u, c[1], c[2] + v) if axis == "y" else (c[0], c[1] + u, c[2] + v)
        rim = [at(2 * math.pi * k / 20, r) for k in range(20)]
        self.tube(name + " Rim", rim, r * 0.16, m, seg=8, closed=True)
        for k in range(spokes):
            a = 2 * math.pi * k / spokes + 0.3
            self.tube(name + " Spoke", [at(a, r * 0.2), at(a, r * 0.92)], r * 0.09, m, seg=6)
        self.cyl(name + " Hub", c, r * 0.24, r * 0.35, axis, m, seg=12)


RED = mat("Extinguisher_Red", (0.78, 0.05, 0.04), 0.28)
RUBBER = mat("Extinguisher_Rubber", (0.05, 0.05, 0.055), 0.6)
CHROME = mat("Extinguisher_Chrome", (0.85, 0.86, 0.88), 0.18, 1.0)
LABEL = mat("Extinguisher_Label", (0.95, 0.95, 0.94), 0.45)
GAUGE = mat("Extinguisher_Gauge", (0.96, 0.96, 0.95), 0.3)
GREEN = mat("Extinguisher_Green", (0.15, 0.7, 0.25), 0.4)
YELLOW = mat("Extinguisher_Yellow", (0.96, 0.82, 0.08), 0.4)

PAN = mat("Pan_Steel", (0.38, 0.37, 0.35), 0.55, 0.6)
SOOT = mat("Pan_Soot", (0.06, 0.055, 0.05), 0.9)
PIPE = mat("Pan_Pipe", (0.34, 0.33, 0.31), 0.5, 0.8)
GALV = mat("Pan_Galvanised", (0.55, 0.55, 0.53), 0.4, 0.9)
HOLE = mat("Pan_Hole", (0.02, 0.02, 0.02), 0.9)
VALVE = mat("Pan_Valve_Red", (0.75, 0.1, 0.08), 0.4)
BRASS = mat("Pan_Brass", (0.72, 0.56, 0.28), 0.35, 1.0)
HOSE = mat("Pan_Hose", (0.08, 0.08, 0.085), 0.55)

# ---- Fire extinguisher: a 6 kg stored-pressure ABC unit ----
ex = Prop("Fire Extinguisher", EX_X)
R = 0.075
ex.lathe("Body", [(0, 0), (R - 0.006, 0), (R, 0.008), (R, 0.40), (R - 0.004, 0.43), (0.06, 0.46), (0.042, 0.478),
                  (0.022, 0.488), (0, 0.49)], RED, seg=48)
ex.lathe("Foot Ring", [(R + 0.001, 0.0), (R + 0.002, 0.004), (R + 0.002, 0.02), (R + 0.001, 0.024)], RUBBER, seg=48)
# Instruction label around the front (-z), a hair proud of the shell.
ex.lathe("Label", [(R + 0.0008, 0.11), (R + 0.0008, 0.36)], LABEL, seg=48, start=-160, span=140, smooth=True)
# Valve, gauge, carry handle and squeeze lever.
ex.cyl("Neck", (0, 0.505, 0), 0.02, 0.03, "y", CHROME, seg=24, bevel=0.002)
ex.box("Valve Body", (0, 0.545, 0), (0.035, 0.05, 0.05), CHROME, bevel=0.006)
ex.cyl("Gauge", (0, 0.55, -0.031), 0.018, 0.012, "z", CHROME, seg=24, bevel=0.002)
ex.cyl("Gauge Face", (0, 0.55, -0.0375), 0.015, 0.002, "z", GAUGE, seg=24)
ex.box("Gauge Green", (0.004, 0.556, -0.0388), (0.012, 0.004, 0.0008), GREEN, bevel=0)
ex.box("Gauge Needle", (0.003, 0.553, -0.0392), (0.0015, 0.011, 0.0006), RUBBER, bevel=0)
ex.profile("Carry Handle", [(0.0, 0.528), (0.125, 0.545), (0.128, 0.558), (0.0, 0.548)], -0.013, 0.013, RUBBER, bevel=0.003)
ex.profile("Lever", [(-0.01, 0.566), (0.135, 0.606), (0.137, 0.62), (-0.01, 0.588)], -0.015, 0.015, RUBBER, bevel=0.003)
ex.cyl("Lever Pivot", (0, 0.575, -0.002), 0.006, 0.042, "x", CHROME, seg=12)
# Safety pin and tamper seal.
ex.cyl("Pin", (0.008, 0.56, 0.02), 0.0025, 0.06, "x", CHROME, seg=8)
ex.tube("Pin Ring", [(0.05, 0.56 + 0.013 * math.cos(a), 0.02 + 0.013 * math.sin(a)) for a in
                     [2 * math.pi * k / 16 for k in range(16)]], 0.0022, YELLOW, seg=6, closed=True)
ex.box("Tamper Seal", (0.032, 0.548, 0.02), (0.006, 0.012, 0.008), GREEN, bevel=0.001)
# Hose from the valve down to the discharge horn, which sits in a clip on the shell.
ex.cyl("Hose Fitting", (-0.025, 0.54, 0), 0.01, 0.02, "x", CHROME, seg=12)
ex.tube("Hose", [(-0.035, 0.54, 0), (-0.06, 0.538, 0), (-0.085, 0.522, 0), (-0.1, 0.49, 0), (-0.106, 0.43, 0),
                 (-0.105, 0.33, 0), (-0.1, 0.24, 0), (-0.098, 0.2, 0)], 0.009, RUBBER, seg=12)
ex.cyl("Horn Collar", (-0.098, 0.19, 0), 0.012, 0.02, "y", CHROME, seg=16)
ex.cyl("Horn", (-0.098, 0.12, 0), 0.03, 0.12, "y", RUBBER, seg=24, r2=0.013)
ex.box("Horn Clip", (-0.087, 0.205, 0), (0.026, 0.022, 0.028), RUBBER, bevel=0.003)

# ---- Training pan ----
pan = Prop("Training Pan", PAN_X)
W, D, FLOOR, WALL = 0.75, 0.6, 0.14, 0.17
for sx in (-1, 1):
    for sz in (-1, 1):
        pan.box("Leg", (sx * (W / 2 - 0.04), FLOOR / 2, sz * (D / 2 - 0.04)), (0.05, FLOOR, 0.05), PAN, bevel=0.003)
pan.box("Floor", (0, FLOOR + 0.005, 0), (W, 0.01, D), PAN, bevel=0.002)
pan.box("Soot", (0, FLOOR + 0.0105, 0), (W - 0.03, 0.001, D - 0.03), SOOT, bevel=0)
for sz in (-1, 1):
    pan.box("Wall", (0, FLOOR + WALL / 2, sz * (D / 2 - 0.004)), (W, WALL, 0.008), PAN, bevel=0.002)
    pan.box("Rim", (0, FLOOR + WALL, sz * (D / 2 + 0.008)), (W + 0.04, 0.008, 0.032), PAN, bevel=0.002)
for sx in (-1, 1):
    pan.box("Wall", (sx * (W / 2 - 0.004), FLOOR + WALL / 2, 0), (0.008, WALL, D), PAN, bevel=0.002)
    pan.box("Rim", (sx * (W / 2 + 0.008), FLOOR + WALL, 0), (0.032, 0.008, D + 0.04), PAN, bevel=0.002)
# Soot on the inside faces of the walls, where the flames lick.
for sz in (-1, 1):
    pan.box("Wall Soot", (0, FLOOR + 0.01 + (WALL - 0.03) / 2, sz * (D / 2 - 0.0085)), (W - 0.02, WALL - 0.03, 0.001), SOOT, bevel=0)
for sx in (-1, 1):
    pan.box("Wall Soot", (sx * (W / 2 - 0.0085), FLOOR + 0.01 + (WALL - 0.03) / 2, 0), (0.001, WALL - 0.03, D - 0.02), SOOT, bevel=0)
# Two perforated burner pipes, joined by a manifold that runs out through the front wall.
for z in (-0.07, 0.07):
    pan.box("Burner", (-0.03, FLOOR + 0.03, z), (0.56, 0.035, 0.035), PIPE, bevel=0.004)
    for i in range(11):
        pan.cyl("Burner Hole", (-0.28 + i * 0.05, FLOOR + 0.0478, z), 0.0045, 0.002, "y", HOLE, seg=8)
pan.box("Manifold", (0.26, FLOOR + 0.03, 0), (0.035, 0.035, 0.175), PIPE, bevel=0.004)
pan.cyl("Inlet Pipe", (0.26, FLOOR + 0.03, -0.2), 0.012, 0.2, "z", GALV, seg=16)
pan.box("Valve Body", (0.26, FLOOR + 0.03, -0.33), (0.045, 0.045, 0.06), BRASS, bevel=0.006)
pan.cyl("Valve Stem", (0.26, FLOOR + 0.07, -0.33), 0.005, 0.05, "y", GALV, seg=8)
pan.wheel("Valve Wheel", (0.26, FLOOR + 0.098, -0.33), 0.038, "y", VALVE)
pan.cyl("Hose Barb", (0.3, FLOOR + 0.03, -0.33), 0.009, 0.04, "x", BRASS, seg=12)
# Supply post to the right, with its own shut-off valve, and the hose to the inlet.
PX, PZ = W / 2 + 0.17, -0.12
pan.cyl("Post Base", (PX, 0.006, PZ), 0.06, 0.012, "y", GALV, seg=24, bevel=0.002)
pan.cyl("Post", (PX, 0.27, PZ), 0.022, 0.52, "y", GALV, seg=20)
pan.box("Post Valve", (PX, 0.55, PZ), (0.05, 0.045, 0.045), BRASS, bevel=0.006)
pan.cyl("Post Cap", (PX, 0.585, PZ), 0.012, 0.03, "y", GALV, seg=12)
pan.cyl("Post Stem", (PX - 0.04, 0.55, PZ), 0.005, 0.04, "x", GALV, seg=8)
pan.wheel("Post Wheel", (PX - 0.06, 0.55, PZ), 0.028, "x", VALVE)
pan.cyl("Post Outlet", (PX + 0.035, 0.53, PZ), 0.009, 0.03, "x", BRASS, seg=12)
pan.tube("Supply Hose", [(PX + 0.05, 0.53, PZ), (PX + 0.08, 0.5, PZ - 0.01), (PX + 0.09, 0.35, PZ - 0.05),
                         (PX + 0.06, 0.06, PZ - 0.12), (PX - 0.02, 0.015, PZ - 0.2), (0.42, 0.02, -0.33),
                         (0.36, 0.1, -0.34), (0.32, FLOOR + 0.03, -0.33)], 0.011, HOSE, seg=12)

# ---- Bake and export ----
MARKER = os.path.join(os.path.dirname(P.__file__), "fire_practice.done")
if os.path.exists(MARKER):
    os.remove(MARKER)
if bpy.app.background:
    t0 = time.time()
    report = {}
    for prop, filename, part, prefix, size in ((ex, "FireExtinguisher.fbx", "Fire Extinguisher", "extinguisher", 1024),
                                                (pan, "TrainingPan.fbx", "Training Pan", "firepan", 1024)):
        exp = bpy.data.collections.new("Export"); bpy.context.scene.collection.children.link(exp)
        origin = (prop.ox, PARK_Y, 0)
        made = P.join_parts({part: list(prop.col.objects)}, {part: origin}, exp)
        bpy.ops.mesh.primitive_plane_add(size=3, location=origin)
        floor = bpy.context.active_object
        for c in list(floor.users_collection): c.objects.unlink(floor)
        exp.objects.link(floor)
        P.bake(list(made.values()), prefix, occluders=[floor], size=size, samples=32)
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
print(len(ex.col.objects), len(pan.col.objects), "objects")
