import socket
import threading
import select
from ..config import HOST, PORT_IN
from ..core.receiver import Receiver

_receiver_thread = None
_stop = False
_server_socket = None

def _receiver_loop(receiver: Receiver):
    global _server_socket, _stop
    try:
        _server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        _server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        _server_socket.bind((HOST, PORT_IN))
        _server_socket.listen(1)
        _server_socket.setblocking(False)
        print(f"[Receiver] Listening on {HOST}:{PORT_IN}")
    except Exception as e:
        print("[Receiver] Bind failed:", e)
        return

    buffer = ""

    while not _stop:
        try:
            readable, _, _ = select.select([_server_socket], [], [], 0.1)
            if _server_socket in readable:
                try:
                    conn, addr = _server_socket.accept()
                    conn.setblocking(False)
                    print("[Receiver] Connected:", addr)

                except Exception:
                    continue

                while not _stop:
                    try:
                        r, _, _ = select.select([conn], [], [], 0.05)
                        if conn in r:
                            chunk = conn.recv(1024)
                            if not chunk:
                                break
                            buffer += chunk.decode()
                            while "\n" in buffer:
                                line, buffer = buffer.split("\n", 1)
                                receiver.handle_message(line)
                    except Exception:
                        break
                conn.close()
        except Exception as e:
            print("[Receiver] Loop error:", e)
    print("[Receiver] Closing...")
    try:
        _server_socket.close()
    except:
        pass
    _server_socket = None


def start(receiver: Receiver):
    global _receiver_thread, _stop
    if _receiver_thread and _receiver_thread.is_alive():
        return
    _stop = False
    _receiver_thread = threading.Thread(target=_receiver_loop, args=(receiver,) ,daemon=True)
    _receiver_thread.start()


def stop():
    global _stop, _server_socket
    _stop = True
    if _server_socket:
        _server_socket.close()
