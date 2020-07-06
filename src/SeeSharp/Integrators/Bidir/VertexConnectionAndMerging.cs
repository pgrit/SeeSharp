using SeeSharp.Core;
using SeeSharp.Core.Geometry;
using SeeSharp.Core.Sampling;
using SeeSharp.Core.Shading;
using SeeSharp.Core.Shading.Emitters;
using SeeSharp.Integrators.Common;
using System;
using System.Numerics;

namespace SeeSharp.Integrators.Bidir {
    public class VertexConnectionAndMerging : BidirBase {
        public bool RenderTechniquePyramid = false;

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
            var primarySamples = new Vector2[] {
                new Vector2(0.5f, 0.5f),
                new Vector2(0.25f, 0.25f),
                new Vector2(0.75f, 0.75f)
            };
            var resolution = new Vector2(scene.FrameBuffer.Width, scene.FrameBuffer.Height);
            float averageArea = 0; int numHits = 0;
            for (int i = 0; i < primarySamples.Length; ++i) {
                Ray ray = scene.Camera.GenerateRay(primarySamples[i] * resolution);
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
                Radius = scene.SceneRadius / 300.0f;
                return;
            }

            averageArea /= numHits;

            // Compute the radius based on the approximated average footprint area
            // Our heuristic aims to have each photon cover roughly a 3x3 region on the image
            Radius = MathF.Sqrt(averageArea) * 1.5f;
        }

        public virtual void ShrinkRadius() {
        }

        public override void PostIteration(uint iteration) {
            ShrinkRadius();
        }

        public override void RegisterSample(ColorRGB weight, float misWeight, Vector2 pixel,
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

            // Classic Bidir requires exactly one light path for every camera path.
            NumLightPaths = scene.FrameBuffer.Width * scene.FrameBuffer.Height;

            InitializeRadius(scene);

            base.Render(scene);

            // Store the technique pyramids
            if (RenderTechniquePyramid) {
                string pathRaw = System.IO.Path.Join(scene.FrameBuffer.Basename, "techs-raw");
                techPyramidRaw.WriteToFiles(pathRaw);

                string pathWeighted = System.IO.Path.Join(scene.FrameBuffer.Basename, "techs-weighted");
                techPyramidWeighted.WriteToFiles(pathWeighted);
            }
        }

        public override void ProcessPathCache() {
            SplatLightVertices();
            photonMap.Build(lightPaths, Radius);
        }

        public virtual ColorRGB PerformMerging(Ray ray, SurfacePoint hit, CameraPath path, float cameraJacobian) {
            ColorRGB estimate = ColorRGB.Black;
            var bsdf = hit.Bsdf;
            photonMap.Query(hit.Position, (vertexIdx, mergeDistanceSquared) => {
                var photon = lightPaths.PathCache[vertexIdx];

                // Check that the path does not exceed the maximum length
                var depth = path.Vertices.Count + photon.Depth;
                if (depth > MaxDepth) return;

                // Compute the contribution of the photon
                var ancestor = lightPaths.PathCache[photon.AncestorId];
                var dirToAncestor = ancestor.Point.Position - photon.Point.Position;
                var bsdfValue = bsdf.EvaluateBsdfOnly(-ray.Direction, dirToAncestor, false);
                var photonContrib = photon.Weight * bsdfValue / NumLightPaths;

                // Compute the missing pdf terms and the MIS weight
                var (pdfLightReverse, pdfCameraReverse) = bsdf.Pdf(-ray.Direction, dirToAncestor, false);
                pdfCameraReverse *= cameraJacobian;
                pdfLightReverse *= SampleWrap.SurfaceAreaToSolidAngle(hit, ancestor.Point);
                if (photon.Depth == 1) {
                    pdfLightReverse += NextEventPdf(hit, ancestor.Point);
                }
                float misWeight = MergeMis(path, photon, pdfCameraReverse, pdfLightReverse);

                // Epanechnikov kernel
                float radiusSquared = Radius * Radius;
                photonContrib *= 2 * (radiusSquared - mergeDistanceSquared);
                photonContrib /= MathF.PI * radiusSquared * radiusSquared;

                RegisterSample(photonContrib * path.throughput, misWeight, path.pixel, path.Vertices.Count, photon.Depth, depth);
                photonContrib *= misWeight;

                estimate += photonContrib;
            }, Radius);
            return estimate;
        }

