import bpy
from ....transport.sender import send_to_blazor
def handle_select_path(msg):
    obj_id = msg.get("id")
    col_id = f"arrow_group_{obj_id}"
    def run():
       
        col = bpy.data.collections.get(col_id)
        bpy.ops.object.select_all(action='DESELECT')
        # Select all objects in the collection
        for obj in col.objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = col.objects[0] if len(col.objects) else None

        send_to_blazor({
            "event": "selected",
            "id": obj_id
        })
    bpy.app.timers.register(run, first_interval=0)