using System.Linq;

namespace SeeSharp.Image;

/// <summary>
/// Provides an image buffer to receive pixel estimates during rendering. Additional named layers can
/// be attached to store AOVs. If tev sync is used, this needs to be disposed of correctly, e.g., via
/// a "using" block.
/// </summary>
public class FrameBuffer : IDisposable {
    /// <summary>
    /// Width of the frame buffer in pixels
    /// </summary>
    public readonly int Width;

    /// <summary>
    /// Height of the frame buffer in pixels
    /// </summary>
    public readonly int Height;

    /// <summary>
    /// The (current) rendered image. Only normalized correctly after <see cref="EndIteration"/>
    /// </summary>
    public RgbImage Image { get; private set; }

    /// <summary>
    /// The reference image. If set, error metrics will be calculated for each iteration
    /// </summary>
    public RgbImage ReferenceImage = null;

    /// <summary>
    /// Automatically added layer that estimates per-pixel variances in the frame buffer
    /// </summary>
    public readonly VarianceLayer PixelVariance = new();

    /// <summary>
    /// Associated meta data that will be stored along with the final rendered image. By default,
    /// contains an item "RenderTime" which tracks the total time in milliseconds over all iterations,
    /// and "NumIterations".
    /// </summary>
    public readonly Dictionary<string, dynamic> MetaData = new();

    readonly Flags flags;
    TevIpc tevIpc;
    readonly string filename;
    readonly Dictionary<string, Layer> layers = new();
    readonly Stopwatch stopwatch = new Stopwatch();

    /// <summary>
    /// Adds a new layer to the frame buffer. Will be written with the final image, either as a layer or
    /// separately, depending on the file format.
    /// </summary>
    public void AddLayer(string name, Layer layer) => layers.Add(name, layer);

    /// <returns>Layer with the given name</returns>
    public Layer GetLayer(string name) => layers[name];

    /// <summary>
    /// 1-based index of the current iteration (i.e., the total number of iterations so far).
    /// If rendering has not started yet, this will be zero.
    /// </summary>
    public int CurIteration { get => curIter; private set => curIter = value; }
    int curIter = 0;

    /// <summary>
    /// The full path to the final rendered image file, but without the extension. Can be used to
    /// generate adequate names for auxiliary files and debug data.
    /// </summary>
    public string Basename {
        get {
            string dir = Path.GetDirectoryName(filename);
            string fileBase = Path.GetFileNameWithoutExtension(filename);
            return Path.Combine(dir, fileBase);
        }
    }

    /// <summary>
    /// File extension of the final image, which also specifies the format.
    /// </summary>
    public string Extension => Path.GetExtension(filename);

    /// <summary>
    /// Flags controlling some behaviour of the buffer
    /// </summary>
    [Flags]
    public enum Flags {
        /// <summary> Use default behaviour </summary>
        None = 0,

        /// <summary> Write the result of each iteration into a distinct file </summary>
        WriteIntermediate = 1,

        /// <summary> Continously update the rendering result after each iteration </summary>
        WriteContinously = 2,

        ///<summary> Like WriteContinously, but sends the data via a socket to the tev viewer </summary>
        SendToTev = 4
    }

    private record ErrorMetric(long TimeMS, float MSE, float RelMSE, float RelMSE_Outlier);
    private List<ErrorMetric> Errors = new();

    /// <param name="width">Width in pixels</param>
    /// <param name="height">Height in pixels</param>
    /// <param name="filename">
    ///     Filename to use when writing the final rendering. Can be null if <see cref="WriteToFile"/> is
    ///     never called without an explicit filename and no flags are set.
    /// </param>
    /// <param name="flags">Controls how incremental results are stored</param>
    public FrameBuffer(int width, int height, string filename, Flags flags = Flags.None) {
        this.filename = filename;
        this.flags = flags;
        Width = width;
        Height = height;
        AddLayer("variance", PixelVariance);
    }

    /// <summary>
    /// Adds a contribution to the frame buffer
    /// </summary>
    /// <param name="x">Horizontal pixel coordinate, [0, Width), left to right</param>
    /// <param name="y">Vertical pixel coordinate, [0, Height), top to bottom</param>
    /// <param name="value">Color to add to the current value</param>
    public virtual void Splat(float x, float y, RgbColor value) {
        Image.AtomicAdd((int)x, (int)y, value / CurIteration);
        PixelVariance.Splat(x, y, value);

        // Catch invalid values in long running Release mode renderings.
        // Ideally can be reproduced with a single sample from a correctly seeded RNG.
        Debug.Assert(float.IsFinite(value.Average));
        if (!float.IsFinite(value.Average)) {
            Logger.Log($"NaN or Inf written to frame buffer! Iteration: {CurIteration}, Pixel: ({x},{y})",
                Verbosity.Warning);
        }
    }

