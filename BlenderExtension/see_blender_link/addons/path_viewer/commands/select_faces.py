import bpy
import bmesh
import json
def handle_select_faces(msg):
    mesh = msg.get("mesh")
    json_indices = msg.get("face_indices")
    indices = json.loads(json_indices)
    def frame_faces(mesh_name, face_indices):
        obj = bpy.data.objects.get(mesh_name)
        if not obj or obj.type != 'MESH':
            print("Mesh not found")
            return

        # Ensure Object Mode first
        if bpy.context.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')

        # Deselect everything
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
        
        # Enter Edit Mode
        bpy.ops.object.mode_set(mode='EDIT')

        # Ensure face select mode
        bpy.ops.mesh.select_mode(type='FACE')

        # Access bmesh
        bm = bmesh.from_edit_mesh(obj.data)
        bm.faces.ensure_lookup_table()

        # Deselect all faces
        for f in bm.faces:
            f.select = False

        for face_index in face_indices:
        # Select target face
            bm.faces[face_index].select = True
            bm.select_history.clear()
            bm.select_history.add(bm.faces[face_index])

        bmesh.update_edit_mesh(obj.data)

    def run():
        frame_faces(mesh, indices)
    bpy.app.timers.register(run, first_interval=0)