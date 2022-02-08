using System.Text.Json;

namespace SeeSharp.IO;

/// <summary>
/// All classes implementing this interface are automatically detected via reflections and used to
/// load mesh files with the corresponding type.
/// </summary>
public interface IMeshLoader {
    /// <summary>
    /// The type of the mesh file, e.g., "obj" or "ply"
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Loads the mesh and adds it to the scene
    /// </summary>
    /// <param name="resultScene">The loaded mesh is added to this scene</param>
    /// <param name="namedMaterials">Set of materials specified in the scene description</param>
    /// <param name="jsonElement">The mesh description in the .json file</param>
    /// <param name="dirname">Full path to the directory containing the .json file</param>
    /// <param name="emissiveMaterials">All emissive materials in the scene</param>
    /// <exception cref="MeshLoadException">Thrown if the file is corrupted</exception>
    void LoadMesh(Scene resultScene, Dictionary<string, Material> namedMaterials,
                  Dictionary<string, RgbColor> emissiveMaterials, JsonElement jsonElement, string dirname);
}