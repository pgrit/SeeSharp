using System.Linq;
using System.Text.Json.Nodes;

namespace SeeSharp.Experiments;

/// <summary>
/// Describes an experiment with a list of named integrators.
/// </summary>
public abstract class Experiment {
    /// <summary>
    /// A "method" is a named integrator with specific parameters
    /// </summary>
    public readonly struct Method {
        /// <summary>
        /// Name of the method. Determines file and directory names.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// The integrator object to run.
        /// </summary>
        public readonly Integrator Integrator;

        /// <summary>
        /// Creates a new method
        /// </summary>
        /// <param name="name">Name of the method. Determines file and directory names.</param>
        /// <param name="integrator">The integrator object to run, with the desired parameters set</param>
        public Method(string name, Integrator integrator) {
            Name = name;
            Integrator = integrator;
        }
    }

    /// <summary>
    /// If true, the Benchmark will drop the method reference after execution. This allows costly integrator
    /// data to be freed as soon as possible. Disadvantage: all integrator state will be lost. The default is false.
    /// </summary>
    public virtual bool DeleteMethodAfterRun => false;

    /// <summary>
    /// Factory function for the methods.
    /// </summary>
    /// <returns>A list of all methods that should be run in a benchmark</returns>
    public abstract List<Method> MakeMethods();

    public IEnumerable<string> MethodNames => MakeMethods().Select(m => m.Name);

    /// <summary>
    /// Called before the experiment is run on a test scene.
    /// </summary>
    /// <param name="scene">The scene that will be rendered</param>
    /// <param name="dir">Output directory</param>
    /// <param name="minDepth">Minimum path length during rendering</param>
    /// <param name="maxDepth">Maximum path length during rendering</param>
    public virtual void OnStartScene(Scene scene, string dir, int minDepth, int maxDepth) { }

    /// <summary>
    /// Called after all methods have been run on a test scene. The default implementation gathers all
    /// rendered images, computes error images if a reference is available, and generates a static .html
    /// overview page.
    /// </summary>
    /// <param name="scene">The scene that was rendered</param>
    /// <param name="dir">
    /// Output directory, each method is in a subdirectory; the method's name is the name of that subdirectory
    /// </param>
    /// <param name="minDepth">Minimum path length during rendering</param>
    /// <param name="maxDepth">Maximum path length during rendering</param>
    public virtual void OnDoneScene(Scene scene, string dir, int minDepth, int maxDepth) {
        var stopwatch = Stopwatch.StartNew();

        string refPath = $"{dir}/Reference.exr";
        RgbImage reference = File.Exists(refPath) ? new(refPath) : null;

        // Create a flip viewer with all the rendered images and the reference (if available)
        var flip = FlipBook.New.SetZoom(FlipBook.InitialZoom.Fit).SetToneMapper(FlipBook.InitialTMO.Exposure(scene.RecommendedExposure));
        float maxError = 0.0f;
        List<float> errors = [];
        List<(string, Image)> errorImages = [];
        List<(string, Image)> squaredErrorImages = [];
        foreach (string method in MethodNames) {
            RgbImage img = new($"{dir}/{method}.exr");
            flip.Add(method, img, FlipBook.DataType.Float16);

            if (reference != null) {
                errorImages.Add(("error " + method, new MonochromeImage(img - reference)));

                var squaredError = Metrics.RelMSEImage(img, reference);
                maxError = Math.Max(maxError, new Histogram(squaredError).Quantile(0.9f));
                errors.Add(Metrics.RelMSE_OutlierRejection(img, reference));
                squaredErrorImages.Add(("relSE " + method, squaredError));
            }
        }
        if (reference != null) {
            flip.Add("Reference", reference);

            // Ensure valid .json if the error is NaN or Inf
            if (!float.IsFinite(maxError))
                maxError = 1.0f;

            flip.AddAll(squaredErrorImages, FlipBook.DataType.Float16, FlipBook.InitialTMO.FalseColor(0.0f, maxError));
            flip.AddAll(errorImages, FlipBook.DataType.Float16, FlipBook.InitialTMO.GLSL("""
                float avg = 100.0 * (rgb.x + rgb.y + rgb.z) / 3.0;
                if (avg < 0.0)
                    rgb = -avg * vec3(1.0, 0.0, 0.0);
                else
                    rgb = avg * vec3(0.0, 1.0, 0.0);
                """));
        }

        // Read the render times and other stats of all methods
        List<string[]> tableRows = [[
            "Method", "relMSE", "Time (ms)", "inefficiency (relMSE * time)", "speed-up", "# iterations",
            "# rays", "# shadow rays",
            "# BSDF eval", "# BSDF sample", "# BSDF pdf"
        ]];
        int idx = 0;
        float? baseline = null;
        foreach (string method in MethodNames) {
            var json = JsonNode.Parse(File.ReadAllText($"{dir}/{method}.json"));
            var numIter = (ulong)json["NumIterations"];
            var renderTimeMs = (ulong)json["RenderTime"];
            var rayStats = json["RayStats"].Deserialize<RayTracerStats>();
            var shadeStats = json["ShadeStats"].Deserialize<ShadingStats>();
            float error = errors.ElementAtOrDefault(idx);

            float inefficiency = error * renderTimeMs / 1000.0f;
            if (!baseline.HasValue)
                baseline = inefficiency;

            tableRows.Add([
                method, $"{error:G3}", $"{renderTimeMs}", $"{inefficiency:G3}", $"{baseline / inefficiency:P0}", $"{numIter}",
                $"{rayStats.NumRays:N0}", $"{rayStats.NumShadowRays:N0}",
                $"{shadeStats.NumMaterialEval:N0}", $"{shadeStats.NumMaterialSample:N0}", $"{shadeStats.NumMaterialPdf:N0}",
            ]);

            idx++;
        }

        // Assemble html code
        flip.SetToolVisibility(false);
        string htmlBody = $"""<div style="display: flex;">{flip.Resize(900,800)}""";
        htmlBody += "</div>";
        htmlBody += "<h3>Statistics</h3>";
        htmlBody += HtmlUtil.MakeTable(tableRows, true);

        string tableStyle = """
        <style>
            table {
                border-collapse: collapse;
            }
            td, th {
                border: none;
                padding: 4px;
            }
            tr:hover { background-color: #e7f2f1; }
            th {
                padding-top: 6px;
                padding-bottom: 6px;
                text-align: left;
                background-color: #4a96af;
                color: white;
                font-size: smaller;
            }
        </style>
        """;

        var html = HtmlUtil.MakeHTML(FlipBook.Header + tableStyle, htmlBody);

        File.WriteAllText($"{dir}/{scene.Name}.html", html);

        Logger.Log($"Assembling {dir}/{scene.Name}.html took {stopwatch.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Called before the experiment is run on a set of scenes
    /// </summary>
    /// <param name="workingDirectory">Output directory</param>
    public virtual void OnStart(string workingDirectory) { }

    /// <summary>
    /// Called after the experiment run has finished for all scenes
    /// </summary>
    /// <param name="workingDirectory">Output directory</param>
    /// <param name="sceneNames">Names of all scenes that have been rendered (each is one directory in the working dir)</param>
    /// <param name="sceneExposures">Recommended LDR exposure levels for each rendered scene</param>
    public virtual void OnDone(string workingDirectory, IEnumerable<string> sceneNames, IEnumerable<float> sceneExposures) { }
}
