using SeeSharp.Geometry;
using SeeSharp.Sampling;
using SimpleImageIO;
using SeeSharp.Shading.Emitters;
using SeeSharp.Integrators.Common;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using TinyEmbree;
using SeeSharp.Common;
using SeeSharp.Integrators.Util;

namespace SeeSharp.Integrators.Bidir {
    /// <summary>
    /// Basis for many bidirectional algorithms. Splits rendering into multiple iterations. Each iteration
    /// traces a certain number of paths from the light sources and one camera path per pixel.
    /// Derived classes can control the sampling decisions and techniques.
    /// </summary>
    public abstract class BidirBase : Integrator {
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
        /// Number of light paths per iteration, or zero (default) to trace one per pixel.
        /// </summary>
        public int NumLightPaths = 0;

        /// <summary>
        /// Minimum length (in edges) of a path that can contribute to the image. If set to 2, e.g., directly
        /// visible lights are not rendered.
        /// </summary>
        public int MinDepth = 1;

        /// <summary>
        /// The base seed to generate camera paths.
        /// </summary>
        public uint BaseSeedCamera = 0xC030114u;

        /// <summary>
        /// The base seed used when sampling paths from the light sources
        /// </summary>
        public uint BaseSeedLight = 0x13C0FEFEu;

        /// <summary>
        /// Can be set to log some or all paths that have been sampled. Full support is still WIP.
        /// </summary>
        public Util.PathLogger PathLogger;

        /// <summary>
        /// The scene that is currently being rendered
        /// </summary>
        public Scene Scene;

        /// <summary>
        /// The current batch of light paths traced during the current iteration
        /// </summary>
        public LightPathCache LightPaths;

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
            public Vector2 Pixel;

            /// <summary>
            /// The product of the local estimators along the path (BSDF * cos / pdf)
            /// </summary>
            public RgbColor Throughput;

            /// <summary>
            /// The pdf values for sampling this path.
            /// </summary>
            public List<PathPdfPair> Vertices;

            /// <summary>
            /// Distances between all points sampled along this path
            /// </summary>
            public List<float> Distances;
        }

        /// <summary>
        /// Called once per iteration after the light paths have been traced.
        /// Use this to create acceleration structures etc.
        /// </summary>
        public abstract void ProcessPathCache();

        /// <summary>
        /// Used by next event estimation to select a light source
        /// </summary>
        /// <param name="from">A point on a surface where next event is performed</param>
        /// <param name="rng">Random number generator</param>
        /// <returns>The selected light and the discrete probability of selecting that light</returns>
        public virtual (Emitter, float) SelectLight(in SurfacePoint from, RNG rng) {
            int idx = rng.NextInt(0, Scene.Emitters.Count);
            return (Scene.Emitters[idx], 1.0f / Scene.Emitters.Count);
        }

        /// <returns>
        /// The discrete probability of selecting the given light when performing next event at the given
        /// shading point.
        /// </returns>
        public virtual float SelectLightPmf(in SurfacePoint from, Emitter em) => 1.0f / Scene.Emitters.Count;

        /// <summary>
        /// Called once for each pixel per iteration. Expected to perform some sort of path tracing,
        /// possibly connecting vertices with those from the light path cache.
        /// </summary>
        /// <returns>The estimated pixel value.</returns>
        public virtual RgbColor EstimatePixelValue(in SurfacePoint cameraPoint, Vector2 filmPosition,
                                                   in Ray primaryRay, float pdfFromCamera,
                                                   RgbColor initialWeight, RNG rng) {
            // The pixel index determines which light path we connect to
            int row = Math.Min((int)filmPosition.Y, Scene.FrameBuffer.Height - 1);
            int col = Math.Min((int)filmPosition.X, Scene.FrameBuffer.Width - 1);
            int pixelIndex = row * Scene.FrameBuffer.Width + col;
            var walk = new CameraRandomWalk(rng, filmPosition, pixelIndex, this);
            return walk.StartFromCamera(filmPosition, cameraPoint, pdfFromCamera, primaryRay, initialWeight);
        }

