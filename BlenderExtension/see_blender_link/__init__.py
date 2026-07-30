from .addons import path_viewer, cursor_tracker

modules = (path_viewer, cursor_tracker,)

def register():
    for m in modules:
        m.register()

def unregister():
    for m in reversed(modules):
        m.unregister()