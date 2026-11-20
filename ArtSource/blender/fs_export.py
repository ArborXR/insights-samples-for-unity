import bpy, os, re

def export_root(root_name, out_dir):
    """Export one prop hierarchy as FBX with clean child names (Blender adds .001 suffixes to keep names unique)."""
    root = bpy.data.objects[root_name]
    parts = [root] + list(root.children_recursive)
    renamed = {}
    for o in parts[1:]:
        base = re.sub(r"\.\d{3}$", "", o.name)
        if base != o.name:
            clash = bpy.data.objects.get(base)
            if clash is not None and clash not in parts:
                renamed[clash] = clash.name
                clash.name = base + "__tmp"
            renamed.setdefault(o, o.name)
            o.name = base
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts: o.select_set(True)
    saved_loc = root.location.copy()
    root.location = (0, 0, 0)
    path = os.path.join(out_dir, root_name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"EMPTY", "MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                             add_leaf_bones=False, bake_anim=False, path_mode="STRIP")
    root.location = saved_loc
    # Restore names: suffixed parts first, then any clashes that were moved aside.
    for o, name in renamed.items():
        o.name = name + "__restore"
    for o, name in renamed.items():
        o.name = name
    return path
