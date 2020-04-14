using System;
using System.Threading.Tasks;
using GroundWrapper;
using Integrators.Common;

namespace Integrators {

    public class LightTracer : Integrator {
        public override void Render(Scene scene) {
            PathCache pathCache = new PathCache(TotalPaths * MaxDepth);

            for (int iter = 0; iter < NumIterations; ++iter) {
                Parallel.For(0, TotalPaths, idx => {
                    var seed = RNG.HashSeed(BaseSeed, (uint)idx, (uint)iter);
                    var rng = new RNG(seed);
                    TraceLightPath(scene, rng, pathCache, idx);
                });

                Parallel.For(0, TotalPaths, idx => {
                    ConnectPathVerticesToCamera(scene, endpointIds[idx], pathCache);
                });

                pathCache.Clear();
            }
        }

        void ConnectPathVerticesToCamera(Scene scene, int vertexId, PathCache pathCache) {
            while (pathCache[vertexId].ancestorId != -1) { // iterate over all vertices that have an ancestor
                var vertex = pathCache[vertexId];
                var ancestor = pathCache[vertex.ancestorId];
                var dirToAncestor = ancestor.point.position - vertex.point.position;

                // Compute image plane location
                var (raster, isVisible) = scene.ProjectOntoFilm(vertex.point.position);
                if (!isVisible)
                    goto Next;

                // Trace shadow ray
                if (scene.IsOccluded(vertex.point, scene.CameraPosition))
                    goto Next;

                // Perform a change of variables from scene surface to pixel area.
                // First: map the scene surface to the solid angle about the camera
                var dirToCam = scene.CameraPosition - vertex.point.position;
                float distToCam = dirToCam.Length();
                float cosToCam = Math.Abs(Vector3.Dot(vertex.point.normal, dirToCam)) / distToCam;
                float surfaceToSolidAngle = cosToCam / (distToCam * distToCam);

                if (distToCam == 0 || cosToCam == 0)
                    goto Next;

                // Second: map the solid angle to the pixel area
                float solidAngleToPixel = scene.ComputeCamaraSolidAngleToPixelJacobian(vertex.point.position);

                // Third: combine to get the full jacobian 
                float surfaceToPixelJacobian = surfaceToSolidAngle * solidAngleToPixel;

                var (bsdfWeight, _) = scene.EvaluateBsdf(vertex.point, dirToAncestor, dirToCam, true);

                ColorRGB weight = vertex.weight * bsdfWeight * surfaceToPixelJacobian * (1.0f / TotalPaths);

                // Compute image contribution and splat
                scene.frameBuffer.Splat(raster.x, raster.y, weight * (1.0f / NumIterations));

            Next:
                vertexId = vertex.ancestorId;
            }
        }

        void TraceLightPath(Scene scene, RNG rng, PathCache pathCache, int pathIdx) {
            var emitter = SelectEmitter(scene, rng); // TODO once this is a proper selection: obtain and consider PDF

            // Sample a ray from the emitter
            var primaryPos = rng.NextFloat2D();
            var primaryDir = rng.NextFloat2D();
            var emitterSample = emitter.WrapPrimaryToRay(primaryPos, primaryDir);
            var radiance = emitter.ComputeEmission(emitterSample.surface.point, emitterSample.direction);

            // Compute the initial weight of the path
            float pdf = emitterSample.surface.jacobian * emitterSample.jacobian;
            var weight = radiance * (emitterSample.shadingCosine / pdf);

            // Perform a random walk, caching the vertices as we go.
            var walker = new CachedRandomWalk(scene, rng, MaxDepth, pathCache);
            walker.StartFromEmitter(emitterSample, weight);

            // keep track of the endpoint
            endpointIds[pathIdx] = walker.lastId;
        }

        Emitter SelectEmitter(Scene scene, RNG rng) {
            return scene.Emitters[0]; // TODO proper selection
        }

        const int TotalPaths = 512 * 512 * 4;
        const UInt32 BaseSeed = 0xC030114;
        const int MaxDepth = 2;
        const int NumIterations = 200;

        int[] endpointIds = new int[TotalPaths];
    }

}