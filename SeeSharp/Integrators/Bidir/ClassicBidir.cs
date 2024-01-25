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
    protected override (Emitter, SurfaceSample) SampleNextEvent(SurfacePoint from, ref RNG rng) {
        var (light, sample) = base.SampleNextEvent(from, ref rng);
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
    protected override RgbColor OnCameraHit(in CameraPath path, ref RNG rng, in SurfaceShader shader,
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
            value += throughput * BidirConnections(shader, ref rng, path, toAncestorJacobian);

        if (depth < MaxDepth && depth + 1 >= MinDepth) {
            for (int i = 0; i < NumShadowRays; ++i) {
                value += throughput * PerformNextEventEstimation(shader, ref rng, path, toAncestorJacobian);
            }
        }

        return value;
    }

    /// <inheritdoc />
    public override float EmitterHitMis(in CameraPath cameraPath, float pdfNextEvent, in BidirPathPdfs pathPdfs) {
        float pdfThis = cameraPath.Vertices[^1].PdfFromAncestor;

        float sumReciprocals = 1.0f;
        sumReciprocals += pdfNextEvent / pdfThis;
        sumReciprocals +=
            CameraPathReciprocals(cameraPath.Vertices.Count - 2, pathPdfs, cameraPath.Pixel) / pdfThis;

        return 1 / sumReciprocals;
    }

    /// <inheritdoc />
    public override float LightTracerMis(PathVertex lightVertex, in BidirPathPdfs pathPdfs, Pixel pixel, float distToCam) {
        float sumReciprocals = 1 + LightPathReciprocals(-1, pathPdfs, pixel) / NumLightPaths.Value;
        return 1 / sumReciprocals;
    }

    /// <inheritdoc />
    public override float BidirConnectMis(in CameraPath cameraPath, PathVertex lightVertex, in BidirPathPdfs pathPdfs) {
        float sumReciprocals = 1.0f;
        int lastCameraVertexIdx = cameraPath.Vertices.Count - 1;
        sumReciprocals += CameraPathReciprocals(lastCameraVertexIdx, pathPdfs, cameraPath.Pixel);
        sumReciprocals += LightPathReciprocals(lastCameraVertexIdx, pathPdfs, cameraPath.Pixel);
        return 1 / sumReciprocals;
    }

    /// <inheritdoc />
    public override float NextEventMis(in CameraPath cameraPath, float pdfNextEvent, float pdfHit,
                                       in BidirPathPdfs pathPdfs, bool isBackground) {
        float sumReciprocals = 1.0f;
        sumReciprocals += pdfHit / pdfNextEvent;
        sumReciprocals +=
            CameraPathReciprocals(cameraPath.Vertices.Count - 1, pathPdfs, cameraPath.Pixel) / pdfNextEvent;
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
    protected virtual float CameraPathReciprocals(int lastCameraVertexIdx, in BidirPathPdfs pdfs, Pixel pixel) {
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
    protected virtual float LightPathReciprocals(int lastCameraVertexIdx, in BidirPathPdfs pdfs, Pixel pixel) {
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