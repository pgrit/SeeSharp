import bpy
from ....transport.sender import send_to_blazor
def handle_delete_path(msg):
    group_id = msg.get("id")
    def run():
        col_name = f"arrow_group_{group_id}"
        col = bpy.data.collections.get(col_name)
        if not col:
            print(f"No arrow group found: {col_name}")
            return

        # Remove all objects inside
        for obj in list(col.objects):
            bpy.data.objects.remove(obj, do_unlink=True)

        # Remove the collection itself
        bpy.data.collections.remove(col)

        print(f"[ArrowGroup] Deleted group {group_id}")
        send_to_blazor({
            "event": "deleted",
            "id": group_id
        })
    bpy.app.timers.register(run, first_interval=0)