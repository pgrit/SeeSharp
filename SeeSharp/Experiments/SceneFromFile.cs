using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SeeSharp.Experiments;

/// <summary>
/// Represents a scene loaded from a directory in the <see cref="SceneRegistry" />.
/// </summary>
public class SceneFromFile : SceneConfig
{
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
    public SceneFromFile WithName(string newName)
    {
        var copy = (SceneFromFile)MemberwiseClone();
        copy.name = newName;
        return copy;
    }

    static readonly Dictionary<string, Type> LegacyIntegratorNames = new()
    {
        { "PathTracer", typeof(PathTracer) },
        { "VCM", typeof(VertexConnectionAndMerging) },
        { "ClassicBidir", typeof(ClassicBidir) },
    };

    static readonly JsonSerializerOptions refSerializerOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
    };

    public static T InpaintNaNs<T>(T image)
        where T : Image
    {
        bool hadAny = false;
        for (int row = 0; row < image.Height; ++row)
        {
            for (int col = 0; col < image.Width; ++col)
            {
                for (int chan = 0; chan < image.NumChannels; ++chan)
                {
                    if (!float.IsFinite(image[col, row, chan]))
                    {
                        float total = 0;
                        int num = 0;
                        void TryAdd(int c, int r)
                        {
                            if (
                                c >= 0
                                && r >= 0
                                && c < image.Width
                                && r < image.Height
                                && float.IsFinite(image[c, r, chan])
                            )
                            {
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
            Logger.Warning(
                "Removed NaN / Inf from reference image (check the reference's .json in the scene directory for details)"
            );
        return image;
    }

    /// <inheritdoc />
    public override RgbImage GetReferenceImage(
        int width,
        int height,
        bool allowRender = true,
        bool forceRender = false
    )
    {
        var (layers, _) = GetReferenceImageDetails(width, height, allowRender, forceRender);
        return InpaintNaNs(layers[""] as RgbImage);
    }

    /// <summary>
    /// Loads a new scene from file
    /// </summary>
    /// <param name="filename">Path to an existing scene's json file</param>
    /// <param name="minDepth">Minimum path length to use when rendering</param>
    /// <param name="maxDepth">Maximum path length to use when rendering</param>
    /// <param name="name">If a name different from the file basename is desired, specify it here.</param>
    public SceneFromFile(string filename, int minDepth, int maxDepth, string name = null)
    {
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
    public static Integrator DefaultReferenceIntegrator =>
        new PathTracer() { BaseSeed = 571298512u, NumIterations = 512 };

    public override string ReferenceLocation => Path.Join(file.DirectoryName, "References");

    string refJsonFilename => Path.Join(ReferenceLocation, "Config.json");

    public override Integrator ReferenceIntegrator
    {
        get
        {
            if (!File.Exists(refJsonFilename))
                return DefaultReferenceIntegrator;

            var json = JsonNode.Parse(File.ReadAllText(refJsonFilename));

#pragma warning disable CA1507 // Complains about "Name" as it happens to coincide with an unrelated property
            string name = (string)json["Name"];
#pragma warning restore CA1507

            if (LegacyIntegratorNames.TryGetValue(name, out Type type))
            {
                Logger.Warning(
                    $"Scene reference specs are using an old convention, {refJsonFilename} will be updated."
                );
                Integrator integrator =
                    json["Settings"].Deserialize(type, refSerializerOptions) as Integrator;
                return ReferenceIntegrator = integrator;
            }

            return DeserializeIntegrator(json, name);
        }
        set
        {
            File.WriteAllText(
                refJsonFilename,
                $$"""
                {
                    "Name": "{{value.GetType().Name}}",
                    "Settings": {{JsonSerializer.Serialize(
                        value,
                        value.GetType(),
                        refSerializerOptions
                    )}}
                }
                """
            );
        }
    }

    /// <summary>
    /// Deserializes an integrator from json
    /// </summary>
    public static Integrator DeserializeIntegrator(JsonNode json, string name)
    {
        Type integratorType = null;
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            integratorType = a.GetType(name);
            if (integratorType != null)
                break;
        }
        if (integratorType == null)
        {
            Logger.Error($"No such integrator: {name}");
            return DefaultReferenceIntegrator;
        }
        if (!integratorType.IsAssignableTo(typeof(Integrator)))
        {
            Logger.Error(
                $"The integrator '{name}' was found, but is not a class derived from {nameof(Integrator)}"
            );
            return DefaultReferenceIntegrator;
        }

        return json["Settings"].Deserialize(integratorType, refSerializerOptions) as Integrator;
    }

    /// <summary>
    /// Creates a scene ready for rendering
    /// </summary>
    /// <returns>A shallow copy of the "blueprint" scene</returns>
    public override Scene MakeScene() => scene.Copy();

    public override (
        Dictionary<string, Image> Layers,
        string JsonMetadata
    ) GetReferenceImageDetails(
        int width,
        int height,
        bool allowRender = true,
        bool forceRender = false
    )
    {
        Directory.CreateDirectory(ReferenceLocation);

        string minDepthString = MinDepth > 1 ? $"MinDepth{MinDepth}-" : "";
        string filename = Path.Join(
            ReferenceLocation,
            $"{minDepthString}MaxDepth{MaxDepth}-Width{width}-Height{height}.exr"
        );
        string fnameJson = Path.ChangeExtension(filename, ".json");

        if (File.Exists(filename) && !forceRender)
        {
            string json = null;
            if (File.Exists(fnameJson))
                json = File.ReadAllText(fnameJson);

            var layers = Layers.LoadFromFile(filename);
            if (layers.TryGetValue("", out Image img))
                return (layers, json);
            else
            {
                // support legacy .exr files
                layers[""] = layers["default"];
                layers.Remove("default");
                return (layers, json);
            }
        }

        if (!allowRender && !forceRender)
        {
            Logger.Warning(
                $"No reference available for {width} x {height}, {MinDepth} - {MaxDepth}"
            );
            return (null, null);
        }

        var refIntegrator = ReferenceIntegrator;

        Logger.Log($"Rendering reference with {refIntegrator.GetType().Name}");

        refIntegrator.MaxDepth = MaxDepth;
        refIntegrator.MinDepth = MinDepth;

        // Output intermediate results exponentially to avoid loosing everything on a crash
        string partialFilename = Path.Join(
            ReferenceLocation,
            $"{minDepthString}MaxDepth{MaxDepth}-Width{width}-Height{height}-partial.exr"
        );
        if (File.Exists(partialFilename))
            File.Delete(partialFilename);

        using Scene scn = MakeScene();
        scn.FrameBuffer = new(
            width,
            height,
            partialFilename,
            FrameBuffer.Flags.IgnoreNanAndInf
                | FrameBuffer.Flags.WriteContinously
                | FrameBuffer.Flags.WriteExponentially
        );

        // Include integrator config in the metadata. This preserves the render
        // settings of each individual image if the current config changes.
        scn.FrameBuffer.MetaData["Integrator"] = refIntegrator.GetType().Name;
        scn.FrameBuffer.MetaData["Settings"] = JsonSerializer.SerializeToNode(
            refIntegrator,
            refIntegrator.GetType(),
            refSerializerOptions
        );

        scn.Prepare();
        refIntegrator.Render(scn);
        scn.FrameBuffer.WriteToFile();

        // We finished without crashing, let's rename the rendered files
        File.Move(partialFilename, filename, true);
        File.Move(Path.ChangeExtension(partialFilename, ".json"), fnameJson, true);

        return (Layers.LoadFromFile(filename), File.ReadAllText(fnameJson));
    }

    public override IEnumerable<(
        int Width,
        int Height,
        int MinDepth,
        int MaxDepth,
        string Filename
    )> AvailableReferences
    {
        get
        {
            DirectoryInfo dir = new(ReferenceLocation);
            List<(int Width, int Height, int MinDepth, int MaxDepth, string Filename)> files = [];

            foreach (var f in dir.EnumerateFiles())
            {
                var m = Regex.Match(
                    f.Name,
                    @"MinDepth(\d+)-MaxDepth(\d+)-Width(\d+)-Height(\d+).exr"
                );
                if (m.Success)
                {
                    int w = int.Parse(m.Groups[3].Value);
                    int h = int.Parse(m.Groups[4].Value);
                    int min = int.Parse(m.Groups[1].Value);
                    int max = int.Parse(m.Groups[2].Value);
                    files.Add((w, h, min, max, f.FullName));
                    continue;
                }

                m = Regex.Match(f.Name, @"MaxDepth(\d+)-Width(\d+)-Height(\d+).exr");
                if (m.Success)
                {
                    int w = int.Parse(m.Groups[2].Value);
                    int h = int.Parse(m.Groups[3].Value);
                    int max = int.Parse(m.Groups[1].Value);
                    files.Add((w, h, 1, max, f.FullName));
                    continue;
                }
            }
            return files;
        }
    }
}