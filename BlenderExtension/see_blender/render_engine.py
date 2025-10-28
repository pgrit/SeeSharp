import bpy
from bpy.props import EnumProperty, IntProperty, BoolProperty, PointerProperty
import os
import tempfile
import seesharp_binaries

from . import exporter

class SeeSharpRenderEngine(bpy.types.RenderEngine):
    bl_idname = "SEE_SHARP"
    bl_label = "SeeSharp"
    bl_use_preview = False
    bl_use_eevee_viewport = True

    def render(self, depsgraph):
        scene = depsgraph.scene
        scale = scene.render.resolution_percentage / 100.0
        size_x = int(scene.render.resolution_x * scale)
        size_y = int(scene.render.resolution_y * scale)

        config = scene.seesharp.config

        with tempfile.TemporaryDirectory() as tempdir:
            exporter.export_scene(tempdir + "/scene.json", scene, depsgraph)

            seesharp_binaries.preview_render(
                tempdir + "/scene.json",
                tempdir + "/Render.hdr",
                size_x, size_y,
                config.samples,
                config.engine,
                config.maxdepth,
                config.denoise
            )

            result = self.begin_result(0, 0, size_x, size_y)
            result.layers[0].load_from_file(tempdir + "/Render.hdr")
            self.end_result(result)


def get_panels():
    exclude_panels = {
        'VIEWLAYER_PT_filter',
        'VIEWLAYER_PT_layer_passes',
    }

    include_panels = {
        'MATERIAL_PT_preview',
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
        col.prop(config, "engine", text="Algorithm")
        col.prop(config, "maxdepth", text="Max. depth")
        col.prop(config, "denoise", text="Denoise")

PATH_DESC = (
    "Simple unidirectional path tracer with next event estimation.\n"
)

VCM_DESC = (
    "Vertex connection and merging.\n"
)

SAMPLES_DESC = (
    "Number of samples per pixel in the rendered image."
)

MAXDEPTH_DESC = (
    "Maximum number of bounces to render. If set to one, only directly visible light\n"
    "sources will be rendered. If set to two, only direct illumination, and so on."
)

DENOISE_DESC = (
    "Whether or not to run a denoiser (Open Image Denoise) on the rendered image"
)

class SeeSharpConfig(bpy.types.PropertyGroup):
    engines = [
        ("PT", "Path Tracer", PATH_DESC, 0),
        ("VCM", "VCM", VCM_DESC, 1),
    ]
    engine: EnumProperty(name="Engine", items=engines, default="PT")
    samples: IntProperty(name="Samples", default=8, min=1, description=SAMPLES_DESC)
    maxdepth: IntProperty(name="Max. bounces", default=8, min=1, description=MAXDEPTH_DESC)
    denoise: BoolProperty(name="Denoise", default=True, description=DENOISE_DESC)

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
