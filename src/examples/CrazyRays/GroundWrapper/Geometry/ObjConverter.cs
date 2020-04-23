using GroundWrapper.Shading;
using GroundWrapper.Shading.Emitters;
using GroundWrapper.Shading.Materials;
using System.Collections.Generic;
using System.Numerics;

namespace GroundWrapper.Geometry {
    public static class ObjConverter {

        struct TriIdx  {
            public ObjMesh.Index v0, v1, v2;
            public int m;

            public TriIdx(ObjMesh.Index v0, ObjMesh.Index v1, ObjMesh.Index v2, int m) {
                this.v0 = v0; this.v1 = v1; this.v2 = v2; this.m = m;
            }
        }

        public static void AddToScene(ObjMesh mesh, Scene scene, Dictionary<string, Material> materialOverride) {
            // Create a dummy constant texture color for incorrect texture references
            var dummyColor  = Image.Constant(new ColorRGB(1.0f, 1.0f, 1.0f));
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
                        baseColor = Image.Constant(objMaterial.specular),
                        specularTintStrength = 1.0f,
                        metallic = 1,
                        roughness = 0,
                    };
                    break;
                case 7: // perfect glass
                    materialParameters = new GenericMaterial.Parameters {
                        baseColor = Image.Constant(objMaterial.specular),
                        metallic = 0,
                        roughness = 0,
                        indexOfRefraction = objMaterial.indexOfRefraction,
                        specularTransmittance = 1.0f,
                        specularTintStrength = 1.0f
                    };
                    break;
                case 2:
                default: // We pretend that anything else would be "2", aka, a phong shader
                    Image baseColor;
                    baseColor = string.IsNullOrEmpty(objMaterial.diffuseTexture)
                        ? Image.Constant(objMaterial.diffuse)
                        : Image.LoadFromFile(System.IO.Path.Join(mesh.basePath, objMaterial.diffuseTexture));

                    // We coarsely map the "ns" term to roughness, use the diffuse color as base color, 
                    // and ignore everything else.
                    materialParameters = new GenericMaterial.Parameters {
                        baseColor = baseColor,
                        roughness = 1 / objMaterial.specularIndex,
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
                // TODO we could group these by material to save some overhead
                //      currently, we are duplicating vertices, ruining every memory
                //      saving we could have gotten from indices
                var triangles = new List<TriIdx>(); 

                // Triangulate faces
                bool has_normals = false;
                bool has_texcoords = false;
                foreach (var group in obj.groups) {
                    foreach (var face in group.faces) {
                        int mtl_idx = face.material;

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
                            triangles.Add(new TriIdx(face.indices[v0], face.indices[prev], face.indices[next], mtl_idx));
                            prev = next;
                        }
                    }
                }

                if (triangles.Count == 0) continue;

                // Create a mesh for each triangle 
                // TODO this could be grouped by material!
                foreach (var triangle in triangles) {
                    string materialName = mesh.file.materials[triangle.m];

                    // Either use the .obj material or the override
                    Material material;
                    if (!materialOverride.TryGetValue(materialName, out material)) {
                        material = materials[materialName];
                    }

                    // Create the mesh
                    var vertices = new Vector3[3] {
                        mesh.file.vertices[triangle.v0.v],
                        mesh.file.vertices[triangle.v1.v],
                        mesh.file.vertices[triangle.v2.v],
                    };
                    var indices = new int[3] { 0, 1, 2 };

                    Vector3[] normals = !has_normals ? null : new Vector3[3] {
                        mesh.file.normals[triangle.v0.n],
                        mesh.file.normals[triangle.v1.n],
                        mesh.file.normals[triangle.v2.n],
                    };
                    Vector2[] uvs = !has_texcoords ? null : new Vector2[3] {
                        mesh.file.texcoords[triangle.v0.t],
                        mesh.file.texcoords[triangle.v1.t],
                        mesh.file.texcoords[triangle.v2.t],
                    };
                    Mesh m = new Mesh(vertices, indices, normals, uvs);
                    m.Material = material;
                    scene.Meshes.Add(m);

                    // Create an emitter if the obj material is emissive
                    // TODO support overrides for this, too?
                    ColorRGB emission;
                    if (emitters.TryGetValue(materialName, out emission)) {
                        var emitter = new DiffuseEmitter(m, emission);
                        scene.Emitters.Add(emitter);
                    }
                }
            }
        }
    }
}
