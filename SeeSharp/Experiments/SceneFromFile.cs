using SimpleImageIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

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
    /// Creates a shallow copy of this scene configuration under a new name.
    /// </summary>
    /// <param name="newName">The new name to use</param>
    /// <returns>Shallow copy with new name</returns>
    public SceneFromFile WithName(string newName) {
        var copy = (SceneFromFile)MemberwiseClone();
        copy.name = newName;
        return copy;
    }

    static readonly Dictionary<string, Type> IntegratorNames = new() {
        { "PathTracer", typeof(Integrators.PathTracer) },
        { "VCM", typeof(Integrators.Bidir.VertexConnectionAndMerging) },
        { "ClassicBidir", typeof(Integrators.Bidir.ClassicBidir) },
    };

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
            return new RgbImage(filename);
        }

        // TODO we could make this even more fancy by triggering a re-render if the hash of the config
        //      file has changed since the last render (which can be embedded in the filename)
        //      Need to be careful about line-endings in git, though.

        string referenceSpecs = Path.Join(refDir, "Config.json");
        Integrators.Integrator refIntegrator = null;
        if (File.Exists(referenceSpecs)) {
            string json = File.ReadAllText(referenceSpecs);

            using (JsonDocument doc = JsonDocument.Parse(json)) {
                string name = doc.RootElement.GetProperty("Name").GetString();
                string settings = doc.RootElement.GetProperty("Settings").GetRawText();

                Type integratorType = IntegratorNames[name];
                refIntegrator = JsonSerializer.Deserialize(settings, integratorType,
                    new JsonSerializerOptions() {
                        IncludeFields = true,
                    }) as Integrators.Integrator;
            }

            Common.Logger.Log("Rendering reference based on Config.json");
        } else {
            refIntegrator = DefaultReferenceIntegrator;

            string settingsJson = JsonSerializer.Serialize(refIntegrator, refIntegrator.GetType(),
                new JsonSerializerOptions() {
                    IncludeFields = true,
                    WriteIndented = true
                });
            string name = IntegratorNames.FirstOrDefault(kv => kv.Value == refIntegrator.GetType()).Key;
            string json = "{ \"Name\": \"" + name + "\", \"Settings\": " + settingsJson + "}";
            File.WriteAllText(referenceSpecs, json);

            Common.Logger.Log("Rendering reference with default integrator");
        }

        refIntegrator.MaxDepth = MaxDepth;
        refIntegrator.MinDepth = MinDepth;

        using Scene scn = MakeScene();
        scn.FrameBuffer = new(width, height, filename);
        scn.Prepare();
        refIntegrator.Render(scn);
        scn.FrameBuffer.WriteToFile();

        return scn.FrameBuffer.Image;
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
    }

    /// <summary>
    /// The default integrator used when rendering reference images, if no config.json file is present.
    /// </summary>
    public virtual Integrators.Integrator DefaultReferenceIntegrator
    => new Integrators.PathTracer() {
        BaseSeed = 571298512u,
        TotalSpp = 512
    };

    /// <summary>
    /// Creates a scene ready for rendering
    /// </summary>
    /// <returns>A shallow copy of the "blueprint" scene</returns>
    public override Scene MakeScene() => scene.Copy();
}