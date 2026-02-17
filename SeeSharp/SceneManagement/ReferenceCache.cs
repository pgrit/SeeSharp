using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SeeSharp.SceneManagement;

/// <summary>
/// Renders, caches, and loads reference images for a scene
/// </summary>
public class ReferenceCache(DirectoryInfo dirname, SceneLoader sceneLoader)
{
    public DirectoryInfo ReferenceDirectory => new(Path.Join(dirname.FullName, "References"));

    string refJsonFilename => Path.Join(ReferenceDirectory.FullName, "Config.json");

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
        set
        {
            ReferenceDirectory.Create();
            File.WriteAllText(refJsonFilename, value.Serialize());
        }
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
        public readonly JsonNode Metadata { get; init; }

        public readonly Dictionary<string, Image> Layers { get; init; }
        public readonly RgbImage Image => Layers[""] as RgbImage;

        /// <summary>
        /// Total render time of this reference image as written by <see cref="FrameBuffer" />
        /// </summary>
        public long? RenderTime => Metadata["RenderTime"]?.GetValue<long>();

        /// <summary>
        /// Number of iterations (usually samples per pixel) used for this reference image, as written by <see cref="FrameBuffer" />
        /// </summary>
        public int? NumIterations => Metadata["NumIterations"]?.GetValue<int>();

        public override bool Equals([NotNullWhen(true)] object obj)
        {
            return File.Equals(obj);
        }

        public override int GetHashCode()
        {
            return File.GetHashCode();
        }

        public override string ToString()
        {
            return File.ToString();
        }

        public static bool operator ==(ReferenceInfo a, ReferenceInfo b) => a.File == b.File;

        public static bool operator !=(ReferenceInfo a, ReferenceInfo b) => a.File != b.File;
    }

    record struct Config(int Width, int Height, int MinDepth, int MaxDepth)
    {
        public bool Matches(ReferenceInfo r) =>
            r.Resolution.Width == Width
            && r.Resolution.Height == Height
            && r.MinDepth == MinDepth
            && r.MaxDepth == MaxDepth;

        public override string ToString()
        {
            string minDepthString = MinDepth > 1 ? $"MinDepth{MinDepth}-" : "";
            return $"{minDepthString}MaxDepth{MaxDepth}-Width{Width}-Height{Height}";
        }
    }

    string MakeBasename(Config cfg) => Path.Join(ReferenceDirectory.FullName, cfg.ToString());

    ReferenceInfo Load(Config cfg)
    {
        string basename = MakeBasename(cfg);
        string filename = basename + ".exr";
        string fnameJson = basename + ".json";

        string json = "{}";
        if (File.Exists(fnameJson))
            json = File.ReadAllText(fnameJson);

        var metaRoot = JsonNode.Parse(json);

        Integrator integrator = null;
        try
        {
            integrator = Integrator.Deserialize(metaRoot["Integrator"]);
        }
        catch
        {
            Logger.Warning($"Invalid integrator JSON in reference meta data for {cfg}");
        }

        return new()
        {
            File = new(filename),
            Resolution = (cfg.Width, cfg.Height),
            MinDepth = cfg.MinDepth,
            MaxDepth = cfg.MaxDepth,
            Integrator = integrator,
            Layers = Layers.LoadFromFile(filename),
            Metadata = metaRoot,
            SeeSharpVersion = metaRoot["SeeSharpVersion"]?.ToString(),
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

        scn.FrameBuffer.MetaData["Integrator"] = JsonNode.Parse(refIntegrator.Serialize());

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
            if (!ReferenceDirectory.Exists)
                yield break;

            foreach (var f in ReferenceDirectory.EnumerateFiles())
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
    public ReferenceInfo AddSamples(
        int numSamples,
        int width,
        int height,
        int maxDepth,
        int minDepth = 1
    )
    {
        Config cfg = new(width, height, minDepth, maxDepth);

        var old = Get(width, height, maxDepth, minDepth, true).Value;

        if (old.Integrator == null)
        {
            Logger.Error($"Cannot add more samples, unknown integrator used for '{old}'. Render a new one and/or combine manually.");
            return old;
        }

        var integrator = old.Integrator.Clone();
        integrator.NumIterations = (uint)numSamples;
        integrator.BaseSeed = BitConverter.ToUInt32(BitConverter.GetBytes(Random.Shared.Next()));

        integrator.MaxDepth = cfg.MaxDepth;
        integrator.MinDepth = cfg.MinDepth;

        // Output intermediate results exponentially to avoid loosing everything on a crash
        string basename = MakeBasename(cfg);
        string filename = basename + "-morespp.exr";

        var scn = sceneLoader.Scene;
        scn.FrameBuffer = new(
            cfg.Width,
            cfg.Height,
            filename,
            FrameBuffer.Flags.IgnoreNanAndInf
                | FrameBuffer.Flags.WriteContinously
                | FrameBuffer.Flags.WriteExponentially
        );

        scn.FrameBuffer.MetaData["Integrator"] = JsonNode.Parse(integrator.Serialize());

        scn.Prepare();
        integrator.Render(scn);

        // Write to file so we get a metadata .json with the right sample count
        scn.FrameBuffer.MetaData["NumIterations"] = old.NumIterations + numSamples;
        scn.FrameBuffer.WriteToFile(basename + ".exr");

        // Combine the images weigthed by their sample count
        // (for main render, everything else uses initial run only)
        float wNew = numSamples / (float)(old.NumIterations + numSamples);
        var img = wNew * scn.FrameBuffer.Image + (1 - wNew) * old.Image;
        old.Layers[""] = img;
        Layers.WriteToExr(basename + ".exr", old.Layers);

        // Delete the intermediate output
        try
        {
            File.Delete(filename);
            File.Delete(Path.ChangeExtension(filename, ".json"));
        }
        catch
        {
            Logger.Warning(
                $"Intermediate output from reference rendering could not be deleted. Try deleting the files manually ('{filename}' and '.json')"
            );
        }

        return Get(width, height, maxDepth, minDepth, true).Value;
    }
}