"""Load the shared palette material and helpers into the open .blend.

Run from Blender's Python console or the MCP:
    exec(open("<repo>/ArtSource/blender/fs_setup.py").read())
"""
import os
import bpy

# exec() leaves __file__ unset: then ART_SOURCE comes from the environment, or the open .blend's grandparent folder.
ART_SOURCE = os.path.dirname(os.path.dirname(os.path.abspath(__file__))) if "__file__" in globals() \
    else os.environ.get("ART_SOURCE") or os.path.dirname(os.path.dirname(bpy.data.filepath))

img = bpy.data.images.get("palette") or bpy.data.images.load(os.path.join(ART_SOURCE, "palette.png"))
mat = bpy.data.materials.get("palette") or bpy.data.materials.new("palette")
mat.use_nodes = True
nodes = mat.node_tree.nodes
bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
tex = next((n for n in nodes if n.type == "TEX_IMAGE"), None) or nodes.new("ShaderNodeTexImage")
tex.image = img
tex.interpolation = "Closest"
mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value = 0.8

for name in ("fs_helpers.py", "fs_export.py"):
    text = bpy.data.texts.get(name) or bpy.data.texts.new(name)
    with open(os.path.join(ART_SOURCE, "blender", name)) as f:
        text.from_string(f.read())
