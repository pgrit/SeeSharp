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

    # Specular Tint
    tint = mat_json.get("specularTintStrength", 0.0)
    principled.inputs["Specular Tint"].default_value = (tint, tint, tint, 1.0)

    # Specular Transmittance (Transmission)
    principled.inputs["Transmission Weight"].default_value =  mat_json.get("specularTransmittance", 0.0)


    # Emission
    emission_json = mat_json.get("emission")
    if emission_json and emission_json.get("type") == "rgb":
        # color = mat_json["emission_color"]["value"]
        if "emission_color" in mat_json:
            color = mat_json["emission_color"].get("value", [1.0, 1.0, 1.0])
            strength = mat_json.get("emission_strength", 0.0)
            principled.inputs["Emission Color"].default_value = (*color[:3], 1.0)
            principled.inputs["Emission Strength"].default_value = strength
        else:
            # fallback to emission value itself
            raw = emission_json.get("value", [0.0, 0.0, 0.0])
            strength = max(raw)
            if strength > 0.0:
                color = [c / strength for c in raw]
            else:
                color = [0.0, 0.0, 0.0]
            principled.inputs["Emission Color"].default_value = (*color, 1.0)
            principled.inputs["Emission Strength"].default_value = strength
            # color = emission_json.get("value", [0.0, 0.0, 0.0])
            # principled.inputs["Emission Color"].default_value = (*color[:3], 1.0)
            # principled.inputs["Emission Strength"].default_value = color[0]
        # strength = mat_json.get("emission_strength", 0.0)
        # principled.inputs["Emission Color"].default_value = (*color[:3], 1.0)
        # principled.inputs["Emission Strength"].default_value = strength
        # if mat_json.get("emissionIsGlossy", False):
        #     principled.inputs["Emission Strength"].default_value = mat_json["emissionExponent"]

    return mat

def load_mesh_with_transform(filepath, global_matrix):
    """
    Load a .ply or .obj mesh and apply a transformation to all imported objects.
    Returns a list of imported objects.
    """
    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=filepath)
  
    after = set(bpy.data.objects)
    new_objs = list(after - before)

    # Apply the global transform to all imported objects
    for obj in new_objs:
        obj.matrix_world = global_matrix

    return new_objs

def load_ply(filepath):
    """Load a .ply mesh and return the created object."""
    before = set(bpy.data.objects)
    bpy.ops.wm.ply_import(filepath=filepath)
    after = set(bpy.data.objects)

    new_objs = list(after - before)
    if new_objs:
        return new_objs[0]
    return None

# def load_ply(filepath):
#     import struct
#     with open(filepath, "rb") as f:
#         data = f.read()

#     # --------------------------------
#     # Parse header
#     # --------------------------------

#     header_end = data.find(b"end_header\n") + len(b"end_header\n")
#     header = data[:header_end].decode("ascii")
#     body = data[header_end:]

#     lines = header.splitlines()

#     is_ascii = True
#     vertex_count = 0
#     face_count = 0
#     vertex_props = []

#     for line in lines:
#         if line.startswith("format"):
#             is_ascii = "ascii" in line
#         elif line.startswith("element vertex"):
#             vertex_count = int(line.split()[-1])
#         elif line.startswith("element face"):
#             face_count = int(line.split()[-1])
#         elif line.startswith("property") and "vertex_indices" not in line:
#             vertex_props.append(line.split()[-1])

#     has_normals = "nx" in vertex_props
#     has_uvs = "s" in vertex_props
#     has_colors = "red" in vertex_props

#     # --------------------------------
#     # Read vertices
#     # --------------------------------

#     verts = []
#     normals = []
#     uvs = []
#     colors = []

#     offset = 0

#     if is_ascii:
#         lines = body.splitlines()
#         for i in range(vertex_count):
#             parts = lines[i].split()
#             idx = 0

#             x, y, z = map(float, parts[idx:idx+3])
#             idx += 3
#             verts.append((x, y, z))

#             if has_normals:
#                 nx, ny, nz = map(float, parts[idx:idx+3])
#                 normals.append((nx, ny, nz))
#                 idx += 3

#             if has_uvs:
#                 s, t = map(float, parts[idx:idx+2])
#                 uvs.append((s, 1 - t))
#                 idx += 2

#             if has_colors:
#                 r, g, b, a = map(int, parts[idx:idx+4])
#                 colors.append((r / 255, g / 255, b / 255, a / 255))

#         face_lines = lines[vertex_count:vertex_count + face_count]

#     else:
#         # for _ in range(vertex_count):
#         #     # Position (always)
#         #     x, y, z = struct.unpack_from("<3f", body, offset)
#         #     offset += 12
#         #     verts.append((x, y, z))

