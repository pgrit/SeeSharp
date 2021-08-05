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
    public class VertexConnectionAndMerging : BidirBase {
        public bool RenderTechniquePyramid = false;

        public bool EnableConnections = true;
        public bool EnableLightTracing = true;
        public bool EnableNextEvent = true;
        public bool EnableBsdfLightHit = true;

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

            if (EnableConnections || NumLightPaths == 0) {
                // Classic Bidir requires exactly one light path for every camera path.
                NumLightPaths = scene.FrameBuffer.Width * scene.FrameBuffer.Height;
            }

            InitializeRadius(scene);

            base.Render(scene);

            // Store the technique pyramids
            if (RenderTechniquePyramid) {
                string pathRaw = Path.Join(scene.FrameBuffer.Basename, "techs-raw");
                techPyramidRaw.WriteToFiles(pathRaw);

                string pathWeighted = Path.Join(scene.FrameBuffer.Basename, "techs-weighted");
                techPyramidWeighted.WriteToFiles(pathWeighted);
            }
        }

        public override void ProcessPathCache() {
            if (EnableLightTracing) SplatLightVertices();
            if (EnableMerging) photonMap.Build(LightPaths, Radius);
        }

        protected virtual void OnMergeSample(RgbColor weight, float misWeight, CameraPath cameraPath,
                                        PathVertex lightVertex, float pdfCameraReverse,
                                        float pdfLightReverse, float pdfNextEvent) {}

        RgbColor Merge((CameraPath path, float cameraJacobian) userData, SurfacePoint hit, Vector3 outDir,
                       int pathIdx, int vertexIdx, float distSqr) {
            var photon = LightPaths.PathCache[pathIdx, vertexIdx];
            CameraPath path = userData.path;
            float cameraJacobian = userData.cameraJacobian;

            // Check that the path does not exceed the maximum length
            var depth = path.Vertices.Count + photon.Depth;
            if (depth > MaxDepth || depth < MinDepth)
                return RgbColor.Black;

            // Compute the contribution of the photon
            var ancestor = LightPaths.PathCache[pathIdx, photon.AncestorId];
            var dirToAncestor = Vector3.Normalize(ancestor.Point.Position - photon.Point.Position);
            var bsdfValue = hit.Material.Evaluate(hit, outDir, dirToAncestor, false);
            var photonContrib = photon.Weight * bsdfValue / NumLightPaths;

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
            float radiusSquared = Radius * Radius;
            photonContrib *= 2 * (radiusSquared - distSqr);
            photonContrib /= MathF.PI * radiusSquared * radiusSquared;

            RegisterSample(photonContrib * path.Throughput, misWeight, path.Pixel, path.Vertices.Count,
                photon.Depth, depth);
            OnMergeSample(photonContrib * path.Throughput, misWeight, path, photon, pdfCameraReverse,
                pdfLightReverse, pdfNextEvent);

            return photonContrib * misWeight;
        }

        public virtual RgbColor PerformMerging(Ray ray, SurfacePoint hit, CameraPath path, float cameraJacobian) {
            if (path.Vertices.Count == 1 && !MergePrimary) return RgbColor.Black;
            if (!EnableMerging) return RgbColor.Black;
            return photonMap.Accumulate((path, cameraJacobian), hit, -ray.Direction, Merge, Radius);
        }

        public override RgbColor OnCameraHit(CameraPath path, RNG rng, int pixelIndex, Ray ray,
                                             SurfacePoint hit, float pdfFromAncestor, RgbColor throughput,
                                             int depth, float toAncestorJacobian) {
            RgbColor value = RgbColor.Black;

            // Was a light hit?
            Emitter light = Scene.QueryEmitter(hit);
            if (light != null && (EnableBsdfLightHit || depth == 1) && depth >= MinDepth) {
                value += throughput * OnEmitterHit(light, hit, ray, path, toAncestorJacobian);
            }

            // Perform connections and merging if the maximum depth has not yet been reached
            if (depth < MaxDepth) {
                if (EnableConnections)
                    value += throughput *
                        BidirConnections(pixelIndex, hit, -ray.Direction, rng, path, toAncestorJacobian);

                value += throughput * PerformMerging(ray, hit, path, toAncestorJacobian);
            }

            if (EnableNextEvent && depth < MaxDepth && depth + 1 >= MinDepth)
                value += throughput * PerformNextEventEstimation(ray, hit, rng, path, toAncestorJacobian);

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
            pathPdfs.GatherLightPdfs(lightVertex, lastCameraVertexIdx - 1, numPdfs);

            // Set the pdf values that are unique to this combination of paths
            if (lastCameraVertexIdx > 0) // only if this is not the primary hit point
                pathPdfs.PdfsLightToCamera[lastCameraVertexIdx - 1] = pdfCameraReverse;
            pathPdfs.PdfsLightToCamera[lastCameraVertexIdx] = lightVertex.PdfFromAncestor;
            pathPdfs.PdfsCameraToLight[lastCameraVertexIdx] = cameraPath.Vertices[^1].PdfFromAncestor;
            pathPdfs.PdfsCameraToLight[lastCameraVertexIdx + 1] = pdfLightReverse + pdfNextEvent;

            // Compute the acceptance probability approximation
            float mergeApproximation = pathPdfs.PdfsLightToCamera[lastCameraVertexIdx]
                                     * MathF.PI * Radius * Radius * NumLightPaths;

            // Compute reciprocals for hypothetical connections along the camera sub-path
            float sumReciprocals = 0.0f;
            sumReciprocals +=
                CameraPathReciprocals(lastCameraVertexIdx, numPdfs, pathPdfs, cameraPath.Pixel)
                / mergeApproximation;
            sumReciprocals +=
                LightPathReciprocals(lastCameraVertexIdx, numPdfs, pathPdfs, cameraPath.Pixel)
                / mergeApproximation;

            // Add the reciprocal for the connection that replaces the last light path edge
            if (lightVertex.Depth > 1 && EnableConnections)
                sumReciprocals += 1 / mergeApproximation;

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
            if (EnableNextEvent) sumReciprocals += pdfNextEvent / pdfThis;

            // All connections along the camera path
            sumReciprocals +=
                CameraPathReciprocals(lastCameraVertexIdx - 1, numPdfs, pathPdfs, cameraPath.Pixel) / pdfThis;

            return 1 / sumReciprocals;
        }

        public override float LightTracerMis(PathVertex lightVertex, float pdfCamToPrimary, float pdfReverse,
                                             float pdfNextEvent, Vector2 pixel, float distToCam) {
            int numPdfs = lightVertex.Depth + 1;
            int lastCameraVertexIdx = -1;
            Span<float> camToLight = stackalloc float[numPdfs];
            Span<float> lightToCam = stackalloc float[numPdfs];

            var pathPdfs = new BidirPathPdfs(LightPaths.PathCache, lightToCam, camToLight);

            pathPdfs.GatherLightPdfs(lightVertex, lastCameraVertexIdx, numPdfs);

            pathPdfs.PdfsCameraToLight[0] = pdfCamToPrimary;
            pathPdfs.PdfsCameraToLight[1] = pdfReverse + pdfNextEvent;

            // Compute the actual weight
            float sumReciprocals = LightPathReciprocals(lastCameraVertexIdx, numPdfs, pathPdfs, pixel);
            sumReciprocals /= NumLightPaths;
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
            pathPdfs.GatherLightPdfs(lightVertex, lastCameraVertexIdx, numPdfs);

            // Set the pdf values that are unique to this combination of paths
            if (lastCameraVertexIdx > 0) // only if this is not the primary hit point
                pathPdfs.PdfsLightToCamera[lastCameraVertexIdx - 1] = pdfCameraReverse;
            pathPdfs.PdfsCameraToLight[lastCameraVertexIdx] = cameraPath.Vertices[^1].PdfFromAncestor;
            pathPdfs.PdfsLightToCamera[lastCameraVertexIdx] = pdfLightToCamera;
            pathPdfs.PdfsCameraToLight[lastCameraVertexIdx + 1] = pdfCameraToLight;
            pathPdfs.PdfsCameraToLight[lastCameraVertexIdx + 2] = pdfLightReverse + pdfNextEvent;

            // Compute reciprocals for hypothetical connections along the camera sub-path
            float sumReciprocals = 1.0f;
            sumReciprocals += CameraPathReciprocals(lastCameraVertexIdx, numPdfs, pathPdfs, cameraPath.Pixel);
            sumReciprocals += LightPathReciprocals(lastCameraVertexIdx, numPdfs, pathPdfs, cameraPath.Pixel);

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
            if (EnableBsdfLightHit) sumReciprocals += pdfHit / pdfNextEvent;

            // All bidirectional connections
            sumReciprocals += CameraPathReciprocals(lastCameraVertexIdx, numPdfs, pathPdfs, cameraPath.Pixel)
                / pdfNextEvent;

            return 1 / sumReciprocals;
        }

        protected virtual float CameraPathReciprocals(int lastCameraVertexIdx, int numPdfs,
                                                      BidirPathPdfs pdfs, Vector2 pixel) {
            float sumReciprocals = 0.0f;
            float nextReciprocal = 1.0f;
            for (int i = lastCameraVertexIdx; i > 0; --i) {
                // Merging at this vertex
                if (EnableMerging) {
                    float acceptProb = pdfs.PdfsLightToCamera[i] * MathF.PI * Radius * Radius;
                    sumReciprocals += nextReciprocal * NumLightPaths * acceptProb;
                }

                nextReciprocal *= pdfs.PdfsLightToCamera[i] / pdfs.PdfsCameraToLight[i];

                // Connecting this vertex to the next one along the camera path
                if (EnableConnections) sumReciprocals += nextReciprocal;
            }

            // Light tracer
            if (EnableLightTracing)
                sumReciprocals +=
                    nextReciprocal * pdfs.PdfsLightToCamera[0] / pdfs.PdfsCameraToLight[0] * NumLightPaths;

            // Merging directly visible (almost the same as the light tracer!)
            if (MergePrimary)
                sumReciprocals += nextReciprocal * NumLightPaths * pdfs.PdfsLightToCamera[0]
                                * MathF.PI * Radius * Radius;

            return sumReciprocals;
        }

        protected virtual float LightPathReciprocals(int lastCameraVertexIdx, int numPdfs, BidirPathPdfs pdfs,
                                                     Vector2 pixel) {
            float sumReciprocals = 0.0f;
            float nextReciprocal = 1.0f;
            for (int i = lastCameraVertexIdx + 1; i < numPdfs; ++i) {
                if (i < numPdfs - 1 && (MergePrimary || i > 0)) { // no merging on the emitter itself
                    // Account for merging at this vertex
                    if (EnableMerging) {
                        float acceptProb = pdfs.PdfsCameraToLight[i] * MathF.PI * Radius * Radius;
                        sumReciprocals += nextReciprocal * NumLightPaths * acceptProb;
                    }
                }

                nextReciprocal *= pdfs.PdfsCameraToLight[i] / pdfs.PdfsLightToCamera[i];

                // Account for connections from this vertex to its ancestor
                if (i < numPdfs - 2) // Connections to the emitter (next event) are treated separately
                    if (EnableConnections) sumReciprocals += nextReciprocal;
            }
            if (EnableBsdfLightHit || EnableNextEvent) sumReciprocals += nextReciprocal; // Next event and hitting the emitter directly
            // TODO / FIXME Bsdf and Nee can only be disabled jointly here: needs proper handling when assembling pdfs
            return sumReciprocals;
        }
    }
}
