using SeeSharp.Core;
using SeeSharp.Core.Geometry;
using SeeSharp.Core.Sampling;
using SeeSharp.Core.Shading;
using SeeSharp.Core.Shading.Emitters;
using SeeSharp.Integrators.Common;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace SeeSharp.Integrators.Bidir {
    public abstract class BidirBase : Integrator {
        public int NumIterations = 2;
        public int NumLightPaths = 0;
        public int MaxDepth = 10;

        public uint BaseSeedLight = 0xC030114u;
        public uint BaseSeedCamera = 0x13C0FEFEu;

        public Scene scene;
        public LightPathCache lightPaths;

        public struct PathPdfPair {
            public float PdfFromAncestor;
            public float PdfToAncestor;
        }

        public struct CameraPath {
            /// <summary>
            /// The pixel position where the path was started.
            /// </summary>
            public Vector2 Pixel;

            /// <summary>
            /// The product of the local estimators along the path (BSDF * cos / pdf)
            /// </summary>
            public ColorRGB Throughput;

            /// <summary>
            /// The pdf values for sampling this path.
            /// </summary>
            public List<PathPdfPair> Vertices;
        }

        /// <summary>
        /// Called once per iteration after the light paths have been traced.
        /// Use this to create acceleration structures etc.
        /// </summary>
        public abstract void ProcessPathCache();

        public virtual (Emitter, float, float) SelectLight(float primary) {
            float scaled = scene.Emitters.Count * primary;
            int idx = Math.Clamp((int)scaled, 0, scene.Emitters.Count - 1);
            var emitter = scene.Emitters[idx];
            return (emitter, 1.0f / scene.Emitters.Count, scaled - idx);
        }

        public virtual float SelectLightPmf(Emitter em) {
            return 1.0f / scene.Emitters.Count;
        }

        public virtual (Emitter, float, float) SelectLight(SurfacePoint from, float primary)
            => SelectLight(primary);

        public virtual float SelectLightPmf(SurfacePoint from, Emitter em)
            => SelectLightPmf(em);

        /// <summary>
        /// Called once for each pixel per iteration. Expected to perform some sort of path tracing,
        /// possibly connecting vertices with those from the light path cache.
        /// </summary>
        /// <returns>The estimated pixel value.</returns>
        public virtual ColorRGB EstimatePixelValue(SurfacePoint cameraPoint, Vector2 filmPosition, Ray primaryRay,
                                                   float pdfFromCamera, ColorRGB initialWeight, RNG rng) {
            // The pixel index determines which light path we connect to
            int row = Math.Min((int)filmPosition.Y, scene.FrameBuffer.Height - 1);
            int col = Math.Min((int)filmPosition.X, scene.FrameBuffer.Width - 1);
            int pixelIndex = row * scene.FrameBuffer.Width + col;
            var walk = new CameraRandomWalk(rng, filmPosition, pixelIndex, this);
            return walk.StartFromCamera(filmPosition, cameraPoint, pdfFromCamera, primaryRay, initialWeight);
        }

        public virtual void PostIteration(uint iteration) { }
        public virtual void PreIteration(uint iteration) { }

        public override void Render(Scene scene) {
            this.scene = scene;

            if (NumLightPaths <= 0) {
                NumLightPaths = scene.FrameBuffer.Width * scene.FrameBuffer.Height;
            }

            lightPaths = new LightPathCache { MaxDepth = MaxDepth, NumPaths = NumLightPaths, Scene = scene };

            for (uint iter = 0; iter < NumIterations; ++iter) {
                scene.FrameBuffer.StartIteration();
                PreIteration(iter);

                lightPaths.TraceAllPaths(iter,
                    (origin, primary, nextDirection) => NextEventPdf(primary.Point, origin.Point));
                ProcessPathCache();
                TraceAllCameraPaths(iter);

                scene.FrameBuffer.EndIteration();
                PostIteration(iter);
            }
        }

        private void TraceAllCameraPaths(uint iter) {
            Parallel.For(0, scene.FrameBuffer.Height,
                row => {
                    for (uint col = 0; col < scene.FrameBuffer.Width; ++col) {
                        uint pixelIndex = (uint)(row * scene.FrameBuffer.Width + col);
                        var seed = RNG.HashSeed(BaseSeedCamera, pixelIndex, (uint)iter);
                        var rng = new RNG(seed);
                        RenderPixel((uint)row, col, rng);
                    }
                }
            );
        }

        private void RenderPixel(uint row, uint col, RNG rng) {
            // Sample a ray from the camera
            var offset = rng.NextFloat2D();
            var filmSample = new Vector2(col, row) + offset;
            var cameraRay = scene.Camera.GenerateRay(filmSample, rng);
            var value = EstimatePixelValue(cameraRay.Point, filmSample, cameraRay.Ray,
                                           cameraRay.PdfRay, cameraRay.Weight, rng);

            // TODO we do nearest neighbor splatting manually here, to avoid numerical
            //      issues if the primary samples are almost 1 (400 + 0.99999999f = 401)
            scene.FrameBuffer.Splat((float)col, (float)row, value);
        }

        /// <summary>
        /// Called for each sample that has a non-zero contribution to the image.
        /// This can be used to write out pyramids of sampling technique images / MIS weights.
        /// The default implementation does nothing.
        /// </summary>
        /// <param name="cameraPathLength">Number of edges in the camera sub-path (0 if light tracer).</param>
        /// <param name="lightPathLength">Number of edges in the light sub-path (0 when hitting the light).</param>
        /// <param name="fullLength">Number of edges forming the full path. Used to disambiguate techniques.</param>
        public virtual void RegisterSample(ColorRGB weight, float misWeight, Vector2 pixel,
                                           int cameraPathLength, int lightPathLength, int fullLength) {
        }

        public abstract float LightTracerMis(PathVertex lightVertex, float pdfCamToPrimary, float pdfReverse,
                                             float pdfNextEvent, Vector2 pixel);

        public void SplatLightVertices() {
            Parallel.For(0, lightPaths.NumPaths, idx => {
                ConnectLightPathToCamera(idx);
            });
        }

        public void ConnectLightPathToCamera(int pathIdx) {
            lightPaths.ForEachVertex(pathIdx, (vertex, ancestor, dirToAncestor) => {
                // Compute image plane location
                var raster = scene.Camera.WorldToFilm(vertex.Point.Position);
                if (!raster.HasValue)
                    return;
                var pixel = new Vector2(raster.Value.X, raster.Value.Y);

                // Perform a change of variables from scene surface to pixel area.
                // TODO this could be computed by the camera itself...
                // First: map the scene surface to the solid angle about the camera
                var dirToCam = scene.Camera.Position - vertex.Point.Position;
                float distToCam = dirToCam.Length();
                float cosToCam = Math.Abs(Vector3.Dot(vertex.Point.Normal, dirToCam)) / distToCam;
                float surfaceToSolidAngle = cosToCam / (distToCam * distToCam);

                if (distToCam == 0 || cosToCam == 0)
                    return;

                // Second: map the solid angle to the pixel area
                float solidAngleToPixel = scene.Camera.SolidAngleToPixelJacobian(vertex.Point.Position);

                // Third: combine to get the full jacobian
                float surfaceToPixelJacobian = surfaceToSolidAngle * solidAngleToPixel;

                // Trace shadow ray
                if (scene.Raytracer.IsOccluded(vertex.Point, scene.Camera.Position))
                    return;

                var bsdfValue = vertex.Point.Material.Evaluate(vertex.Point, dirToAncestor, dirToCam, true);

                // Compute the surface area pdf of sampling the previous vertex instead
                float pdfReverse = vertex.Point.Material.Pdf(vertex.Point, dirToCam, dirToAncestor, false).Item1;
                if (ancestor.Point.Mesh != null) // Unless this is a background, i.e., a directional distribution
                    pdfReverse *= SampleWrap.SurfaceAreaToSolidAngle(vertex.Point, ancestor.Point);

                // Account for next event estimation
                float pdfNextEvent = 0.0f;
                if (vertex.Depth == 1) {
                    pdfNextEvent = NextEventPdf(vertex.Point, ancestor.Point);
                }

                float misWeight = LightTracerMis(vertex, surfaceToPixelJacobian, pdfReverse, pdfNextEvent, pixel);

                // Compute image contribution and splat
                ColorRGB weight = vertex.Weight * bsdfValue * surfaceToPixelJacobian / NumLightPaths;

                scene.FrameBuffer.Splat(pixel.X, pixel.Y, misWeight * weight);
                RegisterSample(weight, misWeight, pixel, 0, vertex.Depth, vertex.Depth + 1);
            });
        }

        public abstract float BidirConnectMis(CameraPath cameraPath, PathVertex lightVertex,
                                              float pdfCameraReverse, float pdfCameraToLight,
                                              float pdfLightReverse, float pdfLightToCamera,
                                              float pdfNextEvent);

        public virtual (int, int, float) SelectBidirPath(SurfacePoint cameraPoint, Vector3 outDir, int pixelIndex, RNG rng) {
            return (pixelIndex, -1, 1.0f);
        }

        public ColorRGB BidirConnections(int pixelIndex, SurfacePoint cameraPoint, Vector3 outDir,
                                         RNG rng, CameraPath path, float reversePdfJacobian) {
            ColorRGB result = ColorRGB.Black;

            // Select a path to connect to (based on pixel index)
            (int lightPathIdx, int lightVertIdx, float lightVertexProb) = SelectBidirPath(cameraPoint, outDir, pixelIndex, rng);

            void Connect(PathVertex vertex, PathVertex ancestor, Vector3 dirToAncestor) {
                // Only allow connections that do not exceed the maximum total path length
                int depth = vertex.Depth + path.Vertices.Count + 1;
                if (depth > MaxDepth) return;

                // Trace shadow ray
                if (scene.Raytracer.IsOccluded(vertex.Point, cameraPoint))
                    return;

                // Compute connection direction
                var dirFromCamToLight = vertex.Point.Position - cameraPoint.Position;

                var bsdfWeightLight = vertex.Point.Material.EvaluateWithCosine(vertex.Point, dirToAncestor, -dirFromCamToLight, true);
                var bsdfWeightCam = cameraPoint.Material.EvaluateWithCosine(cameraPoint, outDir, dirFromCamToLight, false);

                if (bsdfWeightCam == ColorRGB.Black || bsdfWeightLight == ColorRGB.Black)
                    return;

                // Compute the missing pdfs
                var (pdfCameraToLight, pdfCameraReverse) = cameraPoint.Material.Pdf(cameraPoint, outDir, dirFromCamToLight, false);
                pdfCameraReverse *= reversePdfJacobian;
                pdfCameraToLight *= SampleWrap.SurfaceAreaToSolidAngle(cameraPoint, vertex.Point);

                var (pdfLightToCamera, pdfLightReverse) = vertex.Point.Material.Pdf(vertex.Point, dirToAncestor, -dirFromCamToLight, true);
                if (ancestor.Point.Mesh != null) // only convert to surface area if this was an actual surface area sampler
                    pdfLightReverse *= SampleWrap.SurfaceAreaToSolidAngle(vertex.Point, ancestor.Point);
                pdfLightToCamera *= SampleWrap.SurfaceAreaToSolidAngle(vertex.Point, cameraPoint);

                float pdfNextEvent = 0.0f;
                if (vertex.Depth == 1) {
                    pdfNextEvent = NextEventPdf(vertex.Point, ancestor.Point);
                }

                float misWeight = BidirConnectMis(path, vertex, pdfCameraReverse, pdfCameraToLight, pdfLightReverse,
                                                  pdfLightToCamera, pdfNextEvent);
                float distanceSqr = (cameraPoint.Position - vertex.Point.Position).LengthSquared();

                // Avoid NaNs in rare cases
                if (distanceSqr == 0)
                    return;

                ColorRGB weight = vertex.Weight * bsdfWeightLight * bsdfWeightCam / distanceSqr / lightVertexProb;
                result += misWeight * weight;

                RegisterSample(weight * path.Throughput, misWeight, path.Pixel,
                               path.Vertices.Count, vertex.Depth, depth);
            }

            if (lightVertIdx > 0 && lightVertIdx < lightPaths.PathCache.Length(lightPathIdx)) { // specific vertex selected
                var vertex = lightPaths.PathCache[lightPathIdx, lightVertIdx];
                var ancestor = lightPaths.PathCache[lightPathIdx, lightVertIdx - 1];
                var dirToAncestor = ancestor.Point.Position - vertex.Point.Position;
                Connect(vertex, ancestor, dirToAncestor);
            } else if (lightPathIdx >= 0) { // Connect with all vertices along the path
                lightPaths.ForEachVertex(lightPathIdx, Connect);
            }

            return result;
        }

        public abstract float NextEventMis(CameraPath cameraPath, float pdfEmit, float pdfNextEvent, float pdfHit, float pdfReverse);

        public virtual float NextEventPdf(SurfacePoint from, SurfacePoint to) {
            if (to.Mesh == null) { // Background
                var direction = to.Position - from.Position;
                return scene.Background.DirectionPdf(direction);
            } else { // Emissive object
                var emitter = scene.QueryEmitter(to);
                return emitter.PdfArea(to) * SelectLightPmf(from, emitter);
            }
        }

        public virtual (Emitter, SurfaceSample) SampleNextEvent(SurfacePoint from, RNG rng) {
            var (light, lightProb, _) = SelectLight(from, rng.NextFloat());
            var lightSample = light.SampleArea(rng.NextFloat2D());
            lightSample.pdf *= lightProb;
            return (light, lightSample);
        }

        public virtual float ComputeNextEventBackgroundProbability(/*SurfacePoint from*/)
            => scene.Background == null ? 0 : 1 / (1.0f + scene.Emitters.Count);

        public ColorRGB PerformNextEventEstimation(Ray ray, SurfacePoint hit, RNG rng, CameraPath path,
                                                   float reversePdfJacobian) {
            float backgroundProbability = ComputeNextEventBackgroundProbability(/*hit*/);
            if (rng.NextFloat() < backgroundProbability) { // Connect to the background
                if (scene.Background == null)
                    return ColorRGB.Black; // There is no background

                var sample = scene.Background.SampleDirection(rng.NextFloat2D());
                sample.Pdf *= backgroundProbability;
                sample.Weight /= backgroundProbability;
                if (scene.Raytracer.LeavesScene(hit, sample.Direction)) {
                    var bsdfTimesCosine = hit.Material.EvaluateWithCosine(hit, -ray.Direction, sample.Direction, false);

                    // Compute the reverse BSDF sampling pdf
                    var (bsdfForwardPdf, bsdfReversePdf) = hit.Material.Pdf(hit, -ray.Direction, sample.Direction, false);
                    bsdfReversePdf *= reversePdfJacobian;

                    // Compute emission pdf
                    float pdfEmit = scene.Background.RayPdf(hit.Position, -sample.Direction);
                    pdfEmit *= lightPaths.SelectLightPmf(null);

                    // Compute the mis weight
                    float misWeight = NextEventMis(path, pdfEmit, sample.Pdf, bsdfForwardPdf, bsdfReversePdf);

                    // Compute and log the final sample weight
                    var weight = sample.Weight * bsdfTimesCosine;
                    RegisterSample(weight * path.Throughput, misWeight, path.Pixel,
                                   path.Vertices.Count, 0, path.Vertices.Count + 1);
                    return misWeight * weight;
                }
            } else { // Connect to an emissive surface
                if (scene.Emitters.Count == 0)
                    return ColorRGB.Black;

                // Sample a point on the light source
                var (light, lightSample) = SampleNextEvent(hit, rng);
                lightSample.pdf *= (1 - backgroundProbability);
                if (!scene.Raytracer.IsOccluded(hit, lightSample.point)) {
                    Vector3 lightToSurface = hit.Position - lightSample.point.Position;
                    var emission = light.EmittedRadiance(lightSample.point, lightToSurface);

                    var bsdfTimesCosine = hit.Material.EvaluateWithCosine(hit, -ray.Direction, -lightToSurface, false);
                    if (bsdfTimesCosine == ColorRGB.Black)
                        return ColorRGB.Black;

                    // Compute the jacobian for surface area -> solid angle
                    // (Inverse of the jacobian for solid angle pdf -> surface area pdf)
                    float jacobian = SampleWrap.SurfaceAreaToSolidAngle(hit, lightSample.point);
                    if (jacobian == 0) return ColorRGB.Black;

                    // Compute the missing pdf terms
                    var (bsdfForwardPdf, bsdfReversePdf) = hit.Material.Pdf(hit, -ray.Direction, -lightToSurface, false);
                    bsdfForwardPdf *= SampleWrap.SurfaceAreaToSolidAngle(hit, lightSample.point);
                    bsdfReversePdf *= reversePdfJacobian;

                    float pdfEmit = light.PdfRay(lightSample.point, lightToSurface);
                    pdfEmit *= SampleWrap.SurfaceAreaToSolidAngle(lightSample.point, hit);
                    pdfEmit *= lightPaths.SelectLightPmf(light);

                    float misWeight = NextEventMis(path, pdfEmit, lightSample.pdf, bsdfForwardPdf, bsdfReversePdf);

                    var weight = emission * bsdfTimesCosine * (jacobian / lightSample.pdf);
                    RegisterSample(weight * path.Throughput, misWeight, path.Pixel,
                                   path.Vertices.Count, 0, path.Vertices.Count + 1);
                    return misWeight * weight;
                }
            }

            return ColorRGB.Black;
        }

        public abstract float EmitterHitMis(CameraPath cameraPath, float pdfEmit, float pdfNextEvent);

        public ColorRGB OnEmitterHit(Emitter emitter, SurfacePoint hit, Ray ray,
                                     CameraPath path, float reversePdfJacobian) {
            var emission = emitter.EmittedRadiance(hit, -ray.Direction);

            // Compute pdf values
            float pdfEmit = emitter.PdfRay(hit, -ray.Direction);
            pdfEmit *= reversePdfJacobian;
            pdfEmit *= lightPaths.SelectLightPmf(emitter);
            float pdfNextEvent = NextEventPdf(new SurfacePoint(), hit); // TODO get the actual previous point!

            float misWeight = EmitterHitMis(path, pdfEmit, pdfNextEvent);
            RegisterSample(emission * path.Throughput, misWeight, path.Pixel,
                           path.Vertices.Count, 0, path.Vertices.Count);
            return misWeight * emission;
        }

        public ColorRGB OnBackgroundHit(Ray ray, CameraPath path) {
            if (scene.Background == null)
                return ColorRGB.Black;

            // Compute the pdf of sampling the previous point by emission from the background
            float pdfEmit = scene.Background.RayPdf(ray.Origin, -ray.Direction);
            pdfEmit *= lightPaths.SelectLightPmf(null);

            // Compute the pdf of sampling the same connection via next event estimation
            float pdfNextEvent = scene.Background.DirectionPdf(ray.Direction);
            float backgroundProbability = ComputeNextEventBackgroundProbability(/*hit*/);
            pdfNextEvent *= backgroundProbability;

            float misWeight = EmitterHitMis(path, pdfEmit, pdfNextEvent);
            var emission = scene.Background.EmittedRadiance(ray.Direction);
            RegisterSample(emission * path.Throughput, misWeight, path.Pixel,
                           path.Vertices.Count, 0, path.Vertices.Count);
            return misWeight * emission * path.Throughput;
        }

        public abstract ColorRGB OnCameraHit(CameraPath path, RNG rng, int pixelIndex, Ray ray, SurfacePoint hit,
                                             float pdfFromAncestor, ColorRGB throughput, int depth,
                                             float toAncestorJacobian);

        class CameraRandomWalk : RandomWalk {
            int pixelIndex;
            BidirBase integrator;
            CameraPath path;

            public CameraRandomWalk(RNG rng, Vector2 filmPosition, int pixelIndex, BidirBase integrator)
                : base(integrator.scene, rng, integrator.MaxDepth + 1) {
                this.pixelIndex = pixelIndex;
                this.integrator = integrator;
                path.Vertices = new List<PathPdfPair>(integrator.MaxDepth);
                path.Pixel = filmPosition;
            }

            protected override ColorRGB OnInvalidHit(Ray ray, float pdfFromAncestor, ColorRGB throughput, int depth) {
                path.Vertices.Add(new PathPdfPair {
                    PdfFromAncestor = pdfFromAncestor,
                    PdfToAncestor = 0
                });
                path.Throughput = throughput;
                return integrator.OnBackgroundHit(ray, path);
            }

            protected override ColorRGB OnHit(Ray ray, SurfacePoint hit, float pdfFromAncestor, ColorRGB throughput,
                                              int depth, float toAncestorJacobian) {
                path.Vertices.Add(new PathPdfPair {
                    PdfFromAncestor = pdfFromAncestor,
                    PdfToAncestor = 0
                });
                path.Throughput = throughput;
                return integrator.OnCameraHit(path, rng, pixelIndex, ray, hit, pdfFromAncestor,
                                              throughput, depth, toAncestorJacobian);
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