        public override ColorRGB OnCameraHit(CameraPath path, RNG rng, int pixelIndex, Ray ray, SurfacePoint hit,
                                             float pdfFromAncestor, float pdfToAncestor, ColorRGB throughput,
                                             int depth, float toAncestorJacobian) {
            ColorRGB value = ColorRGB.Black;

            // Was a light hit?
            Emitter light = scene.QueryEmitter(hit);
            if (light != null) {
                value += throughput * OnEmitterHit(light, hit, ray, path, toAncestorJacobian);
            }

            // Perform connections and merging if the maximum depth has not yet been reached
            if (depth < MaxDepth) {
                value += throughput * BidirConnections(pixelIndex, hit, -ray.Direction, rng, path, toAncestorJacobian);
                value += throughput * PerformNextEventEstimation(ray, hit, rng, path, toAncestorJacobian);
                value += throughput * PerformMerging(ray, hit, path, toAncestorJacobian);
            }

            return value;
        }

        public virtual float MergeMis(CameraPath cameraPath, PathVertex lightVertex, float pdfCameraReverse,
                                      float pdfLightReverse) {
            int numPdfs = cameraPath.Vertices.Count + lightVertex.Depth;
            int lastCameraVertexIdx = cameraPath.Vertices.Count - 1;

            var pathPdfs = new BidirPathPdfs(lightPaths.PathCache, numPdfs);
            pathPdfs.GatherCameraPdfs(cameraPath, lastCameraVertexIdx);
            pathPdfs.GatherLightPdfs(lightVertex, lastCameraVertexIdx - 1, numPdfs);

            // Set the pdf values that are unique to this combination of paths
            if (lastCameraVertexIdx > 0) // only if this is not the primary hit point
                pathPdfs.pdfsLightToCamera[lastCameraVertexIdx - 1] = pdfCameraReverse;
            pathPdfs.pdfsLightToCamera[lastCameraVertexIdx] = lightVertex.PdfFromAncestor;
            pathPdfs.pdfsCameraToLight[lastCameraVertexIdx] = cameraPath.Vertices[^1].PdfFromAncestor;
            pathPdfs.pdfsCameraToLight[lastCameraVertexIdx + 1] = pdfLightReverse;

            // Compute the acceptance probability approximation
            float mergeApproximation = pathPdfs.pdfsLightToCamera[lastCameraVertexIdx] * MathF.PI * Radius * Radius * NumLightPaths;

            // Compute reciprocals for hypothetical connections along the camera sub-path
            float sumReciprocals = 0.0f;
            sumReciprocals += CameraPathReciprocals(lastCameraVertexIdx, pathPdfs) / mergeApproximation;
            sumReciprocals += LightPathReciprocals(lastCameraVertexIdx, numPdfs, pathPdfs) / mergeApproximation;

            // Add the reciprocal for the connection that replaces the last light path edge
            if (lightVertex.Depth > 1)
                sumReciprocals += 1 / mergeApproximation;

            return 1 / sumReciprocals;
        }

        public override float EmitterHitMis(CameraPath cameraPath, float pdfEmit, float pdfNextEvent) {
            int numPdfs = cameraPath.Vertices.Count;
            int lastCameraVertexIdx = numPdfs - 1;

            if (numPdfs == 1) return 1.0f; // sole technique for rendering directly visible lights.

            var pathPdfs = new BidirPathPdfs(lightPaths.PathCache, numPdfs);
            pathPdfs.GatherCameraPdfs(cameraPath, lastCameraVertexIdx);

            pathPdfs.pdfsLightToCamera[^2] = pdfEmit;

            float pdfThis = cameraPath.Vertices[^1].PdfFromAncestor;

            // Compute the actual weight
            float sumReciprocals = 1.0f;

            // Next event estimation
            sumReciprocals += pdfNextEvent / pdfThis;

            // All connections along the camera path
            sumReciprocals += CameraPathReciprocals(lastCameraVertexIdx - 1, pathPdfs) / pdfThis;

            return 1 / sumReciprocals;
        }

        public override float LightTracerMis(PathVertex lightVertex, float pdfCamToPrimary, float pdfReverse) {
            int numPdfs = lightVertex.Depth + 1;
            int lastCameraVertexIdx = -1;

            var pathPdfs = new BidirPathPdfs(lightPaths.PathCache, numPdfs);

            pathPdfs.GatherLightPdfs(lightVertex, lastCameraVertexIdx, numPdfs);

            pathPdfs.pdfsCameraToLight[0] = pdfCamToPrimary;
            pathPdfs.pdfsCameraToLight[1] = pdfReverse;

            // Compute the actual weight
            float sumReciprocals = LightPathReciprocals(lastCameraVertexIdx, numPdfs, pathPdfs);
            sumReciprocals /= NumLightPaths;
            sumReciprocals += 1;

            return 1 / sumReciprocals;
        }