        /// <summary>
        /// Called once after the end of each rendering iteration (one sample per pixel)
        /// </summary>
        /// <param name="iteration">The 0-based index of the iteration that just finished</param>
        public virtual void PostIteration(uint iteration) { }

        /// <summary>
        /// Called once before the start of each rendering iteration (one sample per pixel)
        /// </summary>
        /// <param name="iteration">The 0-based index of the iteration that will now start</param>
        public virtual void PreIteration(uint iteration) { }

        /// <summary>
        /// Generates the light path cache used to sample and store light paths.
        /// Called at the beginning of the rendering process before the first iteration.
        /// </summary>
        protected virtual LightPathCache MakeLightPathCache()
        => new LightPathCache { MaxDepth = MaxDepth, NumPaths = NumLightPaths, Scene = Scene };

        /// <summary>
        /// Renders the scene with the current settings. Not thread-safe: Only one scene can be rendered at a
        /// time by the same object of this class.
        /// </summary>
        public override void Render(Scene scene) {
            this.Scene = scene;

            if (NumLightPaths <= 0) {
                NumLightPaths = scene.FrameBuffer.Width * scene.FrameBuffer.Height;
            }

            LightPaths = MakeLightPathCache();

            if (EnableDenoiser) DenoiseBuffers = new(scene.FrameBuffer);

            SeeSharp.Common.ProgressBar progressBar = new(NumIterations);
            RNG camSeedGen = new(BaseSeedCamera);
            RenderTimer timer = new();
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

                PreIteration(iter);
                try {
                    LightPaths.TraceAllPaths(iter,
                        (origin, primary, nextDirection) => NextEventPdf(primary.Point, origin.Point));
                    ProcessPathCache();
                    TraceAllCameraPaths(iter);
                } catch {
                    Logger.Log($"Exception in iteration {iter} out of {NumIterations}.", Verbosity.Error);
                    throw;
                }
                PostIteration(iter);
                timer.EndRender();

                if (iter == NumIterations - 1 && EnableDenoiser)
                    DenoiseBuffers.Denoise();
                scene.FrameBuffer.EndIteration();
                timer.EndFrameBuffer();

                progressBar.ReportDone(1, timer.CurrentIterationSeconds);
                timer.EndIteration();
            }

