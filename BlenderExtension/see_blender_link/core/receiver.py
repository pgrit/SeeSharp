import json

class Receiver:
    def __init__(self, dispatcher):
        self.dispatcher = dispatcher

    def handle_message(self, line: str):
        if not line.strip():
            return
        try:
            msg = json.loads(line)
            self.dispatcher.dispatch(msg)
        except Exception as e:
            print("[Receiver] JSON error:", e)