import bpy

def handle_import_scene(msg):
    file_name = msg.get("scene_name")
    def run():
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

        bpy.ops.import_scene.seesharp(filepath=file_name)

    bpy.app.timers.register(run, first_interval=0)