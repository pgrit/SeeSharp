namespace SeeSharp.Integrators.Bidir;

/// <summary>
/// Basis for many bidirectional algorithms. Splits rendering into multiple iterations. Each iteration
/// traces a certain number of paths from the light sources and one camera path per pixel.
/// Derived classes can control the sampling decisions and techniques.
/// </summary>
public abstract class BidirBase<CameraPayloadType> : Integrator {
    /// <summary>
    /// Number of iterations (batches of one sample per pixel) to render
    /// </summary>
    public int NumIterations = 2;

    /// <summary>
    /// The maximum time in milliseconds that should be spent rendering.
    /// Excludes framebuffer overhead and other operations that are not part of the core rendering logic.
    /// </summary>
    public long? MaximumRenderTimeMs;

    /// <summary>
    /// Number of light paths per iteration. If not given, traces one per pixel.
    /// Must only be changed in-between rendering iterations. Otherwise: mayhem.
    /// </summary>
    public int? NumLightPaths;

    /// <summary>
    /// The base seed to generate camera paths.
    /// </summary>
    public uint BaseSeedCamera = 0xC030114u;

    /// <summary>
    /// The base seed used when sampling paths from the light sources
    /// </summary>
    public uint BaseSeedLight = 0x13C0FEFEu;

    /// <summary>
    /// Can be set to log some or all paths that have been sampled. It is up to the derived class to decide
    /// which paths to log and what data to associate with them.
    /// </summary>
    [JsonIgnore] public PathLogger PathLogger;

    /// <summary>
    /// The scene that is currently being rendered
    /// </summary>
    [JsonIgnore] public Scene Scene;

    /// <summary>
    /// The current batch of light paths traced during the current iteration
    /// </summary>
    [JsonIgnore] public LightPathCache LightPaths;

    /// <summary>
    /// If set to true (default) runs Intel Open Image Denoise after the end of the last rendering iteration
    /// </summary>
    public bool EnableDenoiser = true;

    /// <summary>
    /// Logs denoiser-related features at the primary hit points of all camera paths
    /// </summary>
    protected Util.DenoiseBuffers DenoiseBuffers;

    /// <summary>
    /// Forward and backward sampling probabilities of a path edge
    /// </summary>
    public struct PathPdfPair {
        /// <summary>
        /// PDF of sampling this vertex when coming from its actual ancestor
        /// </summary>
        public float PdfFromAncestor;

        /// <summary>
        /// PDF of sampling the ancestor of this vertex, if we were sampling the other way around
        /// </summary>
        public float PdfToAncestor;
    }

    /// <summary>
    /// Tracks the state of a camera path
    /// </summary>
    public struct CameraPath {
        /// <summary>
        /// The pixel position where the path was started.
        /// </summary>
        public Pixel Pixel;

        /// <summary>
        /// The product of the local estimators along the path (BSDF * cos / pdf)
        /// </summary>
        public RgbColor Throughput;

        /// <summary>
        /// The pdf values for sampling this path.
        /// </summary>
        public PathBuffer<PathPdfPair> Vertices;

        /// <summary>
        /// Distances between all points sampled along this path
        /// </summary>
        public PathBuffer<float> Distances;

        /// <summary>
        /// Maximum roughness of any surface material encountered along the path, prior to the currently
        /// last vertex.
        /// </summary>
        public float MaximumPriorRoughness;

        /// <summary>
        /// Roughness of the material at the currently last vertex of the path.
        /// </summary>
        public float CurrentRoughness;

        public int Depth => Vertices.Count;

        public SurfacePoint CurrentPoint;
        public SurfacePoint PreviousPoint;

        /// <summary>
        /// Approximated radius of the pixel footprint, i.e., the projection of the pixel this path started in
        /// onto the visible surface.
        /// </summary>
        public float FootprintRadius;

        public CameraPayloadType Payload;
    }

    /// <summary>
    /// Called once per iteration after the light paths have been traced.
    /// Use this to create acceleration structures etc.
    /// </summary>
    protected abstract void ProcessPathCache();

    /// <summary>
    /// Used by next event estimation to select a light source
    /// </summary>
    /// <param name="from">A point on a surface where next event is performed</param>
    /// <param name="rng">Random number generator</param>
    /// <returns>The selected light and the discrete probability of selecting that light</returns>
    protected virtual (Emitter, float) SelectLight(in SurfacePoint from, ref RNG rng) {
        int idx = rng.NextInt(Scene.Emitters.Count);
        return (Scene.Emitters[idx], 1.0f / Scene.Emitters.Count);
    }

