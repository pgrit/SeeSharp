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
    /// Manages all information required to render a scene. Each object is meant to be used to render one
    /// image. But shallow copies can be made (with replaced frame buffers) to re-render the same scene.
    /// </summary>
    public class Scene {
        /// <summary>
        /// The frame buffer that will receive the rendered image
        /// </summary>
        public FrameBuffer FrameBuffer;

        /// <summary>
        /// The camera, which models how the frame buffer receives light from the scene
        /// </summary>
        public Camera Camera;

        /// <summary>
        /// All meshes in the scene. There needs to be at least one.
        /// </summary>
        public List<Mesh> Meshes = new();

        /// <summary>
        /// Acceleration structure for ray tracing the meshes
        /// </summary>
        public Raytracer Raytracer { get; private set; }

        /// <summary>
        /// All emitters in the scene. There needs to be at least one, unless a background is given.
        /// </summary>
        public List<Emitter> Emitters { get; private set; } = new();

        /// <summary>
        /// Defines radiance from rays that leave the scene. Must be given if no emitters are present.
        /// </summary>
        public Background Background;

        /// <summary>
        /// Center of the geometry in the scene. Computed by <see cref="Prepare"/>
        /// </summary>
        public Vector3 Center { get; private set; }

        /// <summary>
        /// Radius of the scene bounding sphere. Computed by <see cref="Prepare"/>
        /// </summary>
        public float Radius { get; private set; }

        /// <summary>
        /// Axis aligned bounding box of the scene. Computed by <see cref="Prepare"/>
        /// </summary>
        public BoundingBox Bounds { get; private set; }

        /// <summary>
        /// If <see cref="IsValid"/> is false, this list will contain all error messages found during validation.
        /// </summary>
        public List<string> ValidationErrorMessages { get; private set; } = new();

        /// <summary>
        /// Creates a semi-deep copy of the scene. That is, a shallow copy except that all lists of references
        /// are copied into new lists of references. So meshes in the new scene can be removed or added.
        /// </summary>
        /// <returns>A copy of the scene</returns>
        public Scene Copy() {
            Scene cpy = (Scene)MemberwiseClone();
            cpy.Meshes = new(Meshes);
            cpy.Emitters = new(Emitters);
            cpy.ValidationErrorMessages = new(ValidationErrorMessages);
            return cpy;
        }

        /// <summary>
        /// Prepares the scene for rendering. Checks that there is no missing data, builds acceleration
        /// structures, etc.
        /// </summary>
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
            Camera.UpdateResolution(FrameBuffer.Width, FrameBuffer.Height);

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
                    Logger.Log(msg, Verbosity.Error);
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
            if (!meshToEmitter.TryGetValue(point.Mesh, out var emitter))
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

            Matrix4x4 ReadMatrix(JsonElement json) {
                if(json.GetArrayLength() != 9 && json.GetArrayLength() != 12 && json.GetArrayLength() != 16) {
                    Logger.Log($"Invalid matrix: Number of entries {json.GetArrayLength()} is not allowed", Verbosity.Error);
                    return Matrix4x4.Identity;
                }

                Matrix4x4 m = Matrix4x4.Identity;

                // 3x3
                m.M11 = json[0].GetSingle();
                m.M12 = json[1].GetSingle();
                m.M13 = json[2].GetSingle();

                m.M21 = json[4].GetSingle();
                m.M22 = json[5].GetSingle();
                m.M23 = json[6].GetSingle();

                m.M31 = json[8].GetSingle();
                m.M32 = json[9].GetSingle();
                m.M33 = json[10].GetSingle();

                // 3x4
                if(json.GetArrayLength() >= 12) {
                    m.M14 = json[3].GetSingle();
                    m.M24 = json[7].GetSingle();
                    m.M34 = json[11].GetSingle();
                } 

                // 4x4
                if(json.GetArrayLength() == 16) {
                    m.M41 = json[12].GetSingle();
                    m.M42 = json[13].GetSingle();
                    m.M43 = json[14].GetSingle();
                    m.M44 = json[15].GetSingle();
                }

                return m;
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
                } else if (type == "image") {
                    var texturePath = json.GetProperty("filename").GetString();
                    texturePath = Path.Join(Path.GetDirectoryName(path), texturePath);
                    return new TextureRgb(texturePath);
                } else {
                    Logger.Log($"Invalid texture specification: {json}", Verbosity.Error);
                    return new TextureRgb(new RgbColor(1, 0, 1));
                }
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

                    if (t.TryGetProperty("scale", out var scale)) {
                        var sc = ReadVector(scale);
                        result *= Matrix4x4.CreateScale(sc);
                    }

                    if (t.TryGetProperty("rotation", out var rotation)) {
                        var rot = ReadVector(rotation);
                        rot *= MathF.PI / 180.0f;
                        result *= Matrix4x4.CreateFromYawPitchRoll(rot.Y, rot.X, rot.Z);
                    }

                    if (t.TryGetProperty("position", out var position)) {
                        var pos = ReadVector(position);
                        result *= Matrix4x4.CreateTranslation(pos);
                    }

                    // Could be mutually exclusive, but lets keep it this way
                    if (t.TryGetProperty("matrix", out var matrix)) {
                        var mat = ReadMatrix(position);
                        result *= mat;
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
                    Matrix4x4.Invert(camToWorld, out var worldToCam);
                    namedCameras[name] = new PerspectiveCamera(worldToCam, fov); // TODO support DOF thin lens
                    resultScene.Camera = namedCameras[name]; // TODO allow loading of multiple cameras? (and selecting one by name later)
                }

                // Parse the background images
                if (root.TryGetProperty("background", out var backgroundElement)) {
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
                if (root.TryGetProperty("materials", out var materials)) {
                    foreach (var m in materials.EnumerateArray()) {
                        string name = m.GetProperty("name").GetString();

                        float ReadOptionalFloat(string name, float defaultValue) {
                            if (m.TryGetProperty(name, out var elem))
                                return elem.GetSingle();
                            return defaultValue;
                        }

                        bool ReadOptionalBool(string name, bool defaultValue) {
                            if (m.TryGetProperty(name, out var elem))
                                return elem.GetBoolean();
                            return defaultValue;
                        }

                        // Check whether this is a purely diffuse material or "generic"
                        string type = "generic";
                        if (m.TryGetProperty("type", out var elem)) {
                            type = elem.GetString();
                        }

                        if (type == "diffuse") {
                            var parameters = new DiffuseMaterial.Parameters {
                                BaseColor = ReadColorOrTexture(m.GetProperty("baseColor")),
                                Transmitter = ReadOptionalBool("thin", false)
                            };
                            namedMaterials[name] = new DiffuseMaterial(parameters);
                        } else {
                            // TODO check that there are no unsupported parameters
                            var parameters = new GenericMaterial.Parameters {
                                BaseColor = ReadColorOrTexture(m.GetProperty("baseColor")),
                                Roughness = new TextureMono(ReadOptionalFloat("roughness", 0.5f)),
                                Anisotropic = ReadOptionalFloat("anisotropic", 0.0f),
                                DiffuseTransmittance = ReadOptionalFloat("diffuseTransmittance", 1.0f),
                                IndexOfRefraction = ReadOptionalFloat("IOR", 1.0f),
                                Metallic = ReadOptionalFloat("metallic", 0.0f),
                                SpecularTintStrength = ReadOptionalFloat("specularTint", 0.0f),
                                SpecularTransmittance = ReadOptionalFloat("specularTransmittance", 0.0f),
                                Thin = ReadOptionalBool("thin", false)
                            };
                            namedMaterials[name] = new GenericMaterial(parameters);
                        }

                        // Check if the material is emissive
                        if (m.TryGetProperty("emission", out elem)) {
                            RgbColor emission = ReadRgbColor(elem);
                            if (emission != RgbColor.Black)
                                emissiveMaterials.Add(name, emission);
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

                        Vector3[] normals = null;
                        if (m.TryGetProperty("normals", out var normalsJson))
                            normals = ReadVec3Array(m.GetProperty("normals"));

                        Vector2[] uvs = null;
                        if (m.TryGetProperty("uv", out var uvJson))
                            uvs = ReadVec2Array(m.GetProperty("uv"));

                        var mesh = new Mesh(vertices, indices, normals, uvs) { Material = material };

                        Emitter emitter;
                        if (m.TryGetProperty("emission", out var emissionJson)) { // The object is an emitter
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
                    } else if (type == "fbx") {
                        // The path is relative to this .json, we need to make it absolute / relative to the CWD
                        string relpath = m.GetProperty("relativePath").GetString();
                        string dir = Path.GetDirectoryName(path);
                        string filename = Path.Join(dir, relpath);

                        // Load the mesh and add it to the scene. We pass all materials defined in the .json along
                        // they will replace any equally named materials from the .fbx file
                        FbxConverter.AddToScene(filename, resultScene, namedMaterials, emissiveMaterials);
                    }
                }
            }

            return resultScene;
        }

        readonly Dictionary<Mesh, Emitter> meshToEmitter = new();
    }
}