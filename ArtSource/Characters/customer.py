"""Builds sample 14's customer with MPFB (MakeHuman for Blender) and exports Customer.fbx with Idle and Greet takes.

Needs the MPFB extension and its CC0 "makehuman_system_assets" pack. Every asset used here is from that pack, so the
export is CC0. Run it in Blender's GUI (Scripting > Run Script): MPFB reads its UI preferences as it loads, so a
headless Blender can't load it. Textures are copied separately (see TEXTURES below) at reduced size.
"""
import bpy, bmesh, importlib, math, os
from mathutils import Quaternion, Vector
from mathutils.bvhtree import BVHTree

MPFB = "bl_ext.user_default.mpfb."
import addon_utils
if MPFB.rstrip(".") not in bpy.context.preferences.addons:
    addon_utils.enable(MPFB.rstrip("."), default_set=False)
HS = importlib.import_module(MPFB + "services.humanservice").HumanService
TS = importlib.import_module(MPFB + "services.targetservice").TargetService
LS = importlib.import_module(MPFB + "services.locationservice").LocationService
DATA = LS.get_user_data("")
MAIN = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(MAIN, "Assets/InsightsSamples/_Common/Characters/Customer")
SKIN = "skins/middleage_asian_female/middleage_asian_female.mhmat"
SKIN_TINT = (0.86, 0.72, 0.6, 1)  # linear; warms the light skin toward light olive (Customer_Skin's base colour in Unity)
TEXTURES = {  # source in the pack -> file in Textures/, longest side
    "skins/middleage_asian_female/middleage_lightskinned_female_diffuse2.png": ("customer_skin.jpg", 2048),
    "eyes/materials/brown_eye.png": ("customer_eyes.jpg", 512),
    "clothes/male_casualsuit05/male_casualsuit05_diffuse.png": ("customer_clothes.jpg", 1024),
    "clothes/male_casualsuit05/male_casualsuit05_ao.png": ("customer_clothes_ao.jpg", 1024),
    "clothes/male_casualsuit05/male_casualsuit05_normal.png": ("customer_clothes_normal.png", 1024),
    "clothes/shoes02/shoes02_diffuse.png": ("customer_shoes.jpg", 1024),
    "hair/ponytail01/ponytail01_diffuse.png": ("customer_hair.png", 1024),
    "eyebrows/eyebrow002/eyebrow002.png": ("customer_brows.png", 512),
    "eyelashes/eyelashes02/eyelashes02.png": ("customer_lashes.png", 512),
}

col = bpy.data.collections.get("Customer") or bpy.data.collections.new("Customer")
if col.name not in bpy.context.scene.collection.children:
    bpy.context.scene.collection.children.link(col)
for o in list(col.all_objects):
    bpy.data.objects.remove(o, do_unlink=True)
for name in ("Customer_Skin", "Customer_Eyes", "Customer_Brows", "Customer_Lashes", "Customer_Hair", "Customer_Clothes", "Customer_Shoes"):
    if bpy.data.materials.get(name):
        bpy.data.materials.remove(bpy.data.materials[name])
bpy.data.orphans_purge(do_recursive=True)
bpy.context.view_layer.active_layer_collection = bpy.context.view_layer.layer_collection.children[col.name]

# ---- Body, rig, assets ----
macro = {"gender": 0.0, "age": 0.55, "muscle": 0.5, "weight": 0.52, "proportions": 0.6, "height": 0.55, "cupsize": 0.5,
         "firmness": 0.5, "race": {"asian": 0.33, "caucasian": 0.34, "african": 0.33}}
basemesh = HS.create_human(macro_detail_dict=macro)
# A friendly resting face: the default reads as a frown. Targets go on before the proxy is fitted and trimmed.
for target, weight in (("mouth/mouth-angles-up", 0.8), ("mouth/mouth-laugh-lines-out", 0.3), ("eyebrows/eyebrows-angle-up", 0.5),
                       ("eyebrows/eyebrows-trans-up", 0.25), ("cheek/l-cheek-trans-up", 0.3), ("cheek/r-cheek-trans-up", 0.3)):
    TS.load_target(basemesh, os.path.join(os.path.dirname(importlib.import_module(MPFB.rstrip(".")).__file__), "data/targets",
                                          target + ".target.gz"), weight=weight)
