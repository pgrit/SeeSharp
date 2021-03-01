import bpy
from bpy.props import *
import bgl
import os
import subprocess
import tempfile

from . import exporter

class SeeSharpRenderEngine(bpy.types.RenderEngine):
    # These three members are used by blender to set up the
    # RenderEngine; define its internal name, visible name and capabilities.
    bl_idname = "SEE_SHARP"
    bl_label = "SeeSharp"
    bl_use_preview = False

    # Init is called whenever a new render engine instance is created. Multiple
    # instances may exist at the same time, for example for a viewport and final
    # render.
    def __init__(self):
        self.scene_data = None
        self.draw_data = None

    # When the render engine instance is destroy, this is called. Clean up any
    # render engine data here, for example stopping running render threads.
    def __del__(self):
        pass

    # This is the method called by Blender for both final renders (F12) and
    # small preview for materials, world and lights.
    def render(self, depsgraph):
        scene = depsgraph.scene
        scale = scene.render.resolution_percentage / 100.0
        size_x = int(scene.render.resolution_x * scale)
        size_y = int(scene.render.resolution_y * scale)

        config = scene.seesharp.config

        exe = os.path.dirname(__file__) + "/bin/SeeSharp.PreviewRender"
        with tempfile.TemporaryDirectory() as tempdir:
            exporter.export_scene(tempdir + "/scene.json")
            args = [exe]
            args.extend([
                "--scene", tempdir + "/scene.json",
                "--output", tempdir + "/Render.hdr",
                "--resx", str(size_x),
                "--resy", str(size_y),
                "--samples", str(config.samples),
                "--algo", str(config.engine)
            ])
            subprocess.call(args)

            result = self.begin_result(0, 0, size_x, size_y)
            result.layers[0].load_from_file(tempdir + "/Render.hdr")
            self.end_result(result)


# RenderEngines also need to tell UI Panels that they are compatible with.
# We recommend to enable all panels marked as BLENDER_RENDER, and then
# exclude any panels that are replaced by custom panels registered by the
# render engine, or that are not supported.
def get_panels():
    exclude_panels = {
        'VIEWLAYER_PT_filter',
        'VIEWLAYER_PT_layer_passes',
    }

    include_panels = {
        'CYCLES_WORLD_PT_settings',
        'CYCLES_WORLD_PT_settings_surface',
        'CYCLES_MATERIAL_PT_preview',
        'CYCLES_MATERIAL_PT_surface',
        'CYCLES_MATERIAL_PT_settings',
        'CYCLES_MATERIAL_PT_settings_surface'
    }

    panels = []
    for panel in bpy.types.Panel.__subclasses__():
        if hasattr(panel, 'COMPAT_ENGINES') and 'BLENDER_RENDER' in panel.COMPAT_ENGINES:
            if panel.__name__ not in exclude_panels:
                panels.append(panel)
        elif hasattr(panel, 'COMPAT_ENGINES') and panel.__name__ in include_panels:
            panels.append(panel)

    return panels

class SeeSharpPanel(bpy.types.Panel):
    bl_idname = "SEESHARP_RENDER_PT_sampling"
    bl_label = "SeeSharp Settings"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "render"
    COMPAT_ENGINES = {"SEE_SHARP"}

    def draw(self, context):
        config = context.scene.seesharp.config

        col = self.layout.column(align=True)
        col.prop(config, "samples", text="Samples per pixel")
        col.prop(config, "engine", text="Rendering engine")

PATH_DESC = (
    "Simple unidirectional path tracer with next event estimation.\n"
)

VCM_DESC = (
    "Vertex connection and merging.\n"
)

SAMPLES_DESC = (
    "Number of samples per pixel in the rendered image."
)

class SeeSharpConfig(bpy.types.PropertyGroup):
    engines = [
        ("PT", "Path Tracer", PATH_DESC, 0),
        ("VCM", "VCM", VCM_DESC, 1),
    ]
    engine: EnumProperty(name="Engine", items=engines, default="PT")

    samples: IntProperty(name="Samples", default=8, min=1, description=SAMPLES_DESC)

class SeeSharpScene(bpy.types.PropertyGroup):
    config: PointerProperty(type=SeeSharpConfig)

    @classmethod
    def register(cls):
        bpy.types.Scene.seesharp = PointerProperty(
            name="SeeSharp Scene Settings",
            description="SeeSharp scene settings",
            type=cls,
        )

    @classmethod
    def unregister(cls):
        del bpy.types.Scene.seesharp

def register():
    bpy.utils.register_class(SeeSharpConfig)
    bpy.utils.register_class(SeeSharpScene)
    bpy.utils.register_class(SeeSharpPanel)
    bpy.utils.register_class(SeeSharpRenderEngine)
    for panel in get_panels():
        panel.COMPAT_ENGINES.add('SEE_SHARP')

def unregister():
    bpy.utils.unregister_class(SeeSharpConfig)
    bpy.utils.unregister_class(SeeSharpScene)
    bpy.utils.unregister_class(SeeSharpPanel)
    bpy.utils.unregister_class(SeeSharpRenderEngine)
    for panel in get_panels():
        if 'SEE_SHARP' in panel.COMPAT_ENGINES:
            panel.COMPAT_ENGINES.remove('SEE_SHARP')

if __name__ == "__main__":
    register()