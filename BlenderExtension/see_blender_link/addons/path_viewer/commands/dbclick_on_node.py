import bpy
import json
from ....transport.sender import send_to_blazor

def handle_dbclick_on_node(msg):
    json_string = msg.get("path")
    col_id = msg.get("path_id")
    node_list = json.loads(json_string)
    is_full_graph = msg.get("is_full_graph")

    def frame_object(obj):
        # Ensure Object Mode
        if bpy.context.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')

        # Deselect all
        bpy.ops.object.select_all(action='DESELECT')

        # Select and activate object
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj

        # Find a VIEW_3D area
        for area in bpy.context.window.screen.areas:
            if area.type == 'VIEW_3D':
                for region in area.regions:
                    if region.type == 'WINDOW':
                        with bpy.context.temp_override(
                            window=bpy.context.window,
                            area=area,
                            region=region,
                        ):
                            bpy.ops.view3d.view_selected()
                        return

        print("No VIEW_3D area found")

    def view_all(center=False):
        # Ensure Object Mode (safe for view operators)
        if bpy.context.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')

        # Find a VIEW_3D area
        for area in bpy.context.window.screen.areas:
            if area.type == 'VIEW_3D':
                for region in area.regions:
                    if region.type == 'WINDOW':
                        with bpy.context.temp_override(
                            window=bpy.context.window,
                            area=area,
                            region=region,
                        ):
                            bpy.ops.view3d.view_all(center=center)
                        return

        print("No VIEW_3D area found")
    def run():
        collection = bpy.data.collections[f"arrow_group_{col_id}"]
        for obj in collection.objects:
            obj.hide_set(True)
        if (is_full_graph):
            for obj in collection.objects:
                obj.hide_set(False)
            view_all(center=False)
        else:       
            for i in range(len(node_list) - 1):
                var1 = node_list[i]
                var2 = node_list[i + 1]
                for obj in collection.objects:
                    if obj.name == f"{var1}-{var2}" or obj.name == f"{var2}-{var1}":
                        obj.hide_set(False)
                        frame_object(obj)
    bpy.app.timers.register(run, first_interval=0)