#         #     # Normal
#         #     if has_normals:
#         #         nx, ny, nz = struct.unpack_from("<3f", body, offset)
#         #         offset += 12
#         #         normals.append((nx, ny, nz))
#         #     else:
#         #         normals.append((0.0, 0.0, 0.0))

#         #     # UV
#         #     if has_uvs:
#         #         s, t = struct.unpack_from("<2f", body, offset)
#         #         offset += 8
#         #         uvs.append((s, 1 - t))
#         #     else:
#         #         uvs.append((0.0, 0.0))

#         #     # Color
#         #     if has_colors:
#         #         r, g, b, a = struct.unpack_from("<4B", body, offset)
#         #         offset += 4
#         #         colors.append((r/255, g/255, b/255, a/255))
#         #     else:
#         #         colors.append((1.0, 1.0, 1.0, 1.0))
#         for _ in range(vertex_count):
#             x, y, z = struct.unpack_from("<3f", body, offset)
#             offset += 12
#             verts.append((x, y, z))

#             if has_normals:
#                 normals.append(struct.unpack_from("<3f", body, offset))
#                 offset += 12

#             if has_uvs:
#                 s, t = struct.unpack_from("<2f", body, offset)
#                 uvs.append((s, 1 - t))
#                 offset += 8

#             if has_colors:
#                 r, g, b, a = struct.unpack_from("<4B", body, offset)
#                 colors.append((r / 255, g / 255, b / 255, a / 255))
#                 offset += 4

#     print("Vertex count from header:", vertex_count)
#     print("Estimated byte length for vertices:", offset)
#     print("Body length:", len(body))
#     remaining_bytes = len(body) - offset
#     if remaining_bytes < 1:
#         raise ValueError("No bytes left to read faces — check vertex reading offsets!")
#     print("Offset after vertices:", offset)
#     print("Bytes remaining for faces:", len(body) - offset)

#     # --------------------------------
#     # Read faces
#     # --------------------------------

#     faces = []

#     if is_ascii:
#         for line in face_lines:
#             parts = list(map(int, line.split()))
#             faces.append(parts[1:])

#     else:
#         for _ in range(face_count):
#             while offset < len(body):
#                 if offset + 1 > len(body):
#                     print("Reached end of file before reading face length")
#                     break

#                 length = struct.unpack_from("<B", body, offset)[0]
#                 offset += 1

#                 if offset + length*4 > len(body):
#                     print("Incomplete face at offset", offset-1)
#                     break

#                 indices = struct.unpack_from(f"<{length}I", body, offset)
#                 offset += length*4
#                 faces.append(list(indices))
#             # length = struct.unpack_from("<B", body, offset)[0]
#             # offset += 1
#             # indices = struct.unpack_from(f"<{length}I", body, offset)
#             # offset += length * 4
#             # faces.append(list(indices))

#     # --------------------------------
#     # Build Blender mesh
#     # --------------------------------

#     mesh = bpy.data.meshes.new(os.path.basename(filepath))
#     obj = bpy.data.objects.new(mesh.name, mesh)
#     bpy.context.collection.objects.link(obj)

#     bm = bmesh.new()

#     bm_verts = [bm.verts.new(v) for v in verts]
#     bm.verts.ensure_lookup_table()

#     uv_layer = bm.loops.layers.uv.new() if has_uvs else None
#     col_layer = bm.loops.layers.color.new() if has_colors else None

#     max_index = len(bm_verts) - 1

#     for face_indices in faces:
#         if any(i > max_index for i in face_indices):
#             print("Skipping invalid face:", face_indices)
#             continue
#         try:
#             face = bm.faces.new([bm_verts[i] for i in face_indices])
#         except ValueError:
#             continue  # skip duplicate faces

#         face.smooth = has_normals

#         for loop, vi in zip(face.loops, face_indices):
#             if uv_layer:
#                 loop[uv_layer].uv = uvs[vi]

#             if col_layer:
#                 loop[col_layer] = colors[vi]

#     bm.to_mesh(mesh)
#     bm.free()

#     if has_normals:
#         mesh.normals_split_custom_set_from_vertices(normals)

