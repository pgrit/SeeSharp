namespace SeeSharp.Integrators.Bidir;

/// <summary>
/// Implements vertex connection and merging (VCM). An MIS combination of bidirectional path tracing
/// (we are using the vertex caching flavor) and photon mapping (aka merging).
/// </summary>
public class VertexConnectionAndMerging : VertexCacheBidir {
    /// <summary>Whether or not to use merging at the first hit from the camera.</summary>
    public bool MergePrimary = false;

    /// <summary>
    /// If set to false, no merging (aka photon mapping) is performed. Just plain old BDPT.
    /// </summary>
    public bool EnableMerging = true;

    /// <summary>
    /// Maximum number of nearest neighbor photons to search for.
    /// </summary>
    public int MaxNumPhotons = 8;

    TechPyramid techPyramidRaw;
    TechPyramid techPyramidWeighted;

    /// <summary>
    /// The maximum radius used by any merge in the current iteration. Only initialized after rendering started.
    /// </summary>
    public float MaximumRadius { get; protected set; }

    /// <summary>
    /// Indices of the photons in the scene. To be used in conjunction with <see cref="photonMap"/>
    /// </summary>
    protected List<(int PathIndex, int VertexIndex)> photons = new();

    /// <summary>
    /// Acceleration structure to query photons in the scene.
    /// </summary>
    protected NearestNeighborSearch photonMap;

    long TotalCameraPathLength = 0;
    long TotalMergeOperations = 0;
    long TotalMergePhotons = 0;

    /// <summary>
    /// Average number of edges along the camera subpaths
    /// </summary>
    public float AverageCameraPathLength { get; private set; } = 0;

    /// <summary>
    /// Average number of edges along the light subpaths
    /// </summary>
    public float AverageLightPathLength { get; private set; } = 0;

    /// <summary>
    /// Average number of photons found by each merging operation. Depends on the average light path length
    /// and the total number of light subpaths.
    /// </summary>
    public float AveragePhotonsPerQuery { get; private set; } = 0;

    /// <summary>
    /// Initializes the radius for photon mapping. The default implementation samples three rays
    /// on the diagonal of the image. The average pixel footprints at these positions are used to compute
    /// a radius that roughly spans a 3x3 pixel area in the image plane.
    /// </summary>
    protected virtual void InitializeRadius(Scene scene) {
        // Sample a small number of primary rays and compute the average pixel footprint area
        var primarySamples = new[] {
                new Vector2(0.5f, 0.5f),
                new Vector2(0.25f, 0.25f),
                new Vector2(0.75f, 0.75f),
                new Vector2(0.1f, 0.9f),
                new Vector2(0.9f, 0.1f)
            };
        var resolution = new Vector2(scene.FrameBuffer.Width, scene.FrameBuffer.Height);
        float averageArea = 0; int numHits = 0;
        RNG dummyRng = new RNG();
        for (int i = 0; i < primarySamples.Length; ++i) {
            Ray ray = scene.Camera.GenerateRay(primarySamples[i] * resolution, dummyRng).Ray;
            var hit = scene.Raytracer.Trace(ray);
            if (!hit) continue;

            float areaToAngle = scene.Camera.SurfaceAreaToSolidAngleJacobian(hit.Position, hit.Normal);
            float angleToPixel = scene.Camera.SolidAngleToPixelJacobian(ray.Direction);
            float pixelFootprintArea = 1 / (angleToPixel * areaToAngle);
            averageArea += pixelFootprintArea;
            numHits++;
        }

        if (numHits == 0) {
            Console.WriteLine("Error determining pixel footprint: no intersections reported." +
                "Falling back to scene radius fraction.");
            MaximumRadius = scene.Radius / 300.0f;
            return;
        }

        averageArea /= numHits;

        // Compute the radius based on the approximated average footprint area
        // Our heuristic aims to have each photon cover roughly a 1.5 x 1.5 region on the image
        MaximumRadius = MathF.Sqrt(averageArea) * 1.5f / 2.0f;
    }

