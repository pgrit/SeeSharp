using System.Text.Json;

namespace SeeSharp.IO;

/// <summary>
/// Loads a mesh from a binary or ASCII .ply file
/// </summary>
public class PlyLoader : IMeshLoader {
    public string Type => "ply";

    public void LoadMesh(Scene resultScene, Dictionary<string, Material> namedMaterials,
                         Dictionary<string, RgbColor> emissiveMaterials, JsonElement jsonElement, string dirname) {
        // The path is relative to this .json, we need to make it absolute / relative to the CWD
        string relpath = jsonElement.GetProperty("relativePath").GetString();
        string filename = Path.Join(dirname, relpath);

        // In contrast to obj and fbx, ply files only have one material assigned
        string materialName = jsonElement.GetProperty("material").GetString();
        var material = namedMaterials[materialName];

        // Load the mesh and add it to the scene.
        PlyFile plyFile = new();
        if (!plyFile.ParseFile(filename))
            return;

        var mesh = plyFile.ToMesh();
        mesh.Material = material;

        RgbColor emission;
        if (emissiveMaterials != null && emissiveMaterials.TryGetValue(materialName, out emission)) {
            var emitter = new DiffuseEmitter(mesh, emission);
            lock(resultScene) resultScene.Emitters.Add(emitter);
        } else if (jsonElement.TryGetProperty("emission", out var emissionJson)) { // The object is an emitter
            emission = JsonUtils.ReadRgbColor(emissionJson);
            var emitter = new DiffuseEmitter(mesh, emission);
            lock (resultScene) resultScene.Emitters.Add(emitter);
        }
        lock (resultScene) resultScene.Meshes.Add(mesh);
    }
}
