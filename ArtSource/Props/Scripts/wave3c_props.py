"""Builds the last batch of Wave 3 props: WelderCart and WeldingScreen (13), PosTerminal and Smartphone (14), and
ExtinguisherCabinet (17).

Unity-local space, origin on the floor (or the surface a prop rests on, or the wall it hangs on) under its centre,
front at -z.
"""
import bpy, bmesh, math, importlib, os, time
from mathutils import Vector, Matrix
import props_lib as P
importlib.reload(P)

PARK_Y = 34.0


def lin(c): return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def mat(name, c, rough, metal=0.0): return P.material(name, lin(c), rough, metal)


class Prop:
    """Authoring helpers in the prop's Unity-local coordinates."""

    def __init__(self, name, ox):
        self.col = P.collection(name)
        self.ox = ox

    def b(self, u): return Vector((self.ox + u[0], PARK_Y + u[2], u[1]))

    def box(self, name, c, s, m, bevel=0.003, segs=2, tilt=0.0, pivot=None):
        """A box; tilt leans it back (top toward +z) by degrees about x through pivot (default its centre)."""
        o = P.box(self.col, name, self.b(c), (s[0], s[2], s[1]), m, bevel, segs)
        if tilt:
            p = self.b(pivot if pivot is not None else c)
            o.data.transform(Matrix.Translation(p) @ Matrix.Rotation(math.radians(-tilt), 4, "X") @ Matrix.Translation(-p))
        return o

    def cyl(self, name, c, r, d, axis, m, seg=24, bevel=0.0, r2=None):
        return P.cyl(self.col, name, self.b(c), r, d, {"x": "x", "y": "z", "z": "y"}[axis], m, seg, bevel, r2)

    def lathe(self, name, profile, m, centre, seg=32):
        bm = bmesh.new()
        c = self.b(centre)
        verts = [bm.verts.new((c.x + r, c.y, c.z + h)) for r, h in profile]
        edges = [bm.edges.new((verts[i], verts[i + 1])) for i in range(len(verts) - 1)]
        bmesh.ops.spin(bm, geom=verts + edges, cent=c, axis=(0, 0, 1), angle=math.tau, steps=seg, use_duplicate=False)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return P.finish(self.col, name, bm, m)

    def tube(self, name, points, r, m, seg=12, closed=False):
        pts = [self.b(p) for p in points]
        n = len(pts)
        tangents = [((pts[(i + 1) % n] - pts[i - 1]) if closed else (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)])).normalized() for i in range(n)]
        ref = Vector((0, 0, 1)) if abs(tangents[0].z) < 0.9 else Vector((1, 0, 0))
        normal = tangents[0].cross(ref).normalized()
        bm = bmesh.new()
        rings = []
        for i in range(n):
            if i:
                normal = (normal - tangents[i] * normal.dot(tangents[i])).normalized()
            binormal = tangents[i].cross(normal)
            rings.append([bm.verts.new(pts[i] + r * (math.cos(math.tau * k / seg) * normal + math.sin(math.tau * k / seg) * binormal)) for k in range(seg)])
        for i in range(n if closed else n - 1):
            a, c = rings[i], rings[(i + 1) % n]
            for k in range(seg):
                bm.faces.new((a[k], a[(k + 1) % seg], c[(k + 1) % seg], c[k]))
        if not closed:
            bm.faces.new(list(reversed(rings[0])))
            bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return P.finish(self.col, name, bm, m)

    def ring(self, name, c, r, tube_r, axis, m, seg=24):
        """A closed ring of radius r around axis ("y" only) through c."""
        return self.tube(name, [(c[0] + r * math.cos(math.tau * k / seg), c[1], c[2] + r * math.sin(math.tau * k / seg)) for k in range(seg)],
                         tube_r, m, seg=8, closed=True)

    def coil(self, name, c, r, tube_r, m, turns=3, spread=0.012):
        """A coiled cable hanging in the yz plane at x = c[0]."""
        pts = []
        steps = 24 * turns
        for k in range(steps + 1):
            a = math.tau * k / 24
            pts.append((c[0] + spread * (k / steps - 0.5) * turns, c[1] + r * math.cos(a) * 1.15, c[2] + r * math.sin(a)))
        return self.tube(name, pts, tube_r, m, seg=8)

    def curtain(self, name, x0, x1, y0, y1, z, depth, folds, m):
        """A hanging sheet with soft vertical folds, two-sided."""
        nx, ny = folds * 8, 6
        bm = bmesh.new()
        grid = [[bm.verts.new(self.b((x0 + (x1 - x0) * i / nx, y0 + (y1 - y0) * j / ny,
                                      z + depth * math.sin(math.tau * folds * i / nx) * (0.5 + 0.5 * j / ny)))) for j in range(ny + 1)] for i in range(nx + 1)]
        for i in range(nx):
            for j in range(ny):
                bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
        bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.002)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return P.finish(self.col, name, bm, m)


