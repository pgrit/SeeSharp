import bpy
from mathutils import Vector
from ....utils.helper import get_scene_scale, renderer_to_blender_world
import json
from ....transport.sender import send_to_blazor
def handle_create_path(msg):
    json_string = msg.get("graph")
    user_group_id = msg.get("id")
    data = json.loads(json_string)

    def flatten_tree(root):
        nodes: list[dict] = []
        pairs: list[tuple[str, str]] = []
        visited = set()

        def visit(node: dict):
            node_id = node["Id"]
            if node_id in visited:
                return
            visited.add(node_id)

            # collect node

            flat_node = {
                k: v for k, v in node.items()
                if k != "Successors"
            }
            nodes.append(flat_node)
            # collect parent-child edge (skip roots)
            parent_id = node.get("ancestorId")
            if parent_id is not None:
                pairs.append((parent_id, node_id))

            # recurse
            for child in node.get("Successors", []):
                visit(child)

        visit(root)
        return nodes, pairs
    def create_fixed_arrow(A, B, properties, name="Arrow"):
        A = Vector(A)
        B = Vector(B)
        dir_vec = (B - A)
        total_len = dir_vec.length
        if total_len < 1e-6:
            return None

        dir_n = dir_vec.normalized()

        # ---- fixed-size thickness, but *length = A→B exactly* ----
        scene_scale = get_scene_scale()

        tip_len   = scene_scale * 0.03      # fixed tip length (~3% scene)
        shaft_rad = scene_scale * 0.0025    # fixed thickness (~0.25%)
        tip_rad   = scene_scale * 0.008     # tip radius (~0.8%)

        # clamp tip length if segment is short
        tip_len = min(tip_len, total_len * 0.4)

        shaft_len = max(total_len - tip_len, total_len * 0.05)

        # ---- place shaft and tip ----
        shaft_loc = A + dir_n * (shaft_len * 0.5)
        tip_loc   = A + dir_n * (shaft_len + tip_len * 0.5)

        # cleanup
        bpy.ops.object.select_all(action='DESELECT')

        bpy.ops.mesh.primitive_cylinder_add(
            radius=shaft_rad, depth=shaft_len, location=shaft_loc)
        shaft = bpy.context.active_object

        bpy.ops.mesh.primitive_cone_add(
            radius1=tip_rad, depth=tip_len, location=tip_loc)
        tip = bpy.context.active_object

        # rotate to direction
        up = Vector((0,0,1))
        rot_q = up.rotation_difference(dir_n)
        for obj in (shaft, tip):
            obj.rotation_mode = 'QUATERNION'
            obj.rotation_quaternion = rot_q

        # join
        shaft.select_set(True)
        bpy.context.view_layer.objects.active = shaft
        tip.select_set(True)
        bpy.ops.object.join()
        obj = bpy.context.active_object
        obj.name = name
        bpy.ops.object.shade_smooth()

        obj.edge_data.json_data = json.dumps(properties, indent=2)
        obj["is_edge"] = True
        return obj
    
    def assign_colour(obj, typeA, typeB, base_name):
        rgb = (1.0, 0.0, 0.0)
        if (typeA == "BSDFSampleNode"):
            if (typeB == "BSDFSampleNode"):
                rgb = (1.0, 0.0, 0.0)
            elif (typeB == "NextEventNode"):
                rgb = (0.0, 0.0, 1.0)
            elif (typeB == "BackgroundNode"):
                rgb = (0.5, 0.0, 0.5)
            else:
                rgb = (1.0, 0.0, 0.0)
        if (typeA == "LightPathNode" or typeB == "LightPathNode"):
            rgb = (0.0, 1.0, 0.0)
        
        # Create material with color
        mat = bpy.data.materials.new(name=base_name + "_mat")
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1)
        obj.data.materials.append(mat)
        return obj

    def edge_type(start, end):
        if (end == "BSDFSampleNode"):
            return "BSDF"
        elif (end == "NextEventNode"):
            return "Next Event"
        elif (end == "BackgroundNode"):
            return "Background"
        elif (end == "LightPathNode"):
            return "Light Path"
        elif (end == "ConnectionNode"):
            if (start == "BSDFSampleNode"):
                return "Camera Path - Connection"
            elif (start == "LightPathNode"):
                return "Light Path - Connection"
        elif (end == "MergeNode"):
            if (start == "BSDFSampleNode"):
                return "Camera Path - Merge"
            elif (start == "LightPathNode"):
                return "Light Path - Merge"
        else:
            return "Invalid"  

    def run():
        col_name = f"arrow_group_{user_group_id}"
        col = bpy.data.collections.new(col_name)
        bpy.context.scene.collection.children.link(col)

        id_to_node = {}
        nodes, pairs = flatten_tree(data)
    
        for node in nodes:
            pos = node["Position"]
            id_to_node[node["Id"]] = {"pos": renderer_to_blender_world(Vector((pos["X"], pos["Y"], pos["Z"]))),
                                        "data": node,
                                        "type": node["$type"]}

        for start_id, end_id in pairs:
            if (id_to_node[start_id]["type"] == "LightPathNode" or id_to_node[end_id]["type"] == "LightPathNode"):
                # reverse the direction if its light path
                temp_id = start_id
                start_id = end_id
                end_id = temp_id
            start = id_to_node[start_id]["pos"] #id_to_node.get(start_id)
            end = id_to_node[end_id]["pos"] #id_to_node.get(end_id)
            #----------------------------------------Add type-------------------------------
            arrow_type = edge_type(id_to_node[start_id]["type"], id_to_node[end_id]["type"])
            props = id_to_node[end_id]["data"] 
            props = dict([("Type", arrow_type), *props.items()])
            #-------------------------------------------------------------------------------
            if start is None or end is None:
                print(f"Missing node for edge: {start_id} → {end_id}")
                continue

            arrow_name = f"Arrow_{start_id}_to_{end_id}"
            obj = create_fixed_arrow(start, end, props, f"{start_id}-{end_id}")
            obj = assign_colour(obj, id_to_node[start_id]["type"], id_to_node[end_id]["type"], f"{start_id}-{end_id}")
            if obj:
                col.objects.link(obj)
                # Remove from master scene collection (avoid duplicate visible link)
                try:
                    bpy.context.scene.collection.objects.unlink(obj)
                except:
                    pass

            # Tag object with group ID
            obj["blazor_arrow_group"] = user_group_id
        send_to_blazor({
            "event": "created",
            "id": user_group_id
        })
    bpy.app.timers.register(run, first_interval=0)