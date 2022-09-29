namespace SeeSharp.Integrators;

/// <summary>
/// A classic path tracer with next event estimation
/// </summary>
public class PathTracer : Integrator {
    /// <summary>
    /// Used to compute the seeds for all random samplers.
    /// </summary>
    public UInt32 BaseSeed = 0xC030114;

    /// <summary>
    /// Number of samples per pixel to render
    /// </summary>
    public int TotalSpp = 20;

    /// <summary>
    /// The maximum time in milliseconds that should be spent rendering.
    /// Excludes framebuffer overhead and other operations that are not part of the core rendering logic.
    /// </summary>
    public long? MaximumRenderTimeMs;

    /// <summary>
    /// Number of shadow rays to use for next event estimation at each vertex
    /// </summary>
    public int NumShadowRays = 1;

    /// <summary>
    /// Can be set to false to disable BSDF samples for direct illumination (typically a bad idea to turn
    /// this off unless to experiment)
    /// </summary>
    public bool EnableBsdfDI = true;

    /// <summary>
    /// If set to true, renders separate images for each technique combined via multi-sample MIS.
    /// By default, these are BSDF sampling and next event at every path length.
    /// </summary>
    public bool RenderTechniquePyramid = false;

    /// <summary>
    /// If set to true (default) runs Intel Open Image Denoise after the end of the last rendering iteration
    /// </summary>
    public bool EnableDenoiser = true;

    TechPyramid techPyramidRaw;
    TechPyramid techPyramidWeighted;

    protected Util.DenoiseBuffers denoiseBuffers;

    /// <summary>
    /// The scene that is being rendered.
    /// </summary>
    protected Scene scene;

    /// <summary>
    /// Called once for each complete path from the camera to a light.
    /// The default implementation generates a technique pyramid for the MIS samplers.
    /// </summary>
    public virtual void RegisterSample(Vector2 pixel, RgbColor weight, float misWeight, uint depth,
                                       bool isNextEvent) {
        if (!RenderTechniquePyramid)
            return;
        weight /= TotalSpp;
        int cameraEdges = (int)depth - (isNextEvent ? 1 : 0);
        techPyramidRaw.Add(cameraEdges, 0, (int)depth, pixel, weight);
        techPyramidWeighted.Add(cameraEdges, 0, (int)depth, pixel, weight * misWeight);
    }

    /// <summary>
    /// Called for every surface hit, before any sampling takes place.
    /// </summary>
    protected virtual void OnHit(in Ray ray, in Hit hit, in PathState state) { }

    /// <summary>
    /// Called whenever direct illumination was estimated via next event estimation
    /// </summary>
    protected virtual void OnNextEventResult(in Ray ray, in SurfacePoint point, in PathState state,
                                             float misWeight, RgbColor estimate) { }

    /// <summary>
    /// Called whenever an emitter was intersected
    /// </summary>
    protected virtual void OnHitLightResult(in Ray ray, in PathState state, float misWeight,
                                            RgbColor emission, bool isBackground) { }

    /// <summary>
    /// Called before a path is traced, after the initial camera ray was sampled
    /// </summary>
    /// <param name="state">Initial state of the path (only pixel and RNG are set)</param>
    protected virtual void OnStartPath(in PathState state) { }

    /// <summary>
    /// Called after a path has finished tracing and its contribution was added to the corresponding pixel.
    /// </summary>
    protected virtual void OnFinishedPath(in RadianceEstimate estimate, in PathState state) { }

    /// <summary> Called after the scene was submitted, before rendering starts. </summary>
    protected virtual void OnPrepareRender() { }

    /// <summary> Called after rendering of the scene has finished. </summary>
    protected virtual void OnAfterRender() { }

    /// <summary>
    /// Called before each iteration (one sample per pixel), after the frame buffer was updated.
    /// </summary>
    /// <param name="iterIdx">0-based index of the iteration that is about to start</param>
    protected virtual void OnPreIteration(uint iterIdx) { }