GUNMETAL = mat("W3c_Gunmetal", (0.3, 0.31, 0.33), 0.5, 0.4)
DARK = mat("W3c_Dark", (0.14, 0.15, 0.16), 0.5, 0.2)
BLACK = mat("W3c_Black", (0.05, 0.05, 0.055), 0.5)
RUBBER = mat("W3c_Rubber", (0.06, 0.06, 0.065), 0.65)
CHROME = mat("W3c_Chrome", (0.84, 0.85, 0.87), 0.2, 1.0)
BRASS = mat("W3c_Brass", (0.78, 0.6, 0.28), 0.3, 1.0)
GREEN_CYL = mat("W3c_Cylinder_Green", (0.18, 0.42, 0.24), 0.4, 0.2)
GAUGE = mat("W3c_Gauge", (0.95, 0.95, 0.94), 0.3)
LCD = mat("W3c_Lcd", (0.1, 0.14, 0.16), 0.2)
STEEL = mat("W3c_Steel", (0.48, 0.49, 0.5), 0.45, 0.8)
ORANGE = mat("W3c_Curtain", (0.85, 0.45, 0.08), 0.4)
SCREEN = mat("W3c_Screen", (0.05, 0.06, 0.08), 0.15)
PRINTER_PAPER = mat("W3c_Paper", (0.95, 0.95, 0.94), 0.6)
KEY_RED = mat("W3c_Key_Red", (0.8, 0.12, 0.1), 0.4)
KEY_GREEN = mat("W3c_Key_Green", (0.15, 0.65, 0.3), 0.4)
KEY_YELLOW = mat("W3c_Key_Yellow", (0.95, 0.78, 0.1), 0.4)
GRAPHITE = mat("W3c_Graphite", (0.22, 0.23, 0.25), 0.35, 0.6)
RED = mat("W3c_Red", (0.78, 0.07, 0.06), 0.35)
WHITE = mat("W3c_White", (0.96, 0.96, 0.95), 0.4)
GLASS = mat("W3c_Glass", (0.9, 0.93, 0.95), 0.05)

# ---- 13 Welder on its cart, with the shielding-gas cylinder chained behind ----
wc = Prop("Welder Cart", 90.0)
wc.box("Cart Tray", (0, 0.14, 0.02), (0.5, 0.025, 0.72), GUNMETAL, bevel=0.004)
for x in (-0.25, 0.25):
    wc.box("Cart Rail", (x, 0.14, 0.02), (0.025, 0.05, 0.72), GUNMETAL, bevel=0.003)
    wc.cyl("Cart Wheel", (x * 1.12, 0.14, 0.3), 0.14, 0.05, "x", RUBBER, seg=28, bevel=0.01)
    wc.cyl("Cart Wheel Hub", (x * 1.12, 0.14, 0.3), 0.05, 0.055, "x", STEEL, seg=16)
    wc.box("Cart Castor Fork", (x * 0.85, 0.08, -0.3), (0.025, 0.07, 0.04), STEEL, bevel=0.003)
    wc.cyl("Cart Castor", (x * 0.85, 0.045, -0.3), 0.045, 0.025, "x", RUBBER, seg=16, bevel=0.004)
    wc.box("Cart Upright", (x, 0.55, -0.34), (0.03, 0.8, 0.03), GUNMETAL, bevel=0.004)
    wc.box("Cart Hook", (x * 1.08, 0.56, -0.2), (0.02, 0.03, 0.08), GUNMETAL, bevel=0.004)
