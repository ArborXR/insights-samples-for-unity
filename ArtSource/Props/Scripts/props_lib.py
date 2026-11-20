"""Shared helpers for building, baking and exporting the station props.

Blender is Z-up with the station's front at -Y. Exports pre-rotate the geometry half a turn so that
Unity (Y-up, front at -Z) gets parts with no rotations and the same +X.
"""
import bpy, bmesh, math, os
import numpy as np
from mathutils import Vector, Matrix, Euler

MAIN = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
MODELS = os.path.join(MAIN, "Assets/InsightsSamples/_Common/Props/Models")
FLIP = Matrix.Rotation(math.pi, 4, "Z")


def collection(name, clear=True):
    col = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if col.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(col)
    if clear:
        for o in list(col.objects):
            bpy.data.objects.remove(o, do_unlink=True)
    return col


def principled(m):
    m.use_nodes = True
    return next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")


def material(name, color, rough, metal=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    b = principled(m)
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    m.diffuse_color = (*color, 1)
    return m


def finish(col, name, bm, mat, bevel=0.0, segs=2, angle=40, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    ob.data.materials.append(mat)
    col.objects.link(ob)
    if bevel:
        mod = ob.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segs
        mod.limit_method = "ANGLE"
        mod.angle_limit = math.radians(angle)
        mod.harden_normals = True
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    return ob


def box(col, name, center, size, mat, bevel=0.003, segs=2):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2])) + Vector(center)
    return finish(col, name, bm, mat, bevel, segs)


AXES = {"x": (0, math.pi / 2, 0), "y": (math.pi / 2, 0, 0), "z": (0, 0, 0)}


def cyl(col, name, center, r, depth, axis, mat, seg=24, bevel=0.0, r2=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=depth)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Euler(AXES[axis]).to_matrix())
    bmesh.ops.translate(bm, verts=bm.verts, vec=Vector(center))
    return finish(col, name, bm, mat, bevel)


def profile_extrude(col, name, points, x0, x1, mat, bevel=0.0, segs=2, angle=40):
    """Extrudes a closed (y, z) profile along X from x0 to x1."""
    bm = bmesh.new()
    a = [bm.verts.new((x0, y, z)) for y, z in points]
    b = [bm.verts.new((x1, y, z)) for y, z in points]
    k = len(points)
    for i in range(k):
        j = (i + 1) % k
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish(col, name, bm, mat, bevel, segs, angle)


def stadium(cx, cz, half_len, r, seg=10):
    """Closed stadium outline in (x, z), centred on (cx, cz), long along X."""
    pts = []
    for i in range(seg + 1):
        a = -math.pi / 2 + math.pi * i / seg
        pts.append((cx + half_len + r * math.cos(a), cz + r * math.sin(a)))
    for i in range(seg + 1):
        a = math.pi / 2 + math.pi * i / seg
        pts.append((cx - half_len + r * math.cos(a), cz + r * math.sin(a)))
    return pts


def plate_xz(col, name, outline, y0, y1, mat, bevel=0.0, segs=2):
    """Extrudes a closed (x, z) outline along Y from y0 to y1."""
    bm = bmesh.new()
    a = [bm.verts.new((x, y0, z)) for x, z in outline]
    b = [bm.verts.new((x, y1, z)) for x, z in outline]
    k = len(outline)
    for i in range(k):
        j = (i + 1) % k
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish(col, name, bm, mat, bevel, segs)


def view3d():
    if bpy.app.background:
        return {}
    win = bpy.context.window_manager.windows[0]
    area = next(a for a in win.screen.areas if a.type == "VIEW_3D")
    region = next(r for r in area.regions if r.type == "WINDOW")
    return dict(window=win, screen=win.screen, area=area, region=region)


