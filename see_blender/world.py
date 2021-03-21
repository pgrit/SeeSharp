import bpy
from bl_ui.properties_world import WorldButtonsPanel
from bpy.types import Panel
from bpy.props import *

class SeeSharpWorld(bpy.types.PropertyGroup):
    hdr: PointerProperty(
        name="HDR Texture",
        description="HDR image to illuminate the scene with",
        type=bpy.types.Image)

    @classmethod
    def register(cls):
        bpy.types.World.seesharp = PointerProperty(
            name="SeeSharpWorld",
            description="SeeSharp world settings",
            type=cls,
        )

    @classmethod
    def unregister(cls):
        del bpy.types.Scene.seesharp

class SEESHARP_PT_context_world(WorldButtonsPanel, Panel):
    """
    UI Panel for world settings (HDR background)
    """
    COMPAT_ENGINES = {"SEE_SHARP"}
    bl_label = "HDR Background"
    bl_order = 1

    @classmethod
    def poll(cls, context):
        return context.world is not None and context.scene.render.engine == "SEE_SHARP"

    def draw(self, context):
        self.layout.template_ID(context.world.seesharp, "hdr", open="image.open")

def register():
    bpy.utils.register_class(SeeSharpWorld)
    bpy.utils.register_class(SEESHARP_PT_context_world)

def unregister():
    bpy.utils.unregister_class(SeeSharpWorld)
    bpy.utils.unregister_class(SEESHARP_PT_context_world)