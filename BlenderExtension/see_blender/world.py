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
        del bpy.types.World.seesharp

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

def convert_world(world):
    if not world or not world.use_nodes:
        return

    nt = world.node_tree
    out = nt.nodes.get("World Output")
    if not out or not out.inputs["Surface"].links:
        return

    bg = out.inputs["Surface"].links[0].from_node
    if bg.type != 'BACKGROUND':
        return

    if not bg.inputs["Color"].links:
        return

    env = bg.inputs["Color"].links[0].from_node
    if env.type != 'TEX_ENVIRONMENT':
        return
    world.seesharp.hdr = env.image

class ConvertWorldOperator(bpy.types.Operator):
    bl_idname = "seesharp.convert_world"
    bl_label = "Convert World to SeeSharp"
    bl_description = "Extract HDR environment for SeeSharp renderer"

    def execute(self, context):
        convert_world(context.scene.world)
        return {"FINISHED"}

def register():
    bpy.utils.register_class(SeeSharpWorld)
    bpy.utils.register_class(SEESHARP_PT_context_world)
    bpy.utils.register_class(ConvertWorldOperator)

def unregister():
    bpy.utils.unregister_class(SeeSharpWorld)
    bpy.utils.unregister_class(SEESHARP_PT_context_world)
    bpy.utils.unregister_class(ConvertWorldOperator)