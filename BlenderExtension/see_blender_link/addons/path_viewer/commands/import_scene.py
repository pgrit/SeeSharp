import bpy
import os

def handle_import_scene(msg):
    file_name = msg.get("scene_name")
    if not file_name:
        print("No scene path received.")
        return

    # Folder containing the received file
    scene_folder = os.path.dirname(file_name)

    # Folder name becomes the base name
    base_name = os.path.basename(scene_folder)

    blend_path = os.path.join(scene_folder, f"{base_name}.blend")
    json_path = os.path.join(scene_folder, f"{base_name}.json")
    def run():
        if bpy.context.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')
            
        # clear the current scene
        bpy.ops.object.select_all(action='SELECT')
        bpy.ops.object.delete()

        # Remove all meshes, materials, images, etc.
        for block in bpy.data.meshes:
            bpy.data.meshes.remove(block)
        for block in bpy.data.materials:
            bpy.data.materials.remove(block)
        for block in bpy.data.textures:
            bpy.data.textures.remove(block)
        for block in bpy.data.images:
            bpy.data.images.remove(block)
       
        scene_collection = bpy.context.scene.collection
        for col in list(bpy.data.collections):
            if col != scene_collection:
                bpy.data.collections.remove(col)

        # .blend exists -> load it directly
        if os.path.exists(blend_path):
            print(f"Loading blend file: {blend_path}")

            bpy.ops.wm.open_mainfile(filepath=blend_path)
            return None

        # only .json exists -> import and save blend
        if os.path.exists(json_path):
            print(f"Importing scene from json: {json_path}")
            bpy.ops.import_scene.seesharp(filepath=json_path)

            # Save blend file
            print(f"Saving blend file: {blend_path}")
            bpy.ops.wm.save_as_mainfile(filepath=blend_path)

            return None

    bpy.app.timers.register(run, first_interval=0)