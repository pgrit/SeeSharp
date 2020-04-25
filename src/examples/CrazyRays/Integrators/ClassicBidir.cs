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

namespace Integrators {

    public class ClassicBidir : BidirBase {

        public override void Render(Scene scene) {
            // Classic Bidir requires exactly one light path for every camera path.
            NumLightPaths = scene.FrameBuffer.Width * scene.FrameBuffer.Height;
            base.Render(scene);
        }

        public override int TraceLightPath(RNG rng, uint pathIndex) {
            // Select an emitter
            float lightSelPrimary = rng.NextFloat();
            var (emitter, selectProb, _) = SelectLight(lightSelPrimary);

            // Sample a ray from the emitter
            var primaryPos = rng.NextFloat2D();
            var primaryDir = rng.NextFloat2D();
            var emitterSample = emitter.SampleRay(primaryPos, primaryDir); ;

            // Account for the light selection probability also in the MIS weights
            emitterSample.pdf *= lightSelPrimary;

            // TODO refactor: to reduce risk of mistakes, don't pass an emitter sample to "StartFromEmitter",
            //      pass the pdf, weight, and surface point directly instead.

            // Perform a random walk through the scene, storing all vertices along the path
            var walker = new CachedRandomWalk(scene, rng, MaxDepth, pathCache);
            walker.StartFromEmitter(emitterSample, emitterSample.weight / selectProb);

            return walker.lastId;
        }

        public float NextEventPdf(SurfacePoint from, SurfacePoint to) {
            var emitter = scene.QueryEmitter(to);
            float pdf = emitter.PdfArea(to) * SelectLightPmf(from, emitter);
            return pdf;
        }

        public override void ProcessPathCache() {
            // Incorporate the next event estimation pdf into each primary light path vertex.
            Parallel.For(0, endpoints.Length, idx => {
                ForEachVertex(endpoints[idx], (vertex, ancestor, dirToAncestor) => {
                    if (vertex.depth == 1) {
                        vertex.pdfToAncestor += NextEventPdf(vertex.point, ancestor.point);
                    }
                });
            });

            Parallel.For(0, endpoints.Length, idx => {
                ConnectPathVerticesToCamera(endpoints[idx]);
            });
        }

        class CameraRandomWalk : RandomWalk {
            int pixelIndex;
            ClassicBidir integrator;
            CameraPath path;

            public CameraRandomWalk(RNG rng, int pixelIndex, ClassicBidir integrator)
                : base(integrator.scene, rng, integrator.MaxDepth + 1) {
                this.pixelIndex = pixelIndex;
                this.integrator = integrator;
                path.vertices = new List<PathPdfPair>(integrator.MaxDepth);
            }

            protected override ColorRGB OnHit(Ray ray, SurfacePoint hit, float pdfFromAncestor, float pdfToAncestor,
                                              ColorRGB throughput, int depth, float toAncestorJacobian) {
                var value = ColorRGB.Black;

                path.vertices.Add(new PathPdfPair { pdfFromAncestor = pdfFromAncestor, pdfToAncestor = pdfToAncestor });

                // Was a light hit?
                Emitter light = scene.QueryEmitter(hit);
                if (light != null) {
                    value += throughput * integrator.OnEmitterHit(light, hit, ray, path, toAncestorJacobian);
                }

                // Perform connections if the maximum depth has not yet been reached
                if (depth < integrator.MaxDepth) {
                    value += throughput * integrator.BidirConnections(integrator.endpoints[pixelIndex], hit, -ray.direction, path, toAncestorJacobian);
                    value += throughput * integrator.PerformNextEventEstimation(ray, hit, rng, path, toAncestorJacobian);
                }

                return value;
            }

            protected override ColorRGB OnInvalidHit() => ColorRGB.Black;
        }

        public override ColorRGB EstimatePixelValue(SurfacePoint cameraPoint, Vector2 filmPosition, Ray primaryRay,
                                                    float pdfFromCamera, ColorRGB initialWeight, RNG rng) {
            // The pixel index determines which light path we connect to
            int pixelIndex = (int)filmPosition.Y * scene.FrameBuffer.Width + (int)filmPosition.X;
            var walk = new CameraRandomWalk(rng, pixelIndex, this);
            return walk.StartFromCamera(filmPosition, cameraPoint, pdfFromCamera, primaryRay, initialWeight);
        }

        public ColorRGB OnEmitterHit(Emitter emitter, SurfacePoint hit, Ray ray, CameraPath path, float reversePdfJacobian) {
            var emission = emitter.EmittedRadiance(hit, -ray.direction);

            // Compute pdf values
            float pdfEmit = emitter.PdfRay(hit, -ray.direction);
            pdfEmit *= reversePdfJacobian;
            float pdfNextEvent = emitter.PdfArea(hit) / scene.Emitters.Count; // TODO use NextEventPdf() and the previous hit point!

            // MIS weight
            var computer = new ClassicBidirMisComputer(
                lightPathCache: pathCache,
                numLightPaths: NumLightPaths
            );
            float misWeight = computer.Hit(path, pdfEmit, pdfNextEvent);
            var value = misWeight * emission;
            return value;
        }

