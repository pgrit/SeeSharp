import bpy
import json
from ....transport.sender import send_to_blazor

def handle_click_on_node(msg):
    json_string = msg.get("path")
    col_id = msg.get("path_id")
    node_list = json.loads(json_string)
    is_full_graph = msg.get("is_full_graph")
    def run():
        collection = bpy.data.collections[f"arrow_group_{col_id}"]
        for obj in collection.objects:
            obj.hide_set(True)
        if (is_full_graph):
            for obj in collection.objects:
                obj.hide_set(False)
        else:       
            for i in range(len(node_list) - 1):
                var1 = node_list[i]
                var2 = node_list[i + 1]
                for obj in collection.objects:
                    if obj.name == f"{var1}-{var2}" or obj.name == f"{var2}-{var1}":
                        obj.hide_set(False)
    bpy.app.timers.register(run, first_interval=0)