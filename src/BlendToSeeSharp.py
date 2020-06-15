import bpy
from math import degrees
from bpy_extras.io_utils import ExportHelper
from bpy.props import StringProperty
from bpy.types import Operator

def export_obj_meshes(filepath):
    bpy.ops.export_scene.obj(filepath=filepath,
        axis_forward='-Z', axis_up='Y',
        group_by_material=True, group_by_object=True)

def map_texture_or_color(node):
    default_rgb = [node.default_value[0], node.default_value[1], node.default_value[2]]
    try:
        texture = node.links[0].from_node.image
        path = texture.filepath_raw.replace('//', '')
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
    # TODO export at least the diffuse color / texture
    return {}

shader_matcher = {
    "Principled BSDF": map_principled,
    "Diffuse BSDF": map_diffuse,
    # TODO: support black body emitters
    # TODO: support simple graphs (in particular diffuse + glossy combinations)
}

def export_materials(result):
    result["materials"] = []
    for material in list(bpy.data.materials):
        try: # try to interpret as a known shader
            last_shader = material.node_tree.nodes['Material Output'].inputs['Surface'].links[0].from_node
            result["materials"].append(shader_matcher[last_shader.name](last_shader))
        except Exception as e: # use the view shading settings instead
            result["materials"].append(map_view_shader(material))
        result["materials"][-1]["name"] = material.name

def export_cameras(result):
    # TODO support multiple named cameras
    if bpy.context.scene is None:
        camera = bpy.data.scenes[0].camera
    else:
        camera = bpy.context.scene.camera

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
                degrees(-camera.rotation_euler.y) + 180
            ],
            "scale": [ -1.0, 1.0, 1.0 ]
        }
    ]

    result["cameras"] = [
        {
            "fov": degrees(camera.data.angle),
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

def write_some_data(context, filepath):
    export_scene(filepath)
    return {'FINISHED'}

class ExportSomeData(Operator, ExportHelper):
    """This appears in the tooltip of the operator and in the generated docs"""
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
        return write_some_data(context, self.filepath)

def menu_func_export(self, context):
    self.layout.operator(ExportSomeData.bl_idname, text="Text Export Operator")

def register():
    bpy.utils.register_class(ExportSomeData)
    bpy.types.TOPBAR_MT_file_export.append(menu_func_export)

def unregister():
    bpy.utils.unregister_class(ExportSomeData)
    bpy.types.TOPBAR_MT_file_export.remove(menu_func_export)

if __name__ == "__main__":
    register()

    # test call
    bpy.ops.export.to_seesharp('INVOKE_DEFAULT')
