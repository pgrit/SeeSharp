using GroundWrapper;
using System;
using System.Threading.Tasks;
using Integrators.Common;
using System.Collections.Generic;
using GroundWrapper.GroundMath;
using GroundWrapper.Geometry;

namespace Integrators {

    public class ClassicBidir : BidirBase {

        public override void Render(Scene scene) {
            MaxDepth = 11;

            // Classic Bidir requires exactly one light path for every camera path.
            NumLightPaths = scene.frameBuffer.Width * scene.frameBuffer.Height;
            base.Render(scene);
        }

        public override int TraceLightPath(RNG rng, uint pathIndex) {
            var emitter = SelectEmitterForBidir(rng); // TODO once this is a proper selection: obtain and consider PDF

            var primaryPos = rng.NextFloat2D();
            var primaryDir = rng.NextFloat2D();
            var emitterSample = emitter.WrapPrimaryToRay(primaryPos, primaryDir);
            var radiance = emitter.ComputeEmission(emitterSample.surface.point, emitterSample.direction);

            float pdf = emitterSample.surface.jacobian * emitterSample.jacobian;
            var weight = radiance * (emitterSample.shadingCosine / pdf);

            var walker = new CachedRandomWalk(scene, rng, MaxDepth, pathCache);
            walker.StartFromEmitter(emitterSample, weight);

            return walker.lastId;
        }

        public float NextEventPdf(SurfacePoint from, SurfacePoint to) {
            // TODO account for light selection probability once we do proper multi-light support!
            float pdf = scene.Emitters[0].Jacobian(to);
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
                : base(integrator.scene, rng, integrator.MaxDepth) {
                this.pixelIndex = pixelIndex;
                this.integrator = integrator;
                path.vertices = new List<PathPdfPair>(integrator.MaxDepth);
            }

            protected override ColorRGB OnHit(Ray ray, Hit hit, float pdfFromAncestor, float pdfToAncestor,
                                              ColorRGB throughput, int depth, GeometryTerms geometryTerms) {
                var value = ColorRGB.Black;

                path.vertices.Add(new PathPdfPair { pdfFromAncestor = pdfFromAncestor, pdfToAncestor = pdfToAncestor });

                float reverseJacobian = geometryTerms.cosineFrom / geometryTerms.squaredDistance;

                // Was a light hit?
                Emitter light = scene.QueryEmitter(hit.point);
                if (light != null) {
                    value += throughput * integrator.OnEmitterHit(light, hit, ray, path, reverseJacobian);
                }
                value += throughput * integrator.BidirConnections(integrator.endpoints[pixelIndex], hit.point, -ray.direction, path, reverseJacobian);
                value += throughput * integrator.PerformNextEventEstimation(ray, hit, rng, path, reverseJacobian);

                return value;
            }

            protected override ColorRGB OnInvalidHit() => ColorRGB.Black;
        }

        public override ColorRGB EstimatePixelValue(SurfacePoint cameraPoint, Vector2 filmPosition, Ray primaryRay,
                                                    float pdfFromCamera, ColorRGB initialWeight, RNG rng) {
            // The pixel index determines which light path we connect to
            int pixelIndex = (int)filmPosition.y * scene.frameBuffer.Width + (int)filmPosition.x;
            if ((int)filmPosition.y != 20 || (int)filmPosition.x != 170) return ColorRGB.Black;
            var walk = new CameraRandomWalk(rng, pixelIndex, this);
            return walk.StartFromCamera(filmPosition, cameraPoint, pdfFromCamera, primaryRay, initialWeight);
        }

        public ColorRGB OnEmitterHit(Emitter emitter, Hit hit, Ray ray, CameraPath path, float reversePdfJacobian) {
            var emission = emitter.ComputeEmission(hit.point, -ray.direction);

            // Compute pdf values
            float pdfEmit = emitter.RayJacobian(hit.point, -ray.direction);
            pdfEmit *= reversePdfJacobian;
            float pdfNextEvent = emitter.Jacobian(hit.point);

            // MIS weight
            var computer = new ClassicBidirMisComputer(
                lightPathCache: pathCache,
                numLightPaths: NumLightPaths
            );
            float misWeight = computer.Hit(path, pdfEmit, pdfNextEvent);

            return misWeight * emission;
        }

