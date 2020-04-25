using GroundWrapper.Cameras;
using GroundWrapper.Geometry;
using GroundWrapper.Shading;
using GroundWrapper.Shading.Emitters;
using GroundWrapper.Shading.Materials;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;

namespace GroundWrapper {
    public class Scene {
        public Image FrameBuffer;
        public Cameras.Camera Camera;

        public List<Mesh> Meshes = new List<Mesh>();
        public Raytracer Raytracer { get; private set; }

        public List<Emitter> Emitters { get; private set; } = new List<Emitter>();

        public List<string> ValidationErrorMessages { get; private set; } = new List<string>();

        public void Prepare() {
            if (!IsValid)
                throw new System.InvalidOperationException("Cannot finalize an invalid scene.");

            // Prepare the scene geometry for ray tracing.
            Raytracer = new Raytracer();
            for (int idx = 0; idx < Meshes.Count; ++idx) {
                Raytracer.AddMesh(Meshes[idx]);
            }
            Raytracer.CommitScene();

            // Make sure the camera is set for the correct resolution.
            Camera.UpdateFrameBuffer(FrameBuffer);

            // Build the mesh to emitter mapping
            meshToEmitter.Clear();
            foreach (var emitter in Emitters) {
                meshToEmitter.Add(emitter.Mesh, emitter);
            }
        }

        /// <summary>
        /// True, if the scene is valid. Any errors found whil accessing the property will be
        /// reported in the <see cref="ValidationErrorMessages"/> list.
        /// </summary>
        public bool IsValid {
            get {
                ValidationErrorMessages.Clear();

                if (FrameBuffer == null)
                    ValidationErrorMessages.Add("Framebuffer not set.");
                if (Camera == null)
                    ValidationErrorMessages.Add("Camera not set.");
                if (Meshes == null || Meshes.Count == 0)
                    ValidationErrorMessages.Add("No meshes in the scene.");

                int idx = 0;
                foreach (var m in Meshes) {
                    if (m.Material == null)
                        ValidationErrorMessages.Add($"Mesh[{idx}] does not have a material.");
                    idx++;
                }

                return ValidationErrorMessages.Count == 0;
            }
        }

        /// <summary>
        /// Returns the emitter attached to the mesh on which a <see cref="SurfacePoint"/> lies.
        /// </summary>
        /// <param name="point">A point on a mesh surface.</param>
        /// <returns>The attached emitter reference, or null.</returns>
        public Emitter QueryEmitter(SurfacePoint point) {
            Emitter emitter;
            if (!meshToEmitter.TryGetValue(point.mesh, out emitter))
                return null;
            return emitter;
        }

