using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SeeSharp.Experiments;

/// <summary>
/// Renders, caches, and loads reference images for a scene
/// </summary>
public class ReferenceCache(DirectoryInfo dirname, SceneLoader sceneLoader)
{
    public DirectoryInfo ReferenceDirname => new(Path.Join(dirname.FullName, "References"));

    string refJsonFilename => Path.Join(ReferenceDirname.FullName, "Config.json");

    static readonly JsonSerializerOptions refSerializerOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
    };

    static Integrator DefaultReferenceIntegrator =>
        new PathTracer() { BaseSeed = 571298512u, NumIterations = 512 };

    /// <summary>
    /// The integrator used to render reference images for this scene.
    /// </summary>
    public Integrator ReferenceIntegrator
    {
        get
        {
            if (!File.Exists(refJsonFilename))
                return DefaultReferenceIntegrator;
            return Integrator.Deserialize(File.ReadAllText(refJsonFilename))
                ?? DefaultReferenceIntegrator;
        }
        set { File.WriteAllText(refJsonFilename, value.Serialize()); }
    }

    /// <summary>
    /// Metadata of a reference image
    /// </summary>
    public readonly struct ReferenceInfo
    {
        public readonly FileInfo File { get; init; }

        public readonly (int Width, int Height) Resolution { get; init; }
        public readonly int MinDepth { get; init; }
        public readonly int MaxDepth { get; init; }

        /// <summary>
        /// The integrator that rendered this image.
        /// Can be null, if the reference was rendered by a method not
        /// present in the current assembly.
        /// </summary>
        public readonly Integrator Integrator { get; init; }

        /// <summary>
        /// The SeeSharp version used to render this image
        /// </summary>
        public readonly string SeeSharpVersion { get; init; }

        /// <summary>
        /// All metadata that the <see cref="FrameBuffer.WriteToFile(string)" /> method
        /// stored in the accompanying .json file.
        /// </summary>
        public readonly Dictionary<string, JsonNode> Metadata { get; init; }

        public readonly Dictionary<string, Image> Layers { get; init; }
        public readonly RgbImage Image => Layers[""] as RgbImage;
    }

    record struct Config(int Width, int Height, int MinDepth, int MaxDepth)
    {
        public bool Matches(ReferenceInfo r) =>
            r.Resolution.Width == Width
            && r.Resolution.Height == Height
            && r.MinDepth == MinDepth
            && r.MaxDepth == MaxDepth;
    }

    string MakeBasename(Config cfg)
    {
        string minDepthString = cfg.MinDepth > 1 ? $"MinDepth{cfg.MinDepth}-" : "";
        return Path.Join(
            ReferenceDirname.FullName,
            $"{minDepthString}MaxDepth{cfg.MaxDepth}-Width{cfg.Width}-Height{cfg.Height}"
        );
    }

    ReferenceInfo Load(Config cfg)
    {
        string basename = MakeBasename(cfg);
        string filename = basename + ".exr";
        string fnameJson = basename + ".json";

        string json = "{}";
        if (File.Exists(fnameJson))
            json = File.ReadAllText(fnameJson);

        // TODO load metadata, extract integrator and version

        return new()
        {
            File = new(filename),
            Resolution = (cfg.Width, cfg.Height),
            MinDepth = cfg.MinDepth,
            MaxDepth = cfg.MaxDepth,
            Integrator = null, // TODO read from json
            Layers = Layers.LoadFromFile(filename),
            Metadata = null, // TODO copy
            SeeSharpVersion = null, // TODO read from json
        };
    }

    void Render(Config cfg)
    {
        var refIntegrator = ReferenceIntegrator;

        Logger.Log($"Rendering reference with {refIntegrator.GetType().Name}");

        refIntegrator.MaxDepth = cfg.MaxDepth;
        refIntegrator.MinDepth = cfg.MinDepth;

        // Output intermediate results exponentially to avoid loosing everything on a crash
        string basename = MakeBasename(cfg);
        string filename = basename + ".exr";

        var scn = sceneLoader.Scene;
        scn.FrameBuffer = new(
            cfg.Width,
            cfg.Height,
            filename,
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
    }

    /// <summary>
    /// Queries all available reference image configurations for this scene
    /// </summary>
    public IEnumerable<ReferenceInfo> AvailableReferences
    {
        get
        {
            foreach (var f in ReferenceDirname.EnumerateFiles())
            {
                var m = Regex.Match(f.Name, @"MaxDepth(\d+)-Width(\d+)-Height(\d+).exr$");
                if (!m.Success)
                    continue;

                int w = int.Parse(m.Groups[2].Value);
                int h = int.Parse(m.Groups[3].Value);
                int maxDepth = int.Parse(m.Groups[1].Value);

                m = Regex.Match(f.Name, @"MinDepth(\d+)-");
                int minDepth = m.Success ? int.Parse(m.Groups[1].Value) : 1;

                Config cfg = new(w, h, minDepth, maxDepth);
                yield return Load(cfg);
            }
        }
    }

    /// <summary>
    /// Retrieves a matching cached reference, or renders a new one (unless allowRender is false)
    /// </summary>
    public ReferenceInfo? Get(
        int width,
        int height,
        int maxDepth,
        int minDepth = 1,
        bool allowRender = true
    )
    {
        Config cfg = new(width, height, minDepth, maxDepth);

        var matching = AvailableReferences.Where(cfg.Matches);
        if (matching.Any())
            return matching.First();

        if (!allowRender)
            return null;

        Render(cfg);

        return AvailableReferences.Where(cfg.Matches).First();
    }

    /// <summary>
    /// Overwrites any existing reference image with a newly rendered version
    /// </summary>
    public ReferenceInfo ReRender(int width, int height, int maxDepth, int minDepth = 1)
    {
        Config cfg = new(width, height, minDepth, maxDepth);

        var matching = AvailableReferences.Where(cfg.Matches);
        if (matching.Any())
            matching.First().File.Delete();

        return Get(width, height, maxDepth, minDepth, true).Value;
    }

    /// <summary>
    /// Renders a new reference with numSamples iterations and combines it with the
    /// existing image (weighting based on respective sample counts)
    /// </summary>
    public ReferenceInfo AddSamples(int numSamples, int width, int height, int maxDepth, int minDepth = 1)
    {
        // TODO check it exists, if not render new

        // TODO Read integrator from .json of last render

        // TODO set the numiterations
        // set the seed to a new random value

        // TODO render

        // TODO combine images weighted by their relative sample counts

        // TODO update metadata: replace num iterations

        return Get(width, height, maxDepth, minDepth, true).Value;
    }
}
