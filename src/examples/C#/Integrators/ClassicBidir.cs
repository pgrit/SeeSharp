using Ground;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Experiments {
    class ClassicBidir : BidirBase {

        public override List<int> TraceLightPath(Scene scene, RNG rng, ManagedPathCache pathCache, uint pathIndex) {
            var emitter = SelectEmitter(scene, rng); // TODO once this is a proper selection: obtain and consider PDF

            var primaryPos = rng.NextFloat2D();
            var primaryDir = rng.NextFloat2D();
            var emitterSample = emitter.WrapPrimaryToRay(primaryPos, primaryDir);
            Ray ray = scene.SpawnRay(emitterSample.surface.point, emitterSample.direction);

            var radiance = emitter.ComputeEmission(emitterSample.surface.point, emitterSample.direction);

            float pdf = emitterSample.surface.jacobian * emitterSample.jacobian;
            var weight = radiance * (emitterSample.shadingCosine / pdf);

            var walker = new CachedRandomWalk(scene, rng, pathCache, true, MaxDepth);
            var lastVertexId = walker.StartWalk(
                initialPoint: emitterSample.surface.point,
                surfaceAreaPdf: emitterSample.surface.jacobian,
                initialRay: ray,
                directionPdf: emitterSample.jacobian,
                initialWeight: weight);

            return new List<int> { lastVertexId };
        }

        public override void ProcessPathCache(Scene scene, ManagedPathCache pathCache, List<int> endpoints) {
            //Parallel.For(0, endpoints.Count, idx => {
            //    ConnectPathVerticesToCamera(scene, endpoints[idx], pathCache);
            //});
        }

        public override ColorRGB EstimatePixelValue(Scene scene, ManagedPathCache pathCache, 
            List<int> endpoints, Vector2 filmPosition, Ray primaryRay, RNG rng) 
        {
            var hit = scene.TraceRay(primaryRay);

            // Did the ray leave the scene?
            if (!scene.IsValid(hit))
                return ColorRGB.Black;

            ColorRGB value = ColorRGB.Black;

            // Check if a light source was hit.
            Emitter light = scene.QueryEmitter(hit.point);
            if (light != null) {
                float misWeight = 1.0f;

                var emission = light.ComputeEmission(hit.point, -primaryRay.direction);
                value += misWeight * emission;
            }

            // Find the corresponding light path end point
            int pixelIndex = (int)filmPosition.y * scene.frameBuffer.width + (int)filmPosition.x;

            // Connect to all light path vertices
            value += BidirConnections(scene, pathCache, endpoints[pixelIndex], hit.point, -primaryRay.direction);

            value += PerformNextEventEstimation(scene, primaryRay, hit, rng);

            return value;
        }

        Emitter SelectEmitter(Scene scene, RNG rng) {
            return scene.Emitters[0]; // TODO proper selection
        }

        void ConnectPathVerticesToCamera(Scene scene, int vertexId, ManagedPathCache pathCache) {
            ForEachVertex(pathCache, vertexId, (vertex, ancestor, dirToAncestor) => {
                // Compute image plane location
                var (raster, isVisible) = scene.ProjectOntoFilm(vertex.point.position);
                if (!isVisible)
                    return;

                // Trace shadow ray
                if (scene.IsOccluded(vertex.point, scene.CameraPosition))
                    return;

                // Perform a change of variables from scene surface to pixel area.
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

                ColorRGB weight = vertex.weight * bsdfWeight * surfaceToPixelJacobian * (1.0f / NumLightPaths);

                // Compute image contribution and splat
                scene.frameBuffer.Splat(raster.x, raster.y, weight * (1.0f / NumIterations));
            });
        }

        ColorRGB BidirConnections(Scene scene, ManagedPathCache pathCache, int lightEndpoint, SurfacePoint cameraPoint, Vector3 outDir) {
            ColorRGB result = ColorRGB.Black;
            ForEachVertex(pathCache, lightEndpoint, (vertex, ancestor, dirToAncestor) => {
                // Trace shadow ray
                if (scene.IsOccluded(vertex.point, cameraPoint.position))
                    return;

                var geomTerm = scene.ComputeGeometryTerms(cameraPoint, vertex.point).geomTerm;
                if (geomTerm <= float.Epsilon)
                    return;

                // Compute connection direction
                var dirFromCamToLight = vertex.point.position - cameraPoint.position;

                var (bsdfWeightLight, _) = scene.EvaluateBsdf(vertex.point, dirToAncestor, -dirFromCamToLight, true);
                var (bsdfWeightCam, shadingCosine) = scene.EvaluateBsdf(cameraPoint, outDir, dirFromCamToLight, false);

                // TODO use shading cosine here
                ColorRGB weight = vertex.weight * bsdfWeightLight * bsdfWeightCam * geomTerm;

                result += weight;
            });

            return result;
        }

        private ColorRGB PerformNextEventEstimation(Scene scene, Ray ray, Hit hit, RNG rng) {
            // Select a light source
            // TODO implement multi-light support
            var light = scene.Emitters[0];

            // Sample a point on the light source
            var lightSample = light.WrapPrimaryToSurface(rng.NextFloat(), rng.NextFloat());

            if (!scene.IsOccluded(hit.point, lightSample.point.position)) {
                Vector3 lightToSurface = hit.point.position - lightSample.point.position;
                var emission = light.ComputeEmission(lightSample.point, lightToSurface);

                (var bsdfValue, float shadingCosine) = scene.EvaluateBsdf(hit.point,
                    -ray.direction, -lightToSurface, false);

                var geometryTerms = scene.ComputeGeometryTerms(hit.point, lightSample.point);

                var value = ColorRGB.Black;
                if (geometryTerms.cosineFrom > 0) {
                    value = emission * bsdfValue
                        * (geometryTerms.geomTerm / lightSample.jacobian)
                        * (shadingCosine / geometryTerms.cosineFrom);
                }
                return value;
            }

            return new ColorRGB { r=0, g=0, b=0 };
        }
    }
}
