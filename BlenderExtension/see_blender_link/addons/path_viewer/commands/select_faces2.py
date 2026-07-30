import bpy
import bmesh
import json
def handle_select_faces2(msg):
    meshes_ = msg.get("meshes")
    json_indices = msg.get("face_indices")
    indices = json.loads(json_indices)
    meshes = json.loads(meshes_)
   
    def frame_faces(mesh_names, face_indices):
        from collections import defaultdict

        # Validate inputs
        if len(mesh_names) != len(face_indices):
            print("mesh_names and face_indices must have same length")
            return

        # Group faces by mesh
        mesh_to_faces = defaultdict(list)

        for mesh_name, face_index in zip(mesh_names, face_indices):
            mesh_to_faces[mesh_name].append(face_index)

        # Ensure Object Mode
        if bpy.context.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')

        # Deselect everything
        bpy.ops.object.select_all(action='DESELECT')

        objects = []

        # Select all valid objects first
        for mesh_name in mesh_to_faces.keys():

            obj = bpy.data.objects.get(mesh_name)

            if not obj or obj.type != 'MESH':
                print(f"Invalid mesh: {mesh_name}")
                continue

            obj.select_set(True)
            objects.append(obj)

        if not objects:
            return

        # Set one active object
        bpy.context.view_layer.objects.active = objects[0]

        # Enter MULTI-OBJECT edit mode once
        bpy.ops.object.mode_set(mode='EDIT')

        # Stay in FACE selection mode
        bpy.ops.mesh.select_mode(type='FACE')

        # Process each mesh only once
        for obj in objects:

            bm = bmesh.from_edit_mesh(obj.data)
            bm.faces.ensure_lookup_table()

            # Clear previous face selections
            for f in bm.faces:
                f.select = False

            # Select requested faces
            indices = mesh_to_faces[obj.name]

            active_face = None

            for face_index in indices:

                if 0 <= face_index < len(bm.faces):

                    face = bm.faces[face_index]
                    face.select = True
                    active_face = face

                else:
                    print(f"Invalid face index {face_index} for {obj.name}")

            # Keep last selected face active
            if active_face:
                bm.select_history.clear()
                bm.select_history.add(active_face)

            # Update mesh once
            bmesh.update_edit_mesh(obj.data)

        # Frame selected faces
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

                        return
    def run():
        frame_faces(meshes, indices)
    bpy.app.timers.register(run, first_interval=0)