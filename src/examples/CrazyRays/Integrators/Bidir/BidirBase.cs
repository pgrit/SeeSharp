using GroundWrapper;
using GroundWrapper.Geometry;
using GroundWrapper.Sampling;
using GroundWrapper.Shading;
using GroundWrapper.Shading.Emitters;
using Integrators.Common;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Integrators.Bidir {
    public abstract class BidirBase : Integrator {
        public int NumIterations = 2;
        public int NumLightPaths = 0;
        public int MaxDepth = 10;

        public uint BaseSeedLight = 0xC030114u;
        public uint BaseSeedCamera = 0x13C0FEFEu;

        public Scene scene;
        public LightPathCache lightPaths;

        public struct PathPdfPair {
            public float pdfFromAncestor;
            public float pdfToAncestor;
        }

        public struct CameraPath {
            /// <summary>
            /// The pixel position where the path was started.
            /// </summary>
            public Vector2 pixel;

            /// <summary>
            /// The product of the local estimators along the path (BSDF * cos / pdf)
            /// </summary>
            public ColorRGB throughput;

            /// <summary>
            /// The pdf values for sampling this path.
            /// </summary>
            public List<PathPdfPair> vertices;
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

        public virtual float NextEventPdf(SurfacePoint from, SurfacePoint to) {
            var emitter = scene.QueryEmitter(to);
            float pdf = emitter.PdfArea(to) * SelectLightPmf(from, emitter);
            return pdf;
        }

        /// <summary>
        /// Called once for each pixel per iteration. Expected to perform some sort of path tracing,
        /// possibly connecting vertices with those from the light path cache.
        /// </summary>
        /// <returns>The estimated pixel value.</returns>
        public virtual ColorRGB EstimatePixelValue(SurfacePoint cameraPoint, Vector2 filmPosition, Ray primaryRay,
                                                   float pdfFromCamera, ColorRGB initialWeight, RNG rng) {
            // The pixel index determines which light path we connect to
            int pixelIndex = (int)filmPosition.Y * scene.FrameBuffer.Width + (int)filmPosition.X;
            var walk = new CameraRandomWalk(rng, filmPosition, pixelIndex, this);
            return walk.StartFromCamera(filmPosition, cameraPoint, pdfFromCamera, primaryRay, initialWeight);
        }

        public override void Render(Scene scene) {
            this.scene = scene;

            if (NumLightPaths <= 0) {
                NumLightPaths = scene.FrameBuffer.Width * scene.FrameBuffer.Height;
            }

            lightPaths = new LightPathCache { MaxDepth = MaxDepth, NumPaths = NumLightPaths, scene = scene };

            void AddNextEventPdf(ref PathVertex vertex, PathVertex ancestor, Vector3 nextDirection) {
                if (vertex.depth == 1) {
                    vertex.pdfToAncestor += NextEventPdf(vertex.point, ancestor.point);
                }
            }

            for (uint iter = 0; iter < NumIterations; ++iter) {
                //var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                //Console.WriteLine($"starting iteration{iter}");
                lightPaths.TraceAllPaths(iter, AddNextEventPdf);
                //stopwatch.Stop();
                //Console.WriteLine($"done tracing light paths - {stopwatch.ElapsedMilliseconds}ms");
                //stopwatch.Restart();
                ProcessPathCache();
                //stopwatch.Stop();
                //Console.WriteLine($"done processing - {stopwatch.ElapsedMilliseconds}ms");
                //stopwatch.Restart();
                TraceAllCameraPaths(iter);
                //stopwatch.Stop();
                //Console.WriteLine($"done with camera pass - {stopwatch.ElapsedMilliseconds}ms");
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
            Ray primaryRay = scene.Camera.GenerateRay(filmSample);

            // Compute the corresponding solid angle pdf (required for MIS)
            float pdfFromCamera = scene.Camera.SolidAngleToPixelJacobian(primaryRay.direction); // TODO this should be returned by Camera.Sample() which should replace GenerateRay() to follow conventions similar to the BSDF system
            var initialWeight = ColorRGB.White; // TODO this should be computed by the camera and returned by SampleCamera()
            var cameraPoint = new SurfacePoint {
                position = scene.Camera.Position,
                normal = scene.Camera.Direction
            };

            var value = EstimatePixelValue(cameraPoint, filmSample, primaryRay, pdfFromCamera, initialWeight, rng);
            value = value * (1.0f / NumIterations);

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

        public abstract float LightTracerMis(PathVertex lightVertex, float pdfCamToPrimary, float pdfReverse);

        public void SplatLightVertices() {
            Parallel.For(0, lightPaths.endpoints.Length, idx => {
                ConnectLightPathToCamera(lightPaths.endpoints[idx]);
            });
        }

        public void ConnectLightPathToCamera(int endpoint) {
            lightPaths.ForEachVertex(endpoint, (vertex, ancestor, dirToAncestor) => {
                // Compute image plane location
                var raster = scene.Camera.WorldToFilm(vertex.point.position);
                if (!raster.HasValue)
                    return;

                // Perform a change of variables from scene surface to pixel area.
                // TODO this could be computed by the camera itself...
                // First: map the scene surface to the solid angle about the camera
                var dirToCam = scene.Camera.Position - vertex.point.position;
                float distToCam = dirToCam.Length();
                float cosToCam = Math.Abs(Vector3.Dot(vertex.point.normal, dirToCam)) / distToCam;
                float surfaceToSolidAngle = cosToCam / (distToCam * distToCam);

                if (distToCam == 0 || cosToCam == 0)
                    return;

                // Second: map the solid angle to the pixel area
                float solidAngleToPixel = scene.Camera.SolidAngleToPixelJacobian(vertex.point.position);

                // Third: combine to get the full jacobian 
                float surfaceToPixelJacobian = surfaceToSolidAngle * solidAngleToPixel;

                // Trace shadow ray
                if (scene.Raytracer.IsOccluded(vertex.point, scene.Camera.Position))
                    return;

                var bsdf = vertex.point.Bsdf;
                var bsdfValue = bsdf.EvaluateBsdfOnly(dirToAncestor, dirToCam, true);

                // Compute the surface area pdf of sampling the previous vertex instead
                float pdfReverse = bsdf.Pdf(dirToCam, dirToAncestor, false).Item1;
                pdfReverse *= SampleWrap.SurfaceAreaToSolidAngle(vertex.point, ancestor.point);

                // Account for next event estimation
                if (vertex.depth == 1) {
                    pdfReverse += NextEventPdf(vertex.point, ancestor.point);
                }

                float misWeight = LightTracerMis(vertex, surfaceToPixelJacobian, pdfReverse);

                // Compute image contribution and splat
                ColorRGB weight = vertex.weight * bsdfValue * surfaceToPixelJacobian / NumLightPaths;
                scene.FrameBuffer.Splat(raster.Value.X, raster.Value.Y, misWeight * weight * (1.0f / NumIterations));

                var pixel = new Vector2(raster.Value.X, raster.Value.Y);
                RegisterSample(weight, misWeight, pixel, 0, vertex.depth, vertex.depth + 1);
            });
        }

        public abstract float BidirConnectMis(CameraPath cameraPath, PathVertex lightVertex,
                                              float pdfCameraReverse, float pdfCameraToLight,
                                              float pdfLightReverse, float pdfLightToCamera);

        public virtual (int, bool) SelectBidirPath(int pixelIndex, RNG rng) {
            return (lightPaths.endpoints[pixelIndex], true);
        }

        public ColorRGB BidirConnections(int pixelIndex, SurfacePoint cameraPoint, Vector3 outDir,
                                         RNG rng, CameraPath path, float reversePdfJacobian) {
            ColorRGB result = ColorRGB.Black;

            // Select a path to connect to (based on pixel index)
            (int lightEndpoint, bool connectAncestors) = SelectBidirPath(pixelIndex, rng);

            // Precompute the bsdf once, avoids multiple texture lookups and allocations
            var cameraBsdf = cameraPoint.Bsdf;

            void Connect(PathVertex vertex, PathVertex ancestor, Vector3 dirToAncestor) {
                // Only allow connections that do not exceed the maximum total path length
                int depth = vertex.depth + path.vertices.Count + 1;
                if (depth > MaxDepth) return;

                // Trace shadow ray
                if (scene.Raytracer.IsOccluded(vertex.point, cameraPoint))
                    return;

                // Precompute the bsdf once, avoids multiple texture lookups and allocations
                var vertexBsdf = vertex.point.Bsdf;

                // Compute connection direction
                var dirFromCamToLight = vertex.point.position - cameraPoint.position;

                var bsdfWeightLight = vertexBsdf.EvaluateWithCosine(dirToAncestor, -dirFromCamToLight, true);
                var bsdfWeightCam = cameraBsdf.EvaluateWithCosine(outDir, dirFromCamToLight, false);

                if (bsdfWeightCam == ColorRGB.Black || bsdfWeightLight == ColorRGB.Black)
                    return;

                // Compute the missing pdfs
                var (pdfCameraToLight, pdfCameraReverse) = cameraBsdf.Pdf(outDir, dirFromCamToLight, false);
                pdfCameraReverse *= reversePdfJacobian;
                pdfCameraToLight *= SampleWrap.SurfaceAreaToSolidAngle(cameraPoint, vertex.point);

                var (pdfLightToCamera, pdfLightReverse) = vertexBsdf.Pdf(dirToAncestor, -dirFromCamToLight, true);
                pdfLightReverse *= SampleWrap.SurfaceAreaToSolidAngle(vertex.point, ancestor.point);
                pdfLightToCamera *= SampleWrap.SurfaceAreaToSolidAngle(vertex.point, cameraPoint);

                if (vertex.depth == 1) {
                    pdfLightReverse += NextEventPdf(vertex.point, ancestor.point);
                }

                float misWeight = BidirConnectMis(path, vertex, pdfCameraReverse, pdfCameraToLight, pdfLightReverse,
                                                  pdfLightToCamera);
                float distanceSqr = (cameraPoint.position - vertex.point.position).LengthSquared();

                // Avoid NaNs in rare cases
                if (distanceSqr == 0)
                    return;

                ColorRGB weight = vertex.weight * bsdfWeightLight * bsdfWeightCam / distanceSqr;
                result += misWeight * weight;

                RegisterSample(weight * path.throughput, misWeight, path.pixel,
                               path.vertices.Count, vertex.depth, depth);
            }

            if (!connectAncestors) {
                var vertex = lightPaths.pathCache[lightEndpoint];
                if (vertex.ancestorId == -1)
                    return ColorRGB.Black;
                var ancestor = lightPaths.pathCache[vertex.ancestorId];
                var dirToAncestor = ancestor.point.position - vertex.point.position;
                Connect(vertex, ancestor, dirToAncestor);
                return result;
            } else {
                // Connect with all vertices along the path
                lightPaths.ForEachVertex(lightEndpoint, Connect);
                return result;
            }
        }

        public abstract float NextEventMis(CameraPath cameraPath, float pdfEmit, float pdfNextEvent, float pdfHit, float pdfReverse);

        public ColorRGB PerformNextEventEstimation(Ray ray, SurfacePoint hit, RNG rng, CameraPath path, float reversePdfJacobian) {
            // Sample a point on the light source
            var (light, lightProb, _) = SelectLight(hit, rng.NextFloat());
            var lightSample = light.SampleArea(rng.NextFloat2D());
            lightSample.pdf *= lightProb;

            if (!scene.Raytracer.IsOccluded(hit, lightSample.point)) {
                Vector3 lightToSurface = hit.position - lightSample.point.position;
                var emission = light.EmittedRadiance(lightSample.point, lightToSurface);

                var bsdf = hit.Bsdf;
                var bsdfTimesCosine = bsdf.EvaluateWithCosine(-ray.direction, -lightToSurface, false);
                if (bsdfTimesCosine == ColorRGB.Black)
                    return ColorRGB.Black;

                // Compute the jacobian for surface area -> solid angle
                // (Inverse of the jacobian for solid angle pdf -> surface area pdf)
                float jacobian = SampleWrap.SurfaceAreaToSolidAngle(hit, lightSample.point);
                if (jacobian == 0)
                    return ColorRGB.Black;

                // Compute the missing pdf terms
                var (bsdfForwardPdf, bsdfReversePdf) = bsdf.Pdf(-ray.direction, -lightToSurface, false);
                bsdfForwardPdf *= SampleWrap.SurfaceAreaToSolidAngle(hit, lightSample.point);
                bsdfReversePdf *= reversePdfJacobian;

                float pdfEmit = light.PdfRay(lightSample.point, lightToSurface);
                pdfEmit *= SampleWrap.SurfaceAreaToSolidAngle(lightSample.point, hit);

                float misWeight = NextEventMis(path, pdfEmit, lightSample.pdf, bsdfForwardPdf, bsdfReversePdf);

                var weight = emission * bsdfTimesCosine * (jacobian / lightSample.pdf);
                RegisterSample(weight * path.throughput, misWeight, path.pixel, 
                               path.vertices.Count, 0, path.vertices.Count + 1);
                return misWeight * weight;
            }

            return ColorRGB.Black;
        }

        public abstract float EmitterHitMis(CameraPath cameraPath, float pdfEmit, float pdfNextEvent);

        public ColorRGB OnEmitterHit(Emitter emitter, SurfacePoint hit, Ray ray, 
                                     CameraPath path, float reversePdfJacobian) {
            var emission = emitter.EmittedRadiance(hit, -ray.direction);

            // Compute pdf values
            float pdfEmit = emitter.PdfRay(hit, -ray.direction);
            pdfEmit *= reversePdfJacobian;
            float pdfNextEvent = emitter.PdfArea(hit) / scene.Emitters.Count; // TODO use NextEventPdf() and the previous hit point!

            float misWeight = EmitterHitMis(path, pdfEmit, pdfNextEvent);
            RegisterSample(emission * path.throughput, misWeight, path.pixel,
                           path.vertices.Count, 0, path.vertices.Count);

            var value = misWeight * emission;
            return value;
        }

        public abstract ColorRGB OnCameraHit(CameraPath path, RNG rng, int pixelIndex, Ray ray,
                                             SurfacePoint hit, float pdfFromAncestor,
                                             float pdfToAncestor, ColorRGB throughput, int depth,
                                             float toAncestorJacobian);

        class CameraRandomWalk : RandomWalk {
            int pixelIndex;
            BidirBase integrator;
            CameraPath path;

            public CameraRandomWalk(RNG rng, Vector2 filmPosition, int pixelIndex, BidirBase integrator)
                : base(integrator.scene, rng, integrator.MaxDepth + 1) {
                this.pixelIndex = pixelIndex;
                this.integrator = integrator;
                path.vertices = new List<PathPdfPair>(integrator.MaxDepth);
                path.pixel = filmPosition;
            }

            protected override ColorRGB OnHit(Ray ray, SurfacePoint hit, float pdfFromAncestor,
                                              float pdfToAncestor, ColorRGB throughput, int depth,
                                              float toAncestorJacobian, Vector3 nextDirection) {
                path.vertices.Add(new PathPdfPair { 
                    pdfFromAncestor = pdfFromAncestor, 
                    pdfToAncestor = pdfToAncestor 
                });

                path.throughput = throughput;

                return integrator.OnCameraHit(path, rng, pixelIndex, ray, hit, pdfFromAncestor, 
                                              pdfToAncestor, throughput, depth, toAncestorJacobian);
            }

            protected override ColorRGB OnInvalidHit() => ColorRGB.Black;
        }
    }
}