        public (Ray, float, ColorRGB) BsdfSample(Scene scene, Ray ray, SurfacePoint hit, RNG rng) { // TODO this can and should be re-used in the base and for both directions!
            var bsdfSample = hit.Bsdf.Sample(-ray.direction, false, rng.NextFloat2D());
            var bsdfRay = scene.Raytracer.SpawnRay(hit, bsdfSample.direction);
            return (bsdfRay, bsdfSample.pdf, bsdfSample.weight);
        }

        public void ConnectPathVerticesToCamera(int vertexId) {
            ForEachVertex(vertexId, (vertex, ancestor, dirToAncestor) => {
                // Compute image plane location
                var raster = scene.Camera.WorldToFilm(vertex.point.position);
                if (!raster.HasValue) return;

                // Trace shadow ray
                if (scene.Raytracer.IsOccluded(vertex.point, scene.Camera.Position))
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

                var bsdf = vertex.point.Bsdf;
                var bsdfValue = bsdf.EvaluateBsdfOnly(dirToAncestor, dirToCam, true);

                // Compute the surface area pdf of sampling the previous vertex instead
                float pdfReverse = bsdf.Pdf(dirToCam, dirToAncestor, false).Item1;
                pdfReverse *= SampleWrap.SurfaceAreaToSolidAngle(vertex.point, ancestor.point);

                if (vertex.depth == 1) {
                    pdfReverse += NextEventPdf(vertex.point, ancestor.point);
                }

                // Compute MIS weight
                var computer = new ClassicBidirMisComputer(
                    lightPathCache: pathCache,
                    numLightPaths: NumLightPaths
                );
                float misWeight = computer.LightTracer(vertex, surfaceToPixelJacobian, pdfReverse);

                ColorRGB weight = misWeight * vertex.weight * bsdfValue * surfaceToPixelJacobian / NumLightPaths;

                // Compute image contribution and splat
                scene.FrameBuffer.Splat(raster.Value.X, raster.Value.Y, weight * (1.0f / NumIterations));
            });
        }

        public ColorRGB BidirConnections(int lightEndpoint, SurfacePoint cameraPoint, Vector3 outDir, CameraPath path,
                                         float reversePdfJacobian) {
            ColorRGB result = ColorRGB.Black;
            ForEachVertex(lightEndpoint, (vertex, ancestor, dirToAncestor) => {
                // Only allow connections that do not exceed the maximum total path length
                int depth = vertex.depth + path.vertices.Count + 1;
                if (depth > MaxDepth) return;

                // Trace shadow ray
                if (scene.Raytracer.IsOccluded(vertex.point, cameraPoint))
                    return;

                // Compute connection direction
                var dirFromCamToLight = vertex.point.position - cameraPoint.position;

                var bsdfWeightLight = vertex.point.Bsdf.EvaluateBsdfOnly(dirToAncestor, -dirFromCamToLight, true);
                var bsdfWeightCam = cameraPoint.Bsdf.EvaluateWithCosine(outDir, dirFromCamToLight, false);

                if (bsdfWeightCam == ColorRGB.Black || bsdfWeightLight == ColorRGB.Black)
                    return;

                // Compute the missing pdfs
                var (pdfCameraToLight, pdfCameraReverse) = cameraPoint.Bsdf.Pdf(outDir, dirFromCamToLight, false);
                pdfCameraReverse *= reversePdfJacobian;
                pdfCameraToLight *= SampleWrap.SurfaceAreaToSolidAngle(cameraPoint, vertex.point);

                var (pdfLightToCamera, pdfLightReverse) = vertex.point.Bsdf.Pdf(dirToAncestor, -dirFromCamToLight, true);
                pdfLightReverse  *= SampleWrap.SurfaceAreaToSolidAngle(vertex.point, ancestor.point);
                pdfLightToCamera *= SampleWrap.SurfaceAreaToSolidAngle(vertex.point, cameraPoint);

                if (vertex.depth == 1) {
                    pdfLightReverse += NextEventPdf(vertex.point, ancestor.point);
                }

                // Compute the MIS weight
                var computer = new ClassicBidirMisComputer(
                    lightPathCache: pathCache,
                    numLightPaths: NumLightPaths
                );
                float misWeight = computer.BidirConnect(path, vertex, pdfCameraReverse, pdfCameraToLight, pdfLightReverse, pdfLightToCamera);

                ColorRGB weight = misWeight * vertex.weight * bsdfWeightLight * bsdfWeightCam * SampleWrap.SurfaceAreaToSolidAngle(cameraPoint, vertex.point);
                result += weight;
            });

            return result;
        }

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

                var computer = new ClassicBidirMisComputer(
                    lightPathCache: pathCache,
                    numLightPaths: NumLightPaths
                );
                float misWeight = computer.NextEvent(path, pdfEmit, lightSample.pdf, bsdfForwardPdf, bsdfReversePdf);

                var value = misWeight * emission * bsdfTimesCosine * (jacobian / lightSample.pdf);
                return value;
            }

            return ColorRGB.Black;
        }
    }
}
