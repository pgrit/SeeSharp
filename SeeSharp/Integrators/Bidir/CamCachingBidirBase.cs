namespace SeeSharp.Integrators.Bidir;

public class CamCachingBidirBase : Integrator {
    #region Parameters

    public int NumIterations { get; set; } = 1;

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
    /// If false, direct hits of light sources are not included in the estimate
    /// </summary>
    public bool EnableHitting { get; set; } = true;

    /// <summary>
    /// Number of inner-path connections to make
    /// </summary>
    public int NumConnections { get; set; } = 1;

    /// <summary>
    /// Number of shadow rays to use for next event. Disabled if zero.
    /// </summary>
    public int NumShadowRays { get; set; } = 1;

    /// <summary>
    /// Set to false to disable connections between light vertices and the camera
    /// </summary>
    public bool EnableLightTracer { get; set; } = true;

    public bool EnableMerging { get; set; } = true;

    /// <summary>
    /// If set to true, will not use correlation-aware MIS weights (Grittmann et al. 2021)
    /// </summary>
    public bool DisableCorrelAwareMIS { get; set; } = false;

    /// <summary>
    /// If set to true, renders all techniques for all path lengths as separate images, with and without MIS.
    /// This is expensive and should only be used for debugging purposes.
    /// </summary>
    public bool RenderTechniquePyramid = false;

    /// <summary>Whether or not to use merging at the first hit from the camera.</summary>
    public bool MergePrimary = false;

    #endregion Parameters

    public Scene Scene;

    public PathCache CameraPaths;

    TechPyramid techPyramidRaw;
    TechPyramid techPyramidWeighted;

    ProgressBar progressBar;

    public override ProgressBar CurProgressBar => progressBar;

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

    protected NearestNeighborSearch<(int PathIndex, int VertexIndex, float Radius)> photonMap;

    /// <summary>
    /// Shrinks the global maximum radius based on the current camera path.
    /// </summary>
    /// <param name="pixelFootprint">Radius of the pixel footprint at the primary hit point</param>
    /// <returns>The shrunk radius</returns>
    protected virtual float ComputeLocalMergeRadius(float pixelFootprint) {
        return pixelFootprint;
    }

    protected float maxRadius;

    public void BuildImportonAccel() {
        photonMap.Clear();
        maxRadius = 0;
        for (int pathIdx = 0; pathIdx < Scene.FrameBuffer.Width * Scene.FrameBuffer.Height; ++pathIdx) {
            float footprint = float.Sqrt(1 / CameraPaths[pathIdx, 0].PdfFromAncestor);
            float radius = ComputeLocalMergeRadius(footprint);
            maxRadius = float.Max(maxRadius, radius);

            for (int vertIdx = MergePrimary ? 0 : 1; vertIdx < CameraPaths.Length(pathIdx); ++vertIdx) {
                ref var vertex = ref CameraPaths[pathIdx, vertIdx];
                if (vertex.Weight != RgbColor.Black)
                    photonMap.AddPoint(vertex.Point.Position, (pathIdx, vertIdx, radius));
            }
        }
        photonMap.Build();
    }