wc.box("Cart Handle", (0, 0.95, -0.34), (0.53, 0.03, 0.03), GUNMETAL, bevel=0.006)
WB = (0, 0.34, -0.03)
wc.box("Welder Case", WB, (0.42, 0.36, 0.56), DARK, bevel=0.008)
wc.box("Welder Panel", (0, WB[1] + 0.02, WB[2] - 0.281), (0.38, 0.3, 0.003), GRAPHITE, bevel=0.0)
wc.box("Welder Display", (-0.1, WB[1] + 0.12, WB[2] - 0.284), (0.08, 0.04, 0.003), LCD, bevel=0.0)
for x in (-0.07, 0.06):
    wc.cyl("Welder Knob", (x, WB[1] + 0.05, WB[2] - 0.295), 0.022, 0.025, "z", BLACK, seg=20, bevel=0.003)
for x in (-0.09, 0.09):
    wc.cyl("Welder Socket", (x, WB[1] - 0.09, WB[2] - 0.29), 0.015, 0.02, "z", BLACK, seg=16)
for k in range(5):
    wc.box("Welder Louvre", (0.1 + 0.0 * k, WB[1] - 0.06 + k * 0.022, WB[2] - 0.284), (0.12, 0.008, 0.004), BLACK, bevel=0.0)
wc.box("Welder Side Door", (0.212, WB[1], WB[2] + 0.02), (0.004, 0.3, 0.42), GRAPHITE, bevel=0.002)
wc.box("Welder Door Latch", (0.216, WB[1] + 0.08, WB[2] - 0.1), (0.006, 0.04, 0.02), BLACK, bevel=0.001)
# Gas cylinder, regulator and gauges, and its chain.
GX, GZ = 0.08, 0.36
wc.lathe("Gas Cylinder", [(0, 0.16), (0.09, 0.16), (0.095, 0.18), (0.095, 1.02), (0.08, 1.08), (0.04, 1.11), (0.022, 1.115), (0, 1.115)], GREEN_CYL, (GX, 0, GZ))
wc.cyl("Gas Valve", (GX, 1.14, GZ), 0.018, 0.05, "y", BRASS, seg=16)
wc.cyl("Gas Valve Wheel", (GX, 1.175, GZ), 0.025, 0.012, "y", CHROME, seg=16)
wc.box("Gas Regulator", (GX + 0.05, 1.15, GZ), (0.07, 0.04, 0.04), BRASS, bevel=0.006)
for dx in (0.05, 0.11):
    wc.cyl("Gas Gauge", (GX + dx, 1.2, GZ - 0.01), 0.026, 0.02, "z", CHROME, seg=20, bevel=0.003)
    wc.cyl("Gas Gauge Face", (GX + dx, 1.2, GZ - 0.021), 0.022, 0.002, "z", GAUGE, seg=20)
wc.tube("Gas Hose", [(GX + 0.08, 1.13, GZ), (GX + 0.12, 1.05, GZ - 0.05), (0.12, 0.6, 0.27), (0.1, 0.45, 0.25)], 0.006, BLACK, seg=8)
wc.ring("Gas Strap", (GX, 0.75, GZ), 0.098, 0.006, "y", GUNMETAL)
# Cables coiled on the side hooks, leads plugged into the sockets.
for x in (-0.28, 0.28):
    wc.coil("Welder Cable Coil", (x, 0.46, -0.2), 0.11, 0.009, BLACK, turns=3, spread=0.01)