    /// <summary>
    /// Called at the end of each iteration (one sample per pixel), before the frame buffer is updated.
    /// </summary>
    /// <param name="iterIdx">0-based index of the iteration that just ended</param>
    protected virtual void OnPostIteration(uint iterIdx) { }

    /// <summary>
    /// Tracks the current state of a path that is being traced
    /// </summary>
    protected struct PathState {
        /// <summary>
        /// The pixel this path originated from
        /// </summary>
        public readonly Vector2 Pixel { get; init; }

        /// <summary>
        /// Current state of the random number generator
        /// </summary>
        public readonly RNG Rng { get; init; }

        /// <summary>
        /// Product of BSDF terms and cosines, divided by sampling pdfs, along the path so far.
        /// </summary>
        public RgbColor Throughput { get; set; }

        /// <summary>
        /// Number of edges (rays) that have been sampled so far
        /// </summary>
        public uint Depth { get; set; }

        /// <summary>
        /// The previous hit point, if the depth is not 1
        /// </summary>
        public SurfacePoint? PreviousHit { get; set; }

        /// <summary>
        /// The solid angle pdf of the last ray that was sampled (required for MIS)
        /// </summary>
        public float PreviousPdf { get; set; }
    }

    /// <summary>
    /// Outgoing radiance estimate at a shading point, split into multiple components
    /// </summary>
    protected struct RadianceEstimate {
        /// <summary>
        /// Emitted radiance L_e
        /// </summary>
        public RgbColor Emitted { get; init; }

        /// <summary>
        /// Reflected radiance (integral over all directions of incident radiance times BSDF and cosine)
        /// </summary>
        public RgbColor Reflected { get; init; }

        /// <summary>
        /// The pdf of computing the direct illumination contribution via next event estimation.
        /// </summary>
        public float NextEventPdf { get; init; }

        /// <summary>
        /// The full outgoing radiance estimate
        /// </summary>
        public RgbColor Outgoing => Emitted + Reflected;

        /// <summary>
        /// Initializes the structure for a fully black, zero-radiance estimate
        /// </summary>
        public static RadianceEstimate Absorbed => new();
    }