    public override void Render(Scene scene) {
        Scene = scene;

        if (NumLightPaths < 0)
            NumLightPaths = scene.FrameBuffer.Width * scene.FrameBuffer.Height;

        // if (EnableDenoiser) DenoiseBuffers = new(scene.FrameBuffer);
        OnBeforeRender();

        if (RenderTechniquePyramid && MaxDepth > 10) {
            Logger.Warning("MaxDepth is set above 10, but a technique pyramid was requested (RenderTechniquePyramid == true). To avoid excessive memory consumption, the RenderTechniquePyramid flag will be ignored.");
            RenderTechniquePyramid = false;
        }

        if (RenderTechniquePyramid) {
            techPyramidRaw = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                                             minDepth: 1, maxDepth: MaxDepth, merges: EnableMerging);
            techPyramidWeighted = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                                                  minDepth: 1, maxDepth: MaxDepth, merges: EnableMerging);
        }

        CameraPaths = new(scene.FrameBuffer.Width * scene.FrameBuffer.Height, Math.Min(MaxDepth + 1, 10));
        photonMap ??= new();

        progressBar = new(prefix: "Rendering...");
        progressBar.Start(NumIterations);
        RenderTimer timer = new();
        Stopwatch lightTracerTimer = new();
        Stopwatch pathTracerTimer = new();
        Stopwatch accelBuildTimer = new();
        ShadingStatCounter.Reset();
        scene.Raytracer.ResetStats();
        for (uint iter = 0; iter < NumIterations; ++iter) {
            long nextIterTime = timer.RenderTime + timer.PerIterationCost;
            if (MaximumRenderTimeMs.HasValue && nextIterTime > MaximumRenderTimeMs.Value) {
                Logger.Log("Maximum render time exhausted.");
                // if (EnableDenoiser) DenoiseBuffers.Denoise();
                break;
            }

            timer.StartIteration();

            scene.FrameBuffer.StartIteration();
            timer.EndFrameBuffer();

            OnStartIteration(iter);
            try {
                pathTracerTimer.Start();
                Parallel.For(0, Scene.FrameBuffer.Height, row => {
                    for (uint col = 0; col < Scene.FrameBuffer.Width; ++col) {
                        uint pixelIndex = (uint)(row * Scene.FrameBuffer.Width + col);
                        var rng = new RNG(BaseSeedCamera, pixelIndex, iter);
                        TraceCameraPath((uint)row, col, ref rng);
                    }
                });
                pathTracerTimer.Stop();

                accelBuildTimer.Start();
                if (EnableMerging)
                    BuildImportonAccel();
                accelBuildTimer.Stop();

                lightTracerTimer.Start();
                Parallel.For(0, NumLightPaths, idx => {
                    var rng = new RNG(BaseSeedLight, (uint)idx, iter);
                    TraceLightPath(ref rng, idx);
                });
                lightTracerTimer.Stop();
            } catch {
                Logger.Log($"Exception in iteration {iter} out of {NumIterations}.", Verbosity.Error);
                throw;
            }
            OnEndIteration(iter);
            CameraPaths.Clear();
            timer.EndRender();

            // if (iter == NumIterations - 1 && EnableDenoiser)
            //     DenoiseBuffers.Denoise();

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

        if (RenderTechniquePyramid) {
            techPyramidRaw.Normalize(1.0f / Scene.FrameBuffer.CurIteration);
            techPyramidRaw.WriteToFiles(Path.Join(scene.FrameBuffer.Basename, "techs-raw"));

            techPyramidWeighted.Normalize(1.0f / Scene.FrameBuffer.CurIteration);
            techPyramidWeighted.WriteToFiles(Path.Join(scene.FrameBuffer.Basename, "techs-weighted"));
        }
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
                                          int cameraPathLength, int lightPathLength, int fullLength) {
        if (!RenderTechniquePyramid)
            return;

        techPyramidRaw.Add(cameraPathLength, lightPathLength, fullLength, pixel, weight);
        techPyramidWeighted.Add(cameraPathLength, lightPathLength, fullLength, pixel, weight * misWeight);
    }

    /// <summary>
    /// Called for each full path sample generated via next event estimation on a camera path.
    /// </summary>
    /// <param name="weight">The sample contribution not yet weighted by MIS</param>
    /// <param name="misWeight">The MIS weight that will be multiplied on the sample weight</param>
    /// <param name="cameraPath">The camera path until the point where NEE was performed</param>
    /// <param name="pdfNextEvent">Surface area pdf used for next event estimation</param>
    /// <param name="pdfHit">
    /// Surface area pdf of sampling the same light source point by continuing the path
    /// </param>
    /// <param name="pathPdfs">Surface area pdfs of all sampling techniques. </param>
    /// <param name="emitter">The emitter that was connected to</param>
    /// <param name="lightToSurface">
    /// Direction from the point on the light to the last camera vertex.
    /// </param>
    /// <param name="lightPoint">The point on the light</param>
    protected virtual void OnNextEventSample(RgbColor weight, float misWeight, in CameraPathState cameraPath,
                                             float pdfNextEvent, float pdfHit, in BidirPathPdfs pathPdfs,
                                             Emitter emitter, Vector3 lightToSurface, SurfacePoint lightPoint) { }

    /// <summary>
    /// Called for each full path sample generated by hitting a light source during the random walk from
    /// the camera.
    /// </summary>
    /// <param name="weight">The sample contribution not yet weighted by MIS</param>
    /// <param name="misWeight">The MIS weight that will be multiplied on the sample weight</param>
    /// <param name="cameraPath">The camera path until the point where NEE was performed</param>
    /// <param name="pathPdfs">Surface area pdfs of all sampling techniques. </param>
    /// <param name="pdfNextEvent">
    /// Surface area pdf of sampling the same light source point via next event estimation instead.
    /// </param>
    /// <param name="emitter">The emitter that was hit</param>
    /// <param name="lightToSurface">
    /// Direction from the point on the light to the last camera vertex.
    /// </param>
    /// <param name="lightPoint">The point on the light</param>
    protected virtual void OnEmitterHitSample(RgbColor weight, float misWeight, in CameraPathState cameraPath,
                                              float pdfNextEvent, in BidirPathPdfs pathPdfs, Emitter emitter,
                                              Vector3 lightToSurface, SurfacePoint lightPoint) { }

    /// <summary>
    /// Called for each full path generated by connecting a camera sub-path and a light sub-path
    /// via a shadow ray.
    /// </summary>
    /// <param name="weight">The sample contribution not yet weighted by MIS</param>
    /// <param name="misWeight">The MIS weight that will be multiplied on the sample weight</param>
    /// <param name="cameraVertex">The camera path until the point where NEE was performed</param>
    /// <param name="lightPath">Last vertex of the camera sub-path that was connected to</param>
    /// <param name="pathPdfs">Surface area pdfs of all sampling techniques. </param>
    protected virtual void OnBidirConnectSample(RgbColor weight, float misWeight, in PathVertex cameraVertex,
                                                in LightPathState lightPath, in BidirPathPdfs pathPdfs) { }

    public virtual RgbColor OnMissCameraPath(Ray ray, float pdfFromAncestor, ref CameraPathState state) {
        return OnBackgroundHit(ray, state);
    }

    /// <summary>
    /// Computes the MIS weight for next event estimation along a camera path
    /// </summary>
    /// <param name="cameraPath">The camera path</param>
    /// <param name="pathPdfs">Surface area pdfs of all sampling techniques. </param>
    /// <param name="isBackground">True if the path was connected to the background, false if its an area light</param>
    /// <returns>MIS weight</returns>
    public virtual float NextEventMis(in CameraPathState cameraPath, in BidirPathPdfs pathPdfs, bool isBackground) {
        var correlRatio = new CorrelAwareRatios(pathPdfs, cameraPath.PrimaryHitDistance, isBackground, stackalloc float[pathPdfs.NumPdfs - 1], stackalloc float[pathPdfs.NumPdfs - 1]);

        float sumReciprocals = 1.0f;

        // Hitting the light source
        if (EnableHitting)
            sumReciprocals += pathPdfs.PdfsCameraToLight[^1] / pathPdfs.PdfNextEvent;

        // All bidirectional connections
        float radius = ComputeLocalMergeRadius(cameraPath.FootprintRadius);
        sumReciprocals +=
            CameraPathReciprocals(cameraPath.Vertices.Count - 1, pathPdfs, cameraPath.Pixel, radius, correlRatio)
            / pathPdfs.PdfNextEvent;

        return 1 / sumReciprocals;
    }

    /// <summary>
    /// Used by next event estimation to select a light source
    /// </summary>
    /// <param name="from">A point on a surface where next event is performed</param>
    /// <param name="rng">Random number generator</param>
    /// <returns>The selected light and the discrete probability of selecting that light</returns>
    public virtual (Emitter, float) SelectLight(in SurfacePoint from, ref RNG rng) {
        int idx = rng.NextInt(Scene.Emitters.Count);
        return (Scene.Emitters[idx], 1.0f / Scene.Emitters.Count);
    }

    /// <returns>
    /// The discrete probability of selecting the given light when performing next event at the given
    /// shading point.
    /// </returns>
    public virtual float SelectLightPmf(in SurfacePoint from, Emitter em) => 1.0f / Scene.Emitters.Count;

    /// <summary>
    /// Samples an emitter and a point on its surface for next event estimation
    /// </summary>
    /// <param name="from">The shading point</param>
    /// <param name="rng">Random number generator</param>
    /// <returns>The sampled emitter and point on the emitter</returns>
    public virtual (Emitter, SurfaceSample) SampleNextEvent(SurfacePoint from, ref RNG rng) {
        var (light, lightProb) = SelectLight(from, ref rng);
        var lightSample = light.SampleUniformArea(rng.NextFloat2D());
        lightSample.Pdf *= lightProb * NumShadowRays;
        return (light, lightSample);
    }

    /// <summary>
    /// Computes the pdf used by <see cref="SampleNextEvent" />
    /// </summary>
    /// <param name="from">The shading point</param>
    /// <param name="to">The point on the light source</param>
    /// <returns>PDF of next event estimation</returns>
    public virtual float NextEventPdf(SurfacePoint from, SurfacePoint to) {
        float backgroundProbability = ComputeNextEventBackgroundProbability(/*hit*/);
        if (to.Mesh == null) { // Background
            var direction = to.Position - from.Position;
            return Scene.Background.DirectionPdf(direction) * backgroundProbability * NumShadowRays;
        } else { // Emissive object
            var emitter = Scene.QueryEmitter(to);
            return emitter.PdfUniformArea(to) * SelectLightPmf(from, emitter) * (1 - backgroundProbability) * NumShadowRays;
        }
    }

    /// <summary>
    /// The probability of selecting the background for next event estimation, instead of an emissive
    /// surface.
    /// </summary>
    /// <returns>The background selection probability, by default uniform</returns>
    public virtual float ComputeNextEventBackgroundProbability(/*SurfacePoint from*/)
    => Scene.Background == null ? 0 : 1 / (1.0f + Scene.Emitters.Count);

    /// <summary>
    /// Performs next event estimation at the end point of a camera path
    /// </summary>
    /// <param name="shader">Surface shading context at the last camera vertex</param>
    /// <param name="state"></param>
    /// <param name="reversePdfJacobian">
    /// Jacobian to convert the reverse sampling pdf from the last camera vertex to its ancestor from
    /// solid angle to surface area.
    /// </param>
    /// <returns>MIS weighted next event contribution</returns>
    public virtual RgbColor PerformNextEventEstimation(in SurfaceShader shader, in CameraPathState state, float reversePdfJacobian) {
        // Gather the PDFs for next event computation (we do it first to avoid code duplication)
        BidirPathPdfs pathPdfs = new(stackalloc float[state.Depth + 1], stackalloc float[state.Depth + 1]);
        pathPdfs.GatherCameraPdfs(state, state.Depth - 1);

        // Decide between background and surface sampling
        float backgroundProbability = ComputeNextEventBackgroundProbability();
        if (state.Rng.NextFloat() < backgroundProbability) { // Connect to the background
            if (Scene.Background == null)
                return RgbColor.Black; // There is no background

            var sample = Scene.Background.SampleDirection(state.Rng.NextFloat2D());
            sample.Pdf *= backgroundProbability;
            sample.Weight /= backgroundProbability;

            if (sample.Pdf == 0) // Prevent NaN
                return RgbColor.Black;

            if (Scene.Raytracer.LeavesScene(shader.Point, sample.Direction)) {
                var bsdfTimesCosine = shader.EvaluateWithCosine(sample.Direction);

                // Compute the reverse BSDF sampling pdf
                var (bsdfForwardPdf, bsdfReversePdf) = shader.Pdf(sample.Direction);
                bsdfReversePdf *= reversePdfJacobian;

                if (bsdfForwardPdf == 0 || bsdfReversePdf == 0 || bsdfTimesCosine == RgbColor.Black)
                    return RgbColor.Black;

                // Compute emission pdf
                float pdfEmit = ComputeBackgroundPdf(shader.Point.Position, -sample.Direction);

                // Compute the mis weight
                pathPdfs.PdfsLightToCamera[^2] = pdfEmit;
                if (state.Depth > 1) // not for direct illumination
                    pathPdfs.PdfsLightToCamera[^3] = bsdfReversePdf;
                pathPdfs.PdfNextEvent = sample.Pdf;
                pathPdfs.PdfsCameraToLight[^1] = bsdfForwardPdf;
                float misWeight = NextEventMis(state, pathPdfs, true);

                // Compute and log the final sample weight
                var weight = sample.Weight * bsdfTimesCosine;
                RegisterSample(weight * state.PrefixWeight, misWeight, state.Pixel,
                               state.Vertices.Count, 0, state.Vertices.Count + 1);
                OnNextEventSample(weight * state.PrefixWeight, misWeight, state, sample.Pdf,
                    bsdfForwardPdf, pathPdfs, null, -sample.Direction,
                    new() { Position = shader.Point.Position });
                return misWeight * weight;
            }
        } else { // Connect to an emissive surface
            if (Scene.Emitters.Count == 0)
                return RgbColor.Black;

            // Sample a point on the light source
            var (light, lightSample) = SampleNextEvent(shader.Point, ref state.Rng);
            lightSample.Pdf *= 1 - backgroundProbability;

            if (lightSample.Pdf == 0) // Prevent NaN
                return RgbColor.Black;

            if (!Scene.Raytracer.IsOccluded(shader.Point, lightSample.Point)) {
                Vector3 lightToSurface = Vector3.Normalize(shader.Point.Position - lightSample.Point.Position);
                var emission = light.EmittedRadiance(lightSample.Point, lightToSurface);
                if (emission == RgbColor.Black)
                    return RgbColor.Black;

                var bsdfTimesCosine = shader.EvaluateWithCosine(-lightToSurface);
                if (bsdfTimesCosine == RgbColor.Black)
                    return RgbColor.Black;

                // Compute the jacobian for surface area -> solid angle
                // (Inverse of the jacobian for solid angle pdf -> surface area pdf)
                float jacobian = SampleWarp.SurfaceAreaToSolidAngle(shader.Point, lightSample.Point);
                if (jacobian == 0) return RgbColor.Black;

                // Compute the missing pdf terms
                var (bsdfForwardPdf, bsdfReversePdf) = shader.Pdf(-lightToSurface);
                bsdfForwardPdf *= SampleWarp.SurfaceAreaToSolidAngle(shader.Point, lightSample.Point);
                bsdfReversePdf *= reversePdfJacobian;

                if (bsdfForwardPdf == 0 || bsdfReversePdf == 0 || bsdfTimesCosine == RgbColor.Black)
                    return RgbColor.Black;

                float pdfEmit = ComputeEmitterPdf(light, lightSample.Point, lightToSurface,
                    SampleWarp.SurfaceAreaToSolidAngle(lightSample.Point, shader.Point));

                pathPdfs.PdfsLightToCamera[^2] = pdfEmit;
                if (state.Depth > 1) // not for direct illumination
                    pathPdfs.PdfsLightToCamera[^3] = bsdfReversePdf;
                pathPdfs.PdfNextEvent = lightSample.Pdf;
                pathPdfs.PdfsCameraToLight[^1] = bsdfForwardPdf;

                float misWeight = NextEventMis(state, pathPdfs, false);

                var weight = emission * bsdfTimesCosine * (jacobian / lightSample.Pdf);
                RegisterSample(weight * state.PrefixWeight, misWeight, state.Pixel,
                               state.Vertices.Count, 0, state.Vertices.Count + 1);
                OnNextEventSample(weight * state.PrefixWeight, misWeight, state, lightSample.Pdf,
                    bsdfForwardPdf, pathPdfs, light, lightToSurface, lightSample.Point);
                return misWeight * weight;
            }
        }

        return RgbColor.Black;
    }

    /// <summary>
    /// Computes the MIS weight of randomly hitting an emitter
    /// </summary>
    /// <param name="cameraPath">The camera path that hit an emitter</param>
    /// <param name="pathPdfs">Surface area pdfs of all sampling techniques. </param>
    /// <param name="isBackground">true if the emitter that was hit is the background</param>
    /// <returns>MIS weight</returns>
    public virtual float EmitterHitMis(in CameraPathState cameraPath, in BidirPathPdfs pathPdfs, bool isBackground) {
        var correlRatio = new CorrelAwareRatios(pathPdfs, cameraPath.PrimaryHitDistance, isBackground, stackalloc float[pathPdfs.NumPdfs - 1], stackalloc float[pathPdfs.NumPdfs - 1]);

        float sumReciprocals = 1.0f;

        // Next event estimation
        float pdfThis = pathPdfs.PdfsCameraToLight[^1];
        sumReciprocals += pathPdfs.PdfNextEvent / pdfThis;

        // All connections along the camera path
        float radius = ComputeLocalMergeRadius(cameraPath.FootprintRadius);
        sumReciprocals +=
            CameraPathReciprocals(cameraPath.Vertices.Count - 2, pathPdfs, cameraPath.Pixel, radius, correlRatio)
            / pdfThis;

        return 1 / sumReciprocals;
    }

    /// <summary>
    /// Called when a camera path directly intersected an emitter
    /// </summary>
    /// <param name="emitter">The emitter that was hit</param>
    /// <param name="hit">The hit point on the emitter</param>
    /// <param name="outDir">Direction from the illuminated surface towards the point on the light</param>
    /// <param name="state">The camera path</param>
    /// <param name="reversePdfJacobian">
    /// Geometry term to convert a solid angle density on the emitter to a surface area density for
    /// sampling the previous point along the camera path
    /// </param>
    /// <returns>MIS weighted contribution of the emitter</returns>
    public virtual RgbColor OnEmitterHit(Emitter emitter, SurfacePoint hit, Vector3 outDir, in CameraPathState state, float reversePdfJacobian) {
        var emission = emitter.EmittedRadiance(hit, outDir);

        // Compute pdf values
        float pdfEmit = ComputeEmitterPdf(emitter, hit, outDir, reversePdfJacobian);
        float pdfNextEvent = NextEventPdf(new SurfacePoint(), hit); // TODO get the actual previous point!

        var pathPdfs = new BidirPathPdfs(stackalloc float[state.Depth], stackalloc float[state.Depth]);
        pathPdfs.GatherCameraPdfs(state, state.Depth - 1);
        if (state.Depth > 1)
            pathPdfs.PdfsLightToCamera[^2] = pdfEmit;
        pathPdfs.PdfNextEvent = pdfNextEvent;

        float misWeight = state.Depth == 1 ? 1.0f : EmitterHitMis(state, pathPdfs, false);
        RegisterSample(emission * state.PrefixWeight, misWeight, state.Pixel,
                       state.Vertices.Count, 0, state.Vertices.Count);
        OnEmitterHitSample(emission * state.PrefixWeight, misWeight, state, pdfNextEvent, pathPdfs, emitter, outDir, hit);
        return misWeight * emission;
    }

    public virtual RgbColor OnBackgroundHit(Ray ray, in CameraPathState state) {
        if (Scene.Background == null)
            return RgbColor.Black;

        // Even if hitting is disabled, we still consider directly visible backgrounds
        if (!EnableHitting && state.Depth > 1)
            return RgbColor.Black;

        // Compute the pdf of sampling the previous point by emission from the background
        float pdfEmit = ComputeBackgroundPdf(ray.Origin, -ray.Direction);

        // Compute the pdf of sampling the same connection via next event estimation
        float pdfNextEvent = Scene.Background.DirectionPdf(ray.Direction);
        float backgroundProbability = ComputeNextEventBackgroundProbability(/*hit*/);
        pdfNextEvent *= backgroundProbability;

        var pathPdfs = new BidirPathPdfs(stackalloc float[state.Depth], stackalloc float[state.Depth]);
        pathPdfs.GatherCameraPdfs(state, state.Depth - 1);
        if (state.Depth > 1)
            pathPdfs.PdfsLightToCamera[^2] = pdfEmit;
        pathPdfs.PdfNextEvent = pdfNextEvent;

        float misWeight = state.Depth == 1 ? 1.0f : EmitterHitMis(state, pathPdfs, true);
        var emission = Scene.Background.EmittedRadiance(ray.Direction);
        RegisterSample(emission * state.PrefixWeight, misWeight, state.Pixel,
                       state.Vertices.Count, 0, state.Vertices.Count);
        OnEmitterHitSample(emission * state.PrefixWeight, misWeight, state, pdfNextEvent, pathPdfs, null,
            -ray.Direction, new() { Position = ray.Origin });
        return misWeight * emission * state.PrefixWeight;
    }

    public virtual RgbColor OnHitCameraPath(in SurfaceShader shader, float pdfFromAncestor, float toAncestorJacobian, ref CameraPathState state) {
        state.Vertices.Add(new() {
            Point = shader.Point,
            PdfFromAncestor = pdfFromAncestor,
            PdfReverseAncestor = state.NextReversePdf,
            PathId = state.PixelIndex,
            Weight = state.PrefixWeight,
            Depth = (byte)state.Depth,
            DirToAncestor = shader.Context.OutDirWorld,
            JacobianToAncestor = toAncestorJacobian
        });

        RgbColor value = RgbColor.Black;

        // Was a light hit?
        Emitter light = Scene.QueryEmitter(shader.Point);
        if (light != null && (EnableHitting || state.Depth == 1) && state.Depth >= MinDepth) {
            value += state.PrefixWeight * OnEmitterHit(light, shader.Point, shader.Context.OutDirWorld, state, toAncestorJacobian);
        }

        // Next event estimation
        if (state.Depth < MaxDepth && state.Depth + 1 >= MinDepth) {
            for (int i = 0; i < NumShadowRays; ++i) {
                value += state.PrefixWeight * PerformNextEventEstimation(shader, state, toAncestorJacobian);
            }
        }

        return value;
    }

    /// <summary>
    /// Called for each sample generated by the light tracer.
    /// </summary>
    /// <param name="weight">The sample contribution not yet weighted by MIS</param>
    /// <param name="misWeight">The MIS weight that will be multiplied on the sample weight</param>
    /// <param name="pixel">The pixel to which this sample contributes</param>
    /// <param name="lightVertex">The last vertex on the light path</param>
    /// <param name="pathPdfs">Surface area pdfs of all sampling techniques. </param>
    /// <param name="distToCam">Distance of the last vertex to the camera</param>
    protected virtual void OnLightTracerSample(RgbColor weight, float misWeight, Pixel pixel,
                                               PathVertex lightVertex, in BidirPathPdfs pathPdfs, float distToCam) { }

    /// <summary>
    /// Computes the MIS weight of a light tracer sample, for example via the balance heuristic.
    /// </summary>
    /// <param name="lightVertex">The last vertex of the light sub-path</param>
    /// <param name="pathPdfs">Surface area pdfs of all sampling techniques. </param>
    /// <param name="pixel">The pixel on the image the path contributes to</param>
    /// <param name="distToCam">Distance between the camera and the last light path vertex</param>
    /// <returns>MIS weight of the sampled path</returns>
    public virtual float LightTracerMis(PathVertex lightVertex, in BidirPathPdfs pathPdfs, Pixel pixel, float distToCam) {
        var correlRatio = new CorrelAwareRatios(pathPdfs, distToCam, lightVertex.FromBackground,
            stackalloc float[pathPdfs.NumPdfs - 1], stackalloc float[pathPdfs.NumPdfs - 1]);

        float footprintRadius = float.Sqrt(1.0f / pathPdfs.PdfsCameraToLight[0]);

        float radius = ComputeLocalMergeRadius(footprintRadius);
        float sumReciprocals = LightPathReciprocals(-1, pathPdfs, pixel, radius, correlRatio);
        sumReciprocals /= NumLightPaths;
        sumReciprocals += 1;

        return 1 / sumReciprocals;
    }

    void ConnectLightVertexToCamera(ref LightPathState state) {
        if (state.Depth + 1 < MinDepth) return;

        ref var vertex = ref state.Vertices[^1];

        // Compute image plane location
        RNG rng = new(); // TODO / FIXME this is not used atm, so we can pass a dummy. But must update once fancier cameras are implemented!
        var response = Scene.Camera.SampleResponse(vertex.Point, ref rng);
        if (!response.IsValid)
            return;

        if (Scene.Raytracer.IsOccluded(vertex.Point, response.Position))
            return;

        var dirToCam = response.Position - vertex.Point.Position;
        float distToCam = dirToCam.Length();
        dirToCam /= distToCam;

        SurfaceShader shader = new(vertex.Point, dirToCam, false);

        var bsdfValue = shader.Evaluate(vertex.DirToAncestor);
        if (bsdfValue == RgbColor.Black)
            return;

        // The shading cosine only cancels out with the Jacobian if the geometry aligns with the shading geometry
        bsdfValue *=
            float.Abs(Vector3.Dot(vertex.Point.ShadingNormal, vertex.DirToAncestor)) /
            float.Abs(Vector3.Dot(vertex.Point.Normal, vertex.DirToAncestor));

        // Compute the surface area pdf of sampling the previous vertex instead
        var (pdfReverse, _) = shader.Pdf(vertex.DirToAncestor);
        pdfReverse *= vertex.JacobianToAncestor;

        // Gather the PDFs for MIS computation
        var pathPdfs = new BidirPathPdfs(stackalloc float[vertex.Depth + 1], stackalloc float[vertex.Depth + 1]);
        pathPdfs.GatherLightPdfs(state, -1);
        pathPdfs.PdfsCameraToLight[0] = response.PdfEmit;
        pathPdfs.PdfsCameraToLight[1] = pdfReverse;
        if (vertex.Depth == 1)
            pathPdfs.PdfNextEvent = NextEventPdf(vertex.Point, state.Vertices[^2].Point);

        float misWeight = LightTracerMis(vertex, pathPdfs, response.Pixel, distToCam);

        // Compute image contribution and splat
        RgbColor weight = vertex.Weight * bsdfValue * response.Weight / NumLightPaths;
        Scene.FrameBuffer.Splat(response.Pixel, misWeight * weight);

        // Log the sample
        RegisterSample(weight, misWeight, response.Pixel, 0, vertex.Depth, vertex.Depth + 1);
        OnLightTracerSample(weight, misWeight, response.Pixel, vertex, pathPdfs, distToCam);
    }

    /// <summary>
    /// Computes the MIS weight of a bidirectional connection.
    /// </summary>
    /// <param name="cameraVertex">The camera path vertex to which we are connecting</param>
    /// <param name="primaryDistance">Distance of the first camera hit point from the camera</param>
    /// <param name="radius">Photon mapping radius in this pixel</param>
    /// <param name="lightPath">The light path, the last vertex of which is connected</param>
    /// <param name="pathPdfs">Surface area pdfs of all sampling techniques. </param>
    /// <returns>MIS weight for the connection</returns>
    public virtual float BidirConnectMis(in PathVertex cameraVertex, float primaryDistance, float radius, in LightPathState lightPath, in BidirPathPdfs pathPdfs) {
        var correlRatio = new CorrelAwareRatios(pathPdfs, primaryDistance, lightPath.Vertices[0].FromBackground,
            stackalloc float[pathPdfs.NumPdfs - 1], stackalloc float[pathPdfs.NumPdfs - 1]);

        float sumReciprocals = 1.0f;
        int lastCameraVertexIdx = cameraVertex.Depth - 1;
        sumReciprocals += CameraPathReciprocals(lastCameraVertexIdx, pathPdfs, GetPixel(cameraVertex.PathId), radius, correlRatio);
        sumReciprocals += LightPathReciprocals(lastCameraVertexIdx, pathPdfs, GetPixel(cameraVertex.PathId), radius, correlRatio);

        return 1 / sumReciprocals;
    }

    /// <returns>Pixel corresponding to the scalar path index of a camera path</returns>
    public Pixel GetPixel(int pixelIndex) => new(
        pixelIndex % Scene.FrameBuffer.Width,
        pixelIndex / Scene.FrameBuffer.Width
    );

    public virtual RgbColor Connect(in SurfaceShader lightShader, ref LightPathState lightPath, in PathVertex cameraVertex, float lightVertexProb) {
        var lightVertex = lightPath.Vertices[^1];
        float reversePdfJacobian = lightVertex.JacobianToAncestor;

        // Only allow connections that do not exceed the maximum total path length
        int depth = lightPath.Depth + cameraVertex.Depth + 1;
        if (depth > MaxDepth || depth < MinDepth)
            return RgbColor.Black;

        // Trace shadow ray
        if (Scene.Raytracer.IsOccluded(lightVertex.Point, cameraVertex.Point))
            return RgbColor.Black;

        // Compute connection direction
        var dirFromCamToLight = Vector3.Normalize(lightVertex.Point.Position - cameraVertex.Point.Position);

        var bsdfWeightLight = lightShader.Evaluate(-dirFromCamToLight) * float.Abs(Vector3.Dot(lightVertex.Point.Normal, -dirFromCamToLight));
        bsdfWeightLight *=
            float.Abs(Vector3.Dot(lightVertex.Point.ShadingNormal, lightVertex.DirToAncestor)) /
            float.Abs(Vector3.Dot(lightVertex.Point.Normal, lightVertex.DirToAncestor));

        SurfaceShader shader = new(cameraVertex.Point, cameraVertex.DirToAncestor, false);
        var bsdfWeightCam = shader.EvaluateWithCosine(dirFromCamToLight);

        if (bsdfWeightCam == RgbColor.Black || bsdfWeightLight == RgbColor.Black)
            return RgbColor.Black;

        // Compute the missing pdfs
        var (pdfCameraToLight, pdfCameraReverse) = shader.Pdf(dirFromCamToLight);
        pdfCameraReverse *= reversePdfJacobian;
        pdfCameraToLight *= SampleWarp.SurfaceAreaToSolidAngle(shader.Point, lightVertex.Point); // TODO redundant compute with below

        if (pdfCameraToLight == 0) return RgbColor.Black; // TODO figure out how this can happen!

        var (pdfLightToCamera, pdfLightReverse) = lightShader.Pdf(-dirFromCamToLight);
        pdfLightReverse *= lightVertex.JacobianToAncestor;
        pdfLightToCamera *= SampleWarp.SurfaceAreaToSolidAngle(lightVertex.Point, shader.Point);

        // Gather all PDFs for MIS compuation
        int lastCameraVertexIdx = cameraVertex.Depth - 1;

        var pathPdfs = new BidirPathPdfs(stackalloc float[depth], stackalloc float[depth]);
        pathPdfs.GatherCameraPdfs(CameraPaths, cameraVertex, lastCameraVertexIdx);
        pathPdfs.GatherLightPdfs(lightPath, lastCameraVertexIdx);
        if (lightVertex.Depth == 1)
            pathPdfs.PdfNextEvent = NextEventPdf(lightVertex.Point, lightPath.Vertices[^2].Point);

        // Set the pdf values that are unique to this combination of paths
        if (lastCameraVertexIdx > 0) // only if this is not the primary hit point
            pathPdfs.PdfsLightToCamera[lastCameraVertexIdx - 1] = pdfCameraReverse;
        pathPdfs.PdfsCameraToLight[lastCameraVertexIdx] = cameraVertex.PdfFromAncestor;
        pathPdfs.PdfsLightToCamera[lastCameraVertexIdx] = pdfLightToCamera;
        pathPdfs.PdfsCameraToLight[lastCameraVertexIdx + 1] = pdfCameraToLight;
        pathPdfs.PdfsCameraToLight[lastCameraVertexIdx + 2] = pdfLightReverse;

        float primaryDistance = CameraPaths[cameraVertex.PathId, 0].Point.Distance;
        float footprint = float.Sqrt(1 / CameraPaths[cameraVertex.PathId, 0].PdfFromAncestor);
        float radius = ComputeLocalMergeRadius(footprint);

        float misWeight = BidirConnectMis(cameraVertex, primaryDistance, radius, lightPath, pathPdfs);
        float distanceSqr = (shader.Point.Position - lightVertex.Point.Position).LengthSquared();

        // Avoid NaNs in rare cases
        if (distanceSqr == 0)
            return RgbColor.Black;

        RgbColor weight = lightVertex.Weight * bsdfWeightLight * bsdfWeightCam / distanceSqr / lightVertexProb;

        Debug.Assert(float.IsFinite(weight.Average));
        Debug.Assert(float.IsFinite(misWeight));

        RegisterSample(weight * cameraVertex.Weight, misWeight, GetPixel(cameraVertex.PathId), cameraVertex.Depth, lightVertex.Depth, depth);
        OnBidirConnectSample(weight * cameraVertex.Weight, misWeight, cameraVertex, lightPath, pathPdfs);

        return misWeight * weight * cameraVertex.Weight;
    }

    public virtual void PerformConnections(in SurfaceShader shader, ref LightPathState state) {
        if (NumConnections == 0) return;

        RgbColor estimate = RgbColor.Black;
        for (int i = 0; i < CameraPaths.Length(state.PathIndex); ++i) {
            estimate += Connect(shader, ref state, CameraPaths[state.PathIndex, i], 1);
        }
        Scene.FrameBuffer.Splat(GetPixel(state.PathIndex), estimate);
    }

    /// <summary>
    /// Computes the MIS weight for a merge
    /// </summary>
    /// <param name="lightPath">The light subpath, last vertex of it is getting merged</param>
    /// <param name="cameraVertex">The importon we are merging with</param>
    /// <param name="radius">Photon mapping radius in the pixel that we're in</param>
    /// <param name="primaryDistance">Distance of the primary hit of the camera path from the camera</param>
    /// <param name="pathPdfs">Surface area pdfs of all sampling techniques. </param>
    /// <returns>MIS weight (classic balance heuristic)</returns>
    public virtual float MergeMis(in LightPathState lightPath, in PathVertex cameraVertex, float radius, float primaryDistance, in BidirPathPdfs pathPdfs) {
        // Compute the acceptance probability approximation
        int lastCameraVertexIdx = cameraVertex.Depth - 1;
        float mergeApproximation = pathPdfs.PdfsLightToCamera[lastCameraVertexIdx]
                                 * MathF.PI * radius * radius * NumLightPaths;

        var correlRatio = new CorrelAwareRatios(pathPdfs, primaryDistance, lightPath.Vertices[0].FromBackground,
            stackalloc float[pathPdfs.NumPdfs - 1], stackalloc float[pathPdfs.NumPdfs - 1]);
        if (!DisableCorrelAwareMIS) mergeApproximation *= correlRatio[lastCameraVertexIdx];

        if (mergeApproximation == 0.0f) return 0.0f;

        // Compute reciprocals for hypothetical connections along the camera sub-path
        float sumReciprocals = 0.0f;
        sumReciprocals +=
            CameraPathReciprocals(lastCameraVertexIdx, pathPdfs, GetPixel(cameraVertex.PathId), radius, correlRatio)
            / mergeApproximation;
        sumReciprocals +=
            LightPathReciprocals(lastCameraVertexIdx, pathPdfs, GetPixel(cameraVertex.PathId), radius, correlRatio)
            / mergeApproximation;

        // Add the reciprocal for the connection that replaces the last light path edge
        if (lightPath.Depth > 1 && NumConnections > 0)
            sumReciprocals += 1 / mergeApproximation;

        return 1 / sumReciprocals;
    }

    /// <summary>
    /// Called for each individual merge that yields one full path between the camera and a light.
    /// </summary>
    /// <param name="weight">Contribution of the path</param>
    /// <param name="kernelWeight">The PM kernel value that will be multiplied on the weight</param>
    /// <param name="misWeight">MIS weight that will be multiplied on the weight</param>
    /// <param name="cameraVertex">The importon we are merging with</param>
    /// <param name="lightPath">The light subpath, last vertex of it is getting merged</param>
    /// <param name="pathPdfs">Surface area pdfs of all sampling techniques. </param>
    protected virtual void OnMergeSample(RgbColor weight, float kernelWeight, float misWeight,
                                         in PathVertex cameraVertex, in LightPathState lightPath, in BidirPathPdfs pathPdfs) {
    }

    protected virtual void Merge(in LightPathState lightPath, float toAncestorJacobian, in SurfaceShader shader,
                                 (int pathIdx, int vertexIdx, float radius) idx, float distSqr, float radiusSquared) {
        var importon = CameraPaths[idx.pathIdx, idx.vertexIdx];

        // Check that the path does not exceed the maximum length
        var depth = lightPath.Depth + importon.Depth;
        if (depth > MaxDepth || depth < MinDepth)
            return;

        // Discard photons on (almost) perpendicular surfaces. This avoids outliers and somewhat reduces
        // light leaks, but slightly amplifies darkening from kernel estimation bias.
        if (float.Abs(Vector3.Dot(shader.Point.Normal, importon.Point.Normal)) < 0.4f) {
            return;
        }

        // Compute the contribution of the photon
        var bsdfValue = shader.Evaluate(importon.DirToAncestor);
        bsdfValue *=
            float.Abs(Vector3.Dot(shader.Point.ShadingNormal, importon.DirToAncestor)) /
            float.Abs(Vector3.Dot(shader.Point.Normal, importon.DirToAncestor)); // TODO double-check that this is correct after reversal
        var importonWeight = importon.Weight * bsdfValue / NumLightPaths;

        // Early exit + prevent NaN / Inf
        if (importonWeight == RgbColor.Black) return;
        // Prevent outliers due to numerical issues with photons arriving almost parallel to the surface
        if (Math.Abs(Vector3.Dot(importon.DirToAncestor, shader.Point.Normal)) < 1e-4f) return;

        // Compute the missing pdf terms and the MIS weight
        var (pdfCameraReverse, pdfLightReverse) = shader.Pdf(importon.DirToAncestor);
        pdfCameraReverse *= importon.JacobianToAncestor;
        pdfLightReverse *= lightPath.Vertices[^1].JacobianToAncestor;

        int lastCameraVertexIdx = importon.Depth - 1;
        var pathPdfs = new BidirPathPdfs(stackalloc float[depth], stackalloc float[depth]);
        pathPdfs.GatherCameraPdfs(CameraPaths, importon, lastCameraVertexIdx);
        pathPdfs.GatherLightPdfs(lightPath, lastCameraVertexIdx - 1);

        // Set the pdf values that are unique to this combination of paths
        if (lastCameraVertexIdx > 0) // only if this is not the primary hit point
            pathPdfs.PdfsLightToCamera[lastCameraVertexIdx - 1] = pdfCameraReverse;
        pathPdfs.PdfsLightToCamera[lastCameraVertexIdx] = lightPath.Vertices[^1].PdfFromAncestor;
        pathPdfs.PdfsCameraToLight[lastCameraVertexIdx] = importon.PdfFromAncestor;
        pathPdfs.PdfsCameraToLight[lastCameraVertexIdx + 1] = pdfLightReverse;
        if (lightPath.Depth == 1)
            pathPdfs.PdfNextEvent = NextEventPdf(shader.Point, lightPath.Vertices[^2].Point);

        float misWeight = MergeMis(lightPath, importon, idx.radius, CameraPaths[idx.pathIdx, 0].Point.Distance, pathPdfs);

        // Prevent NaNs in corner cases
        if (pdfCameraReverse == 0 || pdfLightReverse == 0)
            return;

        // Epanechnikov kernel
        float kernelWeight = 2 * (radiusSquared - distSqr) / (MathF.PI * radiusSquared * radiusSquared);

        RegisterSample(importonWeight * kernelWeight * lightPath.PrefixWeight, misWeight, GetPixel(importon.PathId), importon.Depth, lightPath.Depth, depth);
        OnMergeSample(importonWeight * lightPath.PrefixWeight, kernelWeight, misWeight, importon, lightPath, pathPdfs);

        // TODO track total merge estimate in each camera vertex
        Scene.FrameBuffer.Splat(GetPixel(importon.PathId), importonWeight * kernelWeight * misWeight * lightPath.PrefixWeight);
    }

    ref struct MergeContext {
        public SurfaceShader Shader;
        public LightPathState State;
    }

    void MergeHelper(Vector3 position, (int PathIdx, int VertexIdx, float Radius) usr, float distance, int numFound, float distToFurthest, ref MergeContext ctx) {
        // Since we search based on the maximum radius, we need to reject some of those with a smaller one
        if (usr.Radius < distance)
            return;

        float radiusSquared = usr.Radius * usr.Radius;
        Merge(ctx.State, ctx.State.Vertices[^1].JacobianToAncestor, ctx.Shader, usr, distance * distance, radiusSquared);
    }

    public virtual void PerformMerges(in SurfaceShader shader, ref LightPathState state) {
        // var state = new MergeState(cameraJacobian, localRadius * localRadius, path, shader);
        MergeContext ctx = new() {
            Shader = shader,
            State = state,
        };
        photonMap.ForAllNearest(shader.Point.Position, 10 /*TODO for less bias in kernel normalization, this should be int.MaxValue, but that is very (!!) expensive in some scenes*/, maxRadius, MergeHelper, ref ctx);
        // OnCombinedMergeSample(shader, ref rng, path, cameraJacobian, state.Estimate);
        // TODO combined sample value must be tracked in the camera vertices if we need it...

    }

    public virtual void OnHitLightPath(in SurfaceShader shader, float pdfFromAncestor, float toAncestorJacobian, ref LightPathState state) {
        state.Vertices.Add(new() {
            Point = shader.Point,
            PdfFromAncestor = pdfFromAncestor,
            PdfReverseAncestor = state.NextReversePdf,
            PathId = state.PathIndex,
            Weight = state.PrefixWeight,
            Depth = (byte)state.Depth,
            JacobianToAncestor = toAncestorJacobian,
            DirToAncestor = shader.Context.OutDirWorld
        });

        if (state.Depth == 2)
            state.Vertices[^1].PdfNextEventAncestor = NextEventPdf(state.Vertices[^2].Point, state.Vertices[^3].Point);

        if (state.Depth < MaxDepth && state.Depth + 1 >= MinDepth && EnableLightTracer) {
            ConnectLightVertexToCamera(ref state);
        }

        if (EnableMerging)
            PerformMerges(shader, ref state);

        if (NumConnections > 0)
            PerformConnections(shader, ref state);
    }

    public virtual void OnContinueCameraPath(float pdfToAncestor, ref CameraPathState state) {
        state.NextReversePdf = pdfToAncestor; // TODO we could simply pass this directly to the OnHit call and cache it in the trace loop
    }

    public virtual void OnContinueLightPath(float pdfToAncestor, ref LightPathState state) {
        state.NextReversePdf = pdfToAncestor; // TODO we could simply pass this directly to the OnHit call and cache it in the trace loop
    }

    public virtual void OnTerminateCameraPath(ref CameraPathState state) {
        CameraPaths.Commit(state.PixelIndex, state.Vertices.AsSpan());
    }

    public virtual void OnTerminateLightPath(ref LightPathState state) { }

    public virtual void OnStartCamera(ref CameraPathState state) { }
    public virtual void OnStartEmitter(Emitter emitter, float prob, EmitterSample emitterSample, ref LightPathState state) { }
    public virtual void OnStartBackground(Ray ray, float pdf, ref LightPathState state) { }

    public ref struct CameraPathState {
        public ref RNG Rng;

        public Pixel Pixel;
        public int PixelIndex;
        public RgbColor PrefixWeight;
        public int Depth;

        public PathBuffer<PathVertex> Vertices;

        /// <summary>
        /// Approximated radius of the pixel footprint, i.e., the projection of the pixel this path started in
        /// onto the visible surface.
        /// </summary>
        public float FootprintRadius;

        public float NextReversePdf;

        public float PrimaryHitDistance;
    }

    readonly ThreadLocal<PathBuffer<PathVertex>> perThreadVertexBuffers = new(() => new(16));

    public virtual void TraceCameraPath(uint row, uint col, ref RNG rng) {
        Pixel pixel = new((int)col, (int)row);

        var sample = Scene.Camera.GenerateRay(new Vector2(col, row) + rng.NextFloat2D(), ref rng);
        CameraPathState state = new() {
            Rng = ref rng,
            Pixel = pixel,
            PixelIndex = pixel.Col + pixel.Row * Scene.FrameBuffer.Width,
            Depth = 1,
            PrefixWeight = sample.Weight,
            Vertices = perThreadVertexBuffers.Value
        };
        state.Vertices.Clear();
        OnStartCamera(ref state);

        RgbColor estimate = RgbColor.Black;
        RgbColor approxThroughput = RgbColor.White;

        Ray ray = sample.Ray;
        SurfacePoint previousPoint = sample.Point;
        float pdfDirection = sample.PdfRay;

        for (; state.Depth < MaxDepth; ++state.Depth) {

            var hit = Scene.Raytracer.Trace(ray);
            if (!hit) {
                estimate += OnMissCameraPath(ray, pdfDirection, ref state);
                break;
            }

            SurfaceShader shader = new(hit, -ray.Direction, false);

            // Convert the PDF of the previous hemispherical sample to surface area
            SanityChecks.IsNormalized(ray.Direction);
            float distSqr = hit.Distance * hit.Distance;
            float cosHit = float.Abs(Vector3.Dot(hit.Normal, ray.Direction));
            float cosPrev = float.Abs(Vector3.Dot(previousPoint.Normal, ray.Direction));

            float pdfFromAncestor = pdfDirection * cosHit / distSqr;

            // Geometry term might be zero due to, e.g., shading normal issues
            // Avoid NaNs in that case by terminating early
            if (pdfFromAncestor == 0) break;

            if (state.Depth == 1) {
                state.PrimaryHitDistance = hit.Distance;
                state.FootprintRadius = float.Sqrt(1 / pdfFromAncestor);
            }

            float jacobian = cosPrev / distSqr;
            estimate += OnHitCameraPath(shader, pdfFromAncestor, jacobian, ref state);

            // Don't sample continuations if we are going to terminate anyway
            if (state.Depth + 1 >= MaxDepth)
                break;

            // Terminate with Russian roulette
            float survivalProb = ComputeSurvivalProbability(hit, ray, ref state);
            if (rng.NextFloat() > survivalProb)
                break;

            // Sample the next direction and convert the reverse pdf
            var dirSample = SampleDirection(shader, ref rng);
            approxThroughput *= dirSample.ApproxReflectance / survivalProb;
            float pdfToAncestor = dirSample.PdfReverse * jacobian;

            OnContinueCameraPath(pdfToAncestor, ref state);

            if (dirSample.PdfForward == 0 || dirSample.Weight == RgbColor.Black)
                break;

            // Continue the path with the next ray
            state.PrefixWeight *= dirSample.Weight / survivalProb;
            pdfDirection = dirSample.PdfForward;
            previousPoint = hit;
            ray = Raytracer.SpawnRay(hit, dirSample.Direction);
        }

        OnTerminateCameraPath(ref state);

        Scene.FrameBuffer.Splat(pixel, estimate);
    }

    public record struct DirectionSample(
        float PdfForward,
        float PdfReverse,
        RgbColor Weight,
        Vector3 Direction,
        RgbColor ApproxReflectance
    ) { }

    public virtual DirectionSample SampleDirection(in SurfaceShader shader, ref RNG rng) {
        var bsdfSample = shader.Sample(rng.NextFloat2D());
        return new(
            bsdfSample.Pdf,
            bsdfSample.PdfReverse,
            bsdfSample.Weight,
            bsdfSample.Direction,
            bsdfSample.Weight
        );
    }

    public virtual (float, float) PdfDirection(in SurfaceShader shader, Vector3 dir)
    => shader.Pdf(dir);

    public virtual float ComputeSurvivalProbability(in SurfacePoint hit, in Ray ray, ref CameraPathState state) {
        if (state.Depth > 4)
            return Math.Clamp(state.PrefixWeight.Average, 0.05f, 0.95f);
        else
            return 1.0f;
    }

    public virtual float ComputeSurvivalProbability(in SurfacePoint hit, in Ray ray, ref LightPathState state) {
        if (state.Depth > 4)
            return Math.Clamp(state.PrefixWeight.Average, 0.05f, 0.95f);
        else
            return 1.0f;
    }

    /// <summary>
    /// Probability of selecting the background instead of a surface emitter
    /// </summary>
    public virtual float BackgroundProbability
    => Scene.Background != null ? 1 / (1.0f + Scene.Emitters.Count) : 0;

    /// <summary>
    /// Randomly samples either the background or an emitter from the scene
    /// </summary>
    /// <returns>The emitter and its selection probability</returns>
    public virtual (Emitter, float) SelectLightForEmission(ref RNG rng) {
        if (BackgroundProbability > 0 && rng.NextFloat() <= BackgroundProbability) {
            return (null, BackgroundProbability);
        } else {
            var emitter = Scene.Emitters[rng.NextInt(Scene.Emitters.Count)];
            return (emitter, (1 - BackgroundProbability) / Scene.Emitters.Count);
        }
    }

    /// <summary>
    /// Computes the sampling probability used by <see cref="SelectLightForEmission"/>
    /// </summary>
    /// <param name="em">An emitter in the scene</param>
    /// <returns>The selection probability</returns>
    public virtual float SelectLightForEmissionProbability(Emitter em) {
        if (em == null) { // background
            return BackgroundProbability;
        } else {
            return (1 - BackgroundProbability) / Scene.Emitters.Count;
        }
    }

    /// <summary>
    /// Samples a ray from an emitter in the scene
    /// </summary>
    /// <param name="rng">Random number generator</param>
    /// <param name="emitter">The emitter to sample from</param>
    /// <returns>Sampled ray, weights, and probabilities</returns>
    public virtual EmitterSample SampleEmitter(ref RNG rng, Emitter emitter) {
        var primaryPos = rng.NextFloat2D();
        var primaryDir = rng.NextFloat2D();
        return emitter.SampleRay(primaryPos, primaryDir);
    }

    /// <summary>
    /// Computes the importance sampling pdf to generate an edge from a point on an emitter to a point
    /// in the scene. Result is the product of two surface area densities and a discrete selection
    /// probability.
    /// </summary>
    /// <param name="emitter">An emitter in the scene</param>
    /// <param name="point">Point on the emitter's surface</param>
    /// <param name="lightToSurface">Direction from the emitter to the illuminated surface</param>
    /// <param name="reversePdfJacobian">
    /// Geometry term to convert the solid angle density on the emitter's surface to a surface area
    /// density at the illuminated point.
    /// </param>
    /// <returns>The full, product surface area density of sampling this ray from the emitter</returns>
    public virtual float ComputeEmitterPdf(Emitter emitter, SurfacePoint point, Vector3 lightToSurface,
                                           float reversePdfJacobian) {
        float pdfEmit = emitter.PdfRay(point, lightToSurface);
        pdfEmit *= reversePdfJacobian;
        pdfEmit *= SelectLightForEmissionProbability(emitter);
        return pdfEmit;
    }

    /// <summary>
    /// Samples a ray from the background into the scene.
    /// </summary>
    /// <param name="rng">Random number generator</param>
    /// <returns>The sampled ray, its weight, and the sampling pdf</returns>
    public virtual (Ray, RgbColor, float) SampleBackground(ref RNG rng) {
        var primaryPos = rng.NextFloat2D();
        var primaryDir = rng.NextFloat2D();
        return Scene.Background.SampleRay(primaryPos, primaryDir);
    }

    /// <summary>
    /// Computes the pdf of sampling a ray from the background that illuminates a point.
    /// </summary>
    /// <param name="from">The illuminated point</param>
    /// <param name="lightToSurface">Direction from the background to the illuminated point</param>
    /// <returns>Sampling density (solid angle times discrete)</returns>
    public virtual float ComputeBackgroundPdf(Vector3 from, Vector3 lightToSurface) {
        float pdfEmit = Scene.Background.RayPdf(from, lightToSurface);
        pdfEmit *= SelectLightForEmissionProbability(null);
        return pdfEmit;
    }

    public ref struct LightPathState {
        public ref RNG Rng;
        public RgbColor PrefixWeight;
        public int Depth;
        public int PathIndex;
        public PathBuffer<PathVertex> Vertices;

        public float NextReversePdf;
    }

    public virtual void TraceLightPath(ref RNG rng, int idx) {
        var (emitter, prob) = SelectLightForEmission(ref rng);

        Ray ray;
        SurfacePoint? previousPoint = null;
        float lastPdf;
        LightPathState state = new() {
            Rng = ref rng,
            Depth = 1,
            Vertices = perThreadVertexBuffers.Value,
            PathIndex = idx
        };
        state.Vertices.Clear();

        if (emitter != null) {
            var emitterSample = SampleEmitter(ref rng, emitter);
            emitterSample.Pdf *= prob;
            state.PrefixWeight = emitterSample.Weight / prob;
            previousPoint = emitterSample.Point;
            lastPdf = emitterSample.Pdf;

            if (emitterSample.Pdf == 0 || emitterSample.Weight == RgbColor.Black)  // Avoid NaNs and terminate early
                return;

            state.Vertices.Add(new() {
                Point = emitterSample.Point,
                PathId = state.PathIndex,
                FromBackground = false,
                Depth = 0,
            });

            OnStartEmitter(emitter, prob, emitterSample, ref state);

            ray = Raytracer.SpawnRay(emitterSample.Point, emitterSample.Direction);
        } else {
            (ray, var weight, lastPdf) = SampleBackground(ref rng);

            // Account for the light selection probability
            lastPdf *= prob;
            weight /= prob;

            if (lastPdf == 0 || weight == RgbColor.Black)  // Avoid NaNs and terminate early
                return;

            Debug.Assert(float.IsFinite(weight.Average));

            state.Vertices.Add(new() {
                Point = new SurfacePoint { Position = ray.Origin },
                PathId = state.PathIndex,
                FromBackground = true,
                Depth = 0,
            });

            OnStartBackground(ray, lastPdf, ref state);
        }

        RgbColor approxThroughput = RgbColor.White;
        for (; state.Depth < MaxDepth; ++state.Depth) {
            var hit = Scene.Raytracer.Trace(ray);
            if (!hit)
                break;

            SurfaceShader shader = new(hit, -ray.Direction, true);

            // If the previous point does not exist, this is a background, so no Jacobian required
            float jacobian = 1;
            float pdfFromAncestor = lastPdf;
            if (previousPoint.HasValue) {
                var d = hit.Position - previousPoint.Value.Position;
                float distSqr = d.LengthSquared();
                d /= float.Sqrt(distSqr);
                float cosHit = float.Abs(Vector3.Dot(hit.Normal, d));
                float cosPrev = float.Abs(Vector3.Dot(previousPoint.Value.Normal, d));
                jacobian = cosPrev / distSqr;
                pdfFromAncestor = lastPdf * cosHit / distSqr;
            }

            // Geometry term might be zero due to, e.g., shading normal issues
            // Avoid NaNs in that case by terminating early
            if (pdfFromAncestor == 0) break;

            OnHitLightPath(shader, pdfFromAncestor, jacobian, ref state);

            // Don't sample continuations if we are going to terminate anyway
            if (state.Depth + 1 >= MaxDepth)
                break;

            // Terminate with Russian roulette
            float survivalProb = ComputeSurvivalProbability(hit, ray, ref state);
            if (rng.NextFloat() > survivalProb)
                break;

            // Sample the next direction and convert the reverse pdf
            var dirSample = SampleDirection(shader, ref rng);
            approxThroughput *= dirSample.ApproxReflectance / survivalProb;
            float pdfToAncestor = dirSample.PdfReverse * jacobian;

            OnContinueLightPath(pdfToAncestor, ref state);

            if (dirSample.PdfForward == 0 || dirSample.Weight == RgbColor.Black)
                break;

            // The direction sample is multiplied by the shading cosine, but we need the geometric one
            dirSample.Weight *=
                float.Abs(Vector3.Dot(hit.Normal, dirSample.Direction)) /
                float.Abs(Vector3.Dot(hit.ShadingNormal, dirSample.Direction));

            // Rendering equation cosine cancels with the Jacobian, but only if geometry and shading geometry align
            dirSample.Weight *=
                float.Abs(Vector3.Dot(hit.ShadingNormal, -ray.Direction)) /
                float.Abs(Vector3.Dot(hit.Normal, -ray.Direction));

            SanityChecks.IsNormalized(ray.Direction);

            // Continue the path with the next ray
            state.PrefixWeight *= dirSample.Weight / survivalProb;
            lastPdf = dirSample.PdfForward;
            previousPoint = hit;
            ray = Raytracer.SpawnRay(hit, dirSample.Direction);
        }

        OnTerminateLightPath(ref state);
    }

    /// <summary>
    /// Tracks the unitless ratio of surrogate probabilities used for correlation-aware MIS
    /// See Grittmann et al. 2021 for details
    /// </summary>
    public readonly ref struct CorrelAwareRatios {
        readonly Span<float> cameraToLight;
        readonly Span<float> lightToCamera;

        public CorrelAwareRatios(BidirPathPdfs pdfs, float distToCam, bool fromBackground, Span<float> cameraToLight, Span<float> lightToCamera) {
            float radius = distToCam * 0.0174550649f; // distance * tan(1°)
            float acceptArea = radius * radius * MathF.PI;
            int numSurfaceVertices = pdfs.PdfsCameraToLight.Length - 1;

            this.cameraToLight = cameraToLight;
            this.lightToCamera = lightToCamera;

            // Gather camera probability
            float product = 1.0f;
            for (int i = 0; i < numSurfaceVertices; ++i) {
                product *= MathF.Min(pdfs.PdfsCameraToLight[i] * acceptArea, 1.0f);
                cameraToLight[i] = product;
            }

            // Gather light probability
            product = 1.0f;
            for (int i = numSurfaceVertices - 1; i >= 0; --i) {
                float next = pdfs.PdfsLightToCamera[i] * acceptArea;

                if (i == numSurfaceVertices - 1 && !fromBackground)
                    next *= acceptArea;

                product *= MathF.Min(next, 1.0f);
                lightToCamera[i] = product;
            }
        }

        public float this[int idx] {
            get {
                if (idx == 0) return 1; // primary merges are not correlated

                float light = lightToCamera[idx];
                float cam = cameraToLight[idx];

                // Prevent NaNs
                if (cam == 0 && light == 0)
                    return 1;

                return cam / (cam + light - cam * light);
            }
        }
    }

    /// <summary>
    /// Computes the PDF ratios along the camera subpath for MIS weight computations
    /// </summary>
    protected virtual float CameraPathReciprocals(int lastCameraVertexIdx, in BidirPathPdfs pdfs,
                                                  Pixel pixel, float radius, in CorrelAwareRatios correlRatio) {
        float sumReciprocals = 0.0f;
        float nextReciprocal = 1.0f;
        for (int i = lastCameraVertexIdx; i > 0; --i) {
            // Merging at this vertex
            if (EnableMerging) {
                float acceptProb = pdfs.PdfsLightToCamera[i] * MathF.PI * radius * radius;
                if (!DisableCorrelAwareMIS) acceptProb *= correlRatio[i];
                sumReciprocals += nextReciprocal * NumLightPaths * acceptProb;
            }

            nextReciprocal *= pdfs.PdfsLightToCamera[i] / pdfs.PdfsCameraToLight[i];

            // Connecting this vertex to the next one along the camera path
            if (NumConnections > 0) sumReciprocals += nextReciprocal;
        }

        // Light tracer
        if (EnableLightTracer)
            sumReciprocals +=
                nextReciprocal * pdfs.PdfsLightToCamera[0] / pdfs.PdfsCameraToLight[0] * NumLightPaths;

        // Merging directly visible (almost the same as the light tracer!)
        if (MergePrimary)
            sumReciprocals += nextReciprocal * NumLightPaths * pdfs.PdfsLightToCamera[0]
                            * MathF.PI * radius * radius;

        return sumReciprocals;
    }

    /// <summary>
    /// Computes the PDF ratios along the light subpath for MIS weight computations
    /// </summary>
    protected virtual float LightPathReciprocals(int lastCameraVertexIdx, in BidirPathPdfs pdfs,
                                                 Pixel pixel, float radius, in CorrelAwareRatios correlRatio) {
        float sumReciprocals = 0.0f;
        float nextReciprocal = 1.0f;
        for (int i = lastCameraVertexIdx + 1; i < pdfs.NumPdfs; ++i) {
            if (i == pdfs.NumPdfs - 1) // Next event
                sumReciprocals += nextReciprocal * pdfs.PdfNextEvent / pdfs.PdfsLightToCamera[i];

            if (i < pdfs.NumPdfs - 1 && (MergePrimary || i > 0)) { // no merging on the emitter itself
                                                                   // Account for merging at this vertex
                if (EnableMerging) {
                    float acceptProb = pdfs.PdfsCameraToLight[i] * MathF.PI * radius * radius;
                    if (!DisableCorrelAwareMIS) acceptProb *= correlRatio[i];
                    sumReciprocals += nextReciprocal * NumLightPaths * acceptProb;
                }
            }

            nextReciprocal *= pdfs.PdfsCameraToLight[i] / pdfs.PdfsLightToCamera[i];

            // Account for connections from this vertex to its ancestor
            if (i < pdfs.NumPdfs - 2) // Connections to the emitter (next event) are treated separately
                if (NumConnections > 0) sumReciprocals += nextReciprocal;
        }
        if (EnableHitting) sumReciprocals += nextReciprocal; // Hitting the emitter directly
        return sumReciprocals;
    }
}