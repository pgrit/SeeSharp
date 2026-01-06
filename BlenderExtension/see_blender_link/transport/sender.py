import socket
import json
from ..config import HOST, PORT_OUT

_blazor_socket = None

def _connect():
    global _blazor_socket
    try:
        _blazor_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        _blazor_socket.connect((HOST, PORT_OUT))
        print("[Blender->Blazor] Connected")
    except Exception as e:
        print("[Blender->Blazor] Connect failed:", e)
        _blazor_socket = None


def send_to_blazor(data: dict):
    global _blazor_socket

    if not _blazor_socket:
        _connect()
        if not _blazor_socket:
            return

    try:
        msg = json.dumps(data) + "\n"
        _blazor_socket.sendall(msg.encode("utf8"))
    except Exception:
        _blazor_socket = None
