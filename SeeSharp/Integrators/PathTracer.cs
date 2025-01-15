namespace SeeSharp.Integrators;

/// <summary>
/// A classic path tracer with next event estimation and no extra user data in the path state.
/// </summary>
public class PathTracer : PathTracerBase<byte> { }

public class PathGraphRenderer : DebugVisualizer {
    void AddNode(PathGraphNode node, Scene scene, float radius) {
        if (node.Ancestor != null) { // TODO-HACK to avoid having a sphere around the camera
            var m = MeshFactory.MakeSphere(node.Position, radius, 16);
            m.UserData = node;
            m.Material = new DiffuseMaterial(new()); // only needed to prevent scene validation errors
            scene.Meshes.Add(m);
        }

        foreach (var s in node.Successors) {
            if (node.Ancestor != null) { // TODO-HACK to avoid having a sphere around the camera
                var m = MeshFactory.MakeCylinder(node.Position, s.Position, radius, 16);
                m.UserData = s;
                m.Material = new DiffuseMaterial(new()); // only needed to prevent scene validation errors
                scene.Meshes.Add(m); // TODO-POLISH remove duplicate code
            }

            AddNode(s, scene, radius);
        }
    }

    public void Render(Scene scene, PathGraph graph) {
        float radius = scene.Radius * 0.005f; // TODO better initialization?

        // Create geometry for the paths nodes and edges
        var sceneCpy = scene.Copy();
        foreach (var node in graph.Roots)
            AddNode(node, sceneCpy, radius);
        sceneCpy.FrameBuffer = scene.FrameBuffer;
        sceneCpy.Prepare();

        base.Render(sceneCpy);
    }

    public override void RenderPixel(Scene scene, uint row, uint col, uint sampleIndex) {
        // Seed the random number generator
        uint pixelIndex = row * (uint)scene.FrameBuffer.Width + col;
        var rng = new RNG(BaseSeed, pixelIndex, sampleIndex);

        // Sample a ray from the camera
        var offset = rng.NextFloat2D();
        Ray ray = scene.Camera.GenerateRay(new Vector2(col, row) + offset, ref rng).Ray;

        RgbColor value = RgbColor.Black;
        RgbColor firstHitValue = RgbColor.Black;
        for (int i = 0; ; i++) {
            SurfacePoint hit = scene.Raytracer.Trace(ray);

            if (!hit) {
                // Only use transparency if there is actually something underneath
                value = firstHitValue;
                break;
            }

            value *= i / (float)(i + 1);

            if (hit.Mesh.UserData is PathGraphNode node) {
                var nodeColor = node.ComputeVisualizerColor();
                value += nodeColor / (i + 1);
                break;
            }

            var surfaceColor = ComputeColor(hit, -ray.Direction, row, col);
            value += surfaceColor / (i + 1);
            ray = Raytracer.SpawnRay(hit, ray.Direction);
            if (i == 0) firstHitValue = surfaceColor;
        }

        // Shade and splat
        scene.FrameBuffer.Splat((int)col, (int)row, value);
    }
}

public class PathGraphNode(Vector3 pos, PathGraphNode ancestor = null) {
    public Vector3 Position = pos;
    public PathGraphNode Ancestor = ancestor;
    public List<PathGraphNode> Successors = [];

    public virtual bool IsBackground => false;

    public PathGraphNode AddSuccessor(PathGraphNode vertex) {
        Successors.Add(vertex);
        return vertex;
    }

    public virtual RgbColor ComputeVisualizerColor() {
        return RgbColor.Black;
    }
}

public class NextEventNode : PathGraphNode {
    public NextEventNode(Vector3 direction, PathGraphNode ancestor, RgbColor emission, float pdf,
                           RgbColor bsdfCos, float misWeight)
    : base(ancestor.Position + direction, ancestor) {
        Emission = emission;
        Pdf = pdf;
        BsdfTimesCosine = bsdfCos;
        MISWeight = misWeight;
    }

    public NextEventNode(SurfacePoint point, PathGraphNode ancestor, RgbColor emission, float pdf,
                           RgbColor bsdfCos, float misWeight)
    : base(point.Position, ancestor) {
        Emission = emission;
        Pdf = pdf;
        BsdfTimesCosine = bsdfCos;
        MISWeight = misWeight;
        Point = point;
    }

