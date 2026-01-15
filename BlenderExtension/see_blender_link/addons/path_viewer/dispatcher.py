from ...core.dispatcher import Dispatcher
from .commands.create_path import handle_create_path
from .commands.delete_path import handle_delete_path
from .commands.select_path import handle_select_path
from .commands.click_on_node import handle_click_on_node
from .commands.dbclick_on_node import handle_dbclick_on_node
from .commands.import_scene import handle_import_scene

dispatcher = Dispatcher()
dispatcher.register("create_path", handle_create_path)
dispatcher.register("delete_path", handle_delete_path)
dispatcher.register("select_path", handle_select_path)
dispatcher.register("click_on_node", handle_click_on_node)
dispatcher.register("dbclick_on_node", handle_dbclick_on_node)
dispatcher.register("import_scene", handle_import_scene)