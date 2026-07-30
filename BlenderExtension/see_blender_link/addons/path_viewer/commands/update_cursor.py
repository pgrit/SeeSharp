import bpy

def handle_update_cursor(msg):
    pos_x = msg.get("pX")
    pos_y = msg.get("pY")
    pos_z = msg.get("pZ")
 
    def run():
        bpy.context.scene.cursor.location = (pos_x, pos_y, pos_z)
    bpy.app.timers.register(run, first_interval=0)