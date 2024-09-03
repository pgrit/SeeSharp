namespace SeeSharp.Integrators.Bidir;

/// <summary>
/// Variation of the bidirectional path tracer that uses the "Light vertex cache" proposed
/// by Davidovic et al [2014] "Progressive Light Transport Simulation on the GPU: Survey and Improvements".
/// A good basis for algorithms that want to control the number of connections, or the resampling logic.
/// </summary>
public class VertexCacheBidir : VertexCacheBidirBase<byte> {}

/// <summary>
/// Variation of the bidirectional path tracer that uses the "Light vertex cache" proposed
/// by Davidovic et al [2014] "Progressive Light Transport Simulation on the GPU: Survey and Improvements".
/// A good basis for algorithms that want to control the number of connections, or the resampling logic.
/// </summary>
public class VertexCacheBidirBase<CameraPayloadType> : BidirBase<CameraPayloadType> {
    /// <summary>
    /// Number of connections to make
    /// </summary>
    public int NumConnections = 1;

    /// <summary>
    /// Number of shadow rays to use for next event. Disabled if zero.
    /// </summary>
    public int NumShadowRays = 1;

    /// <summary>
    /// Set to false to disable connections between light vertices and the camera
    /// </summary>
    public bool EnableLightTracer = true;

    /// <summary>
    /// Set to false to disable contributions from camera subpaths intersecting a light
    /// </summary>
    public bool EnableHitting = true;

    /// <summary>
    /// If set to true, renders all techniques for all path lengths as separate images, with and without MIS.
    /// This is expensive and should only be used for debugging purposes.
    /// </summary>
    public bool RenderTechniquePyramid = false;

    TechPyramid techPyramidRaw;
    TechPyramid techPyramidWeighted;

    /// <summary>
    /// The light vertex resampler
    /// </summary>
    protected VertexSelector vertexSelector;

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
    protected override (int, int, float) SelectBidirPath(SurfacePoint cameraPoint, Vector3 outDir,
                                                         Pixel pixel, ref RNG rng) {
        // Select a single vertex from the entire cache at random
        var (path, vertex) = vertexSelector.Select(ref rng);
        return (path, vertex, BidirSelectDensity(pixel));
    }

    /// <summary>
    /// Computes the effective density of selecting a light path vertex for connection.
    /// That is, the product of the selection probability and the number of samples.
    /// </summary>
    /// <returns>Effective density</returns>
    public virtual float BidirSelectDensity(Pixel pixel) {
        if (vertexSelector.Count == 0) return 0;

        // We select light path vertices uniformly
        float selectProb = 1.0f / vertexSelector.Count;

        // There are "NumLightPaths" samples that could have generated the selected vertex,
        // we repeat the process "NumConnections" times
        float numSamples = NumConnections * NumLightPaths.Value;

        return selectProb * numSamples;
    }

    /// <summary>Adds the contribution to the technique pyramid, if enabled</summary>
    /// <inheritdoc />
    protected override void RegisterSample(RgbColor weight, float misWeight, Pixel pixel,
                                           int cameraPathLength, int lightPathLength, int fullLength) {
        if (!RenderTechniquePyramid)
            return;

        // Technique pyramids are rendered across all iterations
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

        base.Render(scene);

        if (RenderTechniquePyramid) {
            techPyramidRaw.WriteToFiles(Path.Join(scene.FrameBuffer.Basename, "techs-raw"));
            techPyramidWeighted.WriteToFiles(Path.Join(scene.FrameBuffer.Basename, "techs-weighted"));
        }
    }

    /// <summary>
    /// Fraction of light paths that never properly started because their initial ray sampling failed
    /// </summary>
    public float InvalidLightPathFraction { get; private set; }

    /// <summary>
    /// Creates the vertex resampler and computes the light tracing contribution.
    /// </summary>
    protected override void ProcessPathCache() {
        if (NumConnections > 0) vertexSelector = new VertexSelector(LightPaths);
        if (EnableLightTracer) SplatLightVertices();

        // For debug purposes: count the number of paths that never started
        int numEmpty = 0;
        for (int i = 0; i < LightPaths.NumPaths; ++i) {
            if (LightPaths.Length(i) == 0)
                numEmpty++;
        }
        InvalidLightPathFraction = numEmpty / (float)LightPaths.NumPaths;
    }

