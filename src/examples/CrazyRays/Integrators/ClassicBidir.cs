using GroundWrapper;
using System;
using System.Threading.Tasks;

namespace Integrators {

    public class ClassicBidir : BidirBase {

        public ref struct CameraPath {
            // TODO compare performance with stackalloc vs new
            public Span<PathVertex> vertices;
        }

        public ref struct MisWeightComputer {
            public int numLightPaths;

            public Span<float> pdfsLightToCamera;
            public Span<float> pdfsCameraToLight;

            public PathCache lightPathCache;
            
            /// <summary>
            /// Computes the balance heuristic value for the light tracer technique
            /// (splatting a light path vertex onto the image).
            /// </summary>
            /// <param name="lightVertex">The vertex that got splatte</param>
            /// <param name="pdfCamToPrimary">
            ///     The pdf value of sampling the light vertex as the primary vertex of a camera path.
            ///     Measure: surface area [m^2]
            /// </param>
            /// <param name="pdfReverse">
            ///     The pdf value of sampling the ancestor of the light vertex when coming from the camera.
            ///     Measure: surface area [m^2]
            /// </param>
            /// <returns>Balance heuristic weight</returns>
            public float LightTracer(PathVertex lightVertex, float pdfCamToPrimary, float pdfReverse) {
                return 0;
            }

            /// <summary>
            /// Computes the balance heuristic value for the next event technique.
            /// </summary>
            /// <param name="cameraPath">The camera path at the end of which next event estimation happened.</param>
            /// <param name="pdfEmit">
            ///     The probability of sampling the next event edge bidirectionally. 
            ///     Measure: product surface area [m^4]
            /// </param>
            /// <param name="pdfNextEvent">
            ///     The probability of sampling the vertex on the light for next event estimation (current technique). 
            ///     Measure: surface area [m^2]
            /// </param>
            /// <param name="pdfHit">
            ///     The probability of sampling the vertex on the light by importance sampling the BSDF.
            ///     Measure: surface area [m^2]
            /// </param>
            /// <returns>Balance heuristic weight</returns>
            public float NextEvent(CameraPath cameraPath, float pdfEmit, float pdfNextEvent, float pdfHit) {
                return 0;
            }

            /// <summary>
            /// Computes the balance heuristic value for hitting the light with a BSDF sample.
            /// </summary>
            /// <param name="cameraPath">The camera path, the last vertex of which found the light</param>
            /// <param name="pdfEmit">
            ///     The pdf value of sampling the vertex on the light and the previous one bidirectionally.
            ///     Measure: product surface area [m^4]
            /// </param>
            /// <param name="pdfNextEvent">
            ///     The pdf value of sampling the vertex on the light via next event estimation.
            ///     Measure: surface area [m^2]
            /// </param>
            /// <returns>Balance heuristic weight</returns>
            public float Hit(CameraPath cameraPath, float pdfEmit, float pdfNextEvent) {
                return 0;
            }

            public float BidirConnect(CameraPath cameraPath, PathVertex lightVertex, 
                float pdfCameraReverse, float pdfCameraToLight, float pdfLightReverse, float pdfLightToCamera) 
            {
                return 0;
            }
        };


        public override void Render(Scene scene) {
            MaxDepth = 2;

            // Classic Bidir requires exactly one light path for every camera path.
            NumLightPaths = scene.frameBuffer.width * scene.frameBuffer.height;
            base.Render(scene);
        }

        public override int TraceLightPath(Scene scene, RNG rng, PathCache pathCache, uint pathIndex) {
            var emitter = SelectEmitterForBidir(scene, rng); // TODO once this is a proper selection: obtain and consider PDF

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

            return lastVertexId;
        }

        public override void ProcessPathCache(Scene scene, PathCache pathCache, int[] endpoints) {
            Parallel.For(0, endpoints.Length, idx => {
                ConnectPathVerticesToCamera(scene, endpoints[idx], pathCache);
            });
        }

        public override ColorRGB EstimatePixelValue(Scene scene, PathCache pathCache, 
            int[] endpoints, Vector2 filmPosition, Ray primaryRay, RNG rng) 
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

        public void ConnectPathVerticesToCamera(Scene scene, int vertexId, PathCache pathCache) {
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

        public ColorRGB BidirConnections(Scene scene, PathCache pathCache, int lightEndpoint, SurfacePoint cameraPoint, Vector3 outDir) {
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

        public ColorRGB PerformNextEventEstimation(Scene scene, Ray ray, Hit hit, RNG rng) {
            // Sample a point on the light source
            var light = SelectEmitterForNextEvent(scene, rng, ray, hit);
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
