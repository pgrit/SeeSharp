using SeeSharp.Common;
using SeeSharp.Shading.Emitters;
using SeeSharp.Shading.Materials;
using SimpleImageIO;
using System.Collections.Generic;
using System.Numerics;

namespace SeeSharp.Geometry {
    public static class FbxConverter {
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
                RgbColor emission;
                if (emissionOverride != null && emissionOverride.TryGetValue(materialName, out emission)) {
                    var emitter = new DiffuseEmitter(mesh, emission);
                    scene.Emitters.Add(emitter);
                }
            }
        }
    }
}