        public (Ray, float, ColorRGB) BsdfSample(Scene scene, Ray ray, Hit hit, RNG rng) { // TODO this can and should be re-used in the base and for both directions!
            float u = rng.NextFloat();
            float v = rng.NextFloat();
            var bsdfSample = scene.WrapPrimarySampleToBsdf(hit.point,
                -ray.direction, u, v, false);

            (var bsdfValue, float shadingCosine) = scene.EvaluateBsdf(hit.point, -ray.direction,
                bsdfSample.direction, false);

            var bsdfRay = scene.SpawnRay(hit.point, bsdfSample.direction);

            var weight = bsdfSample.pdf == 0.0f ? ColorRGB.Black : bsdfValue * (shadingCosine / bsdfSample.pdf);

            return (bsdfRay, bsdfSample.pdf, weight);
        }

        public void ConnectPathVerticesToCamera(int vertexId) {
            ForEachVertex(vertexId, (vertex, ancestor, dirToAncestor) => {
                // Compute image plane location
                var (raster, isVisible) = scene.ProjectOntoFilm(vertex.point.position);
                if (!isVisible)
                    return;

                // Trace shadow ray
                if (scene.IsOccluded(vertex.point, scene.CameraPosition))
                    return;

                // Perform a change of variables from scene surface to pixel area.
                // TODO this could be computed by the camera itself...
                // First: map the scene surface to the solid angle about the camera
                var dirToCam = scene.CameraPosition - vertex.point.position;
                float distToCam = dirToCam.Length();
                float cosToCam = Math.Abs(Vector3.Dot(vertex.point.normal, dirToCam)) / distToCam;
                float surfaceToSolidAngle = cosToCam / (distToCam * distToCam);

                if (distToCam == 0 || cosToCam == 0)
                    return;

                // Second: map the solid angle to the pixel area
                float solidAngleToPixel = scene.ComputeCamaraSolidAngleToPixelJacobian(vertex.point.position);

                // Third: combine to get the full jacobian 
                float surfaceToPixelJacobian = surfaceToSolidAngle * solidAngleToPixel;

                var (bsdfWeight, _) = scene.EvaluateBsdf(vertex.point, dirToAncestor, dirToCam, true);

                // Compute the surface area pdf of sampling the previous vertex instead
                float pdfReverse = scene.ComputePrimaryToBsdfJacobian(vertex.point, dirToCam, dirToAncestor, false).pdf;
                var geomTerms = scene.ComputeGeometryTerms(vertex.point, ancestor.point);
                pdfReverse *= geomTerms.cosineTo / geomTerms.squaredDistance;

                if (vertex.depth == 1) {
                    pdfReverse += NextEventPdf(vertex.point, ancestor.point);
                }
                
                // Compute MIS weight
                var computer = new ClassicBidirMisComputer(
                    lightPathCache: pathCache,
                    numLightPaths: NumLightPaths
                );
                float misWeight = computer.LightTracer(vertex, surfaceToPixelJacobian, pdfReverse);

                ColorRGB weight = misWeight * vertex.weight * bsdfWeight * surfaceToPixelJacobian * (1.0f / NumLightPaths);

                // Compute image contribution and splat
                scene.frameBuffer.Splat(raster.x, raster.y, weight * (1.0f / NumIterations));
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
                if (scene.IsOccluded(vertex.point, cameraPoint.position))
                    return;

                var geomTerms = scene.ComputeGeometryTerms(cameraPoint, vertex.point);
                if (geomTerms.geomTerm <= float.Epsilon)
                    return;

                // Compute connection direction
                var dirFromCamToLight = vertex.point.position - cameraPoint.position;

                var (bsdfWeightLight, _) = scene.EvaluateBsdf(vertex.point, dirToAncestor, -dirFromCamToLight, true);
                var (bsdfWeightCam, shadingCosine) = scene.EvaluateBsdf(cameraPoint, outDir, dirFromCamToLight, false);

                // Compute the missing pdfs
                var camJacobians = scene.ComputePrimaryToBsdfJacobian(cameraPoint, outDir, dirFromCamToLight, false);
                float pdfCameraReverse = camJacobians.pdfReverse * reversePdfJacobian;
                float pdfCameraToLight = camJacobians.pdf * geomTerms.cosineTo / geomTerms.squaredDistance;

                var lightJacobians = scene.ComputePrimaryToBsdfJacobian(vertex.point, dirToAncestor, -dirFromCamToLight, true);
                var geomTermsToAncestor = scene.ComputeGeometryTerms(vertex.point, ancestor.point);
                float pdfLightReverse = lightJacobians.pdfReverse * geomTermsToAncestor.cosineTo / geomTermsToAncestor.squaredDistance;
                float pdfLightToCamera = lightJacobians.pdf * geomTerms.cosineFrom / geomTerms.squaredDistance;

                if (vertex.depth == 1) {
                    pdfLightReverse += NextEventPdf(vertex.point, ancestor.point);
                }

                // Compute the MIS weight
                var computer = new ClassicBidirMisComputer(
                    lightPathCache: pathCache,
                    numLightPaths: NumLightPaths
                );
                float misWeight = computer.BidirConnect(path, vertex, pdfCameraReverse, pdfCameraToLight, pdfLightReverse, pdfLightToCamera);

                // TODO use shading cosine here
                ColorRGB weight = misWeight * vertex.weight * bsdfWeightLight * bsdfWeightCam * geomTerms.geomTerm;

                result += weight;
            });

            return result;
        }

