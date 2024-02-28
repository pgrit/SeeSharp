using static SeeSharp.IO.IMeshLoader;

namespace SeeSharp.IO;

/// <summary>
/// Converts our parsed wavefront .obj mesh representation into an actual mesh.
/// Triangulates surfaces and makes sure that one mesh is created for each material.
/// </summary>
public class ObjConverter : IMeshLoader {
    /// <inheritdoc />
    public string Type => "obj";

    struct TriIdx {
        public ObjMesh.Index v0, v1, v2;

        public TriIdx(ObjMesh.Index v0, ObjMesh.Index v1, ObjMesh.Index v2) {
            this.v0 = v0; this.v1 = v1; this.v2 = v2;
        }
    }

    /// <summary>
    /// Converts a parsed .obj file into one or more triangle meshes and adds it to the scene.
    /// </summary>
    /// <param name="mesh">The parsed .obj mesh</param>
    /// <param name="materialOverride">
    ///     Materials from the .obj with a name matching one of the keys in this dictionary will be
    ///     replaced by the corresponding dictionary entry
    /// </param>
    /// <param name="emissionOverride">
    ///     If a material name is a key in this dictionary, all meshes with that material will be
    ///     converted to diffuse emitters. The value from the dictionary determines their emitted radiance.
    /// </param>
    public static (IEnumerable<Mesh>, IEnumerable<Emitter>) CreateMeshes(ObjMesh mesh,
                                                                         Dictionary<string, Material> materialOverride,
                                                                         Dictionary<string, EmissionParameters> emissionOverride = null) {
        // Create a dummy constant texture color for incorrect texture references
        var dummyColor = new TextureRgb(RgbColor.White);
        var dummyMaterial = new GenericMaterial(new GenericMaterial.Parameters {
            BaseColor = dummyColor
        });

        List<Mesh> loadedMeshes = new();
        List<Emitter> loadedEmitters = new();

        // Create the materials for this OBJ file
        var errors = new List<string>();
        var materials = new Dictionary<string, Material>();
        var emitters = new Dictionary<string, RgbColor>();
        for (int i = 1, n = mesh.Contents.Materials.Count; i < n; i++) {
            // Try to find the correct material
            string materialName = mesh.Contents.Materials[i];
            ObjMesh.Material objMaterial;
            if (!mesh.Contents.MaterialLib.TryGetValue(materialName, out objMaterial)) {
                errors.Add($"Cannot find material '{materialName}'. Replacing by dummy.");
                materials[materialName] = dummyMaterial;
                continue;
            }

            // Roughly match the different illumination modes to the right parameters of our material.
            GenericMaterial.Parameters materialParameters;
            switch (objMaterial.illuminationModel) {
                case 5: // perfect mirror
                    materialParameters = new GenericMaterial.Parameters {
                        BaseColor = new TextureRgb(objMaterial.specular),
                        SpecularTintStrength = 1.0f,
                        Metallic = 1,
                        Roughness = new TextureMono(0),
                    };
                    break;
                case 7: // perfect glass
                    materialParameters = new GenericMaterial.Parameters {
                        BaseColor = new TextureRgb(objMaterial.specular),
                        Metallic = 0,
                        Roughness = new TextureMono(0),
                        IndexOfRefraction = objMaterial.indexOfRefraction,
                        SpecularTransmittance = 1.0f,
                        SpecularTintStrength = 1.0f
                    };
                    break;
                case 2:
                default: // We pretend that anything else would be "2", aka, a phong shader
                    TextureRgb baseColor;
                    baseColor = string.IsNullOrEmpty(objMaterial.diffuseTexture)
                        ? new TextureRgb(objMaterial.diffuse)
                        : new TextureRgb(System.IO.Path.Join(mesh.BasePath, objMaterial.diffuseTexture));

                    // We coarsely map the "ns" term to roughness, use the diffuse color as base color,
                    // and ignore everything else.
                    materialParameters = new GenericMaterial.Parameters {
                        BaseColor = baseColor,
                        Roughness = new TextureMono(
                            objMaterial.specularIndex == 0
                            ? 1
                            : 1 / objMaterial.specularIndex),
                        Metallic = 0.5f
                    };
                    break;
            }
            materials[materialName] = new GenericMaterial(materialParameters);

            // Check if the material is emissive
            if (objMaterial.emission != RgbColor.Black)
                emitters[materialName] = objMaterial.emission;
        }

        // Convert the faces to triangles & build the new list of indices
        foreach (var obj in mesh.Contents.Objects) {
            // Mapping from material index to triangle definition, per group
            var triangleGroups = new List<Dictionary<int, List<TriIdx>>>();

            // Triangulate faces and group triangles by material
            bool has_normals = false;
            bool has_texcoords = false;
            bool empty = true;
            foreach (var group in obj.Groups) {
                // Create the mapping from material index to triangle definition for this group.
                triangleGroups.Add(new Dictionary<int, List<TriIdx>>());

                foreach (var face in group.Faces) {
                    int mtl_idx = face.Material;
                    triangleGroups[^1].TryAdd(mtl_idx, new List<TriIdx>());

                    // Check if any vertex has a normal or uv coordinate
                    for (int i = 0; i < face.Indices.Count; i++) {
                        has_normals |= (face.Indices[i].NormalIndex != 0);
                        has_texcoords |= (face.Indices[i].TextureIndex != 0);
                    }

                    // Compute the triangle indices for every n-gon
                    int v0 = 0;
                    int prev = 1;
                    for (int i = 1; i < face.Indices.Count - 1; i++) {
                        int next = i + 1;
                        triangleGroups[^1][mtl_idx].Add(new TriIdx(face.Indices[v0], face.Indices[prev], face.Indices[next]));
                        prev = next;

                        empty = false;
                    }
                }
            }

            if (empty) continue;

            // Create a mesh for each group of triangles with identical materials
            foreach (var group in triangleGroups) {
                foreach (var triangleSet in group) {
                    string materialName = mesh.Contents.Materials[triangleSet.Key];

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
                            localVertices.Add(mesh.Contents.Vertices[oldIndex.VertexIndex]);

                            if (has_normals)
                                localNormals.Add(mesh.Contents.Normals[oldIndex.NormalIndex]);

                            if (has_texcoords) {
                                var uv = mesh.Contents.Texcoords[oldIndex.TextureIndex];
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

                    loadedMeshes.Add(m);

                    // Create an emitter if the obj material is emissive
                    RgbColor emission = RgbColor.Black;
                    emitters.TryGetValue(materialName, out emission);

                    IEnumerable<Emitter> emitter = null;
                    if (emissionOverride?.TryGetValue(materialName, out EmissionParameters e) ?? false)
                        emitter = e.IsGlossy
                            ? GlossyEmitter.MakeFromMesh(m, e.Radiance, e.Exponent)
                            : DiffuseEmitter.MakeFromMesh(m, e.Radiance);
                    else if (emission != RgbColor.Black)
                        emitter = DiffuseEmitter.MakeFromMesh(m, emission);

                    if (emitter != null)
                        loadedEmitters.AddRange(emitter);
                }
            }
        }

        return (loadedMeshes, loadedEmitters);
    }

    public (IEnumerable<Mesh>, IEnumerable<Emitter>) LoadMesh(Dictionary<string, Material> namedMaterials,
                                                              Dictionary<string, EmissionParameters> emissiveMaterials,
                                                              JsonElement jsonElement, string dirname) {
        // The path is relative to this .json, we need to make it absolute / relative to the CWD
        string relpath = jsonElement.GetProperty("relativePath").GetString();
        string filename = Path.Join(dirname, relpath);

        // Load the mesh and add it to the scene. We pass all materials defined in the .json along
        // they will replace any equally named materials from the .mtl file.
        var objMesh = ObjMesh.FromFile(filename);
        return CreateMeshes(objMesh, namedMaterials, emissiveMaterials);
    }
}
