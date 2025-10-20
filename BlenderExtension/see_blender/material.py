import math
import bpy
from bpy.props import FloatVectorProperty, PointerProperty, FloatProperty, BoolProperty

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

    emission_is_glossy: BoolProperty(
        name="Glossy Emission",
        description="Emission profile is sharpened like a glossy material's reflectance",
        default=False
    )

    emission_glossy_exponent: FloatProperty(
        name="Glossy Emission Exponent",
        description="Glossy Emission Exponent",
        default=20
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
        del bpy.types.Material.seesharp

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
    seesharp.metallic = shader.inputs["Metallic"].default_value
    seesharp.specularTransmittance = shader.inputs["Transmission Weight"].default_value
    seesharp.specularTint = shader.inputs["Specular Tint"].default_value
    seesharp.indexOfRefraction = shader.inputs["IOR"].default_value

    clr = shader.inputs["Emission Color"].default_value
    seesharp.emission_color = (clr[0], clr[1], clr[2])
    seesharp.emission_strength = shader.inputs["Emission Strength"].default_value

def map_diffuse(shader, seesharp):
    node = shader.inputs['Color']
    tex = map_texture(node)
    if tex:
        seesharp.base_texture = tex
    else:
        seesharp.base_color = (node.default_value[0], node.default_value[1], node.default_value[2])

    seesharp.roughness = 1
    seesharp.indexOfRefraction = 1

def map_glossy(shader, seesharp):
    node = shader.inputs['Color']
    tex = map_texture(node)
    if tex:
        seesharp.base_texture = tex
    else:
        seesharp.base_color = (node.default_value[0], node.default_value[1], node.default_value[2])

    seesharp.roughness = math.sqrt(shader.inputs["Roughness"].default_value)
    seesharp.indexOfRefraction = 1
    seesharp.metallic = 1

def map_translucent(shader, seesharp):
    tex, clr = map_texture(shader.inputs['Color']),
    if clr:
        seesharp.base_color = clr
    if tex:
        seesharp.base_texture = tex

    seesharp.roughness = 1
    seesharp.indexOfRefraction = 1

def map_view_shader(material, seesharp):
    clr = material.diffuse_color
    seesharp.base_color = (clr[0], clr[1], clr[2])
    seesharp.roughness = material.roughness
    seesharp.metallic = material.metallic
    seesharp.indexOfRefraction = 1

def map_emission(shader, seesharp):
    strength = shader.inputs['Strength'].default_value
    color = shader.inputs['Color'].default_value

    seesharp.emission_color = (color[0], color[1], color[2])
    seesharp.emission_strength = strength
    seesharp.base_color = (0, 0, 0)
    seesharp.indexOfRefraction = 1

def map_glass(shader, seesharp):
    clr_node = shader.inputs['Color']
    tex = map_texture(clr_node)
    if tex:
        seesharp.base_texture = tex
    else:
        seesharp.base_color = (clr_node.default_value[0], clr_node.default_value[1], clr_node.default_value[2])

    seesharp.roughness = shader.inputs['Roughness'].default_value
    seesharp.indexOfRefraction = shader.inputs['IOR'].default_value
    seesharp.specularTransmittance = 1
    seesharp.specularTint = 1

shader_matcher = {
    "Principled BSDF": map_principled,
    "Diffuse BSDF": map_diffuse,
    "Glossy BSDF": map_glossy,
    "Translucent BSDF": map_translucent,
    "Emission": map_emission,
    "Glass BSDF": map_glass
}

def convert_material(material):
    last_shader = material.node_tree.nodes['Material Output'].inputs['Surface'].links[0].from_node
    if last_shader.name in shader_matcher:
        return shader_matcher[last_shader.name](last_shader, material.seesharp)
    else:
        return map_view_shader(material, material.seesharp)

class ConvertOperator(bpy.types.Operator):
    bl_idname = "seesharp.convert_material"
    bl_label = "Convert Cycles Material"
    bl_description = "Sets the current material data by coarsely mapping a Cycles node graph"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        return context.material is not None

    def execute(self, context):
        convert_material(context.material)
        return { "FINISHED" }

class ConvertAllOperator(bpy.types.Operator):
    bl_idname = "seesharp.convert_all_materials"
    bl_label = "Convert All Cycles Materials"
    bl_description = "Sets the data of all materials in the scene by coarsely mapping the Cycles node graphs"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        for material in list(bpy.data.materials):
            if material.node_tree is None: continue
            convert_material(material)
        return { "FINISHED" }

def menu_func(self, context):
    self.layout.operator_context = 'INVOKE_DEFAULT'
    self.layout.operator(ConvertAllOperator.bl_idname, text="Convert all materials to SeeSharp")

def register():
    bpy.utils.register_class(SeeSharpMaterial)
    bpy.utils.register_class(ConvertOperator)
    bpy.utils.register_class(ConvertAllOperator)
    bpy.types.TOPBAR_MT_file_import.append(menu_func)

def unregister():
    bpy.utils.unregister_class(SeeSharpMaterial)
    bpy.utils.unregister_class(ConvertOperator)
    bpy.utils.unregister_class(ConvertAllOperator)
