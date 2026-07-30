class Dispatcher:
    def __init__(self):
        self._handlers = {}

    def register(self, command: str, handler):
        """
        handler: function(msg: dict)
        """
        self._handlers.setdefault(command, []).append(handler)

    def dispatch(self, msg: dict):
        cmd = msg.get("command")
        if not cmd:
            print("[Dispatcher] Missing command")
            return

        handlers = self._handlers.get(cmd)
        if not handlers:
            print(f"[Dispatcher] No handlers for '{cmd}'")
            return

        for handler in handlers:
            try:
                handler(msg)
            except Exception as e:
                print(f"[Dispatcher] Error in handler '{cmd}':", e)