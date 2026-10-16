import bpy
import os
from ....transport.sender import send_to_blazor

def handle_export_scene(msg):
    def run():
        base_folder = msg.get("path")
        scene_name = msg.get("scene")

        if not base_folder:
            raise ValueError("Missing export path")

        # Create the folder
        export_folder = os.path.join(base_folder, scene_name)
        os.makedirs(export_folder, exist_ok=True)

        # JSON has the SAME name as the folder
        json_filepath = os.path.join(
            export_folder,
            f"{scene_name}.json"
        )

        bpy.ops.export.to_seesharp(
            filepath=json_filepath
        )

        send_to_blazor({
            "event": "created",
            "id": "ready"
        })
    bpy.app.timers.register(run, first_interval=0)