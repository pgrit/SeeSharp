using static SeeSharp.IO.IMeshLoader;

namespace SeeSharp.IO;

/// <summary>
/// Generates a scene from a .json file
/// </summary>
public static class JsonScene {
    static IMeshLoader[] KnownLoaders {
        get {
            if (_knownLoaders == null) _knownLoaders = TypeFactory<IMeshLoader>.All;
            return _knownLoaders;
        }
    }
    static IMeshLoader[] _knownLoaders;

    private static void ReadMeshes(string path, Scene resultScene, JsonElement root,
                                   Dictionary<string, Material> namedMaterials,
                                   Dictionary<string, EmissionParameters> emissiveMaterials) {
        var meshes = root.GetProperty("objects");

        ProgressBar progressBar = new(prefix: "Loading meshes...");
        progressBar.Start(meshes.GetArrayLength());

        var meshSets = new IEnumerable<Mesh>[meshes.GetArrayLength()];
        var emitterSets = new IEnumerable<Emitter>[meshes.GetArrayLength()];
        Parallel.For(0, meshes.GetArrayLength(), i => {
            JsonElement m = meshes[i];
            string name = m.GetProperty("name").GetString();
            string type = m.GetProperty("type").GetString();

            var loader = Array.Find(KnownLoaders, l => l.Type == type);
            (meshSets[i], emitterSets[i]) = loader.LoadMesh(namedMaterials, emissiveMaterials, m,
                Path.GetDirectoryName(path));

            if (meshSets[i] != null) {
                var iter = meshSets[i].GetEnumerator();
                if (iter.MoveNext()) iter.Current.Name = name;
                int uniqueNum = 0;
                while (iter.MoveNext()) {
                    iter.Current.Name = name + $".{uniqueNum:000}";
                    uniqueNum++;
                }
            }

            lock (progressBar) progressBar.ReportDone(1);
        });

        foreach (var m in meshSets) if (m != null) resultScene.Meshes.AddRange(m);
        foreach (var e in emitterSets) if (e != null) resultScene.Emitters.AddRange(e);
    }

    private static void ReadMaterials(string path, JsonElement root, out Dictionary<string, Material> namedMaterials,
                                      out Dictionary<string, EmissionParameters> emissiveMaterials) {

        var namedMats = new Dictionary<string, Material>();
        var emissiveMats = new Dictionary<string, EmissionParameters>();
        if (root.TryGetProperty("materials", out var materials)) {
            ProgressBar progressBar = new(prefix: "Loading materials...");
            progressBar.Start(materials.GetArrayLength());

            Parallel.ForEach(materials.EnumerateArray(), m => {
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
                        BaseColor = JsonUtils.ReadColorOrTexture(m.GetProperty("baseColor"), path),
                        Transmitter = ReadOptionalBool("thin", false)
                    };
                    lock (namedMats) namedMats[name] = new DiffuseMaterial(parameters) { Name = name };
                } else {
                    // TODO check that there are no unsupported parameters
                    var parameters = new GenericMaterial.Parameters {
                        BaseColor = JsonUtils.ReadColorOrTexture(m.GetProperty("baseColor"), path),
                        Roughness = new TextureMono(ReadOptionalFloat("roughness", 0.5f)),
                        Anisotropic = ReadOptionalFloat("anisotropic", 0.0f),
                        IndexOfRefraction = ReadOptionalFloat("IOR", 1.01f),
                        Metallic = ReadOptionalFloat("metallic", 0.0f),
                        SpecularTintStrength = ReadOptionalFloat("specularTint", 0.0f),
                        SpecularTransmittance = ReadOptionalFloat("specularTransmittance", 0.0f),
                    };
                    lock (namedMats) namedMats[name] = new GenericMaterial(parameters) { Name = name };
                }

                // Check if the material is emissive
                if (m.TryGetProperty("emission", out elem)) {
                    RgbColor emission = JsonUtils.ReadRgbColor(elem);
                    if (emission != RgbColor.Black) {
                        bool isGlossy = ReadOptionalBool("emissionIsGlossy", false);
                        float exponent = ReadOptionalFloat("emissionExponent", 50);
                        lock (emissiveMats)
                            emissiveMats.Add(name, new() {
                                Radiance = emission,
                                IsGlossy = isGlossy,
                                Exponent = exponent
                            });
                    }
                }

                lock (progressBar) progressBar.ReportDone(1);
            });
        }
        namedMaterials = namedMats;
        emissiveMaterials = emissiveMats;
    }

    private static void ReadBackground(string path, Scene resultScene, JsonElement root) {
        if (root.TryGetProperty("background", out var backgroundElement)) {
            string type = backgroundElement.GetProperty("type").GetString();

            if (type == "image") {
                string filename = backgroundElement.GetProperty("filename").GetString();
                string dir = Path.GetDirectoryName(path);
                filename = Path.Join(dir, filename);

                resultScene.Background = new EnvironmentMap(new RgbImage(filename));
            }
        }
    }

    private static void ReadCameras(Scene resultScene, JsonElement root, Dictionary<string, Matrix4x4> namedTransforms) {
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
    }

    private static Dictionary<string, Matrix4x4> ReadNamedTransforms(JsonElement root) {
        var namedTransforms = new Dictionary<string, Matrix4x4>();
        var transforms = root.GetProperty("transforms");
        foreach (var t in transforms.EnumerateArray()) {
            string name = t.GetProperty("name").GetString();

            Matrix4x4 result = Matrix4x4.Identity;

            bool trs = false;
            if (t.TryGetProperty("scale", out var scale)) {
                var sc = JsonUtils.ReadVector(scale);
                result *= Matrix4x4.CreateScale(sc);
                trs = true;
            }

            if (t.TryGetProperty("rotation", out var rotation)) {
                var rot = JsonUtils.ReadVector(rotation);
                rot *= MathF.PI / 180.0f;
                result *= Matrix4x4.CreateFromYawPitchRoll(rot.Y, rot.X, rot.Z);
                trs = true;
            }

            if (t.TryGetProperty("position", out var position)) {
                var pos = JsonUtils.ReadVector(position);
                result *= Matrix4x4.CreateTranslation(pos);
                trs = true;
            }

            if (t.TryGetProperty("matrix", out var matrix)) {
                var mat = JsonUtils.ReadMatrix(matrix);
                result = mat;

                if (trs) Logger.Warning($"Matrix is replacing previous definitions of transform '{name}'");
            }

            namedTransforms[name] = result;
        }

        return namedTransforms;
    }

    /// <summary>
    /// Creates a scene from a .json
    /// </summary>
    /// <param name="path">Full path to the scene description file</param>
    /// <returns>The scene with all meshes, materials, textures, etc. loaded</returns>
    public static Scene LoadFromFile(string path) {
        // String parsing adheres to the OS specified culture settings.
        // However, we always want our .json files to use the decimal point . rather than a comma
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture =
            System.Globalization.CultureInfo.InvariantCulture;

        string jsonString = File.ReadAllText(path);

        var resultScene = new Scene();

        using (JsonDocument document = JsonDocument.Parse(jsonString, new JsonDocumentOptions {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        })) {
            var root = document.RootElement;

            var namedTransforms = ReadNamedTransforms(root);
            ReadCameras(resultScene, root, namedTransforms);
            ReadBackground(path, resultScene, root);
            ReadMaterials(path, root, out var namedMaterials, out var emissiveMaterials);
            ReadMeshes(path, resultScene, root, namedMaterials, emissiveMaterials);

            if (root.TryGetProperty("exposure", out var exposure))
                resultScene.RecommendedExposure = exposure.GetSingle();
        }

        return resultScene;
    }
}