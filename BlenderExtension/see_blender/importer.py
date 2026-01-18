import os
import json
import math
import bpy
import mathutils
import bmesh
from bpy_extras.io_utils import axis_conversion

# ------------------------------------------------------------------------
# Utility
# ------------------------------------------------------------------------

def load_image(path):
    """Loads image into Blender or returns existing."""
    abspath = bpy.path.abspath(path)
    if not os.path.exists(abspath):
        print(f"WARNING: Missing texture: {abspath}")
        return None
    img = bpy.data.images.load(abspath, check_existing=True)
    return img


def make_material(name, mat_json, base_path):
    """Create a Blender material based on SeeSharp material JSON definition."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nodes = nt.nodes
    links = nt.links

    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.location = (-200, 0)
    output.location = (200, 0)
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])

    # -------- Base color (texture or rgb)
    # base_color = mat_json["baseColor"]
    base_color = mat_json.get("baseColor")
    if base_color:
        if base_color["type"] == "rgb":
            principled.inputs["Base Color"].default_value = base_color["value"] + [1.0]
        elif base_color["type"] == "image":
            img_path = os.path.join(base_path, base_color["filename"])
            img = load_image(img_path)
            if img:
                tex = nodes.new("ShaderNodeTexImage")
                tex.image = img
                links.new(tex.outputs["Color"], principled.inputs["Base Color"])

    # -------- Roughness (texture or float)
    roughness = mat_json.get("roughness", 1.0)
    if isinstance(roughness, str):  # texture
        img_path = os.path.join(base_path, roughness)
        img = load_image(img_path)
        if img:
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = img
            tex.location = (-200, -250)
            links.new(tex.outputs["Color"], principled.inputs["Roughness"])
    else:
        principled.inputs["Roughness"].default_value = float(roughness)

    # Metallic, IOR, Anisotropic
    principled.inputs["Metallic"].default_value = mat_json.get("metallic", 0.0)
    principled.inputs["IOR"].default_value = mat_json.get("IOR", 1.45)
    principled.inputs["Anisotropic"].default_value = mat_json.get("anisotropic", 0.0)

    # Emission
    emission_json = mat_json.get("emission")
    if emission_json and emission_json.get("type") == "rgb":
        # color = mat_json["emission_color"]["value"]
        if "emission_color" in mat_json:
            color = mat_json["emission_color"].get("value", [1.0, 1.0, 1.0])
        else:
            # fallback to emission value itself
            color = emission_json.get("value", [0.0, 0.0, 0.0])
        strength = mat_json.get("emission_strength", 0.0)
        principled.inputs["Emission Color"].default_value = (*color[:3], 1.0)
        principled.inputs["Emission Strength"].default_value = strength
        # if mat_json.get("emissionIsGlossy", False):
        #     principled.inputs["Emission Strength"].default_value = mat_json["emissionExponent"]

    return mat

def load_mesh(filepath):
    ext = os.path.splitext(filepath)[1].lower()

    before = set(bpy.data.objects)

    if ext == ".ply":
        bpy.ops.wm.ply_import(filepath=filepath)

    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=filepath)

    else:
        raise RuntimeError(f"Unsupported mesh format: {ext}")

    after = set(bpy.data.objects)
    new_objs = list(after - before)
    if new_objs:
        return new_objs[0]
    return None

# def load_ply(filepath):
#     """Load a .ply mesh and return the created object."""
#     before = set(bpy.data.objects)
#     bpy.ops.wm.ply_import(filepath=filepath)
#     after = set(bpy.data.objects)

#     new_objs = list(after - before)
#     if new_objs:
#         return new_objs[0]
#     return None

def import_trimesh_object(obj, mat_lookup):
    name = obj.get("name", "Trimesh")

    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()

    verts = obj["vertices"]
    indices = obj["indices"]

    # ---- vertices
    bm_verts = []
    for i in range(0, len(verts), 3):
        bm_verts.append(bm.verts.new((verts[i], verts[i+1], verts[i+2])))
    bm.verts.ensure_lookup_table()

    # ---- faces
    for i in range(0, len(indices), 3):
        try:
            bm.faces.new((
                bm_verts[indices[i]],
                bm_verts[indices[i+1]],
                bm_verts[indices[i+2]],
            ))
        except ValueError:
            pass

    bm.to_mesh(mesh)
    bm.free()

    obj_bl = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj_bl)

    # ---- normals (optional)
    if "normals" in obj:
        normals = obj["normals"]
        loop_normals = []

        for loop in mesh.loops:
            vi = loop.vertex_index
            loop_normals.append(mathutils.Vector(normals[vi*3 : vi*3 + 3]))

        mesh.normals_split_custom_set(loop_normals)

    # ---- UVs (optional)
    if "uv" in obj:
        uv_layer = mesh.uv_layers.new(name="UVMap")
        uvs = obj["uv"]
        for poly in mesh.polygons:
            for loop_idx in poly.loop_indices:
                vi = mesh.loops[loop_idx].vertex_index
                uv_layer.data[loop_idx].uv = (
                    uvs[vi*2],
                    uvs[vi*2 + 1]
                )

    # ---- material
    mat_name = obj.get("material")
    if mat_name in mat_lookup:
        mesh.materials.append(mat_lookup[mat_name])

    return obj_bl


# ------------------------------------------------------------------------
# Camera
# ------------------------------------------------------------------------

def import_camera(cam_json, transform_json, scene):
    """Recreate SeeSharp camera"""
    cam_data = bpy.data.cameras.new("Camera")
    cam_obj = bpy.data.objects.new("Camera", cam_data)
    scene.collection.objects.link(cam_obj)
    scene.camera = cam_obj

    # ------------ Transform
    pos = transform_json["position"]
    rot = transform_json["rotation"]

    cam_obj.location = (-pos[0], pos[2], pos[1])

    # inverse Euler mapping
    eul = mathutils.Euler((
        math.radians(rot[0] + 90),    # x_euler
        math.radians(rot[2]),         # y_euler
        math.radians(rot[1] - 180)    # z_euler
    ), 'XYZ')
    cam_obj.rotation_euler = eul

    # ------------ FOV (vertical → horizontal)
    vert_fov = math.radians(cam_json["fov"])
    aspect = scene.render.resolution_y / scene.render.resolution_x
    horiz_fov = 2 * math.atan(math.tan(vert_fov / 2) / aspect)
    cam_data.angle = horiz_fov

    return cam_obj


# ------------------------------------------------------------------------
# Background HDR
# ------------------------------------------------------------------------

def import_background(bg_json, base_path):
    world = bpy.context.scene.world
    world.use_nodes = True
    nt = world.node_tree

    env_tex = nt.nodes.new("ShaderNodeTexEnvironment")
    env_tex.location = (-300, 0)

    fname = os.path.join(base_path, bg_json["filename"])
    img = load_image(fname)
    if img:
        env_tex.image = img

    bg = nt.nodes["Background"]
    nt.links.new(env_tex.outputs["Color"], bg.inputs["Color"])


# ------------------------------------------------------------------------
# Main Import Logic
# ------------------------------------------------------------------------

def import_seesharp(filepath):
    with open(filepath, "r") as f:
        data = json.load(f)

    base_path = os.path.dirname(filepath)

    scene = bpy.context.scene

    # ------------------------------------------------------------------
    # Materials
    # ------------------------------------------------------------------
    mat_lookup = {}
    if "materials" in data:
        for m in data["materials"]:
            mat = make_material(m["name"], m, base_path)
            mat_lookup[m["name"]] = mat

    # ------------------------------------------------------------------
    # Background
    # ------------------------------------------------------------------
    if "background" in data:
        import_background(data["background"], base_path)

    # ------------------------------------------------------------------
    # Camera
    # ------------------------------------------------------------------
    if "cameras" in data and "transforms" in data:
        cam_desc = data["cameras"][0]
        transform = next(t for t in data["transforms"] if t["name"] == cam_desc["transform"])
        import_camera(cam_desc, transform, scene)

    # ------------------------------------------------------------------
    # Meshes / Objects
    # ------------------------------------------------------------------
    global_matrix = axis_conversion(from_forward="Z", from_up="Y").to_4x4()

    if "objects" in data:
        for obj in data["objects"]:
            if obj.get("type") == "trimesh":
                new_obj = import_trimesh_object(obj, mat_lookup)
            else:
                # fallback to existing PLY logic
                ply_path = os.path.join(base_path, obj["relativePath"])
                new_obj = load_mesh(ply_path)
                if not new_obj:
                    print(f"Failed to load {ply_path}")
                    continue

                if obj.get("material") in mat_lookup:
                    new_obj.data.materials.append(mat_lookup[obj["material"]])
            # Apply transform (shared)
            new_obj.matrix_world = global_matrix.inverted()
        # for obj in data["objects"]:    
        #     # ply_path = os.path.join(base_path, obj["relativePath"])
        #     rel_path = obj.get("relativePath")

        #     if not rel_path:
        #         print(f"⚠ Skipping object '{obj.get('name', 'UNKNOWN')}', no relativePath")
        #         continue

        #     ply_path = os.path.join(base_path, rel_path)
        #     new_obj = load_ply(ply_path)
        #     if not new_obj:
        #         print(f"Failed to load {ply_path}")
        #         continue

        #     # Apply SeeSharp → Blender inverse transform
        #     new_obj.matrix_world = global_matrix.inverted()

        #     # Assign material
        #     if obj.get("material") in mat_lookup:
        #         new_obj.data.materials.append(mat_lookup[obj["material"]])

    bpy.context.scene.render.engine = "SEE_SHARP"

    try:
        bpy.ops.seesharp.convert_all_materials()
        print("✔ Converted all materials to SeeSharp")
    except Exception as e:
        print("❌ Failed to convert materials:", e)

    print("SeeSharp scene import finished.")


# ------------------------------------------------------------------------
# Blender Operator
# ------------------------------------------------------------------------
class SeeSharpImporter(bpy.types.Operator):
    """Import SeeSharp scene (.json)"""
    bl_idname = "import_scene.seesharp"
    bl_label = "Import SeeSharp Scene"

    filename_ext = ".json"
    filter_glob: bpy.props.StringProperty(
        default="*.json",
        options={'HIDDEN'}
    )

    filepath: bpy.props.StringProperty(
        name="File Path",
        description="Path to SeeSharp JSON scene",
        maxlen=1024,
        subtype='FILE_PATH'
    )

    def execute(self, context):
        import_seesharp(self.filepath)
        return {'FINISHED'}

    def invoke(self, context, event):
        context.window_manager.fileselect_add(self)
        return {'RUNNING_MODAL'}

def menu_func_import(self, context):
    self.layout.operator(SeeSharpImporter.bl_idname, text="SeeSharp Scene (.json)")


def register():
    bpy.utils.register_class(SeeSharpImporter)
    bpy.types.TOPBAR_MT_file_import.append(menu_func_import)


def unregister():
    bpy.utils.unregister_class(SeeSharpImporter)
    bpy.types.TOPBAR_MT_file_import.remove(menu_func_import)


if __name__ == "__main__":
    register()