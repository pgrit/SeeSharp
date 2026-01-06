bl_info = {
    "name": "SeeBlender Link",
    "author": "Minh Nguyen",
    "version": (1, 0),
    "blender": (4, 5, 0),
    "description": "Enable communication between Blender and SeeSharp application",
    "category": "System",
}

from .addons import path_viewer, cursor_tracker

modules = (path_viewer, cursor_tracker,)

def register():
    for m in modules:
        m.register()

def unregister():
    for m in reversed(modules):
        m.unregister()