def select_only(objs):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def join_parts(parts, pivots, export_col):
    """Copies each part's source objects, applies modifiers, joins them, and moves the pivot."""
    made = {}
    for name, objs in parts.items():
        copies = []
        for o in objs:
            c = o.copy()
            c.data = o.data.copy()
            c.parent = None
            c.matrix_world = o.matrix_world.copy()
            export_col.objects.link(c)
            copies.append(c)
            with bpy.context.temp_override(object=c, active_object=c, selected_objects=[c]):
                for m in list(c.modifiers):
                    bpy.ops.object.modifier_apply(modifier=m.name)
        if len(copies) > 1:
            with bpy.context.temp_override(active_object=copies[0], object=copies[0], selected_objects=copies,
                                           selected_editable_objects=copies):
                bpy.ops.object.join()
        j = copies[0]
        with bpy.context.temp_override(active_object=j, object=j, selected_objects=[j], selected_editable_objects=[j]):
            bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        j["pivot"] = list(pivots[name])
        made[name] = j
    return made


def _image(name, size, noncolor):
    old = bpy.data.images.get(name)
    if old:
        bpy.data.images.remove(old)
    img = bpy.data.images.new(name, size, size, alpha=True)
    img.colorspace_settings.name = "Non-Color" if noncolor else "sRGB"
    return img


def _pixels(img):
    a = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(img.size[1], img.size[0], 4)


def _save(name, arr, path, noncolor):
    old = bpy.data.images.get(name)
    if old:
        bpy.data.images.remove(old)
    img = bpy.data.images.new(name, arr.shape[1], arr.shape[0], alpha=True)
    img.colorspace_settings.name = "Non-Color" if noncolor else "sRGB"
    img.pixels.foreach_set(arr.astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return img


def bake(objs, prefix, occluders=(), size=1024, samples=48, ao_distance=0.25, ao_in_albedo=0.35):
    """Unwraps objs into one shared atlas and bakes it: <prefix>_albedo.png (sRGB, light AO multiplied in) and
    <prefix>_mask.png (R metallic, G ambient occlusion, A smoothness) for URP Lit's metallic and occlusion maps.
    Only objs and occluders are visible to the bake."""
    scn = bpy.context.scene
    keep = set(objs) | set(occluders)
    hidden = [o for o in scn.objects if o not in keep and not o.hide_render]
    for o in hidden:
        o.hide_render = True
    prev_engine = scn.render.engine
    scn.render.engine = "CYCLES"
    scn.cycles.samples = samples
    scn.world.light_settings.distance = ao_distance

    # One UV layout shared by every object, packed together.
    for o in objs:
        me = o.data
        while me.uv_layers:
            me.uv_layers.remove(me.uv_layers[0])
        me.uv_layers.new(name="UVMap")
    select_only(objs)
    with bpy.context.temp_override(**view3d(), active_object=objs[0], object=objs[0], selected_objects=objs,
                                   selected_editable_objects=objs):
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.003, scale_to_bounds=False)
        bpy.ops.uv.pack_islands(margin=0.004, rotate=True)
        bpy.ops.object.mode_set(mode="OBJECT")

    mats = {m for o in objs for m in o.data.materials if m}
    def run(kind, img, setup=None):
        nodes = []
        for m in mats:
            n = m.node_tree.nodes.new("ShaderNodeTexImage")
            n.image = img
            m.node_tree.nodes.active = n
            nodes.append((m, n))
        restore = setup() if setup else None
        select_only(objs)
        with bpy.context.temp_override(**view3d(), active_object=objs[0], object=objs[0], selected_objects=objs,
                                       selected_editable_objects=objs):
            if kind == "DIFFUSE":
                bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, margin=6, use_clear=True)
            else:
                bpy.ops.object.bake(type=kind, margin=6, use_clear=True)
        if restore:
            restore()
        for m, n in nodes:
            m.node_tree.nodes.remove(n)

    def emit(value_of):
        saved = []
        for m in mats:
            b = principled(m)
            saved.append((b, tuple(b.inputs["Emission Color"].default_value), b.inputs["Emission Strength"].default_value))
            v = value_of(b)
            b.inputs["Emission Color"].default_value = (v, v, v, 1)
            b.inputs["Emission Strength"].default_value = 1.0
        def restore():
            for b, c, s in saved:
                b.inputs["Emission Color"].default_value = c
                b.inputs["Emission Strength"].default_value = s
        return restore

    def no_metal():
        # Cycles' diffuse colour pass is black on metals; URP's metallic workflow wants their tint as albedo.
        saved = []
        for m in mats:
            b = principled(m)
            saved.append((b, b.inputs["Metallic"].default_value))
            b.inputs["Metallic"].default_value = 0.0
        def restore():
            for b, v in saved:
                b.inputs["Metallic"].default_value = v
        return restore

    albedo = _image(prefix + "_bake_albedo", size, False)
    ao = _image(prefix + "_bake_ao", size, True)
    metal = _image(prefix + "_bake_metal", size, True)
    rough = _image(prefix + "_bake_rough", size, True)
    run("DIFFUSE", albedo, no_metal)
    run("AO", ao)
    run("EMIT", metal, lambda: emit(lambda b: b.inputs["Metallic"].default_value))
    run("EMIT", rough, lambda: emit(lambda b: b.inputs["Roughness"].default_value))

    A = _pixels(albedo)
    O = _pixels(ao)[..., 0:1]
    Mt = _pixels(metal)[..., 0]
    R = _pixels(rough)[..., 0]
    srgb = np.clip(A[..., :3], 0, 1)
    lin = np.where(srgb <= 0.04045, srgb / 12.92, ((srgb + 0.055) / 1.055) ** 2.4)
    lin *= (1 - ao_in_albedo) + ao_in_albedo * O
    out = np.where(lin <= 0.0031308, lin * 12.92, 1.055 * np.power(np.clip(lin, 0, None), 1 / 2.4) - 0.055)
    alb = np.concatenate([out, np.ones_like(O)], axis=-1)
    mask = np.stack([Mt, O[..., 0], np.zeros_like(Mt), 1 - R], axis=-1)
    _save(prefix + "_albedo", alb, os.path.join(MODELS, prefix + "_albedo.png"), False)
    _save(prefix + "_mask", mask, os.path.join(MODELS, prefix + "_mask.png"), True)
    for img in (albedo, ao, metal, rough):
        bpy.data.images.remove(img)

    for o in hidden:
        o.hide_render = False
    scn.render.engine = prev_engine


