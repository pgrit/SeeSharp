namespace SeeSharp.Integrators.Bidir;

/// <summary>
/// Implements classic bidirectional path tracing as proposed by Veach and Guibas. Each camera path is
/// paired with a light path, and all vertices of both paths are connected pair-wise.
/// </summary>
public class ClassicBidir : BidirBase {
    /// <summary>
    /// Set to true to output the raw images and MIS weighted images of all individual sampling
    /// techniques. The images will be written to a folder with the same name as the output file.
    /// Should only be used for debugging, as it is quite expensive.
    /// </summary>
    public bool RenderTechniquePyramid = false;

    /// <summary>
    /// If set to false, the inner vertices of the paths are no longer connected. That is, only light
    /// tracing and path tracing with next event are enabled.
    /// </summary>
    public bool EnableConnections = true;

    /// <summary>
    /// If set to false, light path vertices are no longer connected directly to the camera.
    /// </summary>
    public bool EnableLightTracer = true;

    /// <summary>
    /// Number of shadow rays to use for next event estimation along the camera path. If set to zero,
    /// no next event estimation is performed.
    /// </summary>
    public int NumShadowRays = 1;

    TechPyramid techPyramidRaw;
    TechPyramid techPyramidWeighted;

    /// <inheritdoc />
    protected override float NextEventPdf(SurfacePoint from, SurfacePoint to) {
        return base.NextEventPdf(from, to) * NumShadowRays;
    }

    /// <inheritdoc />
    protected override (Emitter, SurfaceSample) SampleNextEvent(SurfacePoint from, RNG rng) {
        var (light, sample) = base.SampleNextEvent(from, rng);
        sample.Pdf *= NumShadowRays;
        return (light, sample);
    }

    /// <inheritdoc />
    protected override void RegisterSample(RgbColor weight, float misWeight, Pixel pixel,
                                           int cameraPathLength, int lightPathLength, int fullLength) {
        if (!RenderTechniquePyramid)
            return;
        weight /= NumIterations;
        techPyramidRaw.Add(cameraPathLength, lightPathLength, fullLength, pixel, weight);
        techPyramidWeighted.Add(cameraPathLength, lightPathLength, fullLength, pixel, weight * misWeight);
    }

