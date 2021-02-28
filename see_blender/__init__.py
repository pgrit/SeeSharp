import bpy

from . import blend_to_seesharp
from . import blender_render

bl_info = {
    "name": "SeeSharp Renderer",
    "author": "Pascal Grittmann",
    "version": (0, 1),
    "blender": (2, 80, 0),
    "category": "Import-Export",
}

def register():
    blend_to_seesharp.register()
    blender_render.register()

def unregister():
    blend_to_seesharp.unregister()
    blender_render.unregister()