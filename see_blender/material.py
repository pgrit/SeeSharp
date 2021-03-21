import bpy
from bpy.props import *

class SeeSharpMaterial(bpy.types.PropertyGroup):
    base_color: FloatVectorProperty(
        name="BaseColor",
        description="Material base color",
        default=(1,1,1),
        min=0, max=1,
        subtype="COLOR")

    base_texture: PointerProperty(
        name="BaseColorTexture",
        description="Base color",
        type=bpy.types.Image)

    roughness: FloatProperty(
        name="Roughness",
        description="Roughness",
        default=1,
        min=0, max=1)

    rough_texture: PointerProperty(
        name="RoughnessTexture",
        description="Roughness",
        type=bpy.types.Image)

    metallic: FloatProperty(
        name="Metallic",
        description="Metallic",
        default=0,
        min=0, max=1)

    specularTintStrength: FloatProperty(
        name="SpecularTint",
        description="SpecularTint",
        default=0,
        min=0, max=1)

    anisotropic: FloatProperty(
        name="Anisotropic",
        description="Anisotropic",
        default=0,
        min=0, max=1)

    specularTransmittance: FloatProperty(
        name="SpecularTransmittance",
        description="SpecularTransmittance",
        default=0,
        min=0, max=1)

    indexOfRefraction: FloatProperty(
        name="IOR",
        description="IOR",
        default=1.45,
        min=1, max=5)

    diffuseTransmittance: FloatProperty(
        name="DiffuseTransmittance",
        description="DiffuseTransmittance",
        default=0,
        min=0, max=1)

    thin: BoolProperty(name="Thin", description="Thin")

    emission_color: FloatVectorProperty(
        name="Emission color",
        description="Emission color",
        default=(1,1,1),
        min=0, max=1,
        subtype="COLOR")

    emission_strength: FloatProperty(
        name="Emission strength",
        description="Emission strength",
        default=0, min=0
    )

    @classmethod
    def register(cls):
        bpy.types.Material.seesharp = PointerProperty(
            name="SeeSharpMaterial",
            description="SeeSharp material settings",
            type=cls,
        )

    @classmethod
    def unregister(cls):
        del bpy.types.Scene.seesharp

def map_texture(node):
    try:
        tex_out = node.links[0].from_node.image
        return tex_out
    except:
        pass
    return None

def map_principled(shader, seesharp):
    node = shader.inputs['Base Color']
    tex = map_texture(node)
    if tex:
        seesharp.base_texture = tex
    else:
        seesharp.base_color = (node.default_value[0], node.default_value[1], node.default_value[2])

    seesharp.roughness = shader.inputs["Roughness"].default_value
    seesharp.anisotropic = shader.inputs["Anisotropic"].default_value
    seesharp.indexOfRefraction = shader.inputs["IOR"].default_value
    seesharp.metallic = shader.inputs["Metallic"].default_value
    # diffuse transmittance not directly matched: instead, Blender has a separate
    # roughenss value for the transmission
    seesharp.specularTransmittance = shader.inputs["Transmission"].default_value
    seesharp.specularTint = shader.inputs["Specular Tint"].default_value

def map_diffuse(shader, seesharp):
    node = shader.inputs['Base Color']
    tex = map_texture(node)
    if tex:
        seesharp.base_texture = tex
    else:
        seesharp.base_color = (node.default_value[0], node.default_value[1], node.default_value[2])

    seesharp.roughness = 1

def map_translucent(shader, seesharp):
    tex, clr = map_texture(shader.inputs['Base Color']),
    if clr:
        seesharp.base_color = clr
    if tex:
        seesharp.base_texture = tex

    seesharp.roughness = 1
    seesharp.thin = 1
    seesharp.diffuseTransmittance = 1

def map_view_shader(material, seesharp):
    seesharp.base_color = material.diffuse_color
    seesharp.roughness = material.roughness
    seesharp.metallic = material.metallic

def map_emission(shader, seesharp):
    strength = shader.inputs['Strength'].default_value
    color = shader.inputs['Color'].default_value

    seesharp.emission_color = (color[0], color[1], color[2])
    seesharp.emission_strength = strength
    seesharp.base_color = (0, 0, 0)

shader_matcher = {
    "Principled BSDF": map_principled,
    "Diffuse BSDF": map_diffuse,
    "Translucent BSDF": map_translucent,
    "Emission": map_emission
}

def convert_material(material):
    try: # try to interpret as a known shader
        last_shader = material.node_tree.nodes['Material Output'].inputs['Surface'].links[0].from_node
        return shader_matcher[last_shader.name](last_shader, material.seesharp)
    except Exception as e: # use the view shading settings instead
        map_view_shader(material, material.seesharp)

class ConvertOperator(bpy.types.Operator):
    bl_idname = "seesharp.convert_material"
    bl_label = "Convert Cycles Material"

    def invoke(self, context, event):
        convert_material(context.material)
        return { "FINISHED" }

def register():
    bpy.utils.register_class(SeeSharpMaterial)
    bpy.utils.register_class(ConvertOperator)

def unregister():
    bpy.utils.unregister_class(SeeSharpMaterial)
    bpy.utils.unregister_class(ConvertOperator)