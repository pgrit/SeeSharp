import bpy
def handle_hide_object(msg):
    name = msg.get("name")
    vis = msg.get("visible")

    def set_object_viewport_visibility(object_name, visible):
        obj = bpy.data.objects.get(object_name)
        if obj is None:
            print(f"Object '{object_name}' not found.")
            return
        obj.hide_viewport = not visible

    def run():
        set_object_viewport_visibility(name, vis)
    bpy.app.timers.register(run, first_interval=0)