HS.add_builtin_rig(basemesh, "game_engine")
rig = basemesh.parent
for sub, kind in (("eyes/low-poly/low-poly.mhclo", "Eyes"), ("eyebrows/eyebrow002/eyebrow002.mhclo", "Eyebrows"),
                  ("eyelashes/eyelashes02/eyelashes02.mhclo", "Eyelashes"), ("hair/ponytail01/ponytail01.mhclo", "Hair"),
                  ("clothes/male_casualsuit05/male_casualsuit05.mhclo", "Clothes"), ("clothes/shoes02/shoes02.mhclo", "Clothes")):
    HS.add_mhclo_asset(os.path.join(DATA, sub), basemesh, asset_type=kind, subdiv_levels=0, material_type="MAKESKIN")
bpy.ops.object.select_all(action="DESELECT")
basemesh.select_set(True)
bpy.context.view_layer.objects.active = basemesh
bpy.ops.mpfb.load_library_proxy(filepath=os.path.join(DATA, "proxymeshes/female_generic/female_generic.proxy"), object_type="Proxymeshes")
body = next(o for o in col.all_objects if o.name.startswith("female_generic"))
HS.set_character_skin(os.path.join(DATA, SKIN), basemesh, bodyproxy=body, skin_type="MAKESKIN")
basemesh.hide_set(True)
basemesh.hide_render = True
for o in col.all_objects:
    for md in list(o.modifiers):
        if md.type == "SUBSURF":
            o.modifiers.remove(md)
for m in {m for m in body.data.materials if m}:
    mix = next(n for n in m.node_tree.nodes if n.type == "MIX_RGB")
    mix.blend_type = "MULTIPLY"
    mix.inputs[0].default_value = 1.0
    for i in mix.inputs[1:]:
        if not i.is_linked:
            i.default_value = SKIN_TINT

# ---- Delete body faces hidden under the clothes (rays out along the normal, and fanned around it, all hit cloth) ----
cloth = [o for o in col.all_objects if o.name.endswith(("male_casualsuit05", "shoes02"))]
for o in cloth + [body]:
    o.modifiers["Armature"].show_viewport = False
depsgraph = bpy.context.evaluated_depsgraph_get()
trees = []
for o in cloth:
    evaluated = o.evaluated_get(depsgraph)
    me = evaluated.to_mesh()
    bm = bmesh.new(); bm.from_mesh(me); bm.transform(o.matrix_world)
    trees.append(BVHTree.FromBMesh(bm)); bm.free(); evaluated.to_mesh_clear()
bm = bmesh.new(); bm.from_mesh(body.data)
mw, nm = body.matrix_world, body.matrix_world.to_3x3()
hidden = []
for f in bm.faces:
    c, n = mw @ f.calc_center_median(), (nm @ f.normal).normalized()
    t = (nm @ (f.verts[1].co - f.verts[0].co)).normalized(); s = n.cross(t)
    rays = [n] + [(n + d * 0.5).normalized() for d in (t, -t, s, -s)]
    if all(any(tree.ray_cast(c + n * 0.001, r, 0.07)[0] is not None for tree in trees) for r in rays):
        hidden.append(f)
bmesh.ops.delete(bm, geom=hidden, context="FACES")
bm.to_mesh(body.data); bm.free()
for o in cloth + [body]:
    o.modifiers["Armature"].show_viewport = True

# ---- Names: one material per part, and one shared skin material ----
for prefix, (obj_name, mat_name) in {"female_generic": ("Customer Body", "Customer_Skin"), "Human.low-poly": ("Customer Eyes", "Customer_Eyes"),
                                     "Human.eyebrow": ("Customer Brows", "Customer_Brows"), "Human.eyelashes": ("Customer Lashes", "Customer_Lashes"),
                                     "Human.ponytail": ("Customer Hair", "Customer_Hair"), "Human.male_casualsuit": ("Customer Clothes", "Customer_Clothes"),
                                     "Human.shoes": ("Customer Shoes", "Customer_Shoes")}.items():
    o = next(o for o in col.all_objects if o.name.startswith(prefix))
    first = o.data.materials[0]
    first.name = mat_name
    for i in range(len(o.data.materials)):
        o.data.materials[i] = first
    o.name = o.data.name = obj_name
