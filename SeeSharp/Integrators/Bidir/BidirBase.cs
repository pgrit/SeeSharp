namespace SeeSharp.Integrators.Bidir;

/// <summary>
/// Basis for many bidirectional algorithms. Splits rendering into multiple iterations. Each iteration
/// traces a certain number of paths from the light sources and one camera path per pixel.
/// Derived classes can control the sampling decisions and techniques.
/// </summary>
public abstract partial class BidirBase<CameraPayloadType> : Integrator {

    #region Parameters

    /// <summary>
    /// Number of iterations (batches of one sample per pixel) to render
    /// </summary>
    public int NumIterations { get; set; } = 2;

    /// <summary>
    /// The maximum time in milliseconds that should be spent rendering.
    /// Excludes framebuffer overhead and other operations that are not part of the core rendering logic.
    /// </summary>
    public long? MaximumRenderTimeMs { get; set; }

    /// <summary>
    /// Number of light paths per iteration. If negative given, traces one per pixel.
    /// Must only be changed in-between rendering iterations. Otherwise: mayhem.
    /// </summary>
    public int NumLightPaths { get; set; } = -1;

    /// <summary>
    /// The base seed to generate camera paths.
    /// </summary>
    public uint BaseSeedCamera = 0xC030114u;

    /// <summary>
    /// The base seed used when sampling paths from the light sources
    /// </summary>
    public uint BaseSeedLight = 0x13C0FEFEu;

    /// <summary>
    /// If set to true (default) runs Intel Open Image Denoise after the end of the last rendering iteration
    /// </summary>
    public bool EnableDenoiser = true;

    #endregion Parameters

    /// <summary>
    /// The scene that is currently being rendered
    /// </summary>
    [JsonIgnore] protected Scene Scene;

    /// <summary>
    /// Logs denoiser-related features at the primary hit points of all camera paths
    /// </summary>
    [JsonIgnore] protected DenoiseBuffers DenoiseBuffers;

    /// <summary>
    /// Called once after the end of each rendering iteration (one sample per pixel)
    /// </summary>
    /// <param name="iteration">The 0-based index of the iteration that just finished</param>
    protected virtual void OnEndIteration(uint iteration) { }

    /// <summary>
    /// Called once before the start of each rendering iteration (one sample per pixel)
    /// </summary>
    /// <param name="iteration">The 0-based index of the iteration that will now start</param>
    protected virtual void OnStartIteration(uint iteration) { }

    /// <summary>
    /// Called after all rendering iterations are finished, or the time budget has been exhausted
    /// </summary>
    protected virtual void OnAfterRender() { }

    /// <summary>
    /// Called just before the main render loop starts. Not counted towards the render time. Use this to
    /// initialize auxiliary buffers for debugging purposes.
    /// </summary>
    protected virtual void OnBeforeRender() { }

    ProgressBar progressBar;

    public override ProgressBar CurProgressBar => progressBar;

    /// <summary>
    /// Renders the scene with the current settings. Not thread-safe: Only one scene can be rendered at a
    /// time by the same object of this class.
    /// </summary>
    public override void Render(Scene scene) {
        Scene = scene;

        if (NumLightPaths < 0)
            NumLightPaths = scene.FrameBuffer.Width * scene.FrameBuffer.Height;

        if (EnableDenoiser) DenoiseBuffers = new(scene.FrameBuffer);
        OnBeforeRender();

        progressBar = new(prefix: "Rendering...");
        progressBar.Start(NumIterations);
        RenderTimer timer = new();
        Stopwatch lightTracerTimer = new();
        Stopwatch pathTracerTimer = new();
        ShadingStatCounter.Reset();
        scene.Raytracer.ResetStats();
        for (uint iter = 0; iter < NumIterations; ++iter) {
            long nextIterTime = timer.RenderTime + timer.PerIterationCost;
            if (MaximumRenderTimeMs.HasValue && nextIterTime > MaximumRenderTimeMs.Value) {
                Logger.Log("Maximum render time exhausted.");
                if (EnableDenoiser) DenoiseBuffers.Denoise();
                break;
            }

            timer.StartIteration();

            scene.FrameBuffer.StartIteration();
            timer.EndFrameBuffer();

            OnStartIteration(iter);
            try {
                lightTracerTimer.Start();
                TraceLightPaths(BaseSeedLight, iter);
                ProcessPathCache();
                lightTracerTimer.Stop();
                pathTracerTimer.Start();
                TraceAllCameraPaths(iter);
                pathTracerTimer.Stop();
            } catch {
                Logger.Log($"Exception in iteration {iter} out of {NumIterations}.", Verbosity.Error);
                throw;
            }
            OnEndIteration(iter);
            timer.EndRender();

            if (iter == NumIterations - 1 && EnableDenoiser)
                DenoiseBuffers.Denoise();
            scene.FrameBuffer.EndIteration();
            timer.EndFrameBuffer();

            progressBar.ReportDone(1);
            timer.EndIteration();
        }

        scene.FrameBuffer.MetaData["RenderTime"] = timer.RenderTime;
        scene.FrameBuffer.MetaData["FrameBufferTime"] = timer.FrameBufferTime;
        scene.FrameBuffer.MetaData["PathTracerTime"] = pathTracerTimer.ElapsedMilliseconds;
        scene.FrameBuffer.MetaData["LightTracerTime"] = lightTracerTimer.ElapsedMilliseconds;
        scene.FrameBuffer.MetaData["ShadingStats"] = ShadingStatCounter.Current;
        scene.FrameBuffer.MetaData["RayTracerStats"] = scene.Raytracer.Stats;

        OnAfterRender();
    }

    /// <summary>
    /// Called for each sample that has a non-zero contribution to the image.
    /// This can be used to write out pyramids of sampling technique images / MIS weights.
    /// The default implementation does nothing.
    /// </summary>
    /// <param name="weight">The sample contribution not yet weighted by MIS</param>
    /// <param name="misWeight">The MIS weight that will be multiplied on the sample weight</param>
    /// <param name="pixel">The pixel to which this sample contributes</param>
    /// <param name="cameraPathLength">Number of edges in the camera sub-path (0 if light tracer).</param>
    /// <param name="lightPathLength">Number of edges in the light sub-path (0 when hitting the light).</param>
    /// <param name="fullLength">Number of edges forming the full path. Used to disambiguate techniques.</param>
    protected virtual void RegisterSample(RgbColor weight, float misWeight, Pixel pixel,
                                          int cameraPathLength, int lightPathLength, int fullLength) { }

}