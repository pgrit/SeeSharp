import os
import json
from math import degrees, atan, tan
import bpy
from bpy_extras.io_utils import ExportHelper
from bpy.props import StringProperty, BoolProperty
from bpy.types import Operator

def export_obj_meshes(filepath):
    # Maps the geometry such that: x = -x', y = z', z = -y'
    # Where x',y',z' is the position in the Blender coordinate system.
    bpy.ops.export_scene.obj(filepath=filepath,
        axis_forward='Z', axis_up='Y',
        group_by_material=True, group_by_object=True,
        use_mesh_modifiers=True)
    mtlpath = filepath.replace(".obj", ".mtl")
    os.remove(mtlpath)

def map_rgb(rgb):
    return { "type": "rgb", "value": [ rgb[0], rgb[1], rgb[2] ] }

def map_texture_or_color(node, out_dir):
    default_rgb = [node.default_value[0], node.default_value[1], node.default_value[2]]
    try:
        texture = node.links[0].from_node.image
        path = texture.filepath_raw.replace('//', '')

        # Export the texture and store its path
        name = os.path.basename(path)
        old = texture.filepath_raw
        try:
            texture.filepath_raw = f"{out_dir}/Textures/{name}"
            texture.save()
        finally: # Never break the scene!
            texture.filepath_raw = old

        return { "type": "texture", "path": f"Textures/{name}" }
    except:
        pass
    return { "type": "rgb", "value": default_rgb }

def map_float(node):
    return node.default_value

def map_principled(shader, out_dir):
    return {
        "type": "generic",
        "baseColor": map_texture_or_color(shader.inputs['Base Color'], out_dir),
        "roughness": map_float(shader.inputs["Roughness"]),
        "anisotropic": map_float(shader.inputs["Anisotropic"]),
        "IOR": map_float(shader.inputs["IOR"]),
        "metallic": map_float(shader.inputs["Metallic"]),
        # diffuse transmittance not directly matched: instead, Blender has a separate
        # roughenss value for the transmission
        "specularTransmittance": map_float(shader.inputs["Transmission"]),
        "specularTint": map_float(shader.inputs["Specular Tint"]),
    }

def map_diffuse(shader, out_dir):
    return {
        "type": "diffuse",
        "baseColor": map_texture_or_color(shader.inputs['Color'], out_dir),
    }

def map_view_shader(material, out_dir):
    return {
        "type": "diffuse",
        "baseColor": map_rgb(material.diffuse_color)
    }

def map_emission(shader, out_dir):
    # get the W/m2 scaling factor
    strength = shader.inputs['Strength'].default_value
    print(strength)
    color = shader.inputs['Color'].default_value
    print(color)
    return {
        "type": "diffuse",
        "baseColor": { "type": "rgb", "value": [0,0,0] },
        "emission": map_rgb([color[0] * strength, color[1] * strength, color[2] * strength])
    }

shader_matcher = {
    "Principled BSDF": map_principled,
    "Diffuse BSDF": map_diffuse,
    "Emission": map_emission
}

def export_materials(result, out_dir):
    result["materials"] = []
    for material in list(bpy.data.materials):
        try: # try to interpret as a known shader
            last_shader = material.node_tree.nodes['Material Output'].inputs['Surface'].links[0].from_node
            result["materials"].append(shader_matcher[last_shader.name](last_shader, out_dir))
        except Exception as e: # use the view shading settings instead
            result["materials"].append(map_view_shader(material, out_dir))
        result["materials"][-1]["name"] = material.name.replace(" ", "_")