rig.name, rig.data.name = "Customer", "Customer Rig"

# ---- Poses and actions. Armature space: +x her left, -y her front, +z up ----
bpy.context.scene.render.fps = 30
for pb in rig.pose.bones:
    pb.rotation_mode = "QUATERNION"
    pb.rotation_quaternion = (1, 0, 0, 0)


def update():
    bpy.context.view_layer.update()


def turn(name, axis, degrees):
    pb = rig.pose.bones[name]; update()
    q, m = Quaternion(Vector(axis).normalized(), math.radians(degrees)), pb.matrix.to_quaternion()
    pb.rotation_quaternion = (m.inverted() @ q @ m) @ pb.rotation_quaternion


def aim(name, target):
    pb = rig.pose.bones[name]; update()
    q, m = (pb.tail - pb.head).normalized().rotation_difference(Vector(target).normalized()), pb.matrix.to_quaternion()
    pb.rotation_quaternion = (m.inverted() @ q @ m) @ pb.rotation_quaternion


for side, sx in (("l", 1), ("r", -1)):  # arms relaxed at her sides
    aim(f"upperarm_{side}", (0.15 * sx, 0.02, -1)); aim(f"lowerarm_{side}", (0.07 * sx, -0.22, -1)); aim(f"hand_{side}", (0.03 * sx, -0.12, -1))
update()
relaxed = {pb.name: pb.rotation_quaternion.copy() for pb in rig.pose.bones}


def reset():
    for pb in rig.pose.bones:
        pb.rotation_quaternion = relaxed[pb.name]
    update()


def key(frame):
    for pb in rig.pose.bones:
        pb.keyframe_insert("rotation_quaternion", frame=frame)


def action(name):
    old = bpy.data.actions.get(name)
    if old:
        bpy.data.actions.remove(old)
    act = bpy.data.actions.new(name); act.use_fake_user = True
    rig.animation_data_create(); rig.animation_data.action = act


X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)
action("Idle")  # 4 s loop: breathing, a slow weight shift, small glances
for f, breathe, sway, glance, nod in ((0, 0, 0, 0, 0), (30, 1, 0.5, 3, 1), (60, 0, 1, 1, 0), (90, 1, 0.5, -3, -1), (120, 0, 0, 0, 0)):
    reset()
    turn("pelvis", Z, 1.5 * sway); turn("pelvis", Y, -1.0 * sway)
    turn("spine_02", X, -1.2 * breathe); turn("spine_03", X, -0.8 * breathe)
    turn("neck_01", Z, glance * 0.4); turn("head", Z, glance * 0.6); turn("head", X, nod)
    turn("upperarm_l", Y, -1.2 * breathe); turn("upperarm_r", Y, 1.2 * breathe)
    key(f)
action("Greet")  # 2.4 s: a nod, then a small wave with her right hand
for f, wave in ((0, None), (8, "nod"), (16, None), (26, 0), (32, 1), (38, -1), (44, 1), (50, -1), (56, 0), (72, None)):
    reset()
    if wave == "nod":
        turn("neck_01", X, -6); turn("head", X, -9)
    elif wave is not None:
        turn("head", Z, 4); turn("head", X, -3)
        aim("upperarm_r", (-0.55, -0.35, -0.55)); aim("lowerarm_r", (-0.1, -0.25, 1)); aim("hand_r", (0.0, -0.15, 1))
        turn("lowerarm_r", Y, 14 * wave); turn("hand_r", Y, 8 * wave)
    key(f)
rig.animation_data.action = bpy.data.actions["Idle"]
bpy.context.scene.frame_set(0)

# ---- Export ----
os.makedirs(OUT, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
for o in col.all_objects:
    if o.name.startswith("Customer"):
        o.hide_set(False); o.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Customer.fbx"), use_selection=True, object_types={"ARMATURE", "MESH"},
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", use_mesh_modifiers=False,
                         add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X", bake_anim=True,
                         bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                         bake_anim_simplify_factor=0.0, path_mode="STRIP", embed_textures=False, mesh_smooth_type="FACE")
print("Exported; copy textures with: sips -Z <size> <pack file> --out Textures/<name>, per TEXTURES")