        public override float BidirConnectMis(CameraPath cameraPath, PathVertex lightVertex, float pdfCameraReverse,
                                              float pdfCameraToLight, float pdfLightReverse, float pdfLightToCamera) {
            int numPdfs = cameraPath.Vertices.Count + lightVertex.Depth + 1;
            int lastCameraVertexIdx = cameraPath.Vertices.Count - 1;

            var pathPdfs = new BidirPathPdfs(lightPaths.PathCache, numPdfs);
            pathPdfs.GatherCameraPdfs(cameraPath, lastCameraVertexIdx);
            pathPdfs.GatherLightPdfs(lightVertex, lastCameraVertexIdx, numPdfs);

            // Set the pdf values that are unique to this combination of paths
            if (lastCameraVertexIdx > 0) // only if this is not the primary hit point
                pathPdfs.pdfsLightToCamera[lastCameraVertexIdx - 1] = pdfCameraReverse;
            pathPdfs.pdfsCameraToLight[lastCameraVertexIdx] = cameraPath.Vertices[^1].PdfFromAncestor;
            pathPdfs.pdfsLightToCamera[lastCameraVertexIdx] = pdfLightToCamera;
            pathPdfs.pdfsCameraToLight[lastCameraVertexIdx + 1] = pdfCameraToLight;
            pathPdfs.pdfsCameraToLight[lastCameraVertexIdx + 2] = pdfLightReverse;

            // Compute reciprocals for hypothetical connections along the camera sub-path
            float sumReciprocals = 1.0f;
            sumReciprocals += CameraPathReciprocals(lastCameraVertexIdx, pathPdfs);
            sumReciprocals += LightPathReciprocals(lastCameraVertexIdx, numPdfs, pathPdfs);

            return 1 / sumReciprocals;
        }

        public override float NextEventMis(CameraPath cameraPath, float pdfEmit, float pdfNextEvent, float pdfHit, float pdfReverse) {
            int numPdfs = cameraPath.Vertices.Count + 1;
            int lastCameraVertexIdx = numPdfs - 2;

            var pathPdfs = new BidirPathPdfs(lightPaths.PathCache, numPdfs);

            pathPdfs.GatherCameraPdfs(cameraPath, lastCameraVertexIdx);

            pathPdfs.pdfsCameraToLight[^2] = cameraPath.Vertices[^1].PdfFromAncestor;
            pathPdfs.pdfsLightToCamera[^2] = pdfEmit;
            if (numPdfs > 2) // not for direct illumination
                pathPdfs.pdfsLightToCamera[^3] = pdfReverse;

            // Compute the actual weight
            float sumReciprocals = 1.0f;

            // Hitting the light source
            sumReciprocals += pdfHit / pdfNextEvent;

            // All bidirectional connections
            sumReciprocals += CameraPathReciprocals(lastCameraVertexIdx, pathPdfs) / pdfNextEvent;

            return 1 / sumReciprocals;
        }

        private float CameraPathReciprocals(int lastCameraVertexIdx, BidirPathPdfs pdfs) {
            float sumReciprocals = 0.0f;
            float nextReciprocal = 1.0f;
            for (int i = lastCameraVertexIdx; i > 0; --i) {
                // Merging at this vertex
                float acceptProb = pdfs.pdfsLightToCamera[i] * MathF.PI * Radius * Radius;
                sumReciprocals += nextReciprocal * NumLightPaths * acceptProb;

                nextReciprocal *= pdfs.pdfsLightToCamera[i] / pdfs.pdfsCameraToLight[i];

                // Connecting this vertex to the next one along the camera path
                sumReciprocals += nextReciprocal;
            }

            // Light tracer
            sumReciprocals += nextReciprocal * pdfs.pdfsLightToCamera[0] / pdfs.pdfsCameraToLight[0] * NumLightPaths;

            // Merging directly visible (almost the same as the light tracer!)
            sumReciprocals += nextReciprocal * NumLightPaths * pdfs.pdfsLightToCamera[0] * MathF.PI * Radius * Radius;

            return sumReciprocals;
        }

        private float LightPathReciprocals(int lastCameraVertexIdx, int numPdfs, BidirPathPdfs pdfs) {
            float sumReciprocals = 0.0f;
            float nextReciprocal = 1.0f;
            for (int i = lastCameraVertexIdx + 1; i < numPdfs; ++i) {
                if (i < numPdfs - 1) { // no merging on the emitter itself
                    // Account for merging at this vertex
                    float acceptProb = pdfs.pdfsCameraToLight[i] * MathF.PI * Radius * Radius;
                    sumReciprocals += nextReciprocal * NumLightPaths * acceptProb;
                }

                nextReciprocal *= pdfs.pdfsCameraToLight[i] / pdfs.pdfsLightToCamera[i];

                // Account for connections from this vertex to its ancestor
                if (i < numPdfs - 2) // Connections to the emitter (next event) are treated separately
                    sumReciprocals += nextReciprocal;
            }
            sumReciprocals += nextReciprocal; // Next event and hitting the emitter directly
            return sumReciprocals;
        }
    }
}
