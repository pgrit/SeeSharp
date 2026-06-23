import pkgutil
import importlib
from ...core.dispatcher import Dispatcher
from . import commands

dispatcher = Dispatcher()

for _, module_name, _ in pkgutil.iter_modules(commands.__path__):
    module = importlib.import_module(f"{commands.__name__}.{module_name}")

    for attr_name in dir(module):
        if attr_name.startswith("handle_"):
            handler = getattr(module, attr_name)
            command_name = attr_name.replace("handle_", "")
            dispatcher.register(command_name, handler)