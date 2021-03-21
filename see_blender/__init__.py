import bpy

from . import exporter, render_engine, material_ui, material

bl_info = {
    "name": "SeeSharp Renderer",
    "author": "Pascal Grittmann",
    "version": (0, 1),
    "blender": (2, 80, 0),
    "category": "Import-Export",
}

def register():
    exporter.register()
    render_engine.register()
    material_ui.register()
    material.register()

def unregister():
    exporter.unregister()
    render_engine.unregister()
    material_ui.unregister()
    material.unregister()