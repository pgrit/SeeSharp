import os
import json
from math import degrees, atan, tan
import bpy
from bpy_extras.io_utils import ExportHelper
from bpy.props import StringProperty, BoolProperty
from bpy.types import Operator
from io_scene_fbx.export_fbx_bin import save_single

def export_fbx_meshes(filepath, scene, depsgraph):
    # Not sure what the "operator" is supposed to be, but passing a dummy seems to work fine.
    class DummyOp:
        def report(self, *args, **kwargs):
            print(*args)
    save_single(DummyOp(), scene, depsgraph, filepath, object_types={"MESH"}, context_objects=depsgraph.objects,
        use_tspace=False, axis_forward='Z', axis_up='Y')

def map_rgb(rgb):
    return { "type": "rgb", "value": [ rgb[0], rgb[1], rgb[2] ] }

def map_texture(texture, out_dir):
    path = texture.filepath_raw.replace('//', '')
    if path == '':
        path = texture.name + ".exr"

    # Make sure the image is loaded to memory, so we can write it out
    if not texture.has_data:
        texture.pixels[0]

    os.makedirs(f"{out_dir}/Textures", exist_ok=True)

    # Export the texture and store its path
    name = os.path.basename(path)
    old = texture.filepath_raw
    try:
        texture.filepath_raw = f"{out_dir}/Textures/{name}"
        texture.save()
    finally: # Never break the scene!
        texture.filepath_raw = old

    return { "type": "image", "filename": f"Textures/{name}" }

def material_to_json(material, out_dir):
    if material.base_texture:
        base_color = map_texture(material.base_texture, out_dir)
    else:
        base_color = map_rgb(material.base_color)

    return {
        "type": "generic",
        "baseColor": base_color,
        "roughness": material.roughness,
        "anisotropic": material.anisotropic,
        "diffuseTransmittance": material.diffuseTransmittance,
        "IOR": material.indexOfRefraction,
        "metallic": material.metallic,
        "specularTintStrength": material.specularTintStrength,
        "specularTransmittance": material.specularTransmittance,
        "thin": material.thin,
        "emission": map_rgb((
            material.emission_color[0] * material.emission_strength,
            material.emission_color[1] * material.emission_strength,
            material.emission_color[2] * material.emission_strength
        ))
    }

def export_materials(result, out_dir):
    result["materials"] = []
    for material in list(bpy.data.materials):
        result["materials"].append(material_to_json(material.seesharp, out_dir))
        result["materials"][-1]["name"] = material.name.replace(" ", "_")

def export_background(result, out_dir, scene):
    if scene.world.seesharp.hdr:
        result["background"] = map_texture(scene.world.seesharp.hdr, out_dir)

def export_camera(result, scene):
    camera = scene.camera
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
            # convert horizontal FOV to vertical FOV
            "fov": degrees(2 * atan(aspect_ratio * tan(camera.data.angle / 2))),
            "transform": "camera",
            "name": "default",
            "type": "perspective"
        }
    ]

def export_scene(filepath, scene, depsgraph):
    # export all meshes as obj
    export_fbx_meshes(filepath.replace('json', 'fbx'), scene, depsgraph)

    # Write all materials and cameras to a dict with the layout of our json file
    result = {}
    export_materials(result, os.path.dirname(filepath))
    export_camera(result, scene)
    export_background(result, os.path.dirname(filepath), scene)

    fbx_name = os.path.splitext(os.path.basename(filepath))[0] + '.fbx'

    result['objects'] = [{
        "name": "scene",
        "type": "fbx",
        "relativePath": fbx_name
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
                depsgraph = context.evaluated_depsgraph_get()
                export_scene(self.filepath.replace('.json', f'{frame:04}.json'), context.scene, depsgraph)
        else:
            depsgraph = context.evaluated_depsgraph_get()
            export_scene(self.filepath, context.scene, depsgraph)
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
