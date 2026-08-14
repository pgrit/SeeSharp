import bpy
from mathutils import Vector
from ....utils.helper import get_scene_scale, renderer_to_blender_world
import json
from ....transport.sender import send_to_blazor
def handle_create_arrow(msg):
    id = msg.get("id")
    dir_json = msg.get("dir")
    data = json.loads(dir_json)

    pos= data["pos"]
    dir = data["dir"]
    length = data["len"]
    width = data["width"]
    
    pX = pos["X"]
    pY = pos["Y"]
    pZ = pos["Z"]

    dX = dir["X"]
    dY = dir["Y"]
    dZ = dir["Z"]

    def create_fixed_arrow(P, D, length, width, name="Arrow"):
        P = Vector(P)
        D = Vector(D)

        # total_len = D.length
        total_len = length
        if total_len < 1e-6:
            return None

        dir_n = D.normalized()

        scene_scale = get_scene_scale()
        relative_length = length / scene_scale

        shaft_rad = scene_scale * min(max(relative_length * 0.005, 0.001),0.01)
        tip_rad = scene_scale * min(max(relative_length * 0.015, 0.003), 0.03)
        tip_len = scene_scale * min(max(relative_length * 0.15, 0.02), 0.10)
        tip_len = min(tip_len, length * 0.4)

        width = max(width, 0.01)

        shaft_rad *= width
        tip_rad   *= width

        # clamp tip length if vector is short
        # tip_len = min(tip_len, total_len * 0.4)

        shaft_len = max(total_len - tip_len, total_len * 0.05)

        # ---- place shaft and tip ----
        shaft_loc = P + dir_n * (shaft_len * 0.5)
        tip_loc   = P + dir_n * (shaft_len + tip_len * 0.5)

        # cleanup
        bpy.ops.object.select_all(action='DESELECT')

        bpy.ops.mesh.primitive_cylinder_add(
            radius=shaft_rad, depth=shaft_len, location=shaft_loc)
        shaft = bpy.context.active_object

        bpy.ops.mesh.primitive_cone_add(
            radius1=tip_rad, depth=tip_len, location=tip_loc)
        tip = bpy.context.active_object

        # rotate to direction
        up = Vector((0,0,1))
        rot_q = up.rotation_difference(dir_n)
        for obj in (shaft, tip):
            obj.rotation_mode = 'QUATERNION'
            obj.rotation_quaternion = rot_q

        # join
        shaft.select_set(True)
        bpy.context.view_layer.objects.active = shaft
        tip.select_set(True)
        bpy.ops.object.join()

        obj = bpy.context.active_object
        obj.name = name
        bpy.ops.object.shade_smooth()

        return obj
    
    def get_or_create_collection(col_name):
        """Return existing collection or create a new one."""
        if col_name in bpy.data.collections:
            return bpy.data.collections[col_name]
        else:
            col = bpy.data.collections.new(col_name)
            bpy.context.scene.collection.children.link(col)
            return col
    
    def run():
        if bpy.context.mode != 'OBJECT':
            try:
                bpy.ops.object.mode_set(mode='OBJECT')
            except RuntimeError:
                print("Could not switch to Object Mode.")

        col = get_or_create_collection("probe")
        arrow_name = id 
        if arrow_name in bpy.data.objects:
            old_obj = bpy.data.objects[arrow_name]
            bpy.data.objects.remove(old_obj, do_unlink=True)

        obj = create_fixed_arrow(renderer_to_blender_world(Vector((pX, pY, pZ))),
                                 renderer_to_blender_world(Vector((dX, dY, dZ))),
                                 length,
                                 width,
                                 arrow_name)
        if obj:
            if obj.name not in col.objects:
                col.objects.link(obj)
            # col.objects.link(obj)
            # Remove from master scene collection (avoid duplicate visible link)
            try:
                bpy.context.scene.collection.objects.unlink(obj)
            except:
                pass

            # Select only the new arrow
            bpy.ops.object.select_all(action='DESELECT')
            obj.select_set(True)
            bpy.context.view_layer.objects.active = obj
    bpy.app.timers.register(run, first_interval=0)