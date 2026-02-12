import bpy
import threading
import socket
import time
import json
from mathutils import Vector
from ...config import HOST, PORT_IN, PORT_OUT
from ...transport.sender import send_to_blazor
stop_flag = False
send_thread = None
last_pos = (None, None, None)
# Continuously updated viewport data
current_area = None
current_region = None
current_rv3d = None

view_handler = None


# --------------------------------------------------------------------
# VIEW TRACKER (runs inside Blender's UI thread on every redraw)
# --------------------------------------------------------------------

def update_view_data():
    """
    This function runs every viewport redraw and always updates
    the current view matrix, region, and area.
    """
    global current_area, current_region, current_rv3d

    # Look through all windows and areas until we find a VIEW_3D
    for window in bpy.context.window_manager.windows:
        screen = window.screen
        if not screen:
            continue

        for area in screen.areas:
            if area.type == 'VIEW_3D':
                region = next((r for r in area.regions if r.type == 'WINDOW'), None)
                if not region:
                    continue

                rv3d = area.spaces.active.region_3d
                if rv3d:
                    current_area = area
                    current_region = region
                    current_rv3d = rv3d
                    return


# --------------------------------------------------------------------
# TCP sending loop (runs in background thread)
# --------------------------------------------------------------------

def raycast_and_send_loop():
    global stop_flag, last_pos

    while not stop_flag:

        # Ensure viewport data is available
        if not current_rv3d:
            time.sleep(0.05)
            continue

        try:
            scene = bpy.context.scene
            cursor_pos = scene.cursor.location.copy()

            # Get ray origin = user view position
            view_matrix = current_rv3d.view_matrix
            origin = view_matrix.inverted().translation

            direction = (cursor_pos - origin).normalized()

            depsgraph = bpy.context.evaluated_depsgraph_get()
            hit, loc, normal, face_idx, obj, _ = scene.ray_cast(
                depsgraph, origin, direction
            )
            if loc != last_pos:
                last_pos = loc
                if hit:
                    data = {
                        "event": "cursor_tracked",
                        "object": obj.name,
                        "hit_position": [round(loc.x, 4), round(loc.y, 4), round(loc.z, 4)],
                        "normal": [round(normal.x, 4), round(normal.y, 4), round(normal.z, 4)],
                        "face_index": face_idx,
                        "cursor_position": [round(cursor_pos.x, 4), round(cursor_pos.y, 4), round(cursor_pos.z, 4)]
                    }
                else:
                    data = {
                        "event": "cursor_tracked",
                        "object": None,
                        "cursor_position": [round(cursor_pos.x, 4), round(cursor_pos.y, 4), round(cursor_pos.z, 4)]
                    }
                # s.sendall((json.dumps(data) + "\n").encode("utf8"))
                send_to_blazor(data)
                # print(data)
                print("Sending JSON:", json.dumps(data))
        except Exception as e:
            print("Error in thread loop:", e)
            break

        time.sleep(0.25)

    # s.close()
    print("Stopped sending")


# --------------------------------------------------------------------
# Start / Stop Functions
# --------------------------------------------------------------------

def start_sender():
    global stop_flag, send_thread
    stop_flag = False

    send_thread = threading.Thread(target=raycast_and_send_loop, daemon=True)
    send_thread.start()
    print("Sender started")


def stop_sender():
    global stop_flag
    stop_flag = True
    print("Sender stopping...")


# --------------------------------------------------------------------
# UI Panel + Property
# --------------------------------------------------------------------

def toggle_sender(self, context):
    if self.sending_enabled:
        start_sender()
    else:
        stop_sender()


class CursorTrackerProperties(bpy.types.PropertyGroup):
    sending_enabled: bpy.props.BoolProperty(
        name="Send Cursor Info",
        description="Enable real-time raycast + cursor output",
        default=False,
        update=toggle_sender
    )


class CursorTrackerPanel(bpy.types.Panel):
    bl_label = "Cursor Tracker"
    bl_idname = "cursor_tracker_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'SeeSharp'

    def draw(self, context):
        layout = self.layout
        props = context.scene.cursor_sender_props
        layout.prop(props, "sending_enabled")


# --------------------------------------------------------------------
# Registration + View Handler
# --------------------------------------------------------------------

classes = (
    CursorTrackerProperties,
    CursorTrackerPanel,
)

def register():
    global view_handler

    for cls in classes:
        bpy.utils.register_class(cls)

    bpy.types.Scene.cursor_sender_props = bpy.props.PointerProperty(type=CursorTrackerProperties)

    # Add draw handler to track view continuously
    view_handler = bpy.types.SpaceView3D.draw_handler_add(
        update_view_data, (), 'WINDOW', 'POST_VIEW'
    )
    print("Add-on registered and view tracking started.")


def unregister():
    global view_handler

    stop_sender()

    if view_handler:
        bpy.types.SpaceView3D.draw_handler_remove(view_handler, 'WINDOW')
        view_handler = None

    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)

    del bpy.types.Scene.cursor_sender_props

    print("Add-on unregistered.")
