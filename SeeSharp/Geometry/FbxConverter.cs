using SeeSharp.Common;
using SeeSharp.Shading.Emitters;
using SeeSharp.Shading.Materials;
using SimpleImageIO;
using System.Collections.Generic;
using System.Numerics;

namespace SeeSharp.Geometry {
    /// <summary>
    /// Loads .fbx geometries via Assimp.NET
    /// </summary>
    public static class FbxConverter {
        /// <summary>
        /// Loads a mesh from a .fbx file and adds it to the given scene. Technically, this would also work
        /// for any other file format supported by Assimp, but we are doing some .fbx specific hacks to achieve
        /// the correct scale and other conventions.
        /// </summary>
        /// <param name="filename">Path to an existing .fbx mesh</param>
        /// <param name="scene">The scene to which the mesh should be added</param>
        /// <param name="materialOverride">
        ///     Materials from the .fbx with a name matching one of the keys in this dictionary will be
        ///     replaced by the corresponding dictionary entry
        /// </param>
        /// <param name="emissionOverride">
        ///     If a material name is a key in this dictionary, all meshes with that material will be
        ///     converted to diffuse emitters. The value from the dictionary determines their emitted radiance.
        /// </param>
        public static void AddToScene(string filename, Scene scene, Dictionary<string, Material> materialOverride,
                                      Dictionary<string, RgbColor> emissionOverride = null) {
            // Load the file with some basic post-processing
            Assimp.AssimpContext context = new();
            var assimpScene = context.ImportFile(filename,
                Assimp.PostProcessSteps.Triangulate | Assimp.PostProcessSteps.PreTransformVertices);

            // Add all meshes to the scene
            foreach (var m in assimpScene.Meshes) {
                var material = assimpScene.Materials[m.MaterialIndex];
                string materialName = material.Name;

                Vector3[] vertices = new Vector3[m.VertexCount];
                for (int i = 0; i < m.VertexCount; ++i)
                    vertices[i] = new Vector3(-m.Vertices[i].X, m.Vertices[i].Z, m.Vertices[i].Y) * 0.01f;

                Vector3[] normals = null;
                if (m.HasNormals) {
                    normals = new Vector3[m.VertexCount];
                    for (int i = 0; i < m.VertexCount; ++i)
                        normals[i] = new(-m.Normals[i].X, m.Normals[i].Z, m.Normals[i].Y);
                }

                // We currently only support a single uv channel
                Vector2[] texCoords = null;
                if (m.HasTextureCoords(0)) {
                    texCoords = new Vector2[m.VertexCount];
                    var texCoordChannel = m.TextureCoordinateChannels[0];
                    for (int i = 0; i < m.VertexCount; ++i)
                        texCoords[i] = new(texCoordChannel[i].X, 1 - texCoordChannel[i].Y);
                }

                if (m.TextureCoordinateChannelCount > 1)
                    Logger.Log($"Ignoring additional uv channels in mesh \"{m.Name}\" read from \"{filename}\"",
                        Verbosity.Warning);

                // If the material is not overridden by json, we load the diffuse color, or set an error color
                Material mat;
                if (materialOverride.ContainsKey(materialName)) {
                    mat = materialOverride[materialName];
                } else if (material.HasColorDiffuse) {
                    var c = material.ColorDiffuse;
                    mat = new DiffuseMaterial(new() {
                        BaseColor = new Image.TextureRgb(new RgbColor(c.R, c.G, c.B))
                    });
                } else {
                    mat = new DiffuseMaterial(new() {
                        BaseColor = new Image.TextureRgb(new RgbColor(1, 0, 1))
                    });
                }

                Mesh mesh = new(vertices, m.GetIndices(), normals, texCoords) {
                    Material = mat
                };
                scene.Meshes.Add(mesh);

                // Create an emitter if requested
                if (emissionOverride != null && emissionOverride.TryGetValue(materialName, out var emission)) {
                    var emitter = new DiffuseEmitter(mesh, emission);
                    scene.Emitters.Add(emitter);
                }
            }
        }
    }
}