import bpy
import json
def handle_modify_material(msg):
    data = json.loads(msg.get("data"))
    def run():
        # Get material name
        material_name = data.get("name")
        if not material_name:
            print("Material name is missing.")
            return
        # Find material
        material = bpy.data.materials.get(material_name)
    
        if material is None:
            print(f"Material '{material_name}' not found.")
            return
    
        # Check material type
        material_type = data.get("type")
    
        # Diffuse materials are not modified for now
        if material_type == "diffuse":
            return
    
        # Only modify generic materials
        if material_type != "generic":
            print(f"Unknown material type: {material_type}")
            return
    
        # Make sure SeeSharp material data exists
        seesharp = material.seesharp
    
        # Modify parameters
        if "baseColor" in data:
            color = data["baseColor"]
            seesharp.base_color = (
                color["R"],
                color["G"],
                color["B"]
            )
    
        if "roughness" in data:
            seesharp.roughness = data["roughness"]
    
        if "metallic" in data:
            seesharp.metallic = data["metallic"]
    
        if "specularTintStrength" in data:
            seesharp.specularTintStrength = data["specularTintStrength"]
    
        if "anisotropic" in data:
            seesharp.anisotropic = data["anisotropic"]
    
        if "specularTransmittance" in data:
            seesharp.specularTransmittance = data["specularTransmittance"]
    
        if "indexOfRefraction" in data:
            seesharp.indexOfRefraction = data["indexOfRefraction"]
    bpy.app.timers.register(run, first_interval=0)