    /// <summary>
    /// Should be called before the start of each new rendering iteration. A rendering iteration is one of
    /// multiple equal-sized batches of samples per pixel.
    /// </summary>
    public virtual void StartIteration() {
        if (CurIteration == 0) {
            Image = new RgbImage(Width, Height);
            foreach (var (_, layer) in layers)
                layer.Init(Width, Height);

            if (flags.HasFlag(Flags.SendToTev)) {
                try {
                    tevIpc = new TevIpc();
                    // If a file with the same name is open, close it to avoid conflicts
                    tevIpc.CloseImage(filename);
                    tevIpc.CreateImageSync(filename, Width, Height,
                        layers.Select(kv => (kv.Key, kv.Value.Image))
                            .Append(("default", Image))
                            .ToArray()
                    );
                } catch (Exception exc) {
                    Logger.Warning(exc.ToString());
                    Logger.Warning("Could not connect to tev. Make sure it is running with the correct IP and port.");
                    tevIpc = null;
                }
            }

            MetaData["NumIterations"] = 0;
        }

        CurIteration++;

        // Correct the division by the number of iterations from the previous iterations
        if (CurIteration > 1)
            Image.Scale((CurIteration - 1.0f) / CurIteration);

        foreach (var (_, layer) in layers)
            layer.OnStartIteration(CurIteration);

        stopwatch.Start();
    }

    /// <summary>
    /// Current total time spent between <see cref="StartIteration"/> and <see cref="EndIteration"/>,
    /// i.e, the render time without frame buffer overhead.
    /// </summary>
    public long RenderTimeMs => stopwatch.ElapsedMilliseconds;

    /// <summary>
    /// Notifies that the rendering iteration is finished, intermediate results can be written, and time
    /// measurments can be stopped.
    /// </summary>
    public virtual void EndIteration() {
        stopwatch.Stop();

        MetaData["RenderTime"] = stopwatch.ElapsedMilliseconds;
        MetaData["NumIterations"] += 1;

        foreach (var (_, layer) in layers)
            layer.OnEndIteration(CurIteration);

        if (ReferenceImage != null)
            Errors.Add(ComputeErrorMetric());

        if (flags.HasFlag(Flags.WriteIntermediate)) {
            string name = Basename + "-iter" + CurIteration.ToString("D3")
                + $"-{stopwatch.ElapsedMilliseconds}ms" + Extension;
            WriteToFile(name);
        }

        if (flags.HasFlag(Flags.WriteContinously))
            WriteToFile();

        tevIpc?.UpdateImage(filename);
    }

    /// <summary>
    /// Clears the image and all layers and resets the rendering time to zero
    /// </summary>
    public virtual void Reset() {
        CurIteration = 0;
        stopwatch.Reset();
    }

    /// <summary>
    /// Clears the image and all layers, but keeps the rendering time
    /// </summary>
    public virtual void Clear() {
        CurIteration = 0;
    }

    /// <summary>
    /// Writes the current rendered image to a file on disk.
    /// </summary>
    /// <param name="fname">The desired file name. If not given, uses the final image name.</param>
    public void WriteToFile(string fname = null) {
        if (fname == null) fname = filename;

        string dir = Path.GetDirectoryName(fname);
        string fileBase = Path.GetFileNameWithoutExtension(fname);
        string basename = Path.Combine(dir, fileBase);

        if (Path.GetExtension(fname).ToLower() == ".exr") {
            Layers.WriteToExr(fname,
                layers.Select(kv => (kv.Key, kv.Value.Image))
                    .Append(("default", Image))
                    .ToArray()
            );
        } else {
            // write all layers into individual files
            Image.WriteToFile(fname);

            string ext = Path.GetExtension(fname);
            foreach (var (name, layer) in layers) {
                layer.Image.WriteToFile(basename + "-" + name + ext);
            }
        }

        // Add error metric data if available
        if (Errors.Count > 0)
            MetaData["ErrorMetrics"] = Errors;

        // Write the metadata as json
        string json = System.Text.Json.JsonSerializer.Serialize(MetaData, options: new() {
            WriteIndented = true
        });
        File.WriteAllText(basename + ".json", json);
    }

    /// <summary>
    /// Closes the tev TCP connection, if it was set up.
    /// </summary>
    public void Dispose() {
        tevIpc?.Dispose();
        tevIpc = null;
    }

    private ErrorMetric ComputeErrorMetric() {
        return new(stopwatch.ElapsedMilliseconds,
            SimpleImageIO.Metrics.MSE(Image, ReferenceImage),
            SimpleImageIO.Metrics.RelMSE(Image, ReferenceImage),
            SimpleImageIO.Metrics.RelMSE_OutlierRejection(Image, ReferenceImage));
    }
}