    /// <summary>
    /// Override this to make merging use progressive photon mapping, by shrinking the maximum radius
    /// in each iteration.
    /// </summary>
    /// <param name="iteration">The 0-based index of the iteration that just finished.</param>
    protected virtual void ShrinkRadius(uint iteration) { }

    /// <inheritdoc />
    protected override void OnEndIteration(uint iteration) {
        ShrinkRadius(iteration);

        float numPixels = Scene.FrameBuffer.Width * Scene.FrameBuffer.Height;
        AverageCameraPathLength = TotalCameraPathLength / numPixels;
        AveragePhotonsPerQuery = TotalMergeOperations == 0 ? 0 : TotalMergePhotons / (float)TotalMergeOperations;
        AverageLightPathLength = ComputeAverageLightPathLength();
    }

    protected override void OnAfterRender() {
        base.OnAfterRender();
        Scene.FrameBuffer.MetaData["AverageCameraPathLength"] = AverageCameraPathLength;
        Scene.FrameBuffer.MetaData["AverageLightPathLength"] = AverageLightPathLength;
        Scene.FrameBuffer.MetaData["AveragePhotonsPerQuery"] = AveragePhotonsPerQuery;
    }

    protected override void OnStartIteration(uint iteration) {
        base.OnStartIteration(iteration);

        // Reset statistics
        TotalCameraPathLength = 0;
        TotalMergeOperations = 0;
        TotalMergePhotons = 0;
    }

    protected override void OnCameraPathTerminate(CameraPath path)
    => Interlocked.Add(ref TotalCameraPathLength, path.Vertices.Count);

    private float ComputeAverageLightPathLength() {
        float average = 0;
        if (LightPaths == null) return 0;
        for (int i = 0; i < LightPaths.NumPaths; ++i) {
            int length = LightPaths.PathCache.Length(i);
            average = (length + i * average) / (i + 1);
        }
        return average;
    }

    /// <summary>Splats all non-zero samples into the technique pyramid, if enabled.</summary>
    /// <inheritdoc />
    protected override void RegisterSample(RgbColor weight, float misWeight, Pixel pixel,
                                           int cameraPathLength, int lightPathLength, int fullLength) {
        if (!RenderTechniquePyramid)
            return;

        techPyramidRaw.Add(cameraPathLength, lightPathLength, fullLength, pixel, weight);
        techPyramidWeighted.Add(cameraPathLength, lightPathLength, fullLength, pixel, weight * misWeight);
    }