    /// <returns>
    /// The discrete probability of selecting the given light when performing next event at the given
    /// shading point.
    /// </returns>
    protected virtual float SelectLightPmf(in SurfacePoint from, Emitter em) => 1.0f / Scene.Emitters.Count;

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

    /// <summary>
    /// Generates the light path cache used to sample and store light paths.
    /// Called at the beginning of the rendering process before the first iteration.
    /// </summary>
    protected virtual LightPathCache MakeLightPathCache()
    => new LightPathCache { MaxDepth = MaxDepth, NumPaths = NumLightPaths.Value, Scene = Scene };

    ProgressBar progressBar;

    public override ProgressBar CurProgressBar => progressBar;

    /// <summary>
    /// Renders the scene with the current settings. Not thread-safe: Only one scene can be rendered at a
    /// time by the same object of this class.
    /// </summary>
    public override void Render(Scene scene) {
        Scene = scene;

        if (!NumLightPaths.HasValue) NumLightPaths = scene.FrameBuffer.Width * scene.FrameBuffer.Height;

        LightPaths = MakeLightPathCache();

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
                // Make sure that changes in the light path count are propagated to the cache.
                LightPaths.NumPaths = NumLightPaths.Value;

                lightTracerTimer.Start();
                LightPaths.TraceAllPaths(BaseSeedLight, iter,
                    (origin, primary, nextDirection) => NextEventPdf(primary, origin));
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

    protected virtual void TraceAllCameraPaths(uint iter) {
        CameraRandomWalk walkMod = new(this);

        Parallel.For(0, Scene.FrameBuffer.Height, row => {
            for (uint col = 0; col < Scene.FrameBuffer.Width; ++col) {
                uint pixelIndex = (uint)(row * Scene.FrameBuffer.Width + col);
                var rng = new RNG(BaseSeedCamera, pixelIndex, iter);
                RenderPixel((uint)row, col, ref rng, walkMod);
            }
        });
    }

    protected virtual void RenderPixel(uint row, uint col, ref RNG rng, CameraRandomWalk walkMod) {
        Pixel pixel = new((int)col, (int)row);

        var offset = rng.NextFloat2D();
        var filmSample = new Vector2(col, row) + offset;
        var cameraRay = Scene.Camera.GenerateRay(filmSample, ref rng);

        RandomWalk<CameraPath> walk = new(Scene, ref rng, MaxDepth + 1, walkMod);
        var value = walk.StartFromCamera(cameraRay, pixel, new CameraPath());

        Scene.FrameBuffer.Splat(pixel, value);
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
    protected virtual void OnNextEventSample(RgbColor weight, float misWeight, CameraPath cameraPath,
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
    protected virtual void OnEmitterHitSample(RgbColor weight, float misWeight, CameraPath cameraPath,
                                              float pdfNextEvent, in BidirPathPdfs pathPdfs, Emitter emitter,
                                              Vector3 lightToSurface, SurfacePoint lightPoint) { }

    /// <summary>
    /// Called for each full path generated by connecting a camera sub-path and a light sub-path
    /// via a shadow ray.
    /// </summary>
    /// <param name="weight">The sample contribution not yet weighted by MIS</param>
    /// <param name="misWeight">The MIS weight that will be multiplied on the sample weight</param>
    /// <param name="cameraPath">The camera path until the point where NEE was performed</param>
    /// <param name="lightVertex">Last vertex of the camera sub-path that was connected to</param>
    /// <param name="pathPdfs">Surface area pdfs of all sampling techniques. </param>
    protected virtual void OnBidirConnectSample(RgbColor weight, float misWeight, CameraPath cameraPath,
                                                PathVertex lightVertex, in BidirPathPdfs pathPdfs) { }

    /// <summary>
    /// Computes the MIS weight of a light tracer sample, for example via the balance heuristic.
    /// </summary>
    /// <param name="lightVertex">The last vertex of the light sub-path</param>
    /// <param name="pathPdfs">Surface area pdfs of all sampling techniques. </param>
    /// <param name="pixel">The pixel on the image the path contributes to</param>
    /// <param name="distToCam">Distance between the camera and the last light path vertex</param>
    /// <returns>MIS weight of the sampled path</returns>
    public abstract float LightTracerMis(PathVertex lightVertex, in BidirPathPdfs pathPdfs, Pixel pixel, float distToCam);

    /// <summary>
    /// Connects all vertices along all light paths to the camera via shadow rays ("light tracing").
    /// </summary>
    protected void SplatLightVertices() {
        LightPaths.ForEachVertex(ConnectLightVertexToCamera);
    }

    void ConnectLightVertexToCamera(in PathVertex vertex, in PathVertex ancestor, Vector3 dirToAncestor) {
        if (vertex.Depth + 1 < MinDepth) return;

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

        var bsdfValue = shader.Evaluate(dirToAncestor);
        if (bsdfValue == RgbColor.Black)
            return;

        // The shading cosine only cancels out with the Jacobian if the geometry aligns with the shading geometry
        bsdfValue *=
            float.Abs(Vector3.Dot(vertex.Point.ShadingNormal, dirToAncestor)) /
            float.Abs(Vector3.Dot(vertex.Point.Normal, dirToAncestor));

        // Compute the surface area pdf of sampling the previous vertex instead
        var (pdfReverse, _) = shader.Pdf(dirToAncestor);
        if (ancestor.Point.Mesh != null)
            pdfReverse *= SampleWarp.SurfaceAreaToSolidAngle(vertex.Point, ancestor.Point);

        // Gather the PDFs for MIS computation
        int numPdfs = vertex.Depth + 1;
        int lastCameraVertexIdx = -1;
        Span<float> camToLight = stackalloc float[numPdfs];
        Span<float> lightToCam = stackalloc float[numPdfs];
        var pathPdfs = new BidirPathPdfs(LightPaths, lightToCam, camToLight);
        pathPdfs.GatherLightPdfs(vertex, lastCameraVertexIdx);
        pathPdfs.PdfsCameraToLight[0] = response.PdfEmit;
        pathPdfs.PdfsCameraToLight[1] = pdfReverse;
        if (vertex.Depth == 1)
            pathPdfs.PdfNextEvent = NextEventPdf(vertex.Point, ancestor.Point);

        float misWeight = LightTracerMis(vertex, pathPdfs, response.Pixel, distToCam);

        // Compute image contribution and splat
        RgbColor weight = vertex.Weight * bsdfValue * response.Weight / NumLightPaths.Value;
        Scene.FrameBuffer.Splat(response.Pixel, misWeight * weight);

        // Log the sample
        RegisterSample(weight, misWeight, response.Pixel, 0, vertex.Depth, vertex.Depth + 1);
        OnLightTracerSample(weight, misWeight, response.Pixel, vertex, pathPdfs, distToCam);
    }

    /// <summary>
    /// Computes the MIS weight of a bidirectional connection.
    /// </summary>
    /// <param name="cameraPath">The camera path that was connected to a light vertex</param>
    /// <param name="lightVertex">The light vertex that was connected to</param>
    /// <param name="pathPdfs">Surface area pdfs of all sampling techniques. </param>
    /// <returns>MIS weight for the connection</returns>
    public abstract float BidirConnectMis(in CameraPath cameraPath, PathVertex lightVertex, in BidirPathPdfs pathPdfs);

    /// <summary>
    /// Called to importance sample a light path vertex to connect to. Can either select an entire light
    /// path, or an individual vertex. By default yields a deterministic mapping of each camera path to
    /// all vertices of a light path, like classic bidirectional path tracing.
    /// </summary>
    /// <param name="cameraPoint">The camera vertex where a connection is to be made</param>
    /// <param name="outDir">Direction from the camera vertex towards its ancestor</param>
    /// <param name="pixel">The pixel that the camera path originated in</param>
    /// <param name="rng">Random number generator for sampling a vertex</param>
    /// <returns>
    /// Index of the selected light path, index of the vertex within or -1 for all, and the probability
    /// of sampling that vertex.
    /// </returns>
    protected virtual (int, int, float) SelectBidirPath(SurfacePoint cameraPoint, Vector3 outDir,
                                                        Pixel pixel, ref RNG rng) {
        int row = Math.Min(pixel.Row, Scene.FrameBuffer.Height - 1);
        int col = Math.Min(pixel.Col, Scene.FrameBuffer.Width - 1);
        int pixelIndex = row * Scene.FrameBuffer.Width + col;
        return (pixelIndex, -1, 1.0f);
    }

    RgbColor Connect(in SurfaceShader shader, PathVertex vertex, PathVertex ancestor, Vector3 dirToAncestor,
                     in CameraPath path, float reversePdfJacobian, float lightVertexProb) {
        // Only allow connections that do not exceed the maximum total path length
        int depth = vertex.Depth + path.Vertices.Count + 1;
        if (depth > MaxDepth || depth < MinDepth)
            return RgbColor.Black;

        // Trace shadow ray
        if (Scene.Raytracer.IsOccluded(vertex.Point, shader.Point))
            return RgbColor.Black;

        // Compute connection direction
        var dirFromCamToLight = Vector3.Normalize(vertex.Point.Position - shader.Point.Position);

        SurfaceShader lightShader = new(vertex.Point, dirToAncestor, true);
        var bsdfWeightLight = lightShader.Evaluate(-dirFromCamToLight) * float.Abs(Vector3.Dot(vertex.Point.Normal, -dirFromCamToLight));
        bsdfWeightLight *=
            float.Abs(Vector3.Dot(vertex.Point.ShadingNormal, dirToAncestor)) /
            float.Abs(Vector3.Dot(vertex.Point.Normal, dirToAncestor));

        var bsdfWeightCam = shader.EvaluateWithCosine(dirFromCamToLight);

        if (bsdfWeightCam == RgbColor.Black || bsdfWeightLight == RgbColor.Black)
            return RgbColor.Black;

        // Compute the missing pdfs
        var (pdfCameraToLight, pdfCameraReverse) = shader.Pdf(dirFromCamToLight);
        pdfCameraReverse *= reversePdfJacobian;
        pdfCameraToLight *= SampleWarp.SurfaceAreaToSolidAngle(shader.Point, vertex.Point);

        if (pdfCameraToLight == 0) return RgbColor.Black; // TODO figure out how this can happen!

        var (pdfLightToCamera, pdfLightReverse) = lightShader.Pdf(-dirFromCamToLight);
        if (ancestor.Point.Mesh != null) // not when from background
            pdfLightReverse *= SampleWarp.SurfaceAreaToSolidAngle(vertex.Point, ancestor.Point);
        pdfLightToCamera *= SampleWarp.SurfaceAreaToSolidAngle(vertex.Point, shader.Point);

        // Gather all PDFs for MIS compuation
        int numPdfs = path.Vertices.Count + vertex.Depth + 1;
        int lastCameraVertexIdx = path.Vertices.Count - 1;
        Span<float> camToLight = stackalloc float[numPdfs];
        Span<float> lightToCam = stackalloc float[numPdfs];

        var pathPdfs = new BidirPathPdfs(LightPaths, lightToCam, camToLight);
        pathPdfs.GatherCameraPdfs(path, lastCameraVertexIdx);
        pathPdfs.GatherLightPdfs(vertex, lastCameraVertexIdx);
        if (vertex.Depth == 1)
            pathPdfs.PdfNextEvent = NextEventPdf(vertex.Point, ancestor.Point);

        // Set the pdf values that are unique to this combination of paths
        if (lastCameraVertexIdx > 0) // only if this is not the primary hit point
            pathPdfs.PdfsLightToCamera[lastCameraVertexIdx - 1] = pdfCameraReverse;
        pathPdfs.PdfsCameraToLight[lastCameraVertexIdx] = path.Vertices[^1].PdfFromAncestor;
        pathPdfs.PdfsLightToCamera[lastCameraVertexIdx] = pdfLightToCamera;
        pathPdfs.PdfsCameraToLight[lastCameraVertexIdx + 1] = pdfCameraToLight;
        pathPdfs.PdfsCameraToLight[lastCameraVertexIdx + 2] = pdfLightReverse;

        float misWeight = BidirConnectMis(path, vertex, pathPdfs);
        float distanceSqr = (shader.Point.Position - vertex.Point.Position).LengthSquared();

        // Avoid NaNs in rare cases
        if (distanceSqr == 0)
            return RgbColor.Black;

        RgbColor weight = vertex.Weight * bsdfWeightLight * bsdfWeightCam / distanceSqr / lightVertexProb;

        Debug.Assert(float.IsFinite(weight.Average));
        Debug.Assert(float.IsFinite(misWeight));

        RegisterSample(weight * path.Throughput, misWeight, path.Pixel,
                        path.Vertices.Count, vertex.Depth, depth);
        OnBidirConnectSample(weight * path.Throughput, misWeight, path, vertex, pathPdfs);

        return misWeight * weight;
    }

    /// <summary>
    /// Computes the contribution of inner path connections at the given camera path vertex
    /// </summary>
    /// <param name="shader">Shading info at the last vertex of the camera path</param>
    /// <param name="rng">Random number generator</param>
    /// <param name="path">The camera path</param>
    /// <param name="reversePdfJacobian">
    /// Jacobian to convert a solid angle density at the camera vertex to a surface area density at its
    /// ancestor vertex.
    /// </param>
    /// <returns>The sum of all MIS weighted contributions for inner path connections</returns>
    protected virtual RgbColor BidirConnections(in SurfaceShader shader, ref RNG rng,
                                                in CameraPath path, float reversePdfJacobian) {
        RgbColor result = RgbColor.Black;
        if (NumLightPaths == 0) return result;

        // Select a path to connect to (based on pixel index)
        (int lightPathIdx, int lightVertIdx, float lightVertexProb) =
            SelectBidirPath(shader.Point, shader.Context.OutDirWorld, path.Pixel, ref rng);

        if (lightVertIdx > 0) {
            // specific vertex selected
            var vertex = LightPaths[lightVertIdx]; // TODO-PERFORMANCE this is an expensive binary search ATM --> should we instead pick a random path and vertex within? Or precompute a lookup table for this mapping?
            if (vertex.Depth < 1)
                return result;
            var ancestor = LightPaths[lightVertIdx - 1];
            var dirToAncestor = Vector3.Normalize(ancestor.Point.Position - vertex.Point.Position);
            result += Connect(shader, vertex, ancestor, dirToAncestor, path, reversePdfJacobian, lightVertexProb);
        } else if (lightPathIdx >= 0) {
            // Connect with all vertices along the path
            int n = LightPaths.Length(lightPathIdx);
            for (int i = 1; i < n; ++i) {
                var ancestor = LightPaths[lightPathIdx, i - 1];
                var vertex = LightPaths[lightPathIdx, i];
                var dirToAncestor = Vector3.Normalize(ancestor.Point.Position - vertex.Point.Position);
                result += Connect(shader, vertex, ancestor, dirToAncestor, path, reversePdfJacobian, lightVertexProb);
            }
        }

        return result;
    }

    /// <summary>
    /// Computes the MIS weight for next event estimation along a camera path
    /// </summary>
    /// <param name="cameraPath">The camera path</param>
    /// <param name="pathPdfs">Surface area pdfs of all sampling techniques. </param>
    /// <param name="isBackground">True if the path was connected to the background, false if its an area light</param>
    /// <returns>MIS weight</returns>
    public abstract float NextEventMis(in CameraPath cameraPath, in BidirPathPdfs pathPdfs, bool isBackground);

    /// <summary>
    /// Samples an emitter and a point on its surface for next event estimation
    /// </summary>
    /// <param name="from">The shading point</param>
    /// <param name="rng">Random number generator</param>
    /// <returns>The sampled emitter and point on the emitter</returns>
    protected virtual (Emitter, SurfaceSample) SampleNextEvent(SurfacePoint from, ref RNG rng) {
        var (light, lightProb) = SelectLight(from, ref rng);
        var lightSample = light.SampleUniformArea(rng.NextFloat2D());
        lightSample.Pdf *= lightProb;
        return (light, lightSample);
    }

    /// <summary>
    /// Computes the pdf used by <see cref="SampleNextEvent" />
    /// </summary>
    /// <param name="from">The shading point</param>
    /// <param name="to">The point on the light source</param>
    /// <returns>PDF of next event estimation</returns>
    protected virtual float NextEventPdf(SurfacePoint from, SurfacePoint to) {
        float backgroundProbability = ComputeNextEventBackgroundProbability(/*hit*/);
        if (to.Mesh == null) { // Background
            var direction = to.Position - from.Position;
            return Scene.Background.DirectionPdf(direction) * backgroundProbability;
        } else { // Emissive object
            var emitter = Scene.QueryEmitter(to);
            return emitter.PdfUniformArea(to) * SelectLightPmf(from, emitter) * (1 - backgroundProbability);
        }
    }

    /// <summary>
    /// The probability of selecting the background for next event estimation, instead of an emissive
    /// surface.
    /// </summary>
    /// <returns>The background selection probability, by default uniform</returns>
    protected virtual float ComputeNextEventBackgroundProbability(/*SurfacePoint from*/)
    => Scene.Background == null ? 0 : 1 / (1.0f + Scene.Emitters.Count);

    /// <summary>
    /// Performs next event estimation at the end point of a camera path
    /// </summary>
    /// <param name="shader">Surface shading context at the last camera vertex</param>
    /// <param name="rng">Random number generator</param>
    /// <param name="path">The camera path</param>
    /// <param name="reversePdfJacobian">
    /// Jacobian to convert the reverse sampling pdf from the last camera vertex to its ancestor from
    /// solid angle to surface area.
    /// </param>
    /// <returns>MIS weighted next event contribution</returns>
    protected virtual RgbColor PerformNextEventEstimation(in SurfaceShader shader, ref RNG rng,
                                                          in CameraPath path, float reversePdfJacobian) {
        // Gather the PDFs for next event computation (we do it first to avoid code duplication)
        int numPdfs = path.Vertices.Count + 1;
        int lastCameraVertexIdx = numPdfs - 2;
        Span<float> camToLight = stackalloc float[numPdfs];
        Span<float> lightToCam = stackalloc float[numPdfs];
        var pathPdfs = new BidirPathPdfs(LightPaths, lightToCam, camToLight);
        pathPdfs.GatherCameraPdfs(path, lastCameraVertexIdx);
        pathPdfs.PdfsCameraToLight[^2] = path.Vertices[^1].PdfFromAncestor;

        // Decide between background and surface sampling
        float backgroundProbability = ComputeNextEventBackgroundProbability();
        if (rng.NextFloat() < backgroundProbability) { // Connect to the background
            if (Scene.Background == null)
                return RgbColor.Black; // There is no background

            var sample = Scene.Background.SampleDirection(rng.NextFloat2D());
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
                float pdfEmit = LightPaths.ComputeBackgroundPdf(shader.Point.Position, -sample.Direction);

                // Compute the mis weight
                pathPdfs.PdfsLightToCamera[^2] = pdfEmit;
                if (numPdfs > 2) // not for direct illumination
                    pathPdfs.PdfsLightToCamera[^3] = bsdfReversePdf;
                pathPdfs.PdfNextEvent = sample.Pdf;
                pathPdfs.PdfsCameraToLight[^1] = bsdfForwardPdf;
                float misWeight = NextEventMis(path, pathPdfs, true);

                // Compute and log the final sample weight
                var weight = sample.Weight * bsdfTimesCosine;
                RegisterSample(weight * path.Throughput, misWeight, path.Pixel,
                               path.Vertices.Count, 0, path.Vertices.Count + 1);
                OnNextEventSample(weight * path.Throughput, misWeight, path, sample.Pdf,
                    bsdfForwardPdf, pathPdfs, null, -sample.Direction,
                    new() { Position = shader.Point.Position });
                return misWeight * weight;
            }
        } else { // Connect to an emissive surface
            if (Scene.Emitters.Count == 0)
                return RgbColor.Black;

            // Sample a point on the light source
            var (light, lightSample) = SampleNextEvent(shader.Point, ref rng);
            lightSample.Pdf *= (1 - backgroundProbability);

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

                float pdfEmit = LightPaths.ComputeEmitterPdf(light, lightSample.Point, lightToSurface,
                    SampleWarp.SurfaceAreaToSolidAngle(lightSample.Point, shader.Point));

                pathPdfs.PdfsLightToCamera[^2] = pdfEmit;
                if (numPdfs > 2) // not for direct illumination
                    pathPdfs.PdfsLightToCamera[^3] = bsdfReversePdf;
                pathPdfs.PdfNextEvent = lightSample.Pdf;
                pathPdfs.PdfsCameraToLight[^1] = bsdfForwardPdf;

                float misWeight = NextEventMis(path, pathPdfs, false);

                var weight = emission * bsdfTimesCosine * (jacobian / lightSample.Pdf);
                RegisterSample(weight * path.Throughput, misWeight, path.Pixel,
                               path.Vertices.Count, 0, path.Vertices.Count + 1);
                OnNextEventSample(weight * path.Throughput, misWeight, path, lightSample.Pdf,
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
    /// <returns>MIS weight</returns>
    public abstract float EmitterHitMis(in CameraPath cameraPath, in BidirPathPdfs pathPdfs);

    /// <summary>
    /// Called when a camera path directly intersected an emitter
    /// </summary>
    /// <param name="emitter">The emitter that was hit</param>
    /// <param name="hit">The hit point on the emitter</param>
    /// <param name="outDir">Direction from the illuminated surface towards the point on the light</param>
    /// <param name="path">The camera path</param>
    /// <param name="reversePdfJacobian">
    /// Geometry term to convert a solid angle density on the emitter to a surface area density for
    /// sampling the previous point along the camera path
    /// </param>
    /// <returns>MIS weighted contribution of the emitter</returns>
    protected virtual RgbColor OnEmitterHit(Emitter emitter, SurfacePoint hit, Vector3 outDir,
                                            CameraPath path, float reversePdfJacobian) {
        var emission = emitter.EmittedRadiance(hit, outDir);

        // Compute pdf values
        float pdfEmit = LightPaths.ComputeEmitterPdf(emitter, hit, outDir, reversePdfJacobian);
        float pdfNextEvent = NextEventPdf(new SurfacePoint(), hit); // TODO get the actual previous point!

        int numPdfs = path.Vertices.Count;
        int lastCameraVertexIdx = numPdfs - 1;
        Span<float> camToLight = stackalloc float[numPdfs];
        Span<float> lightToCam = stackalloc float[numPdfs];
        var pathPdfs = new BidirPathPdfs(LightPaths, lightToCam, camToLight);
        pathPdfs.GatherCameraPdfs(path, lastCameraVertexIdx);
        if (numPdfs > 1)
            pathPdfs.PdfsLightToCamera[^2] = pdfEmit;
        pathPdfs.PdfNextEvent = pdfNextEvent;
        pathPdfs.PdfsCameraToLight[^1] = path.Vertices[^1].PdfFromAncestor;

        float misWeight = numPdfs == 1 ? 1.0f : EmitterHitMis(path, pathPdfs);
        RegisterSample(emission * path.Throughput, misWeight, path.Pixel,
                       path.Vertices.Count, 0, path.Vertices.Count);
        OnEmitterHitSample(emission * path.Throughput, misWeight, path, pdfNextEvent, pathPdfs, emitter, outDir, hit);
        return misWeight * emission;
    }

    /// <summary>
    /// Called when a camera path left the scene
    /// </summary>
    /// <param name="ray">The last ray of the camera path that intersected nothing</param>
    /// <param name="path">The camera path</param>
    /// <returns>MIS weighted contribution from the background</returns>
    protected virtual RgbColor OnBackgroundHit(Ray ray, in CameraPath path) {
        if (Scene.Background == null)
            return RgbColor.Black;

        // Compute the pdf of sampling the previous point by emission from the background
        float pdfEmit = LightPaths.ComputeBackgroundPdf(ray.Origin, -ray.Direction);

        // Compute the pdf of sampling the same connection via next event estimation
        float pdfNextEvent = Scene.Background.DirectionPdf(ray.Direction);
        float backgroundProbability = ComputeNextEventBackgroundProbability(/*hit*/);
        pdfNextEvent *= backgroundProbability;

        int numPdfs = path.Vertices.Count;
        int lastCameraVertexIdx = numPdfs - 1;
        Span<float> camToLight = stackalloc float[numPdfs];
        Span<float> lightToCam = stackalloc float[numPdfs];
        var pathPdfs = new BidirPathPdfs(LightPaths, lightToCam, camToLight);
        pathPdfs.GatherCameraPdfs(path, lastCameraVertexIdx);
        if (numPdfs > 1)
            pathPdfs.PdfsLightToCamera[^2] = pdfEmit;
        pathPdfs.PdfNextEvent = pdfNextEvent;
        pathPdfs.PdfsCameraToLight[^1] = path.Vertices[^1].PdfFromAncestor;

        float misWeight = numPdfs == 1 ? 1.0f : EmitterHitMis(path, pathPdfs);
        var emission = Scene.Background.EmittedRadiance(ray.Direction);
        RegisterSample(emission * path.Throughput, misWeight, path.Pixel,
                       path.Vertices.Count, 0, path.Vertices.Count);
        OnEmitterHitSample(emission * path.Throughput, misWeight, path, pdfNextEvent, pathPdfs, null,
            -ray.Direction, new() { Position = ray.Origin });
        return misWeight * emission * path.Throughput;
    }

    /// <summary>
    /// Called for each surface intersection along a camera path
    /// </summary>
    /// <param name="path">The camera path</param>
    /// <param name="rng">Random number generator</param>
    /// <param name="shader">The surface shading context data (normals, material, ...)</param>
    /// <param name="pdfFromAncestor">Solid angle pdf at the previous vertex to sample this ray</param>
    /// <param name="throughput">
    /// Product of geometry terms and BSDFs along the path, divided by sampling pdfs.
    /// </param>
    /// <param name="depth">The number of edges along the path, 1 for the first intersection</param>
    /// <param name="toAncestorJacobian">
    /// Geometry term to convert a solid angle density at this hit point to a surface area density for
    /// sampling its ancestor.
    /// </param>
    /// <returns>Sum of MIS weighted contributions for all sampling techniques performed here</returns>
    protected abstract RgbColor OnCameraHit(in CameraPath path, ref RNG rng, in SurfaceShader shader,
                                            float pdfFromAncestor, RgbColor throughput, int depth,
                                            float toAncestorJacobian);

    /// <summary>
    /// Called for each termination of the camera path
    /// </summary>
    /// <param name="path">The camera path</param>
    protected virtual void OnCameraPathTerminate(in CameraPath path) { }

    protected class CameraRandomWalk(BidirBase<CameraPayloadType> integrator) : RandomWalk<CameraPath>.RandomWalkModifier {
        ThreadLocal<PathBuffer<PathPdfPair>> threadLocalVertices = new(() => new(16));
        ThreadLocal<PathBuffer<float>> threadLocalDistances = new(() => new(16));

        public override void OnStartCamera(ref RandomWalk<CameraPath> walk, CameraRaySample cameraRay, Pixel filmPosition) {
            threadLocalVertices.Value.Clear();
            threadLocalDistances.Value.Clear();

            walk.Payload.Vertices = threadLocalVertices.Value;
            walk.Payload.Distances = threadLocalDistances.Value;
            walk.Payload.Pixel = filmPosition;
            walk.Payload.CurrentPoint = cameraRay.Point;
        }

        public override RgbColor OnInvalidHit(ref RandomWalk<CameraPath> walk, Ray ray, float pdfFromAncestor,
                                              RgbColor throughput, int depth) {
            walk.Payload.Vertices.Add(new PathPdfPair {
                PdfFromAncestor = pdfFromAncestor,
                PdfToAncestor = 0
            });
            walk.Payload.Throughput = throughput;
            walk.Payload.Distances.Add(float.PositiveInfinity);
            return integrator.OnBackgroundHit(ray, walk.Payload);
        }

        public override RgbColor OnHit(ref RandomWalk<CameraPath> walk, in SurfaceShader shader, float pdfFromAncestor,
                                       RgbColor throughput, int depth, float toAncestorJacobian) {
            if (depth == 1 && integrator.EnableDenoiser) {
                var albedo = shader.GetScatterStrength();
                integrator.DenoiseBuffers.LogPrimaryHit(walk.Payload.Pixel, albedo, shader.Context.Normal);
            }

            if (depth == 1) {
                // Compute the pixel footprint (ignoring aspect ratios, approximated based on the camera Jacobian)
                walk.Payload.FootprintRadius = float.Sqrt(1 / pdfFromAncestor);
            }

            walk.Payload.Vertices.Add(new PathPdfPair {
                PdfFromAncestor = pdfFromAncestor,
                PdfToAncestor = 0
            });
            walk.Payload.Throughput = throughput;
            walk.Payload.Distances.Add(shader.Point.Distance);

            walk.Payload.MaximumPriorRoughness = MathF.Max(walk.Payload.CurrentRoughness, walk.Payload.MaximumPriorRoughness);
            walk.Payload.CurrentRoughness = shader.GetRoughness();

            walk.Payload.PreviousPoint = walk.Payload.CurrentPoint;
            walk.Payload.CurrentPoint = shader.Point;
            return integrator.OnCameraHit(walk.Payload, ref walk.rng, shader, pdfFromAncestor, throughput, depth, toAncestorJacobian);
        }

        public override void OnContinue(ref RandomWalk<CameraPath> walk, float pdfToAncestor, int depth) {
            // Update the reverse pdf of the previous vertex.
            var lastVert = walk.Payload.Vertices[^1];
            walk.Payload.Vertices[^1] = new PathPdfPair {
                PdfFromAncestor = lastVert.PdfFromAncestor,
                PdfToAncestor = pdfToAncestor
            };
        }

        public override void OnTerminate(ref RandomWalk<CameraPath> walk) {
            integrator.OnCameraPathTerminate(walk.Payload);
        }
    }
}