def export_background(result, out_dir):
    texture_dir = os.path.join(out_dir, 'Textures')
    try:
        # Try to find an environment texture first
        bgn = bpy.data.worlds['World'].node_tree.nodes["Environment Texture"].image

        # Remove the starting double slash on relative paths
        path = bgn.filepath_raw.replace('//', '')

        # Next, copy the texture file to a location relative to the output file
        export_path = os.path.join(texture_dir, os.path.basename(path))
        old_path = bgn.filepath_raw
        try:
            bgn.filepath_raw = export_path
            bgn.save()
        finally: # Never break the scene!
            bgn.filepath_raw = old_path

        # Store the relative file path in the scene description
        relative_path = "Textures/" + os.path.basename(export_path)
        result["background"] = {
            "type": "image",
            "filename": relative_path
        }
    except:
        try:
            bgn = bpy.data.worlds['World'].node_tree.nodes["Sky Texture"]
            # TODO support Hosek-Wilkie Sky parameters
        except:
            # All else failed: read the constant color!
            bpy.data.worlds['World'].color
            # TODO write into json result

def export_cameras(result):
    # TODO support multiple named cameras
    if bpy.context.scene is None:
        camera = bpy.data.scenes[0].camera
        scene = bpy.data.scenes[0]
    else:
        camera = bpy.context.scene.camera
        scene = bpy.context.scene
    aspect_ratio = scene.render.resolution_y / scene.render.resolution_x

    result["transforms"] = [
        {
            "name": "camera",
            "position": [
                -camera.location.x,
                camera.location.z,
                camera.location.y
            ],
            # At (0,0,0) rotation, the Blender camera faces towards negative z, with positive y pointing up
            # We account for this extra rotation here, because we want it to face _our_ negative z with _our_
            # y axis pointing upwards instead.
            "rotation": [
                degrees(camera.rotation_euler.x) - 90,
                degrees(camera.rotation_euler.z) + 180,
                degrees(camera.rotation_euler.y)
            ],
            "scale": [ 1.0, 1.0, 1.0 ]
        }
    ]

    result["cameras"] = [
        {
            "fov": degrees(2 * atan(aspect_ratio * tan(camera.data.angle / 2))),
            "transform": "camera",
            "name": "default",
            "type": "perspective"
        }
    ]

def export_scene(filepath):
    # export all meshes as obj
    export_obj_meshes(filepath.replace('json', 'obj'))

    # Write all materials and cameras to a dict with the layout of our json file
    result = {}
    export_materials(result, os.path.dirname(filepath))
    export_cameras(result)
    export_background(result, os.path.dirname(filepath))

    obj_name = os.path.splitext(os.path.basename(filepath))[0] + '.obj'

    result['objects'] = [{
        "name": "scene",
        "type": "obj",
        "relativePath": obj_name
    }]

    # Write the result into the .json
    with open(filepath, 'w') as fp:
        json.dump(result, fp, indent=2)

class SeeSharpExport(Operator, ExportHelper):
    """SeeSharp scene exporter"""
    bl_idname = "export.to_seesharp"
    bl_label = "SeeSharp Scene Exporter"

    # ExportHelper mixin class uses this
    filename_ext = ".json"

    filter_glob: StringProperty(
        default="*.json",
        options={'HIDDEN'},
        maxlen=255,  # Max internal buffer length, longer would be clamped.
    )

    animations: BoolProperty(
        name="Export Animations",
        description="If true, writes .json and .obj files for each frame in the animation.",
        default=False,
    )

    def execute(self, context):
        if self.animations is True:
            for frame in range(context.scene.frame_start, context.scene.frame_end+1):
                context.scene.frame_set(frame)
                export_scene(self.filepath.replace('.json', f'{frame:04}.json'))
        else:
            export_scene(self.filepath)
        return {'FINISHED'}

def menu_func_export(self, context):
    self.layout.operator(SeeSharpExport.bl_idname, text="SeeSharp Export")

def register():
    bpy.utils.register_class(SeeSharpExport)
    bpy.types.TOPBAR_MT_file_export.append(menu_func_export)

def unregister():
    bpy.utils.unregister_class(SeeSharpExport)
    bpy.types.TOPBAR_MT_file_export.remove(menu_func_export)

if __name__ == "__main__":
    register()

    # test call
    bpy.ops.export.to_seesharp('INVOKE_DEFAULT')
