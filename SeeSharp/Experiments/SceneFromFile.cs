using System.Linq;
using System.Text.Json.Nodes;

namespace SeeSharp.Experiments;

/// <summary>
/// Represents a scene loaded from a directory in the <see cref="SceneRegistry" />.
/// </summary>
public class SceneFromFile : SceneConfig {
    readonly FileInfo file;
    readonly int maxDepth;
    readonly int minDepth;
    readonly Scene scene;
    string name;

    /// <inheritdoc />
    public override int MaxDepth => maxDepth;

    /// <inheritdoc />
    public override int MinDepth => minDepth;

    /// <inheritdoc />
    public override string Name => name;

    /// <summary>
    /// Absolute path to the directory that this scene was loaded from
    /// </summary>
    public string SourceDirectory => Path.GetFullPath(file.DirectoryName);

    /// <summary>
    /// Creates a shallow copy of this scene configuration under a new name.
    /// </summary>
    /// <param name="newName">The new name to use</param>
    /// <returns>Shallow copy with new name</returns>
    public SceneFromFile WithName(string newName) {
        var copy = (SceneFromFile)MemberwiseClone();
        copy.name = newName;
        return copy;
    }

    static readonly Dictionary<string, Type> LegacyIntegratorNames = new() {
        { "PathTracer", typeof(PathTracer) },
        { "VCM", typeof(VertexConnectionAndMerging) },
        { "ClassicBidir", typeof(ClassicBidir) },
    };

    static readonly JsonSerializerOptions refSerializerOptions = new() {
        IncludeFields = true,
        WriteIndented = true
    };

    static T InpaintNaNs<T>(T image) where T : Image {
        bool hadAny = false;
        for (int row = 0; row < image.Height; ++row) {
            for (int col = 0; col < image.Width; ++col) {
                for (int chan = 0; chan < image.NumChannels; ++chan) {
                    if (!float.IsFinite(image[col, row, chan])) {
                        float total = 0;
                        int num = 0;
                        void TryAdd(int c, int r) {
                            if (c >= 0 && r >= 0 && c < image.Width && r < image.Height && float.IsFinite(image[c, r, chan])) {
                                total += image[c, r, chan];
                                num++;
                            }
                        }
                        TryAdd(col - 1, row);
                        TryAdd(col + 1, row);
                        TryAdd(col, row - 1);
                        TryAdd(col, row + 1);
                        image[col, row, chan] = total / num;

                        hadAny = true;
                    }
                }
            }
        }
        if (hadAny)
            Logger.Warning("Removed NaN / Inf from reference image (check the reference's .json in the scene directory for details)");
        return image;
    }

    /// <summary>
    /// Retrieves a cached reference image with the right resolution and maximum path length. If not
    /// available, a new reference is rendered and added to the cache.
    ///
    /// The reference cache is a directory called "References" next to the .json file that defines
    /// the scene.
    /// </summary>
    /// <param name="width">Width in pixels</param>
    /// <param name="height">Height in pixels</param>
    /// <returns>The reference image</returns>
    public override RgbImage GetReferenceImage(int width, int height) {
        string refDir = Path.Join(file.DirectoryName, "References");
        Directory.CreateDirectory(refDir);

        string minDepthString = MinDepth > 1 ? $"MinDepth{MinDepth}-" : "";
        string filename = Path.Join(refDir, $"{minDepthString}MaxDepth{MaxDepth}-Width{width}-Height{height}.exr");

        if (File.Exists(filename)) {
            // support legacy .exr files
            var layers = Layers.LoadFromFile(filename);
            if (layers.TryGetValue("", out Image img)) return InpaintNaNs(img) as RgbImage;
            else return InpaintNaNs(layers["default"]) as RgbImage;
        }

        string referenceSpecs = Path.Join(refDir, "Config.json");
        Integrator refIntegrator = DefaultReferenceIntegrator;
        if (File.Exists(referenceSpecs)) {
            var json = JsonNode.Parse(File.ReadAllText(referenceSpecs));

#pragma warning disable CA1507 // Bitches about "Name" as it happens to coincide with an unrelated property
            string name = (string)json["Name"];
#pragma warning restore CA1507

            if (LegacyIntegratorNames.TryGetValue(name, out Type type)) {
                Logger.Warning($"Scene reference specs are using an old convention, {referenceSpecs} will be updated.");
                refIntegrator = json["Settings"].Deserialize(type, refSerializerOptions) as Integrator;
                goto IntegratorLoaded;
            }

            // Find the integrator that is specified in the file
            Type integratorType = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) {
                integratorType = a.GetType(name);
                if (integratorType != null)
                    break;
            }
            if (integratorType == null) {
                Logger.Error($"No such integrator: {name}");
                goto IntegratorLoaded;
            }
            if (!integratorType.IsAssignableTo(typeof(Integrator))) {
                Logger.Error($"The integrator '{name}' was found, but is not a class derived from {nameof(Integrator)}");
                goto IntegratorLoaded;
            }

            refIntegrator = json["Settings"].Deserialize(integratorType, refSerializerOptions) as Integrator;

            Logger.Log("Rendering reference based on Config.json");
        }

IntegratorLoaded:
        Logger.Log($"Rendering reference with {refIntegrator.GetType().Name}");

        // Overwrite the reference specs in case they got updated (e.g., migrated to new format)
        File.WriteAllText(referenceSpecs, $$"""
        {
            "Name": "{{refIntegrator.GetType().FullName}}",
            "Settings": {{JsonSerializer.Serialize(refIntegrator, refIntegrator.GetType(), refSerializerOptions)}}
        }
        """);

        refIntegrator.MaxDepth = MaxDepth;
        refIntegrator.MinDepth = MinDepth;

        using Scene scn = MakeScene();
        scn.FrameBuffer = new(width, height, filename);
        scn.Prepare();
        refIntegrator.Render(scn);
        scn.FrameBuffer.WriteToFile();

        return InpaintNaNs(scn.FrameBuffer.Image);
    }

    /// <summary>
    /// Loads a new scene from file
    /// </summary>
    /// <param name="filename">Path to an existing scene's json file</param>
    /// <param name="minDepth">Minimum path length to use when rendering</param>
    /// <param name="maxDepth">Maximum path length to use when rendering</param>
    /// <param name="name">If a name different from the file basename is desired, specify it here.</param>
    public SceneFromFile(string filename, int minDepth, int maxDepth, string name = null) {
        file = new(filename);
        scene = Scene.LoadFromFile(filename);
        this.maxDepth = maxDepth;
        this.minDepth = minDepth;
        this.name = name ?? Path.GetFileNameWithoutExtension(filename);
        scene.Name = this.name;
    }

    /// <summary>
    /// The default integrator used when rendering reference images, if no config.json file is present.
    /// </summary>
    public virtual Integrator DefaultReferenceIntegrator
    => new PathTracer() {
        BaseSeed = 571298512u,
        TotalSpp = 512
    };

    /// <summary>
    /// Creates a scene ready for rendering
    /// </summary>
    /// <returns>A shallow copy of the "blueprint" scene</returns>
    public override Scene MakeScene() => scene.Copy();
}