    /// <summary>
    /// Renders a scene with the current settings. Only one scene can be rendered at a time.
    /// </summary>
    public override void Render(Scene scene) {
        this.scene = scene;

        OnPrepareRender();

        if (RenderTechniquePyramid) {
            techPyramidRaw = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                (int)MinDepth, (int)MaxDepth, false, false, false);
            techPyramidWeighted = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                (int)MinDepth, (int)MaxDepth, false, false, false);
        }

        // Add custom frame buffer layers
        if (EnableDenoiser)
            denoiseBuffers = new(scene.FrameBuffer);

        ProgressBar progressBar = new(prefix: "Rendering...");
        progressBar.Start(TotalSpp);
        RenderTimer timer = new();
        for (uint sampleIndex = 0; sampleIndex < TotalSpp; ++sampleIndex) {
            long nextIterTime = timer.RenderTime + timer.PerIterationCost;
            if (MaximumRenderTimeMs.HasValue && nextIterTime > MaximumRenderTimeMs.Value) {
                Logger.Log("Maximum render time exhausted.");
                if (EnableDenoiser) denoiseBuffers.Denoise();
                break;
            }
            timer.StartIteration();

            scene.FrameBuffer.StartIteration();
            timer.EndFrameBuffer();

            OnPreIteration(sampleIndex);
            Parallel.For(0, scene.FrameBuffer.Height, row => {
                for (uint col = 0; col < scene.FrameBuffer.Width; ++col) {
                    uint pixelIndex = (uint)(row * scene.FrameBuffer.Width + col);
                    RNG rng = new(BaseSeed, pixelIndex, sampleIndex);
                    RenderPixel((uint)row, col, rng);
                }
            });
            OnPostIteration(sampleIndex);
            timer.EndRender();

            if (sampleIndex == TotalSpp - 1 && EnableDenoiser)
                denoiseBuffers.Denoise();
            scene.FrameBuffer.EndIteration();
            timer.EndFrameBuffer();

            progressBar.ReportDone(1);
            timer.EndIteration();
        }

        scene.FrameBuffer.MetaData["RenderTime"] = timer.RenderTime;
        scene.FrameBuffer.MetaData["FrameBufferTime"] = timer.FrameBufferTime;

        OnAfterRender();

        if (RenderTechniquePyramid) {
            string pathRaw = System.IO.Path.Join(scene.FrameBuffer.Basename, "techs-raw");
            techPyramidRaw.WriteToFiles(pathRaw);
            string pathWeighted = System.IO.Path.Join(scene.FrameBuffer.Basename, "techs-weighted");
            techPyramidWeighted.WriteToFiles(pathWeighted);
        }
    }

    private void RenderPixel(uint row, uint col, RNG rng) {
        // Sample a ray from the camera
        var offset = rng.NextFloat2D();
        var pixel = new Vector2(col, row) + offset;
        Ray primaryRay = scene.Camera.GenerateRay(pixel, rng).Ray;

        PathState state = new() {
            Pixel = pixel,
            Rng = rng,
            Throughput = RgbColor.White,
            Depth = 1
        };

        OnStartPath(state);
        var estimate = EstimateIncidentRadiance(primaryRay, ref state);
        OnFinishedPath(estimate, state);

        scene.FrameBuffer.Splat(col, row, estimate.Outgoing);
    }

    protected virtual RadianceEstimate EstimateIncidentRadiance(in Ray ray, ref PathState state) {
        // Trace the next ray
        if (state.Depth > MaxDepth)
            return RadianceEstimate.Absorbed;
        var hit = scene.Raytracer.Trace(ray);

        RgbColor directHitContrib = RgbColor.Black;
        float nextEventPdf = 0;

        if (!hit && state.Depth >= MinDepth) {
            (directHitContrib, nextEventPdf) = OnBackgroundHit(ray, state);
            return new RadianceEstimate {
                Emitted = directHitContrib,
                NextEventPdf = nextEventPdf
            };
        } else if (!hit) {
            return RadianceEstimate.Absorbed;
        }

        OnHit(ray, hit, state);

        if (state.Depth == 1 && EnableDenoiser) {
            var albedo = ((SurfacePoint)hit).Material.GetScatterStrength(hit);
            denoiseBuffers.LogPrimaryHit(state.Pixel, albedo, hit.ShadingNormal);
        }

        // Check if a light source was hit.
        Emitter light = scene.QueryEmitter(hit);
        if (light != null && state.Depth >= MinDepth) {
            (directHitContrib, nextEventPdf) = OnLightHit(ray, hit, state, light);
        }

        RgbColor nextEventContrib = RgbColor.Black;
        // Perform next event estimation
        if (state.Depth + 1 >= MinDepth && state.Depth < MaxDepth) {
            for (int i = 0; i < NumShadowRays; ++i) {
                nextEventContrib += PerformBackgroundNextEvent(ray, hit, state);
                nextEventContrib += PerformNextEventEstimation(ray, hit, state);
            }
        }

        // Terminate early if this is the last desired bounce
        if (state.Depth >= MaxDepth) {
            return new RadianceEstimate {
                Emitted = directHitContrib,
                NextEventPdf = nextEventPdf,
                Reflected = nextEventContrib
            };
        }

        // Sample a direction to continue the random walk
        (var bsdfRay, float bsdfPdf, var bsdfSampleWeight) = SampleDirection(ray, hit, state);
        if (bsdfPdf == 0 || bsdfSampleWeight == RgbColor.Black)
            return new RadianceEstimate {
                Emitted = directHitContrib,
                NextEventPdf = nextEventPdf,
                Reflected = nextEventContrib
            };

        // Recursively estimate the incident radiance and log the result
        state.Throughput *= bsdfSampleWeight;
        state.Depth += 1;
        state.PreviousHit = hit;
        state.PreviousPdf = bsdfPdf;
        var nested = EstimateIncidentRadiance(bsdfRay, ref state);

        return new RadianceEstimate {
            Emitted = directHitContrib,
            NextEventPdf = nextEventPdf,
            Reflected = nextEventContrib + nested.Outgoing * bsdfSampleWeight
        };
    }

    protected virtual (RgbColor, float) OnBackgroundHit(in Ray ray, in PathState state) {
        if (scene.Background == null || !EnableBsdfDI)
            return (RgbColor.Black, 0);

        float misWeight = 1.0f;
        float pdfNextEvent = 0;
        if (state.Depth > 1) {
            // Compute the balance heuristic MIS weight
            pdfNextEvent = scene.Background.DirectionPdf(ray.Direction) * NumShadowRays;
            misWeight = 1 / (1 + pdfNextEvent / state.PreviousPdf);
        }

        var emission = scene.Background.EmittedRadiance(ray.Direction);
        RegisterSample(state.Pixel, emission * state.Throughput, misWeight, state.Depth, false);
        OnHitLightResult(ray, state, misWeight, emission, true);
        return (misWeight * emission, pdfNextEvent);
    }

    protected virtual (RgbColor, float) OnLightHit(in Ray ray, in SurfacePoint hit, in PathState state,
                                                   Emitter light) {
        float misWeight = 1.0f;
        float pdfNextEvt = 0;
        if (state.Depth > 1) { // directly visible emitters are not explicitely connected
                               // Compute the solid angle pdf of next event
            var jacobian = SampleWarp.SurfaceAreaToSolidAngle(state.PreviousHit.Value, hit);
            pdfNextEvt = light.PdfUniformArea(hit) / scene.Emitters.Count * NumShadowRays / jacobian;

            // Compute power heuristic MIS weights
            float pdfRatio = pdfNextEvt / state.PreviousPdf;
            misWeight = 1 / (pdfRatio * pdfRatio + 1);

            if (!EnableBsdfDI) misWeight = 0;
        }

        var emission = light.EmittedRadiance(hit, -ray.Direction);
        RegisterSample(state.Pixel, emission * state.Throughput, misWeight, state.Depth, false);
        OnHitLightResult(ray, state, misWeight, emission, false);
        return (misWeight * emission, pdfNextEvt);
    }

    protected virtual RgbColor PerformBackgroundNextEvent(in Ray ray, in SurfacePoint hit, in PathState state) {
        if (scene.Background == null)
            return RgbColor.Black; // There is no background

        var sample = scene.Background.SampleDirection(state.Rng.NextFloat2D());
        if (scene.Raytracer.LeavesScene(hit, sample.Direction)) {
            var bsdfTimesCosine = hit.Material.EvaluateWithCosine(
                hit, -ray.Direction, sample.Direction, false);
            var pdfBsdf = DirectionPdf(hit, -ray.Direction, sample.Direction, state);

            // Prevent NaN / Inf
            if (pdfBsdf == 0 || sample.Pdf == 0)
                return RgbColor.Black;

            // Since the densities are in solid angle unit, no need for any conversions here
            float misWeight = EnableBsdfDI ? 1 / (1.0f + pdfBsdf / (sample.Pdf * NumShadowRays)) : 1;
            var contrib = sample.Weight * bsdfTimesCosine / NumShadowRays;

            Debug.Assert(float.IsFinite(contrib.Average));
            Debug.Assert(float.IsFinite(misWeight));

            RegisterSample(state.Pixel, contrib * state.Throughput, misWeight, state.Depth + 1, true);
            OnNextEventResult(ray, hit, state, misWeight, contrib);
            return misWeight * contrib;
        }
        return RgbColor.Black;
    }

    protected virtual RgbColor PerformNextEventEstimation(in Ray ray, in SurfacePoint hit, in PathState state) {
        if (scene.Emitters.Count == 0)
            return RgbColor.Black;

        // Select a light source
        int idx = state.Rng.NextInt(0, scene.Emitters.Count);
        var light = scene.Emitters[idx];
        float lightSelectProb = 1.0f / scene.Emitters.Count;

        // Sample a point on the light source
        var lightSample = light.SampleUniformArea(state.Rng.NextFloat2D());
        Vector3 lightToSurface = Vector3.Normalize(hit.Position - lightSample.Point.Position);

        if (!scene.Raytracer.IsOccluded(hit, lightSample.Point)) {
            var emission = light.EmittedRadiance(lightSample.Point, lightToSurface);

            // Compute the jacobian for surface area -> solid angle
            // (Inverse of the jacobian for solid angle pdf -> surface area pdf)
            float jacobian = SampleWarp.SurfaceAreaToSolidAngle(hit, lightSample.Point);
            var bsdfCos = hit.Material.EvaluateWithCosine(hit, -ray.Direction, -lightToSurface, false);

            // Compute surface area PDFs
            float pdfNextEvt = lightSample.Pdf * lightSelectProb * NumShadowRays;
            float pdfBsdfSolidAngle = DirectionPdf(hit, -ray.Direction, -lightToSurface, state);
            float pdfBsdf = pdfBsdfSolidAngle * jacobian;

            // Avoid Inf / NaN
            if (jacobian == 0) return RgbColor.Black;

            Debug.Assert(pdfBsdf != 0 || bsdfCos == RgbColor.Black,
                "Non-zero BSDF value not sampled by forward path tracing!");

            // Compute the resulting power heuristic weights
            float pdfRatio = pdfBsdf / pdfNextEvt;
            float misWeight = EnableBsdfDI ? 1.0f / (pdfRatio * pdfRatio + 1) : 1;

            // Compute the final sample weight, account for the change of variables from light source area
            // to the hemisphere about the shading point.
            var pdf = lightSample.Pdf / jacobian * lightSelectProb * NumShadowRays;
            RegisterSample(state.Pixel, emission / pdf * bsdfCos * state.Throughput, misWeight,
                state.Depth + 1, true);
            OnNextEventResult(ray, hit, state, misWeight, emission / pdf * bsdfCos);
            return misWeight * emission / pdf * bsdfCos;
        }
        return RgbColor.Black;
    }

    /// <summary>
    /// Computes the solid angle pdf that <see cref="SampleDirection"/> is using
    /// </summary>
    /// <param name="hit">The surface point</param>
    /// <param name="outDir">Direction the path was coming from</param>
    /// <param name="sampledDir">Direction that could have been sampled</param>
    /// <param name="state">The current state of the path</param>
    /// <returns>Pdf of sampling "sampledDir" when coming from "outDir".</returns>
    protected virtual float DirectionPdf(in SurfacePoint hit, Vector3 outDir, Vector3 sampledDir,
                                         in PathState state)
    => hit.Material.Pdf(hit, outDir, sampledDir, false).Item1;

    /// <summary>
    /// Samples a direction to continue the path
    /// </summary>
    /// <param name="ray">Previous ray</param>
    /// <param name="hit">Current hit point</param>
    /// <param name="state">Current state of the path</param>
    /// <returns>
    /// The next ray, its pdf, and the contribution (bsdf * cosine / pdf).
    /// If sampling was not successful, the pdf will be zero and the path should be terminated.
    /// </returns>
    protected virtual (Ray, float, RgbColor) SampleDirection(in Ray ray, in SurfacePoint hit,
                                                             in PathState state) {
        var primary = state.Rng.NextFloat2D();
        var bsdfSample = hit.Material.Sample(hit, -ray.Direction, false, primary);
        var bsdfRay = Raytracer.SpawnRay(hit, bsdfSample.Direction);
        return (bsdfRay, bsdfSample.Pdf, bsdfSample.Weight);
    }
}