using SeeSharp.Core.Shading;
using SeeSharp.Core.Shading.Emitters;
using SeeSharp.Core.Shading.Materials;
using SeeSharp.Core.Image;
using System.Collections.Generic;
using System.Numerics;

namespace SeeSharp.Core.Geometry {
    public static class ObjConverter {

        struct TriIdx  {
            public ObjMesh.Index v0, v1, v2;

            public TriIdx(ObjMesh.Index v0, ObjMesh.Index v1, ObjMesh.Index v2) {
                this.v0 = v0; this.v1 = v1; this.v2 = v2;
            }
        }

        public static void AddToScene(ObjMesh mesh, Scene scene, Dictionary<string, Material> materialOverride,
                                      Dictionary<string, ColorRGB> emissionOverride = null) {
            // Create a dummy constant texture color for incorrect texture references
            var dummyColor  = Image<ColorRGB>.Constant(new ColorRGB(1.0f, 1.0f, 1.0f));
            var dummyMaterial = new GenericMaterial(new GenericMaterial.Parameters{
                baseColor = dummyColor
            });

            // Create the materials for this OBJ file
            var errors = new List<string>();
            var materials = new Dictionary<string, Material>();
            var emitters = new Dictionary<string, ColorRGB>();
            for (int i = 1, n = mesh.file.materials.Count; i < n; i++) {
                // Try to find the correct material
                string materialName = mesh.file.materials[i];
                ObjMesh.Material objMaterial;
                if (!mesh.file.materialLib.TryGetValue(materialName, out objMaterial)) {
                    errors.Add($"Cannot find material '{materialName}'. Replacing by dummy.");
                    materials[materialName] = dummyMaterial;
                    continue;
                }

                // Roughly match the different illumination modes to the right parameters of our material.
                GenericMaterial.Parameters materialParameters;
                switch (objMaterial.illuminationModel) {
                case 5: // perfect mirror
                    materialParameters = new GenericMaterial.Parameters {
                        baseColor = Image<ColorRGB>.Constant(objMaterial.specular),
                        specularTintStrength = 1.0f,
                        metallic = 1,
                        roughness = 0,
                    };
                    break;
                case 7: // perfect glass
                    materialParameters = new GenericMaterial.Parameters {
                        baseColor = Image<ColorRGB>.Constant(objMaterial.specular),
                        metallic = 0,
                        roughness = 0,
                        indexOfRefraction = objMaterial.indexOfRefraction,
                        specularTransmittance = 1.0f,
                        specularTintStrength = 1.0f
                    };
                    break;
                case 2:
                default: // We pretend that anything else would be "2", aka, a phong shader
                    Image<ColorRGB> baseColor;
                    baseColor = string.IsNullOrEmpty(objMaterial.diffuseTexture)
                        ? Image<ColorRGB>.Constant(objMaterial.diffuse)
                        : Image<ColorRGB>.LoadFromFile(System.IO.Path.Join(mesh.basePath, objMaterial.diffuseTexture));

                    // We coarsely map the "ns" term to roughness, use the diffuse color as base color,
                    // and ignore everything else.
                    materialParameters = new GenericMaterial.Parameters {
                        baseColor = baseColor,
                        roughness = objMaterial.specularIndex == 0 ? 1 : 1 / objMaterial.specularIndex,
                        metallic = 0.5f
                    };
                    break;
                }
                materials[materialName] = new GenericMaterial(materialParameters);

                // Check if the material is emissive
                if (objMaterial.emission != ColorRGB.Black)
                    emitters[materialName] = objMaterial.emission;
            }

            // Convert the faces to triangles & build the new list of indices
            foreach (var obj in mesh.file.objects) {
                // Mapping from material index to triangle definition, per group
                var triangleGroups = new List<Dictionary<int, List<TriIdx>>>();

                // Triangulate faces and group triangles by material
                bool has_normals = false;
                bool has_texcoords = false;
                bool empty = true;
                foreach (var group in obj.groups) {
                    // Create the mapping from material index to triangle definition for this group.
                    triangleGroups.Add(new Dictionary<int, List<TriIdx>>());

                    foreach (var face in group.faces) {
                        int mtl_idx = face.material;
                        triangleGroups[^1].TryAdd(mtl_idx, new List<TriIdx>());

                        // Check if any vertex has a normal or uv coordinate
                        for (int i = 0; i < face.indices.Count; i++) {
                            has_normals |= (face.indices[i].n != 0);
                            has_texcoords |= (face.indices[i].t != 0);
                        }

                        // Compute the triangle indices for every n-gon
                        int v0 = 0;
                        int prev = 1;
                        for (int i = 1; i < face.indices.Count - 1; i++) {
                            int next = i + 1;
                            triangleGroups[^1][mtl_idx].Add(new TriIdx(face.indices[v0], face.indices[prev], face.indices[next]));
                            prev = next;

                            empty = false;
                        }
                    }
                }

                if (empty) continue;

                // Create a mesh for each group of triangles with identical materials
                foreach (var group in triangleGroups) {
                    foreach (var triangleSet in group) {
                        string materialName = mesh.file.materials[triangleSet.Key];

                        // Either use the .obj material or the override
                        Material material;
                        if (materialOverride == null || !materialOverride.TryGetValue(materialName, out material)) {
                            material = materials[materialName];
                        }

                        // Copy all required vertices, normals, and texture coordinates for this group
                        // We do not support vertices that share a postion but not a normal, hence we map the full tripel to one int
                        var objToLocal = new Dictionary<ObjMesh.Index, int>();
                        var localVertices = new List<Vector3>();
                        var localNormals = has_normals ? new List<Vector3>() : null;
                        var localTexcoords = has_texcoords ? new List<Vector2>() : null;
                        int RemapIndex(ObjMesh.Index oldIndex) {
                            int newIndex;
                            if (!objToLocal.TryGetValue(oldIndex, out newIndex)) {
                                localVertices.Add(mesh.file.vertices[oldIndex.v]);

                                if (has_normals)
                                    localNormals.Add(mesh.file.normals[oldIndex.n]);

                                if (has_texcoords) {
                                    var uv = mesh.file.texcoords[oldIndex.t];
                                    uv.Y = 1 - uv.Y;
                                    localTexcoords.Add(uv);
                                }

                                newIndex = localVertices.Count - 1;
                                objToLocal[oldIndex] = newIndex;
                            }
                            return newIndex;
                        }

                        var indices = new List<int>(triangleSet.Value.Count * 3);
                        foreach (var triangle in triangleSet.Value) {
                            indices.Add(RemapIndex(triangle.v0));
                            indices.Add(RemapIndex(triangle.v1));
                            indices.Add(RemapIndex(triangle.v2));
                        }

                        // Create and add the mesh
                        Mesh m = new Mesh(localVertices.ToArray(), indices.ToArray(), localNormals?.ToArray(),
                                          localTexcoords?.ToArray()) {
                            Material = material
                        };
                        scene.Meshes.Add(m);

                        // Create an emitter if the obj material is emissive
                        ColorRGB emission;
                        if (emissionOverride != null && emissionOverride.TryGetValue(materialName, out emission)) {
                            var emitter = new DiffuseEmitter(m, emission);
                            scene.Emitters.Add(emitter);
                        } else if (emitters.TryGetValue(materialName, out emission)) {
                            var emitter = new DiffuseEmitter(m, emission);
                            scene.Emitters.Add(emitter);
                        }
                    }
                }
            }
        }
    }
}
