using static SeeSharp.IO.IMeshLoader;

namespace SeeSharp.IO;

internal class TriMeshLoader : IMeshLoader {
    public string Type => "trimesh";

    public (IEnumerable<Mesh>, IEnumerable<Emitter>) LoadMesh(Dictionary<string, Material> namedMaterials,
                                                              Dictionary<string, EmissionParameters> emissiveMaterials,
                                                              JsonElement jsonElement, string dirname) {
        string materialName = jsonElement.GetProperty("material").GetString();
        var material = namedMaterials[materialName];

        Vector3[] ReadVec3Array(JsonElement json) {
            var result = new Vector3[json.GetArrayLength() / 3];
            for (int idx = 0; idx < json.GetArrayLength(); idx += 3) {
                result[idx / 3].X = json[idx + 0].GetSingle();
                result[idx / 3].Y = json[idx + 1].GetSingle();
                result[idx / 3].Z = json[idx + 2].GetSingle();
            }
            return result;
        }

        Vector2[] ReadVec2Array(JsonElement json) {
            var result = new Vector2[json.GetArrayLength() / 2];
            for (int idx = 0; idx < json.GetArrayLength(); idx += 2) {
                result[idx / 2].X = json[idx + 0].GetSingle();
                result[idx / 2].Y = json[idx + 1].GetSingle();
            }
            return result;
        }

        int[] ReadIntArray(JsonElement json) {
            var result = new int[json.GetArrayLength()];
            int idx = 0;
            foreach (var v in json.EnumerateArray())
                result[idx++] = v.GetInt32();
            return result;
        }

        Vector3[] vertices = ReadVec3Array(jsonElement.GetProperty("vertices"));
        int[] indices = ReadIntArray(jsonElement.GetProperty("indices"));

        Vector3[] normals = null;
        if (jsonElement.TryGetProperty("normals", out var normalsJson))
            normals = ReadVec3Array(jsonElement.GetProperty("normals"));

        Vector2[] uvs = null;
        if (jsonElement.TryGetProperty("uv", out var uvJson))
            uvs = ReadVec2Array(jsonElement.GetProperty("uv"));

        var mesh = new Mesh(vertices, indices, normals, uvs) { Material = material };

        IEnumerable<Emitter> emitters = null;
        if (emissiveMaterials != null && emissiveMaterials.TryGetValue(materialName, out var emission)) {
            emitters = emission.IsGlossy
                ? GlossyEmitter.MakeFromMesh(mesh, emission.Radiance, emission.Exponent)
                : DiffuseEmitter.MakeFromMesh(mesh, emission.Radiance);
        } else if (jsonElement.TryGetProperty("emission", out var emissionJson)) {
            emitters = DiffuseEmitter.MakeFromMesh(mesh, JsonUtils.ReadRgbColor(emissionJson));
        }

        return (new[] { mesh }, emitters);
    }
}