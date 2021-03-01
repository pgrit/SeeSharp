import bpy

from . import exporter
from . import render_engine

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

def unregister():
    exporter.unregister()
    render_engine.unregister()