    /// <inheritdoc />
    protected override RgbColor OnCameraHit(in CameraPath path, ref RNG rng, in SurfaceShader shader,
                                            float pdfFromAncestor, RgbColor throughput, int depth,
                                            float toAncestorJacobian) {
        RgbColor value = RgbColor.Black;

        // Was a light hit?
        Emitter light = Scene.QueryEmitter(shader.Point);
        if (light != null && EnableHitting && depth >= MinDepth) {
            value += throughput * OnEmitterHit(light, shader.Point, shader.Context.OutDirWorld, path, toAncestorJacobian);
        }

        // Perform connections if the maximum depth has not yet been reached
        if (depth < MaxDepth) {
            for (int i = 0; i < NumConnections; ++i) {
                value += throughput * BidirConnections(shader, ref rng, path, toAncestorJacobian);
            }
        }

        if (depth < MaxDepth && depth + 1 >= MinDepth) {
            for (int i = 0; i < NumShadowRays; ++i) {
                value += throughput * PerformNextEventEstimation(shader, ref rng, path, toAncestorJacobian);
            }
        }

        return value;
    }

    /// <inheritdoc />
    public override float EmitterHitMis(in CameraPath cameraPath, in BidirPathPdfs pathPdfs) {
        float pdfThis = pathPdfs.PdfsCameraToLight[^1];

        float sumReciprocals = 1.0f;
        sumReciprocals += pathPdfs.PdfNextEvent / pdfThis;
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
        sumReciprocals += CameraPathReciprocals(lastCameraVertexIdx, pathPdfs, cameraPath.Pixel)
            / BidirSelectDensity(cameraPath.Pixel);
        sumReciprocals += LightPathReciprocals(lastCameraVertexIdx, pathPdfs, cameraPath.Pixel)
            / BidirSelectDensity(cameraPath.Pixel);
        return 1 / sumReciprocals;
    }

    /// <inheritdoc />
    public override float NextEventMis(in CameraPath cameraPath, in BidirPathPdfs pathPdfs, bool isBackground) {
        float sumReciprocals = 1.0f;
        if (EnableHitting)
            sumReciprocals += pathPdfs.PdfsCameraToLight[^1] / pathPdfs.PdfNextEvent;
        sumReciprocals +=
            CameraPathReciprocals(cameraPath.Vertices.Count - 1, pathPdfs, cameraPath.Pixel) / pathPdfs.PdfNextEvent;
        return 1 / sumReciprocals;
    }

    /// <inheritdoc />
    protected virtual float CameraPathReciprocals(int lastCameraVertexIdx, in BidirPathPdfs pdfs, Pixel pixel) {
        float sumReciprocals = 0.0f;
        float nextReciprocal = 1.0f;
        for (int i = lastCameraVertexIdx; i > 0; --i) { // all bidir connections
            nextReciprocal *= pdfs.PdfsLightToCamera[i] / pdfs.PdfsCameraToLight[i];
            if (NumConnections > 0)
                sumReciprocals += nextReciprocal * BidirSelectDensity(pixel);
        }
        if (EnableLightTracer)
            sumReciprocals +=
                nextReciprocal * pdfs.PdfsLightToCamera[0] / pdfs.PdfsCameraToLight[0] * NumLightPaths.Value;
        return sumReciprocals;
    }

    /// <inheritdoc />
    protected virtual float LightPathReciprocals(int lastCameraVertexIdx, in BidirPathPdfs pdfs, Pixel pixel) {
        float sumReciprocals = 0.0f;
        float nextReciprocal = 1.0f;
        for (int i = lastCameraVertexIdx + 1; i < pdfs.NumPdfs; ++i) {
             if (i == pdfs.NumPdfs - 1) // Next event
                sumReciprocals += nextReciprocal * pdfs.PdfNextEvent / pdfs.PdfsLightToCamera[i];
            nextReciprocal *= pdfs.PdfsCameraToLight[i] / pdfs.PdfsLightToCamera[i];
            if (i < pdfs.NumPdfs - 2 && NumConnections > 0) // Connections to the emitter (next event) are treated separately
                sumReciprocals += nextReciprocal * BidirSelectDensity(pixel);
        }
        if (EnableHitting) sumReciprocals += nextReciprocal; // Hitting the emitter directly
        return sumReciprocals;
    }
}