            scene.FrameBuffer.MetaData["RenderTime"] = timer.RenderTime;
            scene.FrameBuffer.MetaData["FrameBufferTime"] = timer.FrameBufferTime;
        }

        private void TraceAllCameraPaths(uint iter) {
            Parallel.For(0, Scene.FrameBuffer.Height, row => {
                for (uint col = 0; col < Scene.FrameBuffer.Width; ++col) {
                    uint pixelIndex = (uint)(row * Scene.FrameBuffer.Width + col);
                    var rng = new RNG(BaseSeedCamera, pixelIndex, iter);
                    RenderPixel((uint)row, col, rng);
                }
            });
        }

        private void RenderPixel(uint row, uint col, RNG rng) {
            // Sample a ray from the camera
            var offset = rng.NextFloat2D();
            var filmSample = new Vector2(col, row) + offset;
            var cameraRay = Scene.Camera.GenerateRay(filmSample, rng);
            var value = EstimatePixelValue(cameraRay.Point, filmSample, cameraRay.Ray,
                                           cameraRay.PdfRay, cameraRay.Weight, rng);

            // TODO we do nearest neighbor splatting manually here, to avoid numerical
            //      issues if the primary samples are almost 1 (400 + 0.99999999f = 401)
            Scene.FrameBuffer.Splat((float)col, (float)row, value);
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
        protected virtual void RegisterSample(RgbColor weight, float misWeight, Vector2 pixel,
                                              int cameraPathLength, int lightPathLength, int fullLength) {}

        /// <summary>
        /// Called for each sample generated by the light tracer.
        /// </summary>
        /// <param name="weight">The sample contribution not yet weighted by MIS</param>
        /// <param name="misWeight">The MIS weight that will be multiplied on the sample weight</param>
        /// <param name="pixel">The pixel to which this sample contributes</param>
        /// <param name="lightVertex">The last vertex on the light path</param>
        /// <param name="pdfCamToPrimary">
        /// Surface area pdf of sampling the last light path vertex when starting from the camera
        /// </param>
        /// <param name="pdfReverse">
        /// Surface area pdf of sampling the ancestor of the last light path vertex, when starting from the
        /// camera.
        /// </param>
        /// <param name="pdfNextEvent">
        /// Surface area pdf of sampling the ancestor of the last light path vertex via next event estimation.
        /// Will be zero unless this is a direct illumination sample.
        /// </param>
        /// <param name="distToCam">Distance of the last vertex to the camera</param>
        protected virtual void OnLightTracerSample(RgbColor weight, float misWeight, Vector2 pixel,
                                                   PathVertex lightVertex, float pdfCamToPrimary, float pdfReverse,
                                                   float pdfNextEvent, float distToCam) {}

        /// <summary>
        /// Called for each full path sample generated via next event estimation on a camera path.
        /// </summary>
        /// <param name="weight">The sample contribution not yet weighted by MIS</param>
        /// <param name="misWeight">The MIS weight that will be multiplied on the sample weight</param>
        /// <param name="cameraPath">The camera path until the point where NEE was performed</param>
        /// <param name="pdfEmit">
        /// Surface area pdf of sampling the last camera vertex when starting at the light source.
        /// This also includes the pdf of sampling the point on the light during emission.
        /// </param>
        /// <param name="pdfNextEvent">Surface area pdf used for next event estimation</param>
        /// <param name="pdfHit">
        /// Surface area pdf of sampling the same light source point by continuing the path
        /// </param>
        /// <param name="pdfReverse">
        /// Surface area pdf of sampling the ancestor of the last camera vertex, when starting at the light
        /// source.
        /// </param>
        /// <param name="emitter">The emitter that was connected to</param>
        /// <param name="lightToSurface">
        /// Direction from the point on the light to the last camera vertex.
        /// </param>
        /// <param name="lightPoint">The point on the light</param>
        protected virtual void OnNextEventSample(RgbColor weight, float misWeight, CameraPath cameraPath,
                                                 float pdfEmit, float pdfNextEvent, float pdfHit, float pdfReverse,
                                                 Emitter emitter, Vector3 lightToSurface, SurfacePoint lightPoint) {}

        /// <summary>
        /// Called for each full path sample generated by hitting a light source during the random walk from
        /// the camera.
        /// </summary>
        /// <param name="weight">The sample contribution not yet weighted by MIS</param>
        /// <param name="misWeight">The MIS weight that will be multiplied on the sample weight</param>
        /// <param name="cameraPath">The camera path until the point where NEE was performed</param>
        /// <param name="pdfEmit">
        /// Surface area pdf of sampling the last camera vertex when starting at the light source.
        /// This also includes the pdf of sampling the point on the light during emission.
        /// </param>
        /// <param name="pdfNextEvent">
        /// Surface area pdf of sampling the same light source point via next event estimation instead.
        /// </param>
        /// <param name="emitter">The emitter that was hit</param>
        /// <param name="lightToSurface">
        /// Direction from the point on the light to the last camera vertex.
        /// </param>
        /// <param name="lightPoint">The point on the light</param>
        protected virtual void OnEmitterHitSample(RgbColor weight, float misWeight, CameraPath cameraPath,
                                                  float pdfEmit, float pdfNextEvent, Emitter emitter,
                                                  Vector3 lightToSurface, SurfacePoint lightPoint) {}

        /// <summary>
        /// Called for each full path generated by connecting a camera sub-path and a light sub-path
        /// via a shadow ray.
        /// </summary>
        /// <param name="weight">The sample contribution not yet weighted by MIS</param>
        /// <param name="misWeight">The MIS weight that will be multiplied on the sample weight</param>
        /// <param name="cameraPath">The camera path until the point where NEE was performed</param>
        /// <param name="lightVertex">Last vertex of the camera sub-path that was connected to</param>
        /// <param name="pdfCameraReverse">
        /// Surface area pdf of sampling the ancestor of the last camera vertex when continuing the light
        /// sub-path instead
        /// </param>
        /// <param name="pdfCameraToLight">
        /// Surface area pdf of sampling the connecting edge by continuing the camera path instead
        /// </param>
        /// <param name="pdfLightReverse">
        /// Surface area pdf of sampling the ancestor of the last light vertex when continuing the camera
        /// sub-path instead
        /// </param>
        /// <param name="pdfLightToCamera">
        /// Surface area pdf of sampling the connecting edge by continuing the light path instead
        /// </param>
        /// <param name="pdfNextEvent">
        /// Zero if the light sub-path has more than one edge. Otherwise, the surface area pdf of sampling
        /// that edge via next event estimation from the camera instead
        /// </param>
        protected virtual void OnBidirConnectSample(RgbColor weight, float misWeight, CameraPath cameraPath,
                                                    PathVertex lightVertex, float pdfCameraReverse,
                                                    float pdfCameraToLight, float pdfLightReverse,
                                                    float pdfLightToCamera, float pdfNextEvent) {}

        /// <summary>
        /// Computes the MIS weight of a light tracer sample, for example via the balance heuristic.
        /// </summary>
        /// <param name="lightVertex">The last vertex of the light sub-path</param>
        /// <param name="pdfCamToPrimary">
        /// Surface area pdf of sampling the last light path vertex when starting from the camera
        /// </param>
        /// <param name="pdfReverse">
        /// Surface area pdf of sampling the ancestor of the last light path vertex, when starting from the
        /// camera.
        /// </param>
        /// <param name="pdfNextEvent">
        /// Surface area pdf of sampling the ancestor of the last light path vertex via next event estimation.
        /// Will be zero unless this is a direct illumination sample.
        /// </param>
        /// <param name="pixel">The pixel on the image the path contributes to</param>
        /// <param name="distToCam">Distance between the camera and the last light path vertex</param>
        /// <returns>MIS weight of the sampled path</returns>
        public abstract float LightTracerMis(PathVertex lightVertex, float pdfCamToPrimary, float pdfReverse,
                                             float pdfNextEvent, Vector2 pixel, float distToCam);

        /// <summary>
        /// Connects all vertices along all light paths to the camera via shadow rays ("light tracing").
        /// </summary>
        public void SplatLightVertices() {
            Parallel.For(0, LightPaths.NumPaths, idx => {
                ConnectLightPathToCamera(idx);
            });
        }

        /// <summary>
        /// Connects the vertices of the i-th path to the camera ("light tracing")
        /// </summary>
        /// <param name="pathIdx">Index of the path in the cache</param>
        protected virtual void ConnectLightPathToCamera(int pathIdx) {
            LightPaths.ForEachVertex(pathIdx,
            (int pathIdx, in PathVertex vertex, in PathVertex ancestor, Vector3 dirToAncestor) => {
                if (vertex.Depth + 1 < MinDepth) return;

                // Compute image plane location
                var response = Scene.Camera.SampleResponse(vertex.Point, null); // TODO get an RNG in there for DOF!
                if (!response.IsValid)
                    return;

                if (Scene.Raytracer.IsOccluded(vertex.Point, response.Position))
                    return;

                var dirToCam = response.Position - vertex.Point.Position;
                float distToCam = dirToCam.Length();
                dirToCam /= distToCam;

                var bsdfValue = vertex.Point.Material.Evaluate(vertex.Point, dirToAncestor, dirToCam, true);
                if (bsdfValue == RgbColor.Black)
                    return;

                // Compute the surface area pdf of sampling the previous vertex instead
                float pdfReverse =
                    vertex.Point.Material.Pdf(vertex.Point, dirToCam, dirToAncestor, false).Item1;
                if (ancestor.Point.Mesh != null)
                    pdfReverse *= SampleWarp.SurfaceAreaToSolidAngle(vertex.Point, ancestor.Point);

                // Account for next event estimation
                float pdfNextEvent = 0.0f;
                if (vertex.Depth == 1) {
                    pdfNextEvent = NextEventPdf(vertex.Point, ancestor.Point);
                }

                float misWeight =
                    LightTracerMis(vertex, response.PdfEmit, pdfReverse, pdfNextEvent, response.Pixel, distToCam);

                // Compute image contribution and splat
                RgbColor weight = vertex.Weight * bsdfValue * response.Weight / NumLightPaths;
                Scene.FrameBuffer.Splat(response.Pixel.X, response.Pixel.Y, misWeight * weight);

                // Log the sample
                RegisterSample(weight, misWeight, response.Pixel, 0, vertex.Depth, vertex.Depth + 1);
                OnLightTracerSample(weight, misWeight, response.Pixel, vertex, response.PdfEmit, pdfReverse,
                    pdfNextEvent, distToCam);

                if (PathLogger != null && misWeight != 0 && weight != RgbColor.Black) {
                    var logId = PathLogger.StartNew(response.Pixel);
                    for (int i = 0; i < vertex.Depth + 1; ++i) {
                        PathLogger.Continue(logId, LightPaths.PathCache[pathIdx, i].Point.Position, 1);
                    }
                    PathLogger.SetContrib(logId, misWeight * weight);
                }
            });
        }

        public abstract float BidirConnectMis(CameraPath cameraPath, PathVertex lightVertex,
                                              float pdfCameraReverse, float pdfCameraToLight,
                                              float pdfLightReverse, float pdfLightToCamera,
                                              float pdfNextEvent);

        protected virtual (int, int, float) SelectBidirPath(SurfacePoint cameraPoint, Vector3 outDir,
                                                            int pixelIndex, RNG rng)
        => (pixelIndex, -1, 1.0f);

        public virtual RgbColor BidirConnections(int pixelIndex, SurfacePoint cameraPoint, Vector3 outDir,
                                                 RNG rng, CameraPath path, float reversePdfJacobian) {
            RgbColor result = RgbColor.Black;
            if (NumLightPaths == 0) return result;

            // Select a path to connect to (based on pixel index)
            (int lightPathIdx, int lightVertIdx, float lightVertexProb) =
                SelectBidirPath(cameraPoint, outDir, pixelIndex, rng);

            void Connect(PathVertex vertex, PathVertex ancestor, Vector3 dirToAncestor) {
                // Only allow connections that do not exceed the maximum total path length
                int depth = vertex.Depth + path.Vertices.Count + 1;
                if (depth > MaxDepth || depth < MinDepth) return;

                // Trace shadow ray
                if (Scene.Raytracer.IsOccluded(vertex.Point, cameraPoint))
                    return;

                // Compute connection direction
                var dirFromCamToLight = Vector3.Normalize(vertex.Point.Position - cameraPoint.Position);

                var bsdfWeightLight = vertex.Point.Material.EvaluateWithCosine(vertex.Point, dirToAncestor,
                    -dirFromCamToLight, true);
                var bsdfWeightCam = cameraPoint.Material.EvaluateWithCosine(cameraPoint, outDir,
                    dirFromCamToLight, false);

                if (bsdfWeightCam == RgbColor.Black || bsdfWeightLight == RgbColor.Black)
                    return;

                // Compute the missing pdfs
                var (pdfCameraToLight, pdfCameraReverse) =
                    cameraPoint.Material.Pdf(cameraPoint, outDir, dirFromCamToLight, false);
                pdfCameraReverse *= reversePdfJacobian;
                pdfCameraToLight *= SampleWarp.SurfaceAreaToSolidAngle(cameraPoint, vertex.Point);

                var (pdfLightToCamera, pdfLightReverse) =
                    vertex.Point.Material.Pdf(vertex.Point, dirToAncestor, -dirFromCamToLight, true);
                if (ancestor.Point.Mesh != null) // not when from background
                    pdfLightReverse *= SampleWarp.SurfaceAreaToSolidAngle(vertex.Point, ancestor.Point);
                pdfLightToCamera *= SampleWarp.SurfaceAreaToSolidAngle(vertex.Point, cameraPoint);

                float pdfNextEvent = 0.0f;
                if (vertex.Depth == 1) {
                    pdfNextEvent = NextEventPdf(vertex.Point, ancestor.Point);
                }

                float misWeight = BidirConnectMis(path, vertex, pdfCameraReverse, pdfCameraToLight,
                    pdfLightReverse, pdfLightToCamera, pdfNextEvent);
                float distanceSqr = (cameraPoint.Position - vertex.Point.Position).LengthSquared();

                // Avoid NaNs in rare cases
                if (distanceSqr == 0)
                    return;

                RgbColor weight =
                    vertex.Weight * bsdfWeightLight * bsdfWeightCam / distanceSqr / lightVertexProb;
                result += misWeight * weight;

                RegisterSample(weight * path.Throughput, misWeight, path.Pixel,
                               path.Vertices.Count, vertex.Depth, depth);
                OnBidirConnectSample(weight * path.Throughput, misWeight, path, vertex, pdfCameraReverse,
                    pdfCameraToLight, pdfLightReverse, pdfLightToCamera, pdfNextEvent);
            }

            if (lightVertIdx > 0 && lightVertIdx < LightPaths.PathCache.Length(lightPathIdx)) {
                // specific vertex selected
                var vertex = LightPaths.PathCache[lightPathIdx, lightVertIdx];
                var ancestor = LightPaths.PathCache[lightPathIdx, lightVertIdx - 1];
                var dirToAncestor = Vector3.Normalize(ancestor.Point.Position - vertex.Point.Position);
                Connect(vertex, ancestor, dirToAncestor);
            } else if (lightPathIdx >= 0) {
                // Connect with all vertices along the path
                int n = LightPaths.PathCache.Length(lightPathIdx);
                for (int i = 1; i < n; ++i) {
                    var ancestor = LightPaths.PathCache[lightPathIdx, i-1];
                    var vertex = LightPaths.PathCache[lightPathIdx, i];
                    var dirToAncestor = Vector3.Normalize(ancestor.Point.Position - vertex.Point.Position);
                    Connect(vertex, ancestor, dirToAncestor);
                }
            }

            return result;
        }

        public abstract float NextEventMis(CameraPath cameraPath, float pdfEmit, float pdfNextEvent,
                                           float pdfHit, float pdfReverse);

        public virtual float NextEventPdf(SurfacePoint from, SurfacePoint to) {
            float backgroundProbability = ComputeNextEventBackgroundProbability(/*hit*/);
            if (to.Mesh == null) { // Background
                var direction = to.Position - from.Position;
                return Scene.Background.DirectionPdf(direction) * backgroundProbability;
            } else { // Emissive object
                var emitter = Scene.QueryEmitter(to);
                return emitter.PdfArea(to) * SelectLightPmf(from, emitter) * (1 - backgroundProbability);
            }
        }

        public virtual (Emitter, SurfaceSample) SampleNextEvent(SurfacePoint from, RNG rng) {
            var (light, lightProb) = SelectLight(from, rng);
            var lightSample = light.SampleArea(rng.NextFloat2D());
            lightSample.Pdf *= lightProb;
            return (light, lightSample);
        }

        public virtual float ComputeNextEventBackgroundProbability(/*SurfacePoint from*/)
        => Scene.Background == null ? 0 : 1 / (1.0f + Scene.Emitters.Count);

        public virtual RgbColor PerformNextEventEstimation(Ray ray, SurfacePoint hit, RNG rng, CameraPath path,
                                                           float reversePdfJacobian) {
            float backgroundProbability = ComputeNextEventBackgroundProbability(/*hit*/);
            if (rng.NextFloat() < backgroundProbability) { // Connect to the background
                if (Scene.Background == null)
                    return RgbColor.Black; // There is no background

                var sample = Scene.Background.SampleDirection(rng.NextFloat2D());
                sample.Pdf *= backgroundProbability;
                sample.Weight /= backgroundProbability;

                if (sample.Pdf == 0) // Prevent NaN
                    return RgbColor.Black;

                if (Scene.Raytracer.LeavesScene(hit, sample.Direction)) {
                    var bsdfTimesCosine = hit.Material.EvaluateWithCosine(hit, -ray.Direction,
                        sample.Direction, false);

                    // Compute the reverse BSDF sampling pdf
                    var (bsdfForwardPdf, bsdfReversePdf) = hit.Material.Pdf(hit, -ray.Direction,
                        sample.Direction, false);
                    bsdfReversePdf *= reversePdfJacobian;

                    // Compute emission pdf
                    float pdfEmit = LightPaths.ComputeBackgroundPdf(hit.Position, -sample.Direction);

                    // Compute the mis weight
                    float misWeight = NextEventMis(path, pdfEmit, sample.Pdf, bsdfForwardPdf, bsdfReversePdf);

                    // Compute and log the final sample weight
                    var weight = sample.Weight * bsdfTimesCosine;
                    RegisterSample(weight * path.Throughput, misWeight, path.Pixel,
                                   path.Vertices.Count, 0, path.Vertices.Count + 1);
                    OnNextEventSample(weight * path.Throughput, misWeight, path, pdfEmit, sample.Pdf,
                        bsdfForwardPdf, bsdfReversePdf, null, -sample.Direction,
                        new() { Position = hit.Position });
                    return misWeight * weight;
                }
            } else { // Connect to an emissive surface
                if (Scene.Emitters.Count == 0)
                    return RgbColor.Black;

                // Sample a point on the light source
                var (light, lightSample) = SampleNextEvent(hit, rng);
                lightSample.Pdf *= (1 - backgroundProbability);

                if (lightSample.Pdf == 0) // Prevent NaN
                    return RgbColor.Black;

                if (!Scene.Raytracer.IsOccluded(hit, lightSample.Point)) {
                    Vector3 lightToSurface = Vector3.Normalize(hit.Position - lightSample.Point.Position);
                    var emission = light.EmittedRadiance(lightSample.Point, lightToSurface);

                    var bsdfTimesCosine =
                        hit.Material.EvaluateWithCosine(hit, -ray.Direction, -lightToSurface, false);
                    if (bsdfTimesCosine == RgbColor.Black)
                        return RgbColor.Black;

                    // Compute the jacobian for surface area -> solid angle
                    // (Inverse of the jacobian for solid angle pdf -> surface area pdf)
                    float jacobian = SampleWarp.SurfaceAreaToSolidAngle(hit, lightSample.Point);
                    if (jacobian == 0) return RgbColor.Black;

                    // Compute the missing pdf terms
                    var (bsdfForwardPdf, bsdfReversePdf) =
                        hit.Material.Pdf(hit, -ray.Direction, -lightToSurface, false);
                    bsdfForwardPdf *= SampleWarp.SurfaceAreaToSolidAngle(hit, lightSample.Point);
                    bsdfReversePdf *= reversePdfJacobian;

                    float pdfEmit = LightPaths.ComputeEmitterPdf(light, lightSample.Point, lightToSurface,
                        SampleWarp.SurfaceAreaToSolidAngle(lightSample.Point, hit));

                    float misWeight =
                        NextEventMis(path, pdfEmit, lightSample.Pdf, bsdfForwardPdf, bsdfReversePdf);

                    var weight = emission * bsdfTimesCosine * (jacobian / lightSample.Pdf);
                    RegisterSample(weight * path.Throughput, misWeight, path.Pixel,
                                   path.Vertices.Count, 0, path.Vertices.Count + 1);
                    OnNextEventSample(weight * path.Throughput, misWeight, path, pdfEmit, lightSample.Pdf,
                        bsdfForwardPdf, bsdfReversePdf, light, lightToSurface, lightSample.Point);
                    return misWeight * weight;
                }
            }

            return RgbColor.Black;
        }

        public abstract float EmitterHitMis(CameraPath cameraPath, float pdfEmit, float pdfNextEvent);

        public virtual RgbColor OnEmitterHit(Emitter emitter, SurfacePoint hit, Ray ray,
                                             CameraPath path, float reversePdfJacobian) {
            var emission = emitter.EmittedRadiance(hit, -ray.Direction);

            // Compute pdf values
            float pdfEmit = LightPaths.ComputeEmitterPdf(emitter, hit, -ray.Direction, reversePdfJacobian);
            float pdfNextEvent = NextEventPdf(new SurfacePoint(), hit); // TODO get the actual previous point!

            float misWeight = EmitterHitMis(path, pdfEmit, pdfNextEvent);
            RegisterSample(emission * path.Throughput, misWeight, path.Pixel,
                           path.Vertices.Count, 0, path.Vertices.Count);
            OnEmitterHitSample(emission * path.Throughput, misWeight, path, pdfEmit, pdfNextEvent, emitter,
                -ray.Direction, hit);
            return misWeight * emission;
        }

        public virtual RgbColor OnBackgroundHit(Ray ray, CameraPath path) {
            if (Scene.Background == null)
                return RgbColor.Black;

            // Compute the pdf of sampling the previous point by emission from the background
            float pdfEmit = LightPaths.ComputeBackgroundPdf(ray.Origin, -ray.Direction);

            // Compute the pdf of sampling the same connection via next event estimation
            float pdfNextEvent = Scene.Background.DirectionPdf(ray.Direction);
            float backgroundProbability = ComputeNextEventBackgroundProbability(/*hit*/);
            pdfNextEvent *= backgroundProbability;

            float misWeight = EmitterHitMis(path, pdfEmit, pdfNextEvent);
            var emission = Scene.Background.EmittedRadiance(ray.Direction);
            RegisterSample(emission * path.Throughput, misWeight, path.Pixel,
                           path.Vertices.Count, 0, path.Vertices.Count);
            OnEmitterHitSample(emission * path.Throughput, misWeight, path, pdfEmit, pdfNextEvent, null,
                -ray.Direction, new() { Position = ray.Origin });
            return misWeight * emission * path.Throughput;
        }

        public abstract RgbColor OnCameraHit(CameraPath path, RNG rng, int pixelIndex, Ray ray,
                                             SurfacePoint hit, float pdfFromAncestor, RgbColor throughput,
                                             int depth, float toAncestorJacobian);

        class CameraRandomWalk : RandomWalk {
            int pixelIndex;
            BidirBase integrator;
            CameraPath path;

            public CameraRandomWalk(RNG rng, Vector2 filmPosition, int pixelIndex, BidirBase integrator)
                : base(integrator.Scene, rng, integrator.MaxDepth + 1) {
                this.pixelIndex = pixelIndex;
                this.integrator = integrator;
                path.Vertices = new List<PathPdfPair>(integrator.MaxDepth);
                path.Distances = new List<float>(integrator.MaxDepth);
                path.Pixel = filmPosition;
            }

            protected override RgbColor OnInvalidHit(Ray ray, float pdfFromAncestor, RgbColor throughput,
                                                     int depth) {
                path.Vertices.Add(new PathPdfPair {
                    PdfFromAncestor = pdfFromAncestor,
                    PdfToAncestor = 0
                });
                path.Throughput = throughput;
                path.Distances.Add(float.PositiveInfinity);
                return integrator.OnBackgroundHit(ray, path);
            }

            protected override RgbColor OnHit(Ray ray, SurfacePoint hit, float pdfFromAncestor,
                                              RgbColor throughput, int depth, float toAncestorJacobian) {
                if (depth == 1 && integrator.EnableDenoiser) {
                    var albedo = ((SurfacePoint)hit).Material.GetScatterStrength(hit);
                    integrator.DenoiseBuffers.LogPrimaryHit(path.Pixel, albedo, hit.ShadingNormal);
                }

                path.Vertices.Add(new PathPdfPair {
                    PdfFromAncestor = pdfFromAncestor,
                    PdfToAncestor = 0
                });
                path.Throughput = throughput;
                path.Distances.Add(hit.Distance);
                return integrator.OnCameraHit(path, rng, pixelIndex, ray, hit, pdfFromAncestor, throughput,
                    depth, toAncestorJacobian);
            }

            protected override void OnContinue(float pdfToAncestor, int depth) {
                // Update the reverse pdf of the previous vertex.
                // TODO this currently assumes that no splitting is happening!
                var lastVert = path.Vertices[^1];
                path.Vertices[^1] = new PathPdfPair {
                    PdfFromAncestor = lastVert.PdfFromAncestor,
                    PdfToAncestor = pdfToAncestor
                };
            }
        }
    }
}