def single_material(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    obj.data.polygons.foreach_set("material_index", [0] * len(obj.data.polygons))


def export(filename, made, origin):
    """Exports joined parts. Each part's mesh is centred on its pivot, which becomes its position."""
    # Free up the part names: Blender would otherwise export "Name.001" where a source object is called "Name".
    renamed = []
    for name in made:
        other = bpy.data.objects.get(name)
        if other is not None and other not in made.values():
            other.name = name + " (source)"
            renamed.append((other, name))
    for name, j in made.items():
        pivot = Vector(j["pivot"])
        j.data.transform(Matrix.Translation(-pivot))
        j.data.transform(FLIP)
        j.location = FLIP @ (pivot - Vector(origin))
        j.name = name
        j.data.name = name
    select_only(list(made.values()))
    bpy.ops.export_scene.fbx(filepath=os.path.join(MODELS, filename), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="OFF", use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False, path_mode="STRIP", embed_textures=False)
    for o in made.values():
        o.name = o.name + " (exported)"
    for other, name in renamed:
        other.name = name


def cleanup(export_col):
    for o in list(export_col.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.collections.remove(export_col)


def triangles(objs):
    dg = bpy.context.evaluated_depsgraph_get()
    n = 0
    for o in objs:
        me = o.evaluated_get(dg).to_mesh()
        me.calc_loop_triangles()
        n += len(me.loop_triangles)
        o.evaluated_get(dg).to_mesh_clear()
    return n
