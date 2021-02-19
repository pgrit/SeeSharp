import os
from math import degrees
import bpy
from bpy_extras.io_utils import ExportHelper
from bpy.props import StringProperty
from bpy.types import Operator

def export_obj_meshes(filepath):
    bpy.ops.export_scene.obj(filepath=filepath,
        axis_forward='-Z', axis_up='Y',
        group_by_material=True, group_by_object=True,
        use_mesh_modifiers=True)
    mtlpath = filepath.replace(".obj", ".mtl")
    os.remove(mtlpath)

def map_rgb(rgb):
    return { "type": "rgb", "value": [ rgb[0], rgb[1], rgb[2] ] }

def map_texture_or_color(node):
    default_rgb = [node.default_value[0], node.default_value[1], node.default_value[2]]
    try:
        texture = node.links[0].from_node.image
        path = texture.filepath_raw.replace('//', '')
        # Always use unix-style separators, so the generated file is portable
        path = texture.filepath_raw.replace('\\', '/')
        return { "type": "texture", "path": path }
    except:
        pass
    return { "type": "rgb", "value": default_rgb }

def map_float(node):
    return node.default_value

def map_principled(shader):
    return {
        "type": "generic",
        "baseColor": map_texture_or_color(shader.inputs['Base Color']),
        "roughness": map_float(shader.inputs["Roughness"]),
        "anisotropic": map_float(shader.inputs["Anisotropic"]),
        "IOR": map_float(shader.inputs["IOR"]),
        "metallic": map_float(shader.inputs["Metallic"]),
        # diffuse transmittance not directly matched: instead, Blender has a separate
        # roughenss value for the transmission
        "specularTransmittance": map_float(shader.inputs["Transmission"]),
        "specularTint": map_float(shader.inputs["Specular Tint"]),
    }

def map_diffuse(shader):
    return {
        "type": "diffuse",
        "baseColor": map_texture_or_color(shader.inputs['Color']),
    }

def map_view_shader(material):
    return {
        "type": "diffuse",
        "baseColor": map_rgb(material.diffuse_color)
    }

def map_emission(shader):
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

def export_materials(result):
    result["materials"] = []
    for material in list(bpy.data.materials):
        try: # try to interpret as a known shader
            last_shader = material.node_tree.nodes['Material Output'].inputs['Surface'].links[0].from_node
            result["materials"].append(shader_matcher[last_shader.name](last_shader))
        except Exception as e: # use the view shading settings instead
            result["materials"].append(map_view_shader(material))
        result["materials"][-1]["name"] = material.name.replace(" ", "_")

def export_background(result, filepath):
    out_dir = os.path.dirname(filepath)
    texture_dir = os.path.join(out_dir, 'textures')
    if not os.path.exists(texture_dir):
        os.makedirs(texture_dir)

    try:
        # Try to find an environment texture first
        bgn = bpy.data.worlds['World'].node_tree.nodes["Environment Texture"].image
        path = bgn.filepath_raw.replace('//', '')

        # Next, copy the texture file to a location relative to the output file
        export_path = os.path.join(texture_dir, os.path.basename(path))
        old_path = bgn.filepath_raw
        bgn.filepath_raw = export_path
        bgn.save()
        bgn.filepath_raw = old_path

        # Store the relative file path in the scene description
        relative_path = "textures/" + os.path.basename(export_path)
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
                camera.location.x,
                camera.location.z,
                -camera.location.y
            ],
            "rotation": [
                degrees(camera.rotation_euler.x) - 90,
                degrees(camera.rotation_euler.z),
                degrees(camera.rotation_euler.y) + 180
            ],
            "scale": [ -1.0, 1.0, 1.0 ]
        }
    ]

    result["cameras"] = [
        {
            "fov": degrees(camera.data.angle) * aspect_ratio,
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
    export_materials(result)
    export_cameras(result)
    export_background(result, filepath)

    import os
    obj_name = os.path.splitext(os.path.basename(filepath))[0] + '.obj'

    result['objects'] = [{
        "name": "scene",
        "type": "obj",
        "relativePath": obj_name
    }]

    # Write the result into the .json
    import json
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

    def execute(self, context):
        export_scene(self.filepath)
        return {'FINISHED'}

def menu_func_export(self, context):
    self.layout.operator(SeeSharpExport.bl_idname, text="SeeSharp Export")

bl_info = {
    "name": "SeeSharp Export",
    "author": "Pascal Grittmann",
    "version": (0, 1),
    "blender": (2, 80, 0),
    "category": "Import-Export",
}

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