        public ColorRGB PerformNextEventEstimation(Ray ray, Hit hit, RNG rng, CameraPath path, float reversePdfJacobian) {
            // Sample a point on the light source
            var light = SelectEmitterForNextEvent(rng, ray, hit);
            var lightSample = light.WrapPrimaryToSurface(rng.NextFloat(), rng.NextFloat());

            if (!scene.IsOccluded(hit.point, lightSample.point.position)) {
                Vector3 lightToSurface = hit.point.position - lightSample.point.position;
                var emission = light.ComputeEmission(lightSample.point, lightToSurface);

                (var bsdfValue, float shadingCosine) = scene.EvaluateBsdf(hit.point,
                    -ray.direction, -lightToSurface, false);

                var geometryTerms = scene.ComputeGeometryTerms(hit.point, lightSample.point);

                // Compute the missing pdf terms
                var bsdfPdfs = scene.ComputePrimaryToBsdfJacobian(hit.point, -ray.direction, -lightToSurface, false);
                float pdfHit = bsdfPdfs.pdf * geometryTerms.cosineTo / geometryTerms.squaredDistance;
                float pdfReverse = bsdfPdfs.pdfReverse * reversePdfJacobian;

                float pdfEmit = light.RayJacobian(lightSample.point, lightToSurface);
                pdfEmit *= geometryTerms.cosineFrom / geometryTerms.squaredDistance;

                var computer = new ClassicBidirMisComputer(
                    lightPathCache: pathCache,
                    numLightPaths: NumLightPaths
                );
                float misWeight = computer.NextEvent(path, pdfEmit, lightSample.jacobian, pdfHit, pdfReverse);

                var value = ColorRGB.Black;
                if (geometryTerms.cosineFrom > 0) {
                    value = misWeight * emission * bsdfValue
                        * (geometryTerms.geomTerm / lightSample.jacobian)
                        * (shadingCosine / geometryTerms.cosineFrom);
                }

                return value;
            }

            return new ColorRGB { r=0, g=0, b=0 };
        }
    }
}