    /// <inheritdoc />
    public override void Render(Scene scene) {
        if (RenderTechniquePyramid) {
            techPyramidRaw = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                                             minDepth: 1, maxDepth: MaxDepth, merges: true);
            techPyramidWeighted = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                                                  minDepth: 1, maxDepth: MaxDepth, merges: true);
        }

        InitializeRadius(scene);

        if (photonMap == null) photonMap = new();

        base.Render(scene);

        // Store the technique pyramids
        if (RenderTechniquePyramid) {
            techPyramidRaw.Normalize(1.0f / Scene.FrameBuffer.CurIteration);
            string pathRaw = Path.Join(scene.FrameBuffer.Basename, "techs-raw");
            techPyramidRaw.WriteToFiles(pathRaw);

            techPyramidWeighted.Normalize(1.0f / Scene.FrameBuffer.CurIteration);
            string pathWeighted = Path.Join(scene.FrameBuffer.Basename, "techs-weighted");
            techPyramidWeighted.WriteToFiles(pathWeighted);
        }

        photonMap.Dispose();
        photonMap = null;
    }

    /// <summary>
    /// Generates the acceleration structure for merging
    /// </summary>
    protected override void ProcessPathCache() {
        base.ProcessPathCache();

        if (EnableMerging) {
            photonMap.Clear();
            for (int i = 0; i < LightPaths.PathCache.NumVertices; ++i) {
                var vertex = LightPaths.PathCache[i];
                if (vertex.PathId >= 0 && vertex.Depth >= 1 && vertex.Weight != RgbColor.Black) {
                    photonMap.AddPoint(vertex.Point.Position, i);
                }
            }
            photonMap.Build();
        }
    }

    /// <summary>
    /// Called for each individual merge that yields one full path between the camera and a light.
    /// </summary>
    /// <param name="weight">Contribution of the path</param>
    /// <param name="kernelWeight">The PM kernel value that will be multiplied on the weight</param>
    /// <param name="misWeight">MIS weight that will be multiplied on the weight</param>
    /// <param name="cameraPath">The camera subpath</param>
    /// <param name="lightVertex">The last vertex of the light subpath</param>
    /// <param name="pdfCameraReverse">
    /// Surface area PDF to sample the previous vertex along the camera path when coming from the light
    /// </param>
    /// <param name="pdfLightReverse">
    /// Surface are PDF to sample the previous vertex along the light subpath when coming from the camera
    /// </param>
    /// <param name="pdfNextEvent">
    /// If the light vertex is the primary hit after the light source: the surface area PDF of next event
    /// to sample the same edge. Otherwise zero.
    /// </param>
    protected virtual void OnMergeSample(RgbColor weight, float kernelWeight, float misWeight,
                                         CameraPath cameraPath, PathVertex lightVertex, float pdfCameraReverse,
                                         float pdfLightReverse, float pdfNextEvent)
    => Interlocked.Increment(ref TotalMergePhotons);

    /// <summary>
    /// Called after each full photon mapping operation is finished, i.e., after all nearby photons
    /// have been found and merged wtih
    /// </summary>
    /// <param name="shader">Shading info at the merge position</param>
    /// <param name="rng">Current RNG state</param>
    /// <param name="path">The camera prefix path</param>
    /// <param name="cameraJacobian">Geometry term used to turn a PDF over outgoing directions into a surface area density.</param>
    /// <param name="estimate">The computed photon mapping contribution</param>
    protected virtual void OnCombinedMergeSample(in SurfaceShader shader, RNG rng, CameraPath path,
                                                 float cameraJacobian, RgbColor estimate)
    => Interlocked.Increment(ref TotalMergeOperations);

    protected virtual RgbColor Merge((CameraPath path, float cameraJacobian) userData, in SurfaceShader shader,
                                     int vertexIdx, float distSqr, float radiusSquared) {
        var photon = LightPaths.PathCache[vertexIdx];
        CameraPath path = userData.path;
        float cameraJacobian = userData.cameraJacobian;

        // Check that the path does not exceed the maximum length
        var depth = path.Vertices.Count + photon.Depth;
        if (depth > MaxDepth || depth < MinDepth)
            return RgbColor.Black;

        // Compute the contribution of the photon
        var ancestor = LightPaths.PathCache[photon.AncestorId];
        var dirToAncestor = Vector3.Normalize(ancestor.Point.Position - shader.Point.Position);
        var bsdfValue = shader.Evaluate(dirToAncestor);
        var photonContrib = photon.Weight * bsdfValue / NumLightPaths.Value;

        // Early exit + prevent NaN / Inf
        if (photonContrib == RgbColor.Black) return RgbColor.Black;
        // Prevent outliers du to numerical issues with photons arriving almost parallel to the surface
        if (Math.Abs(Vector3.Dot(dirToAncestor, shader.Point.Normal)) < 1e-4f) return RgbColor.Black;

        // Compute the missing pdf terms and the MIS weight
        var (pdfLightReverse, pdfCameraReverse) = shader.Pdf(dirToAncestor);
        pdfCameraReverse *= cameraJacobian;
        pdfLightReverse *= SampleWarp.SurfaceAreaToSolidAngle(shader.Point, ancestor.Point);
        float pdfNextEvent = (photon.Depth == 1) ? NextEventPdf(shader.Point, ancestor.Point) : 0;
        float misWeight = MergeMis(path, photon, pdfCameraReverse, pdfLightReverse, pdfNextEvent);

        // Prevent NaNs in corner cases
        if (pdfCameraReverse == 0 || pdfLightReverse == 0)
            return RgbColor.Black;

        // Epanechnikov kernel
        float kernelWeight = 2 * (radiusSquared - distSqr) / (MathF.PI * radiusSquared * radiusSquared);

        RegisterSample(photonContrib * kernelWeight * path.Throughput, misWeight, path.Pixel, path.Vertices.Count,
            photon.Depth, depth);
        OnMergeSample(photonContrib * path.Throughput, kernelWeight, misWeight, path, photon, pdfCameraReverse,
            pdfLightReverse, pdfNextEvent);

        return photonContrib * kernelWeight * misWeight;
    }

    /// <summary>
    /// Shrinks the global maximum radius based on the current camera path.
    /// </summary>
    /// <param name="primaryDistance">Distance between the camera and the primary hit point</param>
    /// <returns>The shrunk radius</returns>
    protected virtual float ComputeLocalMergeRadius(float primaryDistance) {
        float footprint = primaryDistance * MathF.Tan(0.1f * MathF.PI / 180);
        return MathF.Min(footprint, MaximumRadius);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="shader">Surface shading info at the last camera path vertex</param>
    /// <param name="rng">Random number generator for random decisions required during merging</param>
    /// <param name="path">The camera subpath data</param>
    /// <param name="cameraJacobian">
    ///     Geometry term used to turn a PDF over outgoing directions into a surface area density.
    /// </param>
    /// <returns>MIS weighted contribution due to merging</returns>
    protected virtual RgbColor PerformMerging(SurfaceShader shader, RNG rng, CameraPath path, float cameraJacobian) {
        if (path.Vertices.Count == 1 && !MergePrimary) return RgbColor.Black;
        if (!EnableMerging) return RgbColor.Black;
        float localRadius = ComputeLocalMergeRadius(path.Distances[0]);

        RgbColor estimate = RgbColor.Black;
        photonMap.ForAllNearest(shader.Point.Position, MaxNumPhotons, localRadius, (position, idx, distance, numFound, maxDist) => {
            float radiusSquared = numFound == MaxNumPhotons ? maxDist * maxDist : localRadius * localRadius;
            estimate += Merge((path, cameraJacobian), shader, idx, distance * distance, radiusSquared);
        });
        OnCombinedMergeSample(shader, rng, path, cameraJacobian, estimate);
        return estimate;
    }

    protected override RgbColor OnBackgroundHit(Ray ray, CameraPath path) {
        if (!EnableHitting && path.Vertices.Count > 1) return RgbColor.Black;
        return base.OnBackgroundHit(ray, path);
    }

    /// <inheritdoc />
    protected override RgbColor OnCameraHit(CameraPath path, RNG rng, in SurfaceShader shader,
                                            float pdfFromAncestor, RgbColor throughput, int depth,
                                            float toAncestorJacobian) {
        RgbColor value = RgbColor.Black;

        // Was a light hit?
        Emitter light = Scene.QueryEmitter(shader.Point);
        if (light != null && (EnableHitting || depth == 1) && depth >= MinDepth) {
            value += throughput * OnEmitterHit(light, shader.Point, shader.Context.OutDirWorld, path, toAncestorJacobian);
        }

        // Perform connections and merging if the maximum depth has not yet been reached
        if (depth < MaxDepth) {
            for (int i = 0; i < NumConnections; ++i) {
                value += throughput * BidirConnections(shader, rng, path, toAncestorJacobian);
            }
            value += throughput * PerformMerging(shader, rng, path, toAncestorJacobian);
        }

        if (depth < MaxDepth && depth + 1 >= MinDepth) {
            for (int i = 0; i < NumShadowRays; ++i) {
                value += throughput * PerformNextEventEstimation(shader, rng, path, toAncestorJacobian);
            }
        }

        return value;
    }

    /// <summary>
    /// Computes the MIS weight for a merge
    /// </summary>
    /// <param name="cameraPath">The camera subpath</param>
    /// <param name="lightVertex">Last vertex of the light subpath</param>
    /// <param name="pdfCameraReverse">
    /// Surface area pdf to sample the previous vertex along the camera subpath when coming from the light
    /// </param>
    /// <param name="pdfLightReverse">
    /// Surface area pdf to sample the previous vertex along the light subpath when coming from the camera
    /// </param>
    /// <param name="pdfNextEvent">
    /// If the light subpath consists of only one edge: the surface area PDF of next event estimation
    /// </param>
    /// <returns>MIS weight (classic balance heuristic)</returns>
    public virtual float MergeMis(CameraPath cameraPath, PathVertex lightVertex, float pdfCameraReverse,
                                  float pdfLightReverse, float pdfNextEvent) {
        int numPdfs = cameraPath.Vertices.Count + lightVertex.Depth;
        int lastCameraVertexIdx = cameraPath.Vertices.Count - 1;
        Span<float> camToLight = stackalloc float[numPdfs];
        Span<float> lightToCam = stackalloc float[numPdfs];

        var pathPdfs = new BidirPathPdfs(LightPaths.PathCache, lightToCam, camToLight);
        pathPdfs.GatherCameraPdfs(cameraPath, lastCameraVertexIdx);
        pathPdfs.GatherLightPdfs(lightVertex, lastCameraVertexIdx - 1);

        // Set the pdf values that are unique to this combination of paths
        if (lastCameraVertexIdx > 0) // only if this is not the primary hit point
            pathPdfs.PdfsLightToCamera[lastCameraVertexIdx - 1] = pdfCameraReverse;
        pathPdfs.PdfsLightToCamera[lastCameraVertexIdx] = lightVertex.PdfFromAncestor;
        pathPdfs.PdfsCameraToLight[lastCameraVertexIdx] = cameraPath.Vertices[^1].PdfFromAncestor;
        pathPdfs.PdfsCameraToLight[lastCameraVertexIdx + 1] = pdfLightReverse + pdfNextEvent;

        // Compute the acceptance probability approximation
        float radius = ComputeLocalMergeRadius(cameraPath.Distances[0]);
        float mergeApproximation = pathPdfs.PdfsLightToCamera[lastCameraVertexIdx]
                                 * MathF.PI * radius * radius * NumLightPaths.Value;

        // Compute reciprocals for hypothetical connections along the camera sub-path
        float sumReciprocals = 0.0f;
        sumReciprocals +=
            CameraPathReciprocals(lastCameraVertexIdx, pathPdfs, cameraPath.Pixel, radius)
            / mergeApproximation;
        sumReciprocals +=
            LightPathReciprocals(lastCameraVertexIdx, pathPdfs, cameraPath.Pixel, radius)
            / mergeApproximation;

        // Add the reciprocal for the connection that replaces the last light path edge
        if (lightVertex.Depth > 1 && NumConnections > 0)
            sumReciprocals += BidirSelectDensity(cameraPath.Pixel) / mergeApproximation;

        return 1 / sumReciprocals;
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
        float radius = ComputeLocalMergeRadius(cameraPath.Distances[0]);
        sumReciprocals +=
            CameraPathReciprocals(lastCameraVertexIdx - 1, pathPdfs, cameraPath.Pixel, radius)
            / pdfThis;

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
        float radius = ComputeLocalMergeRadius(distToCam);
        float sumReciprocals = LightPathReciprocals(lastCameraVertexIdx, pathPdfs, pixel, radius);
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
        float radius = ComputeLocalMergeRadius(cameraPath.Distances[0]);
        float sumReciprocals = 1.0f;
        sumReciprocals += CameraPathReciprocals(lastCameraVertexIdx, pathPdfs, cameraPath.Pixel, radius)
            / BidirSelectDensity(cameraPath.Pixel);
        sumReciprocals += LightPathReciprocals(lastCameraVertexIdx, pathPdfs, cameraPath.Pixel, radius)
            / BidirSelectDensity(cameraPath.Pixel);

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
        if (EnableHitting) sumReciprocals += pdfHit / pdfNextEvent;

        // All bidirectional connections
        float radius = ComputeLocalMergeRadius(cameraPath.Distances[0]);
        sumReciprocals +=
            CameraPathReciprocals(lastCameraVertexIdx, pathPdfs, cameraPath.Pixel, radius)
            / pdfNextEvent;

        return 1 / sumReciprocals;
    }

    /// <summary>
    /// Computes the PDF ratios along the camera subpath for MIS weight computations
    /// </summary>
    protected virtual float CameraPathReciprocals(int lastCameraVertexIdx, BidirPathPdfs pdfs,
                                                  Pixel pixel, float radius) {
        float sumReciprocals = 0.0f;
        float nextReciprocal = 1.0f;
        for (int i = lastCameraVertexIdx; i > 0; --i) {
            // Merging at this vertex
            if (EnableMerging) {
                float acceptProb = pdfs.PdfsLightToCamera[i] * MathF.PI * radius * radius;
                sumReciprocals += nextReciprocal * NumLightPaths.Value * acceptProb;
            }

            nextReciprocal *= pdfs.PdfsLightToCamera[i] / pdfs.PdfsCameraToLight[i];

            // Connecting this vertex to the next one along the camera path
            if (NumConnections > 0) sumReciprocals += nextReciprocal * BidirSelectDensity(pixel);
        }

        // Light tracer
        if (EnableLightTracer)
            sumReciprocals +=
                nextReciprocal * pdfs.PdfsLightToCamera[0] / pdfs.PdfsCameraToLight[0] * NumLightPaths.Value;

        // Merging directly visible (almost the same as the light tracer!)
        if (MergePrimary)
            sumReciprocals += nextReciprocal * NumLightPaths.Value * pdfs.PdfsLightToCamera[0]
                            * MathF.PI * radius * radius;

        return sumReciprocals;
    }

    /// <summary>
    /// Computes the PDF ratios along the light subpath for MIS weight computations
    /// </summary>
    protected virtual float LightPathReciprocals(int lastCameraVertexIdx, BidirPathPdfs pdfs,
                                                 Pixel pixel, float radius) {
        float sumReciprocals = 0.0f;
        float nextReciprocal = 1.0f;
        for (int i = lastCameraVertexIdx + 1; i < pdfs.NumPdfs; ++i) {
            if (i < pdfs.NumPdfs - 1 && (MergePrimary || i > 0)) { // no merging on the emitter itself
                                                                   // Account for merging at this vertex
                if (EnableMerging) {
                    float acceptProb = pdfs.PdfsCameraToLight[i] * MathF.PI * radius * radius;
                    sumReciprocals += nextReciprocal * NumLightPaths.Value * acceptProb;
                }
            }

            nextReciprocal *= pdfs.PdfsCameraToLight[i] / pdfs.PdfsLightToCamera[i];

            // Account for connections from this vertex to its ancestor
            if (i < pdfs.NumPdfs - 2) // Connections to the emitter (next event) are treated separately
                if (NumConnections > 0) sumReciprocals += nextReciprocal * BidirSelectDensity(pixel);
        }
        if (EnableHitting || NumShadowRays != 0) sumReciprocals += nextReciprocal; // Next event and hitting the emitter directly
                                                                                   // TODO / FIXME Bsdf and Nee can only be disabled jointly here: needs proper handling when assembling pdfs
        return sumReciprocals;
    }
}