    /// <inheritdoc />
    public override void Render(Scene scene) {
        if (RenderTechniquePyramid) {
            techPyramidRaw = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                                             minDepth: 1, maxDepth: MaxDepth, merges: false);
            techPyramidWeighted = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                                                  minDepth: 1, maxDepth: MaxDepth, merges: false);
        }

        if (NumLightPaths.HasValue && NumLightPaths.Value != scene.FrameBuffer.Width * scene.FrameBuffer.Height) {
            throw new ArgumentOutOfRangeException(nameof(NumLightPaths), NumLightPaths,
                "Classic Bidir requires exactly one light path for every camera path");
        }

        base.Render(scene);

        // Store the technique pyramids
        if (RenderTechniquePyramid) {
            string pathRaw = Path.Join(scene.FrameBuffer.Basename, "techs-raw");
            techPyramidRaw.WriteToFiles(pathRaw);

            string pathWeighted = Path.Join(scene.FrameBuffer.Basename, "techs-weighted");
            techPyramidWeighted.WriteToFiles(pathWeighted);
        }
    }

    /// <inheritdoc />
    protected override void ProcessPathCache() {
        if (EnableLightTracer) SplatLightVertices();
    }

    /// <inheritdoc />
    protected override RgbColor OnCameraHit(CameraPath path, RNG rng, in SurfaceShader shader,
                                            float pdfFromAncestor, RgbColor throughput, int depth,
                                            float toAncestorJacobian) {
        RgbColor value = RgbColor.Black;

        // Was a light hit?
        Emitter light = Scene.QueryEmitter(shader.Point);
        if (light != null && depth >= MinDepth) {
            value += throughput * OnEmitterHit(light, shader.Point, shader.Context.OutDirWorld, path, toAncestorJacobian);
        }

        // Perform connections if the maximum depth has not yet been reached
        if (depth < MaxDepth && EnableConnections)
            value += throughput * BidirConnections(shader, rng, path, toAncestorJacobian);

        if (depth < MaxDepth && depth + 1 >= MinDepth) {
            for (int i = 0; i < NumShadowRays; ++i) {
                value += throughput * PerformNextEventEstimation(shader, rng, path, toAncestorJacobian);
            }
        }

        return value;
    }

    /// <inheritdoc />
    public override float EmitterHitMis(CameraPath cameraPath, float pdfEmit, float pdfNextEvent) {
        int numPdfs = cameraPath.Vertices.Count;
        int lastCameraVertexIdx = numPdfs - 1;
        Span<float> camToLight = stackalloc float[numPdfs];
        Span<float> lightToCam = stackalloc float[numPdfs];

        if (numPdfs == 1) return 1.0f; // sole technique for rendering directly visible lights.

        var pathPdfs = new BidirPathPdfs(LightPaths.PathCache, lightToCam, camToLight);
        pathPdfs.GatherCameraPdfs(cameraPath, lastCameraVertexIdx);

        pathPdfs.PdfsLightToCamera[^2] = pdfEmit;

        float pdfThis = cameraPath.Vertices[^1].PdfFromAncestor;

        // Compute the actual weight
        float sumReciprocals = 1.0f;

        // Next event estimation
        sumReciprocals += pdfNextEvent / pdfThis;

        // All connections along the camera path
        sumReciprocals +=
            CameraPathReciprocals(lastCameraVertexIdx - 1, pathPdfs, cameraPath.Pixel) / pdfThis;

        return 1 / sumReciprocals;
    }

    /// <inheritdoc />
    public override float LightTracerMis(PathVertex lightVertex, float pdfCamToPrimary, float pdfReverse,
                                         float pdfNextEvent, Pixel pixel, float distToCam) {
        int numPdfs = lightVertex.Depth + 1;
        int lastCameraVertexIdx = -1;
        Span<float> camToLight = stackalloc float[numPdfs];
        Span<float> lightToCam = stackalloc float[numPdfs];

        var pathPdfs = new BidirPathPdfs(LightPaths.PathCache, lightToCam, camToLight);

        pathPdfs.GatherLightPdfs(lightVertex, lastCameraVertexIdx);

        pathPdfs.PdfsCameraToLight[0] = pdfCamToPrimary;
        pathPdfs.PdfsCameraToLight[1] = pdfReverse + pdfNextEvent;

        // Compute the actual weight
        float sumReciprocals = LightPathReciprocals(lastCameraVertexIdx, pathPdfs, pixel);
        sumReciprocals /= NumLightPaths.Value;
        sumReciprocals += 1;

        return 1 / sumReciprocals;
    }

    /// <inheritdoc />
    public override float BidirConnectMis(CameraPath cameraPath, PathVertex lightVertex,
                                          float pdfCameraReverse, float pdfCameraToLight,
                                          float pdfLightReverse, float pdfLightToCamera,
                                          float pdfNextEvent) {
        int numPdfs = cameraPath.Vertices.Count + lightVertex.Depth + 1;
        int lastCameraVertexIdx = cameraPath.Vertices.Count - 1;
        Span<float> camToLight = stackalloc float[numPdfs];
        Span<float> lightToCam = stackalloc float[numPdfs];

        var pathPdfs = new BidirPathPdfs(LightPaths.PathCache, lightToCam, camToLight);
        pathPdfs.GatherCameraPdfs(cameraPath, lastCameraVertexIdx);
        pathPdfs.GatherLightPdfs(lightVertex, lastCameraVertexIdx);

        // Set the pdf values that are unique to this combination of paths
        if (lastCameraVertexIdx > 0) // only if this is not the primary hit point
            pathPdfs.PdfsLightToCamera[lastCameraVertexIdx - 1] = pdfCameraReverse;
        pathPdfs.PdfsCameraToLight[lastCameraVertexIdx] = cameraPath.Vertices[^1].PdfFromAncestor;
        pathPdfs.PdfsLightToCamera[lastCameraVertexIdx] = pdfLightToCamera;
        pathPdfs.PdfsCameraToLight[lastCameraVertexIdx + 1] = pdfCameraToLight;
        pathPdfs.PdfsCameraToLight[lastCameraVertexIdx + 2] = pdfLightReverse + pdfNextEvent;

        // Compute reciprocals for hypothetical connections along the camera sub-path
        float sumReciprocals = 1.0f;
        sumReciprocals += CameraPathReciprocals(lastCameraVertexIdx, pathPdfs, cameraPath.Pixel);
        sumReciprocals += LightPathReciprocals(lastCameraVertexIdx, pathPdfs, cameraPath.Pixel);

        return 1 / sumReciprocals;
    }

    /// <inheritdoc />
    public override float NextEventMis(CameraPath cameraPath, float pdfEmit, float pdfNextEvent,
                                       float pdfHit, float pdfReverse) {
        int numPdfs = cameraPath.Vertices.Count + 1;
        int lastCameraVertexIdx = numPdfs - 2;
        Span<float> camToLight = stackalloc float[numPdfs];
        Span<float> lightToCam = stackalloc float[numPdfs];

        var pathPdfs = new BidirPathPdfs(LightPaths.PathCache, lightToCam, camToLight);

        pathPdfs.GatherCameraPdfs(cameraPath, lastCameraVertexIdx);

        pathPdfs.PdfsCameraToLight[^2] = cameraPath.Vertices[^1].PdfFromAncestor;
        pathPdfs.PdfsLightToCamera[^2] = pdfEmit;
        if (numPdfs > 2) // not for direct illumination
            pathPdfs.PdfsLightToCamera[^3] = pdfReverse;

        // Compute the actual weight
        float sumReciprocals = 1.0f;

        // Hitting the light source
        sumReciprocals += pdfHit / pdfNextEvent;

        // All bidirectional connections
        sumReciprocals +=
            CameraPathReciprocals(lastCameraVertexIdx, pathPdfs, cameraPath.Pixel) / pdfNextEvent;

        return 1 / sumReciprocals;
    }

    /// <summary>
    /// Computes the sum of pdf ratios needed for the balance heuristic weight, along the camera prefix.
    /// </summary>
    /// <param name="lastCameraVertexIdx">
    /// Index of the last vertex that was sampled from the camera, identifies the current technique.
    /// </param>
    /// <param name="pdfs">The sampling pdfs along the path</param>
    /// <param name="pixel">The pixel that this sample contributes to</param>
    /// <returns>Sum of the pdfs of all other techniques divided by the current technique.</returns>
    protected virtual float CameraPathReciprocals(int lastCameraVertexIdx, BidirPathPdfs pdfs, Pixel pixel) {
        float sumReciprocals = 0.0f;
        float nextReciprocal = 1.0f;
        for (int i = lastCameraVertexIdx; i > 0; --i) { // all bidir connections
            nextReciprocal *= pdfs.PdfsLightToCamera[i] / pdfs.PdfsCameraToLight[i];
            if (EnableConnections) sumReciprocals += nextReciprocal;
        }
        // Light tracer
        if (EnableLightTracer)
            sumReciprocals +=
                nextReciprocal * pdfs.PdfsLightToCamera[0] / pdfs.PdfsCameraToLight[0] * NumLightPaths.Value;
        return sumReciprocals;
    }

    /// <summary>
    /// Computes the sum of pdf ratios needed for the balance heuristic weight, along the light suffix.
    /// </summary>
    /// <param name="lastCameraVertexIdx">
    /// Index of the last vertex that was sampled from the camera, identifies the current technique.
    /// </param>
    /// <param name="pdfs">The sampling pdfs along the path</param>
    /// <param name="pixel">The pixel that this sample contributes to</param>
    /// <returns>Sum of the pdfs of all other techniques divided by the current technique.</returns>
    protected virtual float LightPathReciprocals(int lastCameraVertexIdx, BidirPathPdfs pdfs, Pixel pixel) {
        float sumReciprocals = 0.0f;
        float nextReciprocal = 1.0f;
        for (int i = lastCameraVertexIdx + 1; i < pdfs.NumPdfs; ++i) {
            nextReciprocal *= pdfs.PdfsCameraToLight[i] / pdfs.PdfsLightToCamera[i];
            if (EnableConnections && i < pdfs.NumPdfs - 2) // Next event is treated separately
                sumReciprocals += nextReciprocal;
        }
        sumReciprocals += nextReciprocal; // Next event and hitting the emitter directly
        return sumReciprocals;
    }
}