#     return obj

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
    if "matrix" in transform_json:
        m = transform_json["matrix"]

        # JSON is row-major → Blender needs column-major
        mat = mathutils.Matrix((
            (m[0],  m[4],  m[8],  m[12]),
            (m[1],  m[5],  m[9],  m[13]),
            (m[2],  m[6],  m[10], m[14]),
            (m[3],  m[7],  m[11], m[15]),
        ))
        conv = axis_conversion(from_forward="Z", from_up="Y").to_4x4()
        #conv = axis_conversion(from_forward="Z", from_up="Y", to_forward='-Z', to_up='Y').to_4x4()
        cam_obj.matrix_world = conv @ mat
    else:
        pos = transform_json.get("position", [0, 0, 0])
        rot = transform_json.get("rotation", [0, 0, 0])

        cam_obj.location = (-pos[0], pos[2], pos[1])

        # inverse Euler mapping
        eul = mathutils.Euler((
            math.radians(rot[0] + 90),    # x_euler
            # math.radians(90 - rot[0]),
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

# def import_background(bg_json, base_path):
#     world = bpy.context.scene.world
#     world.use_nodes = True
#     nt = world.node_tree

#     env_tex = nt.nodes.new("ShaderNodeTexEnvironment")
#     env_tex.location = (-300, 0)

#     fname = os.path.join(base_path, bg_json["filename"])
#     img = load_image(fname)
#     if img:
#         env_tex.image = img

#     bg = nt.nodes["Background"]
#     nt.links.new(env_tex.outputs["Color"], bg.inputs["Color"])

def import_background(bg_json, base_path):
    scene = bpy.context.scene

    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")

    world = scene.world
    world.use_nodes = True
    nt = world.node_tree

    # Clear existing nodes
    nt.nodes.clear()

    # Create nodes
    env_tex = nt.nodes.new("ShaderNodeTexEnvironment")
    env_tex.location = (-600, 0)

    bg = nt.nodes.new("ShaderNodeBackground")
    bg.location = (-300, 0)

    output = nt.nodes.new("ShaderNodeOutputWorld")
    output.location = (0, 0)

    # Load image
    fname = os.path.join(base_path, bg_json["filename"])
    img = load_image(fname)
    if img:
        env_tex.image = img
        img.colorspace_settings.name = "Linear Rec.709"
    # Optional strength
    if "strength" in bg_json:
        bg.inputs["Strength"].default_value = bg_json["strength"]

    # Connect nodes
    nt.links.new(env_tex.outputs["Color"], bg.inputs["Color"])
    nt.links.new(bg.outputs["Background"], output.inputs["Surface"])

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
        bpy.ops.seesharp.convert_world()

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

    # APPLY OBJECT-LEVEL EMISSION FOR OLDER EXPORTER VERSION
    def apply_object_emission(obj, emission_json):
        if not emission_json:
            return

        mat = obj.data.materials[0]
        mat.use_nodes = True
        nt = mat.node_tree
        principled = next(
            n for n in nt.nodes
            if n.type == "BSDF_PRINCIPLED"
        )

        color = emission_json.get("value", [0.0, 0.0, 0.0])

        # SeeSharp uses radiance → treat magnitude as strength
        strength = max(color)

        principled.inputs["Emission Color"].default_value = (
            color[0] / strength,
            color[1] / strength,
            color[2] / strength,
            1.0
        )
        principled.inputs["Emission Strength"].default_value = strength

    def normalize_material_name(name):
        import re
        if not name:
            return None
        match = re.match(r"^(.*?)(?:\.(\d+))?$", name)
        base = match.group(1)
        idx = match.group(2)

        if idx is None:
            return base

        new_idx = int(idx) - 1
        if new_idx <= 0:
            return base
        else:
            return f"{base}.{str(new_idx).zfill(3)}"
        
    if "objects" in data:
        for obj in data["objects"]:
            if obj.get("type") == "trimesh":
                new_obj = import_trimesh_object(obj, mat_lookup)
                new_obj.matrix_world = global_matrix

                # APPLY OBJECT-LEVEL EMISSION
                if "emission" in obj:
                    apply_object_emission(new_obj, obj["emission"])
            else:
                # fallback to existing PLY logic
                path = os.path.join(base_path, obj["relativePath"])
                ext = os.path.splitext(path)[1].lower()

                if ext == ".ply":
                    new_obj = load_ply(path)
                    if not new_obj:
                        print(f"Failed to load {path}")
                        continue
                    if obj.get("material") in mat_lookup:
                        new_obj.data.materials.append(mat_lookup[obj["material"]])
                    new_obj.matrix_world = global_matrix

                    # APPLY OBJECT-LEVEL EMISSION
                    if "emission" in obj:
                        apply_object_emission(new_obj, obj["emission"])
                elif ext == ".obj":
                    new_objs = load_mesh_with_transform(path, global_matrix)
                    if not new_objs:
                        print(f"Failed to load {path}")
                        continue
                    # Assign material to all loaded objects
                    for new_obj in new_objs:
                        for i, mat in enumerate(new_obj.data.materials):
                            if not mat:
                                continue
                            norm_name = normalize_material_name(mat.name)
                            if norm_name in mat_lookup:
                                new_obj.data.materials[i] = mat_lookup[norm_name]

                        # APPLY OBJECT-LEVEL EMISSION
                        if "emission" in obj:
                            apply_object_emission(new_obj, obj["emission"])
                else:
                    raise RuntimeError(f"Unsupported mesh format: {ext}")
              
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