wc.tube("Welder Lead", [(-0.09, WB[1] - 0.09, WB[2] - 0.3), (-0.15, WB[1] - 0.12, WB[2] - 0.34), (-0.26, 0.42, -0.24)], 0.009, BLACK, seg=8)
wc.tube("Welder Lead", [(0.09, WB[1] - 0.09, WB[2] - 0.3), (0.15, WB[1] - 0.12, WB[2] - 0.34), (0.26, 0.42, -0.24)], 0.009, BLACK, seg=8)

# ---- 13 Welding screen: steel frame on castor feet, orange curtain on grommets ----
ws = Prop("Welding Screen", 93.0)
FW, FH = 1.6, 1.8
for x in (-FW / 2, FW / 2):
    ws.box("Screen Upright", (x, FH / 2 + 0.06, 0), (0.035, FH, 0.035), STEEL, bevel=0.003)
    ws.box("Screen Foot", (x, 0.07, 0), (0.04, 0.035, 0.6), STEEL, bevel=0.003)
    for z in (-0.27, 0.27):
        ws.box("Screen Castor Fork", (x, 0.04, z), (0.025, 0.035, 0.03), STEEL, bevel=0.003)
        ws.cyl("Screen Castor", (x, 0.025, z), 0.025, 0.018, "x", RUBBER, seg=14, bevel=0.003)
for y in (0.16, FH + 0.06):
    ws.box("Screen Rail", (0, y, 0), (FW, 0.035, 0.035), STEEL, bevel=0.003)
n = 9
for k in range(n):
    x = -FW / 2 + 0.08 + (FW - 0.16) * k / (n - 1)
    ws.cyl("Screen Grommet", (x, FH - 0.0, -0.012), 0.008, 0.004, "z", CHROME, seg=10)
    ws.tube("Screen Tie", [(x, FH - 0.0, -0.012), (x, FH + 0.04, -0.02), (x, FH + 0.075, 0.0)], 0.0025, BLACK, seg=6)
ws.curtain("Screen Curtain", -FW / 2 + 0.04, FW / 2 - 0.04, 0.2, FH + 0.01, -0.02, 0.012, 5, ORANGE)

# ---- 14 POS terminal: tilted touchscreen on a printer base, card reader on an arm ----
pos = Prop("POS Terminal", 96.0)
pos.box("POS Foot", (0, 0.012, 0.02), (0.26, 0.024, 0.22), GRAPHITE, bevel=0.008)
pos.box("POS Body", (0, 0.11, 0.04), (0.17, 0.18, 0.15), GRAPHITE, bevel=0.012)
pos.box("POS Printer Door", (0, 0.1, -0.0365), (0.13, 0.12, 0.004), DARK, bevel=0.002)
pos.box("POS Receipt Slot", (0, 0.158, -0.038), (0.1, 0.006, 0.004), BLACK, bevel=0.0)
pos.box("POS Receipt", (0, 0.168, -0.045), (0.07, 0.025, 0.002), PRINTER_PAPER, bevel=0.0, tilt=-20)
SC = (0, 0.33, 0.02)
pos.box("POS Neck", (0, 0.22, 0.06), (0.08, 0.07, 0.06), GRAPHITE, bevel=0.008)
pos.box("POS Screen Housing", SC, (0.38, 0.27, 0.03), BLACK, bevel=0.006, tilt=18)
pos.box("POS Screen", (SC[0], SC[1] + 0.004, SC[2] - 0.0155), (0.35, 0.235, 0.001), SCREEN, bevel=0.0, tilt=18, pivot=SC)
# Card reader on its swivel arm, to the right.
pos.tube("POS Arm", [(0.08, 0.12, 0.06), (0.17, 0.13, 0.05), (0.22, 0.14, 0.02)], 0.012, GRAPHITE, seg=10)
RC = (0.25, 0.19, 0.0)
pos.box("POS Reader", RC, (0.08, 0.14, 0.025), BLACK, bevel=0.006, tilt=25)
pos.box("POS Reader Screen", (RC[0], RC[1] + 0.035, RC[2] - 0.013), (0.055, 0.035, 0.001), SCREEN, bevel=0.0, tilt=25, pivot=RC)
for i, m in enumerate((KEY_RED, KEY_YELLOW, KEY_GREEN)):
    pos.box("POS Reader Key", (RC[0] - 0.02 + i * 0.02, RC[1] - 0.045, RC[2] - 0.0135), (0.014, 0.008, 0.003), m, bevel=0.001, tilt=25, pivot=RC)
