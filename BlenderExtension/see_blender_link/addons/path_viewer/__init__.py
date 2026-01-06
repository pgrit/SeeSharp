import bpy
from ...transport.receiver import start, stop
from ...core.receiver import Receiver
from .dispatcher import dispatcher
import json

class EdgeProps(bpy.types.PropertyGroup):
    json_data: bpy.props.StringProperty(name="Edge JSON Data")


class PathViewerProps(bpy.types.PropertyGroup):
    enabled: bpy.props.BoolProperty(
        name="Enable Path Viewer",
        description="Listen for path viewer command sent from Blazor",
        default=False,
        update=lambda self, context: toggle_receiver(self, context)
    )

receiver = Receiver(dispatcher)

def toggle_receiver(self, context):
    if self.enabled:
        print(receiver.dispatcher._handlers)
        start(receiver)
    else:
        stop()

class PathViewerPanel(bpy.types.Panel):
    bl_label = "Path Viewer"
    bl_idname = "VIEW3D_PT_path_viewer"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "SeeSharp"

    def draw(self, context):
        layout = self.layout
        props = context.scene.path_viewer_props
        layout.prop(props, "enabled")

def draw_dict(layout, data, level=0):
    """Recursively draw any dict or list in Blender UI."""
    if isinstance(data, dict):
        for key, value in data.items():
            row = layout.row()
            if isinstance(value, (dict, list)):
                col = layout.column()
                col.label(text=f"{key}:")
                box = col.box()
                draw_dict(box, value, level + 1)
            else:
                row.label(text=f"{key}: {value}")
    elif isinstance(data, list):
        for i, entry in enumerate(data):
            col = layout.column()
            col.label(text=f"[{i}]")
            if isinstance(entry, (dict, list)):
                box = col.box()
                draw_dict(box, entry, level + 1)
            else:
                col.label(text=str(entry))

class EdgePanel(bpy.types.Panel):
    bl_label = "Edge Settings"
    bl_idname = "EDGE_DATA_PT_panel"
    bl_space_type = 'PROPERTIES'
    bl_region_type = 'WINDOW'
    bl_context = "data"   # IMPORTANT → this puts the panel under Object Data Properties

    @classmethod
    def poll(cls, context):
        obj = context.object
        return obj and obj.get("is_edge") == True
    
    def draw(self, context):
        layout = self.layout
        obj = context.object

        json_str = obj.edge_data.json_data
        if not json_str:
            layout.label(text="No data.")
            return

        try:
            data = json.loads(json_str)
            draw_dict(layout.box(), data)
        except Exception as e:
            layout.label(text=f"JSON Error: {e}")

classes = (
    PathViewerProps,
    PathViewerPanel,
    EdgeProps,
    EdgePanel
)

def register():
    for cls in classes:
        bpy.utils.register_class(cls)

    bpy.types.Scene.path_viewer_props = bpy.props.PointerProperty(
        type=PathViewerProps
    )

    bpy.types.Object.edge_data = bpy.props.PointerProperty(type=EdgeProps)

    print("[BlazorReceiver] Add-on registered")


def unregister():
    stop()
    del bpy.types.Object.edge_data

    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)

    del bpy.types.Scene.path_viewer_props

    print("[BlazorReceiver] Add-on unregistered")