using System.Text.Json.Nodes;

namespace SeeSharp.Experiments;

/// <summary>
/// Represents a scene loaded from a directory in the <see cref="SceneRegistry" />.
/// </summary>
public class SceneFromFile : SceneConfig {
    readonly FileInfo file;
    readonly Scene scene;
    string name;

    /// <inheritdoc />
    public override int MaxDepth { get; set; }

    /// <inheritdoc />
    public override int MinDepth { get; set; }

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

    public static T InpaintNaNs<T>(T image) where T : Image {
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
    /// <param name="allowRender">If false, missing references are not rendered and null is returned instead</param>
    /// <param name="config">Use integrator parameters if provided, otherwise read them from the JSON file</param>
    /// <returns>The reference image</returns>
    public override RgbImage GetReferenceImage(int width, int height, bool allowRender = true, Integrator config = null) {
        string refDir = Path.Join(file.DirectoryName, "References");
        Directory.CreateDirectory(refDir);

        string minDepthString = MinDepth > 1 ? $"MinDepth{MinDepth}-" : "";
        string filename = Path.Join(refDir, $"{minDepthString}MaxDepth{MaxDepth}-Width{width}-Height{height}.exr");

        if (File.Exists(filename)) {
            // support legacy .exr files
            var layers = Layers.LoadFromFile(filename);
            if (layers.TryGetValue("", out Image img)) return InpaintNaNs(img) as RgbImage;
            else return InpaintNaNs(layers["default"]) as RgbImage;

            // TODO read the SeeSharp version from the .json and print a warning if it does not match (major/minor only, not patch)
        }

        if (!allowRender) {
            Logger.Warning($"No reference available for {width} x {height}, {MinDepth} - {MaxDepth}");
            return null;
        }

        string referenceSpecs = Path.Join(refDir, "Config.json");
        Integrator refIntegrator = DefaultReferenceIntegrator;
        if (config != null) {
            refIntegrator = config;
            goto IntegratorLoaded;
        }

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
            "Name": "{{refIntegrator.GetType().Name}}",
            "Settings": {{JsonSerializer.Serialize(refIntegrator, refIntegrator.GetType(), refSerializerOptions)}}
        }
        """);

        refIntegrator.MaxDepth = MaxDepth;
        refIntegrator.MinDepth = MinDepth;

        string partialFilename = Path.Join(refDir, $"{minDepthString}MaxDepth{MaxDepth}-Width{width}-Height{height}-partial.exr");
        if (File.Exists(partialFilename)) File.Delete(partialFilename);

        using Scene scn = MakeScene();
        scn.FrameBuffer = new(width, height, partialFilename,
            FrameBuffer.Flags.IgnoreNanAndInf |
            FrameBuffer.Flags.WriteContinously |
            FrameBuffer.Flags.WriteExponentially); // output intermediate results exponentially to avoid loosing everything on a crash
        
        // Write settings to metadata for preview parameters in the info table
        scn.FrameBuffer.MetaData["Name"] = refIntegrator.GetType().Name;
        scn.FrameBuffer.MetaData["Settings"] =  JsonSerializer.SerializeToNode(refIntegrator, refIntegrator.GetType(), refSerializerOptions);
        
        scn.Prepare();
        refIntegrator.Render(scn);
        scn.FrameBuffer.WriteToFile();

        if (File.Exists(filename)) File.Delete(filename);
        File.Move(partialFilename, filename);

        string pJson = Path.ChangeExtension(partialFilename, ".json");
        string fJson = Path.ChangeExtension(filename, ".json");
        if (File.Exists(fJson)) File.Delete(fJson);
        if (File.Exists(pJson)) File.Move(pJson, fJson);
        
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
        MaxDepth = maxDepth;
        MinDepth = minDepth;
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