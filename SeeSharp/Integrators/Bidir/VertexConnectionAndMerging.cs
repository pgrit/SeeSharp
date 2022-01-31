using SeeSharp.Geometry;
using SeeSharp.Integrators.Common;
using SeeSharp.Sampling;
using SeeSharp.Shading.Emitters;
using SimpleImageIO;
using System;
using System.IO;
using System.Numerics;
using TinyEmbree;

namespace SeeSharp.Integrators.Bidir {
    public class VertexConnectionAndMerging : VertexCacheBidir {
        /// <summary>Whether or not to use merging at the first hit from the camera.</summary>
        public bool MergePrimary = false;

        public bool EnableMerging = true;

        TechPyramid techPyramidRaw;
        TechPyramid techPyramidWeighted;

        protected PhotonHashGrid photonMap = new PhotonHashGrid();

        public float Radius;

        /// <summary>
        /// Initializes the radius for photon mapping. The default implementation samples three rays
        /// on the diagonal of the image. The average pixel footprints at these positions are used to compute
        /// a radius that roughly spans a 3x3 pixel area in the image plane.
        /// </summary>
        public virtual void InitializeRadius(Scene scene) {
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
                Radius = scene.Radius / 300.0f;
                return;
            }

            averageArea /= numHits;

            // Compute the radius based on the approximated average footprint area
            // Our heuristic aims to have each photon cover roughly a 1.5 x 1.5 region on the image
            Radius = MathF.Sqrt(averageArea) * 1.5f / 2.0f;
        }

        public virtual void ShrinkRadius() { }

        protected override void PostIteration(uint iteration) {
            ShrinkRadius();
        }

        protected override void RegisterSample(RgbColor weight, float misWeight, Vector2 pixel,
                                               int cameraPathLength, int lightPathLength, int fullLength) {
            if (!RenderTechniquePyramid)
                return;
            weight /= NumIterations;
            techPyramidRaw.Add(cameraPathLength, lightPathLength, fullLength, pixel, weight);
            techPyramidWeighted.Add(cameraPathLength, lightPathLength, fullLength, pixel, weight * misWeight);
        }