    public readonly RgbColor Emission;
    public readonly float Pdf;
    public readonly RgbColor BsdfTimesCosine;
    public readonly float MISWeight;
    public readonly SurfacePoint? Point;

    public override bool IsBackground => !Point.HasValue;
    public override RgbColor ComputeVisualizerColor() => new(0.1f, 0.8f, 0.01f);
}

public class BSDFSampleNode : PathGraphNode {
    public BSDFSampleNode(SurfacePoint point, PathGraphNode ancestor, RgbColor scatterWeight, float survivalProb) : base(point.Position, ancestor) {
        ScatterWeight = scatterWeight;
        SurvivalProbability = survivalProb;
        Point = point;
    }

    public BSDFSampleNode(SurfacePoint point, PathGraphNode ancestor, RgbColor scatterWeight, float survivalProb, RgbColor emission, float misWeight) : base(point.Position, ancestor) {
        ScatterWeight = scatterWeight;
        SurvivalProbability = survivalProb;
        Emission = emission;
        MISWeight = misWeight;
        Point = point;
    }

    public readonly RgbColor ScatterWeight;
    public readonly float SurvivalProbability;
    public readonly RgbColor Emission;
    public readonly float MISWeight;
    public readonly SurfacePoint Point;

    public override RgbColor ComputeVisualizerColor() => new(0.1f, 0.4f, 0.8f);
}

public class BackgroundNode : PathGraphNode {
    public BackgroundNode(Vector3 direction, PathGraphNode ancestor, RgbColor contrib, float misWeight) : base(ancestor.Position + direction) {
        Emission = contrib;
        MISWeight = misWeight;
    }
    public readonly RgbColor Emission;
    public readonly float MISWeight;
    public override bool IsBackground => true;

    public override RgbColor ComputeVisualizerColor() => new(0.1f, 0.1f, 0.9f);
}

public class PathGraph {
    public List<PathGraphNode> Roots = [];
}

/// <summary>
/// A classic path tracer with next event estimation. Additional per-path user data can be tracked via the
/// generic type provided.
/// </summary>
public class PathTracerBase<PayloadType> : Integrator {
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
    public OutlierReplayCache<uint, int> OutlierCache;

    protected DenoiseBuffers denoiseBuffers;

    /// <summary>
    /// The scene that is being rendered.
    /// </summary>
    protected Scene scene;

    public PathGraph ReplayPath(Scene scene, Pixel pixel, int originalWidth, uint baseSeed, int iteration) {
        this.scene = scene;

        uint pixelIndex = (uint)(pixel.Row * originalWidth + pixel.Col);
        RNG rng = new(baseSeed, pixelIndex, (uint)iteration);
        PathGraph graph = new();
        RenderPixel((uint)pixel.Row, (uint)pixel.Col, ref rng, graph);
        return graph;
    }

