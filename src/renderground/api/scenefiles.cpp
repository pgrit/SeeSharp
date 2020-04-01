#include "api/internal.h"
#include "renderground.h"

#include <rapidjson/document.h>
#include <rapidjson/schema.h>
#include <rapidjson/istreamwrapper.h>
#include <rapidjson/ostreamwrapper.h>
#include <rapidjson/writer.h>

#include <fstream>
#include <unordered_map>
#include <string>

bool IsValidSceneFile(const char* filename, rapidjson::Document& json) {
    // Load and parse the schema file
    std::ifstream inStreamSchema("renderground-scene-schema.json");
    rapidjson::IStreamWrapper schemaStreamWrapper(inStreamSchema);
    rapidjson::Document schemeDoc;
    schemeDoc.ParseStream(schemaStreamWrapper);
    ApiCheck(!schemeDoc.HasParseError());
    rapidjson::SchemaDocument schema(schemeDoc);

    // Load and parse the scene file
    std::ifstream inputStream(filename);
    rapidjson::IStreamWrapper streamWrapper(inputStream);
    json.ParseStream(streamWrapper);
    ApiCheck(!json.HasParseError());

    // Validate against the schema
    rapidjson::SchemaValidator validator(schema);
    if (!json.Accept(validator)) {
        std::cerr << "Error while parsing '" << filename << "': " << std::endl
                  << "Invalid keyword: " << validator.GetInvalidSchemaKeyword() << std::endl;
        return false;
    }
    return true;
}

template <typename T>
Vector3 ReadVector(T& elem) {
    auto& values = elem.GetArray();
    ApiCheck(values.Size() == 3);
    Vector3 res;
    res.x = values[0].GetFloat();
    res.y = values[1].GetFloat();
    res.z = values[2].GetFloat();
    return res;
}

int CreateSingleValueImage(const Vector3 rgbValue) {
    const auto tex = CreateImageRGB(1, 1);
    ColorRGB color { rgbValue.x, rgbValue.y, rgbValue.z };
    AddSplatRGB(tex, 0, 0, color);
    return tex;
}

template <typename T>
int ReadColorOrTexture(T& elem) {
    std::string type = elem["type"].GetString();

    if (type == "rgb") {
        auto rgbColor = ReadVector(elem["value"]);
        return CreateSingleValueImage(rgbColor);
    }

    std::cerr << "Error: Unsupported color type '" << type << "'."
              << std::endl;
    return -1;
}

template <typename T>
void ReadFloatArray(T& elem, std::vector<float>& out) {
    auto data = elem.GetArray();
    out.reserve(data.Size());
    for (auto& v : data) {
        out.push_back(v.GetFloat());
    }
}

template <typename T>
void ReadIntArray(T& elem, std::vector<int>& out) {
    auto data = elem.GetArray();
    out.reserve(data.Size());
    for (auto& v : data) {
        out.push_back(v.GetInt());
    }
}

