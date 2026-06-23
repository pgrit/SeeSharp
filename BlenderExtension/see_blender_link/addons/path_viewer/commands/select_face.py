import bpy
import bmesh

def handle_select_face(msg):
    mesh = msg.get("mesh")
    face_id = msg.get("face_id")

    def frame_face(mesh_name, face_index):
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

        # Enter Edit Mode
        bpy.ops.object.mode_set(mode='EDIT')

        # Ensure face select mode
        bpy.ops.mesh.select_mode(type='FACE')

        # Access bmesh
        bm = bmesh.from_edit_mesh(obj.data)
        bm.faces.ensure_lookup_table()

        if face_index >= len(bm.faces):
            print("Invalid face index")
            return

        # Deselect all faces
        for f in bm.faces:
            f.select = False

        # Select target face
        bm.faces[face_index].select = True
        bm.select_history.clear()
        bm.select_history.add(bm.faces[face_index])

        bmesh.update_edit_mesh(obj.data)

        # Find a VIEW_3D area and frame
        for area in bpy.context.window.screen.areas:
            if area.type == 'VIEW_3D':
                for region in area.regions:
                    if region.type == 'WINDOW':
                        with bpy.context.temp_override(
                            window=bpy.context.window,
                            area=area,
                            region=region,
                            active_object=obj,
                                object=obj,
                        ):
                            bpy.ops.view3d.view_selected()
                        return

        print("No VIEW_3D area found")

    def run():
        frame_face(mesh, face_id)
    bpy.app.timers.register(run, first_interval=0)