for r in range(3):
    for c in range(3):
        pos.box("POS Reader Key", (RC[0] - 0.02 + c * 0.02, RC[1] - 0.025 + r * 0.013 - 0.0 , RC[2] - 0.0135), (0.014, 0.008, 0.003), GRAPHITE, bevel=0.001, tilt=25, pivot=RC)
pos.tube("POS Cable", [(-0.05, 0.03, 0.1), (-0.1, 0.01, 0.16), (-0.2, 0.006, 0.2)], 0.004, BLACK, seg=6)

# ---- 14 Smartphone lying face up ----
ph = Prop("Smartphone", 99.0)
ph.box("Phone Body", (0, 0.004, 0), (0.074, 0.008, 0.155), GRAPHITE, bevel=0.004, segs=3)
ph.box("Phone Bezel", (0, 0.0082, 0), (0.07, 0.001, 0.151), BLACK, bevel=0.0)
ph.box("Phone Button", (0.0375, 0.004, -0.02), (0.002, 0.003, 0.02), GRAPHITE, bevel=0.0)
ph.box("Phone Screen", (0, 0.0088, 0), (0.066, 0.0005, 0.146), SCREEN, bevel=0.0)

# ---- 17 Extinguisher wall cabinet, empty (its extinguisher is out at the practice pan) ----
ec = Prop("Extinguisher Cabinet", 101.0)
CWd, CHt, CDp = 0.42, 0.78, 0.22  # origin: bottom centre of the back face, on the wall
ec.box("Cabinet Back", (0, CHt / 2, -0.005), (CWd, CHt, 0.01), RED, bevel=0.002)
for x in (-CWd / 2 + 0.006, CWd / 2 - 0.006):
    ec.box("Cabinet Side", (x, CHt / 2, -CDp / 2), (0.012, CHt, CDp), RED, bevel=0.003)
for y in (0.006, CHt - 0.006):
    ec.box("Cabinet End", (0, y, -CDp / 2), (CWd, 0.012, CDp), RED, bevel=0.003)
DZ = -CDp - 0.008
ec.box("Door Stile", (-CWd / 2 + 0.03, CHt / 2, DZ), (0.04, CHt - 0.02, 0.018), RED, bevel=0.004)
ec.box("Door Stile", (CWd / 2 - 0.03, CHt / 2, DZ), (0.04, CHt - 0.02, 0.018), RED, bevel=0.004)
ec.box("Door Rail", (0, 0.03, DZ), (CWd - 0.02, 0.04, 0.018), RED, bevel=0.004)
ec.box("Door Rail", (0, CHt - 0.03, DZ), (CWd - 0.02, 0.04, 0.018), RED, bevel=0.004)
ec.box("Door Rail", (0, CHt - 0.15, DZ), (CWd - 0.02, 0.03, 0.018), RED, bevel=0.004)
ec.box("Door Sign", (0, CHt - 0.09, DZ - 0.001), (CWd - 0.1, 0.09, 0.012), WHITE, bevel=0.002)
ec.box("Door Hinge", (-CWd / 2 + 0.006, CHt / 2, DZ), (0.008, CHt - 0.08, 0.012), STEEL, bevel=0.002)
ec.box("Door Lock", (CWd / 2 - 0.03, CHt / 2 - 0.02, DZ - 0.012), (0.02, 0.03, 0.012), CHROME, bevel=0.003)
for y in (0.28, 0.52):
    ec.box("Cabinet Bracket", (0, y, -0.04), (0.16, 0.025, 0.06), RED, bevel=0.003)
    ec.box("Cabinet Bracket Slot", (0.04, y, -0.0705), (0.03, 0.008, 0.001), BLACK, bevel=0.0)