extern "C" {

GROUND_API bool LoadSceneFromFile(const char* filename, int frameBufferId) {
    rapidjson::Document json;
    if (!IsValidSceneFile(filename, json)) {
        // TODO proper error reporting mechanism? Throwing exceptions?
        //      Printing to std::cerr? Both?
        return false;
    }

    // Generate all transforms
    std::unordered_map<std::string, int> namedTransforms;
    for (auto t = json["transforms"].Begin(); t != json["transforms"].End(); ++t) {
        std::string name = (*t)["name"].GetString();

        Vector3 pos { 0, 0, 0 };
        if (t->HasMember("position")) {
            pos = ReadVector((*t)["position"]);
        }

        Vector3 rot { 0, 0, 0 };
        if (t->HasMember("rotation")) {
            rot = ReadVector((*t)["rotation"]);
        }

        Vector3 scale { 1, 1, 1 };
        if (t->HasMember("scale")) {
            scale = ReadVector((*t)["scale"]);
        }

        if (namedTransforms.find(name) != namedTransforms.end()) {
            std::cerr << "Warning: Duplicate transform '" << name << "'" << std::endl;
        }

        namedTransforms[name] = CreateTransform(pos, rot, scale);
    }

    // Generate all cameras, set the one named "default" or the
    // first one in the list as default.
    std::unordered_map<std::string, int> namedCameras;
    for (auto c = json["cameras"].Begin(); c != json["cameras"].End(); ++c) {
        std::string name = (*c)["name"].GetString();

        std::string type = (*c)["type"].GetString();
        ApiCheck(type == "perspective");

        float fov = (*c)["fov"].GetFloat();
        ApiCheck(fov > 0 && fov < 180);

        std::string transform = (*c)["transform"].GetString();
        auto iter = namedTransforms.find(transform);
        if (iter == namedTransforms.end()) {
            std::cerr << "Error: The transform '" << transform << "' applied to the camera '"
                      << name << "' was not defined." << std::endl;
            return false;
        }

        if (namedCameras.find(name) != namedCameras.end()) {
            std::cerr << "Warning: Duplicate camera '" << name << "'" << std::endl;
        }

        namedCameras[name] = CreatePerspectiveCamera(iter->second, fov, frameBufferId);
    }

    // Generate all materials
    std::unordered_map<std::string, int> namedMaterials;
    std::unordered_map<std::string, UberShaderParams> namedMaterialParameters;
    for (auto m = json["materials"].Begin(); m != json["materials"].End(); ++m) {
        std::string name = (*m)["name"].GetString();

        UberShaderParams params {
            -1, // baseColor
            -1  // emission
        };

        params.baseColorTexture = ReadColorOrTexture((*m)["baseColor"]);

        if (namedMaterials.find(name) != namedMaterials.end()) {
            std::cerr << "Warning: Duplicate material '" << name << "'" << std::endl;
        }

        namedMaterials[name] = AddUberMaterial(&params);

        // We keep track of the parameters: the current scene file format allows
        // specifying emission on a per-object basis. This requires creating new
        // copies of the material with adjusted emission later on.
        namedMaterialParameters[name] = params;
    }

    // Generate all triangle meshes
    std::unordered_map<std::string, int> namedMeshes;
    for (auto m = json["objects"].Begin(); m != json["objects"].End(); ++m) {
        std::string name = (*m)["name"].GetString();

        std::string materialName = (*m)["material"].GetString();
        auto materialIter = namedMaterials.find(materialName);
        if (materialIter == namedMaterials.end()) {
            std::cerr << "Error: The material named '" << materialName << "'"
                      << " used by mesh '" << name << "' was not defined." << std::endl;
            return false;
        }
        auto materialId = materialIter->second;

        if ((*m).HasMember("emission")) { // The object is an emitter
            // Read the emission color or texture
            auto emission = ReadColorOrTexture((*m)["emission"]);

            // Instatiate a new material
            UberShaderParams params = namedMaterialParameters[materialName];
            params.emissionTexture = emission;
            materialId = AddUberMaterial(&params);
        }

        std::string type = (*m)["type"].GetString();
        if (type != "trimesh") {
            std::cerr << "Object type '" << type << "' not supported." << std::endl;
            return false;
        } // TODO support reading mesh data from .ply and .obj files

        std::vector<float> vertices;
        std::vector<int> indices;
        std::vector<float> normals;
        std::vector<float> uvs;

        ReadFloatArray((*m)["vertices"], vertices);
        ReadIntArray((*m)["indices"], indices);
        if (m->HasMember("normals"))
            ReadFloatArray((*m)["normals"], normals);
        if (m->HasMember("uv"))
            ReadFloatArray((*m)["uv"], uvs);

        if (vertices.size() % 3 != 0) {
            std::cerr << "Error: Corrupted vertex data in mesh '" << name << "'. "
                      << "The number of floats in the vertex array is not a multiple of 3."
                      << std::endl;
            return false;
        }

        if (normals.size() > 0 && normals.size() != vertices.size()) {
            std::cerr << "Error: Corrupted vertex data in mesh '" << name << "'. "
                      << "Number of vertices and per-vertex normals do not match."
                      << std::endl;
        }

        if (uvs.size() > 0 && uvs.size() / 2 != vertices.size() / 3) {
            std::cerr << "Error: Corrupted vertex data in mesh '" << name << "'. "
                      << "Number of vertices and UV coordinates do not match."
                      << std::endl;
        }

        if (namedMeshes.find(name) != namedMeshes.end()) {
            std::cerr << "Warning: Duplicate camera '" << name << "'" << std::endl;
        }

        const auto meshId = AddTriangleMesh(
            vertices.data(), vertices.size() / 3,
            indices.data(), indices.size(),
            uvs.size() > 0 ? uvs.data() : nullptr,
            normals.size() > 0 ? normals.data() : nullptr);

        AssignMaterial(meshId, materialId);

        namedMeshes[name] = meshId;
    }

    return true;
}

GROUND_API void WriteSceneToFile(const char* filename) {
    rapidjson::Document json;

    // TODO generate .json elements for all objects currently in the scene

    // Store all transforms

    // Store all cameras

    // Save all textures as .exr

    // Store all materials

    // Export all triangle meshes as .ply

    // Store all mesh references in .json

    std::ofstream outputStream(filename);
    rapidjson::OStreamWrapper streamWrapper(outputStream);
    rapidjson::Writer<rapidjson::OStreamWrapper> writer(streamWrapper);
    json.Accept(writer);
}

}