        public override void Render(Scene scene) {
            if (RenderTechniquePyramid) {
                techPyramidRaw = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                                                 minDepth: 1, maxDepth: MaxDepth, merges: true);
                techPyramidWeighted = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                                                      minDepth: 1, maxDepth: MaxDepth, merges: true);
            }

            InitializeRadius(scene);

            base.Render(scene);

            // Store the technique pyramids
            // TODO we are rendering some of these twice and replacing all but the merging ones, now that
            //      the base class has changed! Only write merges here.
            if (RenderTechniquePyramid) {
                string pathRaw = Path.Join(scene.FrameBuffer.Basename, "techs-raw");
                techPyramidRaw.WriteToFiles(pathRaw);

                string pathWeighted = Path.Join(scene.FrameBuffer.Basename, "techs-weighted");
                techPyramidWeighted.WriteToFiles(pathWeighted);
            }
        }

        protected override void ProcessPathCache() {
            base.ProcessPathCache();
            if (EnableMerging) photonMap.Build(LightPaths, Radius);
        }

        protected virtual void OnMergeSample(RgbColor weight, float kernelWeight, float misWeight, CameraPath cameraPath,
                                        PathVertex lightVertex, float pdfCameraReverse,
                                        float pdfLightReverse, float pdfNextEvent) { }

        RgbColor Merge((CameraPath path, float cameraJacobian) userData, SurfacePoint hit, Vector3 outDir,
                       int pathIdx, int vertexIdx, float distSqr, float radiusSquared) {
            var photon = LightPaths.PathCache[pathIdx, vertexIdx];
            CameraPath path = userData.path;
            float cameraJacobian = userData.cameraJacobian;

            // Check that the path does not exceed the maximum length
            var depth = path.Vertices.Count + photon.Depth;
            if (depth > MaxDepth || depth < MinDepth)
                return RgbColor.Black;

            // Compute the contribution of the photon
            var ancestor = LightPaths.PathCache[pathIdx, photon.AncestorId];
            var dirToAncestor = Vector3.Normalize(ancestor.Point.Position - hit.Position);
            var bsdfValue = hit.Material.Evaluate(hit, outDir, dirToAncestor, false);
            var photonContrib = photon.Weight * bsdfValue / NumLightPaths.Value;

            // Compute the missing pdf terms and the MIS weight
            var (pdfLightReverse, pdfCameraReverse) =
                hit.Material.Pdf(hit, outDir, dirToAncestor, false);
            pdfCameraReverse *= cameraJacobian;
            pdfLightReverse *= SampleWarp.SurfaceAreaToSolidAngle(hit, ancestor.Point);
            float pdfNextEvent = (photon.Depth == 1) ? NextEventPdf(hit, ancestor.Point) : 0;
            float misWeight = MergeMis(path, photon, pdfCameraReverse, pdfLightReverse, pdfNextEvent);

            // Prevent NaNs in corner cases
            if (photonContrib == RgbColor.Black || pdfCameraReverse == 0 || pdfLightReverse == 0)
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
            return MathF.Min(footprint, Radius);
        }

        public virtual RgbColor PerformMerging(Ray ray, SurfacePoint hit, CameraPath path, float cameraJacobian) {
            if (path.Vertices.Count == 1 && !MergePrimary) return RgbColor.Black;
            if (!EnableMerging) return RgbColor.Black;
            float localRadius = ComputeLocalMergeRadius(path.Distances[0]);
            return photonMap.Accumulate((path, cameraJacobian), hit, -ray.Direction, Merge, localRadius);
        }

        protected override RgbColor OnCameraHit(CameraPath path, RNG rng, Ray ray, SurfacePoint hit,
                                                float pdfFromAncestor, RgbColor throughput, int depth,
                                                float toAncestorJacobian) {
            RgbColor value = RgbColor.Black;

            // Was a light hit?
            Emitter light = Scene.QueryEmitter(hit);
            if (light != null && (EnableHitting || depth == 1) && depth >= MinDepth) {
                value += throughput * OnEmitterHit(light, hit, ray, path, toAncestorJacobian);
            }

            // Perform connections and merging if the maximum depth has not yet been reached
            if (depth < MaxDepth) {
                for (int i = 0; i < NumConnections && EnableConnections; ++i) {
                    value += throughput * BidirConnections(hit, -ray.Direction, rng, path, toAncestorJacobian);
                }
                value += throughput * PerformMerging(ray, hit, path, toAncestorJacobian);
            }

            if (depth < MaxDepth && depth + 1 >= MinDepth) {
                for (int i = 0; i < NumShadowRays; ++i) {
                    value += throughput * PerformNextEventEstimation(ray, hit, rng, path, toAncestorJacobian);
                }
            }

            return value;
        }

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
            if (lightVertex.Depth > 1 && EnableConnections)
                sumReciprocals += BidirSelectDensity() / mergeApproximation;

            return 1 / sumReciprocals;
        }

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

        public override float LightTracerMis(PathVertex lightVertex, float pdfCamToPrimary, float pdfReverse,
                                             float pdfNextEvent, Vector2 pixel, float distToCam) {
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
                / BidirSelectDensity();
            sumReciprocals += LightPathReciprocals(lastCameraVertexIdx, pathPdfs, cameraPath.Pixel, radius)
                / BidirSelectDensity();

            return 1 / sumReciprocals;
        }

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

        protected virtual float CameraPathReciprocals(int lastCameraVertexIdx, BidirPathPdfs pdfs,
                                                      Vector2 pixel, float radius) {
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
                if (EnableConnections) sumReciprocals += nextReciprocal * BidirSelectDensity();
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

        protected virtual float LightPathReciprocals(int lastCameraVertexIdx, BidirPathPdfs pdfs,
                                                     Vector2 pixel, float radius) {
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
                    if (EnableConnections) sumReciprocals += nextReciprocal * BidirSelectDensity();
            }
            if (EnableHitting || NumShadowRays != 0) sumReciprocals += nextReciprocal; // Next event and hitting the emitter directly
            // TODO / FIXME Bsdf and Nee can only be disabled jointly here: needs proper handling when assembling pdfs
            return sumReciprocals;
        }
    }
}