    /// <summary>
    /// Called once for each complete path from the camera to a light.
    /// The default implementation generates a technique pyramid for the MIS samplers.
    /// </summary>
    public virtual void RegisterSample(Pixel pixel, RgbColor weight, float misWeight, uint depth,
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
    protected virtual void OnHit(in Ray ray, in Hit hit, ref PathState state) { }

    /// <summary>
    /// Called whenever direct illumination was estimated via next event estimation
    /// </summary>
    protected virtual void OnNextEventResult(in SurfaceShader shader, in PathState state,
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
    protected virtual void OnStartPath(ref PathState state) { }

    /// <summary>
    /// Called after a path has finished tracing and its contribution was added to the corresponding pixel.
    /// </summary>
    protected virtual void OnFinishedPath(RgbColor estimate, ref PathState state) { }

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
    /// Called at the end of each iteration before the frame buffer is updated, just after
    /// <see cref="OnPostIteration" />. The difference to the latter is that execution time in this method is
    /// counted towards the frame buffer cost, not the render cost. Use this to update AOVs, process
    /// frame buffer layers, run progressive denoising, or output debug visualizations.
    /// </summary>
    /// <param name="iterIdx">0-based index of the iteration that just ended</param>
    protected virtual void PostprocessIteration(uint iterIdx) { }

    /// <summary>
    /// Tracks the current state of a path that is being traced
    /// </summary>
    protected ref struct PathState {
        /// <summary>
        /// The pixel this path originated from
        /// </summary>
        public Pixel Pixel { get; set; }

        /// <summary>
        /// Current state of the random number generator
        /// </summary>
        public ref RNG Rng;

        /// <summary>
        /// Product of BSDF terms and cosines, divided by sampling pdfs, along the path so far.
        /// </summary>
        public RgbColor PrefixWeight { get; set; }

        /// <summary>
        /// Product of (approximated) surface reflectances along the path so far. Useful for Russian roulette.
        /// (Without path guiding, this is usually the same as the PrefixWeight)
        /// </summary>
        public RgbColor ApproxThroughput { get; set; }

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

        /// <summary>
        /// BSDF weight divided by PDF at the previous vertex
        /// </summary>
        public RgbColor PreviousScatterWeight { get; set; }

        /// <summary>
        /// Probability to keep the path alive at the previous vertex
        /// </summary>
        public float PreviousSurvivalProbability { get; set; }

        /// <summary>
        /// Additional per-path data defined in a derived class.
        /// </summary>
        public PayloadType UserData { get; set; }
    }

    ProgressBar progressBar;

    public override ProgressBar CurProgressBar => progressBar;

    /// <summary>
    /// Renders a scene with the current settings. Only one scene can be rendered at a time.
    /// </summary>
    public override void Render(Scene scene) {
        this.scene = scene;

        OnPrepareRender();

        if (RenderTechniquePyramid) {
            techPyramidRaw = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                MinDepth, MaxDepth, false, false, false);
            techPyramidWeighted = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                MinDepth, MaxDepth, false, false, false);
        }

        // TODO add flag to enable / disable this if it is too expensive as an always-on option
        // TODO add setting for number of outliers to track
        OutlierCache = new(BaseSeed, scene.FrameBuffer.Width, scene.FrameBuffer.Height, 4);

        // Add custom frame buffer layers
        if (EnableDenoiser)
            denoiseBuffers = new(scene.FrameBuffer);

        progressBar = new(prefix: "Rendering...");
        progressBar.Start(TotalSpp);
        RenderTimer timer = new();
        ShadingStatCounter.Reset();
        scene.Raytracer.ResetStats();
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
                    RenderPixel((uint)row, col, ref rng, null);
                }
            });
            OnPostIteration(sampleIndex);
            timer.EndRender();

            if (sampleIndex == TotalSpp - 1 && EnableDenoiser)
                denoiseBuffers.Denoise();
            PostprocessIteration(sampleIndex);
            scene.FrameBuffer.EndIteration();
            timer.EndFrameBuffer();

            progressBar.ReportDone(1);
            timer.EndIteration();
        }

        scene.FrameBuffer.MetaData["RenderTime"] = timer.RenderTime;
        scene.FrameBuffer.MetaData["FrameBufferTime"] = timer.FrameBufferTime;
        scene.FrameBuffer.MetaData["ShadingStats"] = ShadingStatCounter.Current;
        scene.FrameBuffer.MetaData["RayTracerStats"] = scene.Raytracer.Stats;

        OnAfterRender();

        if (RenderTechniquePyramid) {
            string pathRaw = Path.Join(scene.FrameBuffer.Basename, "techs-raw");
            techPyramidRaw.WriteToFiles(pathRaw);
            string pathWeighted = Path.Join(scene.FrameBuffer.Basename, "techs-weighted");
            techPyramidWeighted.WriteToFiles(pathWeighted);
        }
    }

    /// <summary>
    /// Decides the Russian roulette probability. The default uses a naive mix of minimum depth and current
    /// path throughput.
    /// </summary>
    /// <param name="ray">The last ray</param>
    /// <param name="point">The current hit point</param>
    /// <param name="state">State of the path (contains throughput, length, etc.)</param>
    /// <returns>Probability with which to continue the path. Must be in [0, 1]</returns>
    protected virtual float ComputeSurvivalProbability(in Ray ray, in SurfacePoint point, in PathState state) {
        if (state.Depth > 4)
            return Math.Clamp(state.ApproxThroughput.Average, 0.05f, 0.95f);
        else
            return 1.0f;
    }

    /// <summary>
    /// Updates the estimate of one pixel. Called once per iteration for every pixel.
    /// </summary>
    protected virtual void RenderPixel(uint row, uint col, ref RNG rng, PathGraph graph = null) {
        // Sample a ray from the camera
        var offset = rng.NextFloat2D();
        var pixel = new Vector2(col, row) + offset;
        Ray primaryRay = scene.Camera.GenerateRay(pixel, ref rng).Ray;

        PathState state = new() {
            Pixel = new((int)col, (int)row),
            Rng = ref rng,
            PrefixWeight = RgbColor.White,
            ApproxThroughput = RgbColor.White,
            Depth = 1,
            PreviousScatterWeight = RgbColor.White,
            PreviousSurvivalProbability = 1
        };

        graph?.Roots.Add(new(primaryRay.Origin));

        OnStartPath(ref state);
        var estimate = EstimateIncidentRadiance(primaryRay, ref state, graph?.Roots[^1]);
        OnFinishedPath(estimate, ref state);

        if (graph == null) {
            // we must not change the outlier cache while replaying paths
            OutlierCache.Notify(state.Pixel, new() {
                Weight = estimate,
                LocalReplayInfo = scene.FrameBuffer.CurIteration - 1
            });

            scene.FrameBuffer.Splat(state.Pixel, estimate);
        }
    }

    protected virtual RgbColor EstimateIncidentRadiance(Ray ray, ref PathState state, PathGraphNode graphVertex = null) {
        RgbColor radianceEstimate = RgbColor.Black;

        while (state.Depth <= MaxDepth) {
            var hit = scene.Raytracer.Trace(ray);

            // Did the ray leave the scene?
            if (!hit) {
                if (state.Depth >= MinDepth) {
                    var (misWeight, contrib) = OnBackgroundHit(ray, ref state);
                    radianceEstimate += state.PrefixWeight * misWeight * contrib;
                    graphVertex = graphVertex?.AddSuccessor(new BackgroundNode(ray.Direction, graphVertex, contrib, misWeight));
                }

                break;
            }

            OnHit(ray, hit, ref state);

            SurfaceShader shader = new(hit, -ray.Direction, false);

            if (state.Depth == 1 && EnableDenoiser) {
                var albedo = shader.GetScatterStrength();
                denoiseBuffers.LogPrimaryHit(state.Pixel, albedo, hit.ShadingNormal);
            }

            // Check if a light source was hit.
            Emitter light = scene.QueryEmitter(hit);
            if (light != null && state.Depth >= MinDepth) {
                var (misWeight, contrib) = OnLightHit(ray, hit, ref state, light);
                radianceEstimate += state.PrefixWeight * misWeight * contrib;
                graphVertex = graphVertex?.AddSuccessor(new BSDFSampleNode(hit, graphVertex, state.PreviousScatterWeight, state.PreviousSurvivalProbability, contrib, misWeight));
            } else {
                graphVertex = graphVertex?.AddSuccessor(new BSDFSampleNode(hit, graphVertex, state.PreviousScatterWeight, state.PreviousSurvivalProbability));
            }

            // Path termination with Russian roulette
            float survivalProb = ComputeSurvivalProbability(ray, hit, state);
            if (state.Rng.NextFloat() > survivalProb || state.Depth == MaxDepth)
                break;

            // Perform next event estimation
            if (state.Depth + 1 >= MinDepth) {
                RgbColor nextEventContrib = RgbColor.Black;
                for (int i = 0; i < NumShadowRays; ++i) {
                    nextEventContrib += PerformBackgroundNextEvent(shader, ref state, graphVertex);
                    nextEventContrib += PerformNextEventEstimation(shader, ref state, graphVertex);
                }
                radianceEstimate += state.PrefixWeight * nextEventContrib / survivalProb;
            }

            // Sample a direction to continue the random walk
            (ray, float bsdfPdf, var bsdfSampleWeight, var approxReflectance) = SampleDirection(shader, state);
            if (bsdfPdf == 0 || bsdfSampleWeight == RgbColor.Black)
                break;

            // Recursively estimate the incident radiance and log the result
            state.PrefixWeight *= bsdfSampleWeight / survivalProb;
            state.ApproxThroughput *= approxReflectance / survivalProb;
            state.Depth++;
            state.PreviousHit = hit;
            state.PreviousPdf = bsdfPdf * survivalProb;
            state.PreviousScatterWeight = bsdfSampleWeight / survivalProb;
            state.PreviousSurvivalProbability = survivalProb;
        }

        return radianceEstimate;
    }

    protected virtual (float, RgbColor) OnBackgroundHit(in Ray ray, ref PathState state) {
        if (scene.Background == null || !EnableBsdfDI)
            return (0, RgbColor.Black);

        float misWeight = 1.0f;
        float pdfNextEvent;
        if (state.Depth > 1) {
            // Compute the balance heuristic MIS weight
            pdfNextEvent = scene.Background.DirectionPdf(ray.Direction) * NumShadowRays;
            misWeight = 1 / (1 + pdfNextEvent / state.PreviousPdf);
        }

        var emission = scene.Background.EmittedRadiance(ray.Direction);
        RegisterSample(state.Pixel, emission * state.PrefixWeight, misWeight, state.Depth, false);
        OnHitLightResult(ray, state, misWeight, emission, true);
        return (misWeight, emission);
    }

    protected virtual (float, RgbColor) OnLightHit(in Ray ray, in SurfacePoint hit, ref PathState state, Emitter light) {
        float misWeight = 1.0f;
        float pdfNextEvt;
        if (state.Depth > 1) { // directly visible emitters are not explicitely connected
                               // Compute the solid angle pdf of next event
            var jacobian = SampleWarp.SurfaceAreaToSolidAngle(state.PreviousHit.Value, hit);
            pdfNextEvt = light.PdfUniformArea(hit) / scene.Emitters.Count * NumShadowRays / jacobian;

            // Compute balance heuristic MIS weights
            float pdfRatio = pdfNextEvt / state.PreviousPdf;
            misWeight = 1 / (pdfRatio + 1);

            if (!EnableBsdfDI) misWeight = 0;
        }

        var emission = light.EmittedRadiance(hit, -ray.Direction);
        RegisterSample(state.Pixel, emission * state.PrefixWeight, misWeight, state.Depth, false);
        OnHitLightResult(ray, state, misWeight, emission, false);
        return (misWeight, emission);
    }

    protected virtual RgbColor PerformBackgroundNextEvent(in SurfaceShader shader, ref PathState state, PathGraphNode graphVertex) {
        if (scene.Background == null)
            return RgbColor.Black; // There is no background

        var sample = scene.Background.SampleDirection(state.Rng.NextFloat2D());
        if (scene.Raytracer.LeavesScene(shader.Point, sample.Direction)) {
            var bsdfTimesCosine = shader.EvaluateWithCosine(sample.Direction);
            var pdfBsdf = DirectionPdf(shader, sample.Direction, state);

            // Prevent NaN / Inf
            if (pdfBsdf == 0 || sample.Pdf == 0)
                return RgbColor.Black;

            // Since the densities are in solid angle unit, no need for any conversions here
            float misWeight = EnableBsdfDI ? 1 / (1.0f + pdfBsdf / (sample.Pdf * NumShadowRays)) : 1;
            var contrib = sample.Weight * bsdfTimesCosine / NumShadowRays;

            Debug.Assert(float.IsFinite(contrib.Average));
            Debug.Assert(float.IsFinite(misWeight));

            RegisterSample(state.Pixel, contrib * state.PrefixWeight, misWeight, state.Depth + 1, true);
            OnNextEventResult(shader, state, misWeight, contrib);

            if (contrib != RgbColor.Black)
                graphVertex?.AddSuccessor(new NextEventNode(sample.Direction, graphVertex, sample.Weight * sample.Pdf, sample.Pdf, bsdfTimesCosine, misWeight));

            return misWeight * contrib;
        }
        return RgbColor.Black;
    }

    protected virtual RgbColor PerformNextEventEstimation(in SurfaceShader shader, ref PathState state, PathGraphNode graphVertex) {
        if (scene.Emitters.Count == 0)
            return RgbColor.Black;

        // Select a light source
        int idx = state.Rng.NextInt(scene.Emitters.Count);
        var light = scene.Emitters[idx];
        float lightSelectProb = 1.0f / scene.Emitters.Count;

        // Sample a point on the light source
        var lightSample = light.SampleUniformArea(state.Rng.NextFloat2D());
        Vector3 lightToSurface = Vector3.Normalize(shader.Point.Position - lightSample.Point.Position);

        if (!scene.Raytracer.IsOccluded(shader.Point, lightSample.Point)) {
            var emission = light.EmittedRadiance(lightSample.Point, lightToSurface);

            // Compute the jacobian for surface area -> solid angle
            // (Inverse of the jacobian for solid angle pdf -> surface area pdf)
            float jacobian = SampleWarp.SurfaceAreaToSolidAngle(shader.Point, lightSample.Point);
            var bsdfCos = shader.EvaluateWithCosine(-lightToSurface);

            // Compute surface area PDFs
            float pdfNextEvt = lightSample.Pdf * lightSelectProb * NumShadowRays;
            float pdfBsdfSolidAngle = DirectionPdf(shader, -lightToSurface, state);
            float pdfBsdf = pdfBsdfSolidAngle * jacobian;

            // Avoid Inf / NaN
            if (jacobian == 0) return RgbColor.Black;

            // Compute the resulting balance heuristic weights
            float pdfRatio = pdfBsdf / pdfNextEvt;
            float misWeight = EnableBsdfDI ? 1.0f / (pdfRatio + 1) : 1;

            // Compute the final sample weight, account for the change of variables from light source area
            // to the hemisphere about the shading point.
            var pdf = lightSample.Pdf / jacobian * lightSelectProb * NumShadowRays;
            var contrib = emission / pdf * bsdfCos;

            RegisterSample(state.Pixel, contrib * state.PrefixWeight, misWeight, state.Depth + 1, true);
            OnNextEventResult(shader, state, misWeight, contrib);

            if (contrib != RgbColor.Black)
                graphVertex?.AddSuccessor(new NextEventNode(lightSample.Point, graphVertex, emission, pdf, bsdfCos, misWeight));

            return misWeight * contrib;
        }
        return RgbColor.Black;
    }

    /// <summary>
    /// Computes the solid angle pdf that <see cref="SampleDirection"/> is using
    /// </summary>
    /// <param name="shader">Shading context at the hit point where the path is resumed</param>
    /// <param name="sampledDir">Direction that could have been sampled</param>
    /// <param name="state">The current state of the path</param>
    /// <returns>Pdf of sampling "sampledDir" when coming from "outDir".</returns>
    protected virtual float DirectionPdf(in SurfaceShader shader, Vector3 sampledDir,
                                         PathState state)
    => shader.Pdf(sampledDir).Item1;

    /// <summary>
    /// Samples a direction to continue the path
    /// </summary>
    /// <param name="shader">Shading context at the hit point where the path is resumed</param>
    /// <param name="state">Current state of the path</param>
    /// <returns>
    /// The next ray, its pdf, and the contribution (bsdf * cosine / pdf).
    /// If sampling was not successful, the pdf will be zero and the path should be terminated.
    /// The last value returned is an approximation of the surface reflectance. Under the assumption that
    /// BSDF importance sampling is perfect, this is BSDF_value / BSDF_pdf. This is returned here so the
    /// integrator does not have to recompute the BSDF pdf. Useful for path guiding applications.
    /// </returns>
    protected virtual (Ray, float, RgbColor, RgbColor) SampleDirection(in SurfaceShader shader, in PathState state) {
        var primary = state.Rng.NextFloat2D();
        var bsdfSample = shader.Sample(primary);
        var bsdfRay = Raytracer.SpawnRay(shader.Point, bsdfSample.Direction);
        return (bsdfRay, bsdfSample.Pdf, bsdfSample.Weight, bsdfSample.Weight);
    }
}