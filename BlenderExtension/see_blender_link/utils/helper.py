import bpy
from mathutils import Vector
from bpy_extras.io_utils import axis_conversion

def get_scene_scale():
    """Return diagonal of bounding box of all mesh objects."""
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    if not meshes:
        return 1.0

    min_v = Vector((1e10, 1e10, 1e10))
    max_v = Vector((-1e10, -1e10, -1e10))

    for obj in meshes:
        for v in obj.bound_box:
            w = obj.matrix_world @ Vector(v)
            min_v = Vector((min(min_v[i], w[i]) for i in range(3)))
            max_v = Vector((max(max_v[i], w[i]) for i in range(3)))

    return (max_v - min_v).length

def renderer_to_blender_world(hit_render_pos):
    # This must match your export axis conversion
    global_matrix = axis_conversion(
        to_forward="Z",
        to_up="Y",
    ).to_4x4()
    return global_matrix.inverted() @ Vector(hit_render_pos)