        /// <summary>
        /// Loads a .json file and parses it as a scene. Assumes the file has been validated against the correct schema.
        /// </summary>
        /// <param name="path">Path to the .json scene file.</param>
        /// <returns>The scene created from the file.</returns>
        public static Scene LoadFromFile(string path) {
            string jsonString = File.ReadAllText(path);

            Vector3 ReadVector(JsonElement json) {
                return new Vector3(
                    json[0].GetSingle(),
                    json[1].GetSingle(),
                    json[2].GetSingle());
            }

            ColorRGB ReadColorRGB(JsonElement json) {
                var vec = ReadVector(json.GetProperty("value"));
                return new ColorRGB(vec.X, vec.Y, vec.Z);
            }

            Image ReadColorOrTexture(JsonElement json) {
                string type = json.GetProperty("type").GetString();
                if (type == "rgb") {
                    var rgb = ReadColorRGB(json);
                    return Image.Constant(rgb);
                } else if (type == "texture") {
                    var texturePath = json.GetProperty("path").GetString();
                    texturePath = Path.Join(Path.GetDirectoryName(path), texturePath);
                    return Image.LoadFromFile(texturePath);
                } else
                    return null;
            }

            var resultScene = new Scene();

            using (JsonDocument document = JsonDocument.Parse(jsonString)) {
                var root = document.RootElement;

                // Parse all transforms
                var namedTransforms = new Dictionary<string, Matrix4x4>();
                var transforms = root.GetProperty("transforms");
                foreach (var t in transforms.EnumerateArray()) {
                    string name = t.GetProperty("name").GetString();

                    Matrix4x4 result = Matrix4x4.Identity;

                    JsonElement scale;
                    if (t.TryGetProperty("scale", out scale)) {
                        var sc = ReadVector(scale);
                        result = result * Matrix4x4.CreateScale(sc);
                    }

                    JsonElement rotation;
                    if (t.TryGetProperty("rotation", out rotation)) {
                        var rot = ReadVector(rotation);
                        rot *= MathF.PI / 180.0f;
                        result = result * Matrix4x4.CreateFromYawPitchRoll(rot.Y, rot.X, rot.Z);
                    }

                    JsonElement position;
                    if (t.TryGetProperty("position", out position)) {
                        var pos = ReadVector(position);
                        result = result * Matrix4x4.CreateTranslation(pos);
                    }

                    namedTransforms[name] = result;
                }

                // Parse all cameras
                var namedCameras = new Dictionary<string, Camera>();
                var cameras = root.GetProperty("cameras");
                foreach (var c in cameras.EnumerateArray()) {
                    string name = c.GetProperty("name").GetString();
                    string type = c.GetProperty("type").GetString();
                    float fov = c.GetProperty("fov").GetSingle();
                    string transform = c.GetProperty("transform").GetString();
                    var camToWorld = namedTransforms[transform];
                    Matrix4x4 worldToCam;
                    Matrix4x4.Invert(camToWorld, out worldToCam);
                    namedCameras[name] = new PerspectiveCamera(worldToCam, fov, null);
                    resultScene.Camera = namedCameras[name]; // TODO allow loading of multiple cameras? (and selecting one by name later)
                }

                // Parse all materials
                var namedMaterials = new Dictionary<string, Material>();
                JsonElement materials;
                if (root.TryGetProperty("materials", out materials)) {
                    foreach (var m in materials.EnumerateArray()) {
                        string name = m.GetProperty("name").GetString();

                        float ReadOptionalFloat(string name, float defaultValue) {
                            JsonElement elem;
                            if (m.TryGetProperty(name, out elem))
                                return elem.GetSingle();
                            return defaultValue;
                        }

                        bool ReadOptionalBool(string name, bool defaultValue) {
                            JsonElement elem;
                            if (m.TryGetProperty(name, out elem))
                                return elem.GetBoolean();
                            return defaultValue;
                        }

                        var parameters = new GenericMaterial.Parameters {
                            baseColor = ReadColorOrTexture(m.GetProperty("baseColor")),
                            roughness = ReadOptionalFloat("roughness", 0.5f),
                            anisotropic = ReadOptionalFloat("anisotropic", 0.0f),
                            diffuseTransmittance = ReadOptionalFloat("diffuseTransmittance", 1.0f),
                            indexOfRefraction = ReadOptionalFloat("IOR", 1.0f),
                            metallic = ReadOptionalFloat("metallic", 0.0f),
                            specularTintStrength = ReadOptionalFloat("specularTint", 0.0f),
                            specularTransmittance = ReadOptionalFloat("specularTransmittance", 0.0f),
                            thin = ReadOptionalBool("thin", false)
                        };

                        namedMaterials[name] = new GenericMaterial(parameters);
                    }
                }

                // Parse all triangle meshes
                var namedMeshes = new Dictionary<string, Mesh>();
                var meshes = root.GetProperty("objects");
                foreach (var m in meshes.EnumerateArray()) {
                    string name = m.GetProperty("name").GetString();

                    string type = m.GetProperty("type").GetString();
                    if (type == "trimesh") {
                        string materialName = m.GetProperty("material").GetString();
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

                        Vector3[] vertices = ReadVec3Array(m.GetProperty("vertices"));
                        int[] indices = ReadIntArray(m.GetProperty("indices"));

                        JsonElement normalsJson;
                        Vector3[] normals = null;
                        if (m.TryGetProperty("normals", out normalsJson))
                            normals = ReadVec3Array(m.GetProperty("normals"));

                        JsonElement uvJson;
                        Vector2[] uvs = null;
                        if (m.TryGetProperty("uv", out uvJson))
                            uvs = ReadVec2Array(m.GetProperty("uv"));

                        var mesh = new Mesh(vertices, indices, normals, uvs);
                        mesh.Material = material;

                        Emitter emitter;
                        JsonElement emissionJson;
                        if (m.TryGetProperty("emission", out emissionJson)) { // The object is an emitter
                            // TODO update schema to allow different types of emitters here
                            var emission = ReadColorRGB(emissionJson);
                            emitter = new DiffuseEmitter(mesh, emission);
                            resultScene.Emitters.Add(emitter);
                        }
                        namedMeshes[name] = mesh;
                        resultScene.Meshes.Add(mesh);
                    } else if (type == "obj") {
                        // The path is relative to this .json, we need to make it absolute / relative to the CWD
                        string relpath = m.GetProperty("relativePath").GetString();
                        string dir = Path.GetDirectoryName(path);
                        string filename = Path.Join(dir, relpath);

                        // Load the mesh and add it to the scene. We pass all materials defined in the .json along
                        // they will replace any equally named materials from the .mtl file.
                        var objMesh = ObjMesh.FromFile(filename);
                        ObjConverter.AddToScene(objMesh, resultScene, namedMaterials);
                    }
                }
            }

            return resultScene;
        }

        Dictionary<Mesh, Emitter> meshToEmitter = new Dictionary<Mesh, Emitter>();
    }
}