ec.box("Door Glass", (0, (0.05 + CHt - 0.165) / 2, DZ + 0.002), (CWd - 0.08, CHt - 0.215, 0.004), GLASS, bevel=0.0)

# ---- Bake and export ----
MARKER = os.path.join(os.path.dirname(P.__file__), "wave3c_props.done")
if os.path.exists(MARKER):
    os.remove(MARKER)
if bpy.app.background:
    t0 = time.time()
    report = {}

    def objs(prop, *prefixes, exclude=()):
        return [o for o in prop.col.objects
                if (not prefixes or o.name.startswith(prefixes)) and not (exclude and o.name.startswith(exclude))]

    jobs = [
        (wc, "WelderCart.fbx", "weldercart", {"Welder Cart": (objs(wc), (0, 0, 0))}, ()),
        (ws, "WeldingScreen.fbx", "weldscreen", {"Welding Screen": (objs(ws), (0, 0, 0))}, ()),
        (pos, "PosTerminal.fbx", "pos", {"POS Terminal": (objs(pos, exclude=("POS Screen",)), (0, 0, 0)), "POS Screen": (objs(pos, "POS Screen"), SC)}, ("POS Screen",)),
        (ph, "Smartphone.fbx", "phone", {"Smartphone": (objs(ph, exclude=("Phone Screen",)), (0, 0, 0)), "Phone Screen": (objs(ph, "Phone Screen"), (0, 0, 0))}, ("Phone Screen",)),
        (ec, "ExtinguisherCabinet.fbx", "extcabinet", {"Extinguisher Cabinet": (objs(ec, exclude=("Door Glass",)), (0, 0, 0)), "Cabinet Glass": (objs(ec, "Door Glass"), (0, 0, 0))}, ("Cabinet Glass",)),
    ]
    for prop, filename, prefix, parts, unbaked in jobs:
        exp = bpy.data.collections.new("Export"); bpy.context.scene.collection.children.link(exp)
        origin = tuple(prop.b((0, 0, 0)))
        made = P.join_parts({k: v[0] for k, v in parts.items()}, {k: tuple(prop.b(v[1])) for k, v in parts.items()}, exp)
        bpy.ops.mesh.primitive_plane_add(size=4, location=origin)
        floor = bpy.context.active_object
        for c in list(floor.users_collection): c.objects.unlink(floor)
        exp.objects.link(floor)
        baked_parts = [o for k, o in made.items() if k not in unbaked]
        P.bake(baked_parts, prefix, occluders=([floor] if prefix != "extcabinet" else []) + [made[k] for k in unbaked], size=1024, samples=32)
        bpy.data.objects.remove(floor, do_unlink=True)
        name = prefix.capitalize() + "_Baked"
        baked = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        for o in baked_parts: P.single_material(o, baked)
        for k in unbaked:
            P.single_material(made[k], bpy.data.materials.get(k.replace(" ", "_")) or bpy.data.materials.new(k.replace(" ", "_")))
        P.export(filename, made, origin)
        report[filename] = {k: P.triangles([v]) for k, v in made.items()}
        P.cleanup(exp)
    bpy.ops.wm.save_mainfile()
    with open(MARKER, "w") as f:
        f.write(f"{report} in {round(time.time() - t0, 1)}s\n")
print({p.col.name: len(p.col.objects) for p in (wc, ws, pos, ph, ec)})
