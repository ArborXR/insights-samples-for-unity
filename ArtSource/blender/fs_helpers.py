import bpy, bmesh, math
from mathutils import Matrix, Vector, Euler

PALETTE_NAMES = ["navy","purple","light_purple","light_grey","grey","green","safety_red","black",
                 "silver","safety_yellow","white","cardboard","brass","dark_grey","label_cream","flame_orange"]

def swatch_uv(name):
    i = PALETTE_NAMES.index(name)
    col, row = i % 4, i // 4
    return ((col + 0.5) / 4.0, 1.0 - (row + 0.5) / 4.0)

def empty(name, parent=None, loc=(0, 0, 0)):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.1
    bpy.context.scene.collection.objects.link(e)
    e.parent = parent
    e.location = loc
    return e

def part(name, kind, dims, color, parent, pivot=(0, 0, 0), offset=(0, 0, 0), rot=(0, 0, 0), segments=12):
    bm = bmesh.new()
    if kind == "cyl":
        r, d = dims
        bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=r, radius2=r, depth=d)
    elif kind == "cone":
        r1, r2, d = dims
        bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=r1, radius2=r2, depth=d)
    elif kind == "box":
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=Vector(dims), verts=bm.verts)
    elif kind == "torus":
        major, minor = dims
        # Low-poly ring: a thin cylinder loop approximated by a cone with a hole is overkill; use a short tube.
        bmesh.ops.create_circle(bm, cap_ends=False, segments=segments, radius=major)
        geom = bmesh.ops.extrude_edge_only(bm, edges=bm.edges[:])
        bmesh.ops.translate(bm, vec=Vector((0, 0, minor * 2)), verts=[v for v in geom["geom"] if isinstance(v, bmesh.types.BMVert)])
    m = Matrix.Translation(Vector(offset)) @ Euler(rot).to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    uv = bm.loops.layers.uv.new("UVMap")
    u, v = swatch_uv(color)
    for f in bm.faces:
        f.smooth = False
        for l in f.loops:
            l[uv].uv = (u, v)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(bpy.data.materials["palette"])
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = pivot
    return obj

def merge_into(target, *others):
    """Join extra parts into an existing part, keeping the target's name and pivot.

    Parts share a parent and carry no object rotation (rotations are baked into the mesh),
    so each part's offset from the target is just the difference in location.
    """
    bm = bmesh.new()
    bm.from_mesh(target.data)
    for o in others:
        m = o.data.copy()
        m.transform(Matrix.Translation(o.location - target.location))
        bm.from_mesh(m)
        bpy.data.meshes.remove(m)
        old = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if old.users == 0:
            bpy.data.meshes.remove(old)
    bm.to_mesh(target.data)
    bm.free()
    return target
