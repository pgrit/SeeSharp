using SeeSharp.Cameras;
using SeeSharp.Geometry;
using SeeSharp.Shading.Background;
using SeeSharp.Shading.Emitters;
using SeeSharp.Shading.Materials;
using SeeSharp.Image;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using TinyEmbree;
using SimpleImageIO;
using SeeSharp.Common;

namespace SeeSharp {
    /// <summary>
    /// Manages all information required to render a scene.
    /// </summary>
    public class Scene {
        public FrameBuffer FrameBuffer;
        public Camera Camera;

        public List<Mesh> Meshes = new();
        public Raytracer Raytracer { get; private set; }

        public List<Emitter> Emitters { get; private set; } = new();
        public Background Background;

        public Vector3 Center { get; private set; }
        public float Radius { get; private set; }
        public BoundingBox Bounds { get; private set; }

        public List<string> ValidationErrorMessages { get; private set; } = new();

        /// <summary>
        /// Creates a semi-deep copy of the scene. That is, a shallow copy except that all lists of references
        /// are copied into new lists of references. So meshes in the new scene can be removed or added.
        /// </summary>
        /// <returns>A copy of the scene</returns>
        public Scene Copy() {
            Scene cpy = (Scene)this.MemberwiseClone();
            cpy.Meshes = new(this.Meshes);
            cpy.Emitters = new(this.Emitters);
            cpy.ValidationErrorMessages = new(this.ValidationErrorMessages);
            return cpy;
        }

        public void Prepare() {
            if (!IsValid)
                throw new InvalidOperationException("Cannot finalize an invalid scene.");

            // Prepare the scene geometry for ray tracing.
            Raytracer = new();
            for (int idx = 0; idx < Meshes.Count; ++idx) {
                Raytracer.AddMesh(Meshes[idx]);
            }
            Raytracer.CommitScene();

            // Compute the bounding sphere and bounding box of the scene.
            // 1) Compute the center (the average of all vertex positions), and bounding box
            Bounds = BoundingBox.Empty;
            var center = Vector3.Zero;
            ulong totalVertices = 0;
            for (int idx = 0; idx < Meshes.Count; ++idx) {
                foreach (var vert in Meshes[idx].Vertices) {
                    center += vert;
                    Bounds = Bounds.GrowToContain(vert);
                }
                totalVertices += (ulong)Meshes[idx].Vertices.Length;
            }
            Center = center / totalVertices;

            // 2) Compute the radius of the tight bounding box: the distance to the furthest vertex
            float radius = 0;
            for (int idx = 0; idx < Meshes.Count; ++idx) {
                foreach (var vert in Meshes[idx].Vertices) {
                    radius = MathF.Max((vert - Center).LengthSquared(), radius);
                }
            }
            Radius = MathF.Sqrt(radius);

            // If a background is set, pass the scene center and radius to it
            if (Background != null) {
                Background.SceneCenter = Center;
                Background.SceneRadius = Radius;
            }

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

                if (Emitters.Count == 0 && Background == null)
                    ValidationErrorMessages.Add("No emitters and no background in the scene.");

                foreach (string msg in ValidationErrorMessages) {
                    Common.Logger.Log(msg, Common.Verbosity.Error);
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
            if (!meshToEmitter.TryGetValue(point.Mesh, out emitter))
                return null;
            return emitter;
        }

        /// <summary>
        /// Loads a .json file and parses it as a scene. Assumes the file has been validated against
        /// the correct schema.
        /// </summary>
        /// <param name="path">Path to the .json scene file.</param>
        /// <returns>The scene created from the file.</returns>
        public static Scene LoadFromFile(string path) {
            // String parsing adheres to the OS specified culture settings.
            // However, we always want our .json files to use the decimal point . rather than a comma
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture =
                System.Globalization.CultureInfo.InvariantCulture;

            string jsonString = File.ReadAllText(path);

            Vector3 ReadVector(JsonElement json) {
                return new Vector3(
                    json[0].GetSingle(),
                    json[1].GetSingle(),
                    json[2].GetSingle());
            }

            RgbColor ReadRgbColor(JsonElement json) {
                var vec = ReadVector(json.GetProperty("value"));
                return new RgbColor(vec.X, vec.Y, vec.Z);
            }

            TextureRgb ReadColorOrTexture(JsonElement json) {
                string type = json.GetProperty("type").GetString();
                if (type == "rgb") {
                    var rgb = ReadRgbColor(json);
                    return new TextureRgb(rgb);
                } else if (type == "texture") {
                    var texturePath = json.GetProperty("path").GetString();
                    texturePath = Path.Join(Path.GetDirectoryName(path), texturePath);
                    return new TextureRgb(texturePath);
                } else
                    return null;
            }

            var resultScene = new Scene();

            using (JsonDocument document = JsonDocument.Parse(jsonString, new JsonDocumentOptions {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            })) {
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

                // Parse the background images
                JsonElement backgroundElement;
                if (root.TryGetProperty("background", out backgroundElement)) {
                    string type = backgroundElement.GetProperty("type").GetString();

                    if (type == "image") {
                        string filename = backgroundElement.GetProperty("filename").GetString();
                        string dir = Path.GetDirectoryName(path);
                        filename = Path.Join(dir, filename);

                        resultScene.Background = new EnvironmentMap(new RgbImage(filename));
                    }
                }

                // Parse all materials
                var watch = System.Diagnostics.Stopwatch.StartNew();
                Logger.Log("Start parsing materials.", Verbosity.Debug);
                var namedMaterials = new Dictionary<string, Material>();
                var emissiveMaterials = new Dictionary<string, RgbColor>();
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

                        // Check whether this is a purely diffuse material or "generic"
                        string type = "generic";
                        JsonElement elem;
                        if (m.TryGetProperty("type", out elem)) {
                            type = elem.GetString();
                        }

                        if (type == "diffuse") {
                            var parameters = new DiffuseMaterial.Parameters {
                                baseColor = ReadColorOrTexture(m.GetProperty("baseColor")),
                                transmitter = ReadOptionalBool("thin", false)
                            };
                            namedMaterials[name] = new DiffuseMaterial(parameters);
                        } else {
                            var parameters = new GenericMaterial.Parameters {
                                baseColor = ReadColorOrTexture(m.GetProperty("baseColor")),
                                roughness = new TextureMono(ReadOptionalFloat("roughness", 0.5f)),
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

                        // Check if the material is emissive
                        if (m.TryGetProperty("emission", out elem)) {
                            emissiveMaterials.Add(name, ReadRgbColor(elem));
                        }
                    }
                }
                watch.Stop();
                Logger.Log($"Done parsing materials after {watch.ElapsedMilliseconds}ms.", Verbosity.Info);

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
                            var emission = ReadRgbColor(emissionJson);
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
                        ObjConverter.AddToScene(objMesh, resultScene, namedMaterials, emissiveMaterials);
                    }
                }
            }

            return resultScene;
        }

        Dictionary<Mesh, Emitter> meshToEmitter = new Dictionary<Mesh, Emitter>();
    }
}