using GroundWrapper;
using GroundWrapper.GroundMath;
using GroundWrapper.Geometry;

namespace Integrators.Common {
    public class RandomWalk {
        public RandomWalk(Scene scene, RNG rng, int maxDepth) {
            this.scene = scene;
            this.rng = rng;
            this.maxDepth = maxDepth;
        }

        public virtual ColorRGB StartFromCamera(Vector2 filmPosition, SurfacePoint cameraPoint, 
            float pdfFromCamera, Ray primaryRay, ColorRGB initialWeight) {
            isOnLightSubpath = false;
            return ContinueWalk(primaryRay, cameraPoint, pdfFromCamera, initialWeight, 1);
        }

        public virtual ColorRGB StartFromEmitter(EmitterSample emitterSample, ColorRGB initialWeight) {
            isOnLightSubpath = true;
            Ray ray = scene.SpawnRay(emitterSample.surface.point, emitterSample.direction);
            return ContinueWalk(ray, emitterSample.surface.point, emitterSample.jacobian, initialWeight, 1);
        }

        // TODO implement this function for splitting support!
        //public ColorRGB StartOnSurface(bool isOnLightSubpath) {
        //    this.isOnLightSubpath = isOnLightSubpath;
        //}

        protected virtual ColorRGB OnInvalidHit() {
            return ColorRGB.Black;
        }

        protected virtual ColorRGB OnHit(Ray ray, Hit hit, float pdfFromAncestor, float pdfToAncestor, 
                                         ColorRGB throughput, int depth, GeometryTerms geometryTerms) {
            return ColorRGB.Black;
        }

        protected virtual (float, float, ColorRGB, Vector3) SampleNextDirection(Hit hit, Ray ray) {
            // Sample the next direction from the BSDF
            float u = rng.NextFloat();
            float v = rng.NextFloat();
            var bsdfSample = scene.WrapPrimarySampleToBsdf(hit.point, -ray.direction, u, v, isOnLightSubpath);
            var (bsdfValue, shadingCosine) =
                scene.EvaluateBsdf(hit.point, -ray.direction, bsdfSample.direction, isOnLightSubpath);

            return (
                bsdfSample.pdf, 
                bsdfSample.pdfReverse,
                bsdfValue * (shadingCosine / bsdfSample.pdf),
                bsdfSample.direction
            );
        }

        ColorRGB ContinueWalk(Ray ray, SurfacePoint previousPoint, float pdfDirection, ColorRGB throughput, int initialDepth) {
            ColorRGB estimate = ColorRGB.Black;
            for (int depth = initialDepth; depth < maxDepth; ++depth) {
                var hit = scene.TraceRay(ray);
                if (!scene.IsValid(hit)) {
                    estimate += OnInvalidHit();
                    break;
                }

                // Convert the PDF of the previous hemispherical sample to surface area
                var geometryTerms = scene.ComputeGeometryTerms(previousPoint, hit.point);
                float pdfFromAncestor = pdfDirection * geometryTerms.cosineTo / geometryTerms.squaredDistance;

                // Sample the next direction (required to know the reverse pdf)
                var (pdfNext, pdfReverse, weight, direction) = SampleNextDirection(hit, ray);

                // Compute the surface area pdf of sampling the previous path segment backwards
                float pdfToAncestor = pdfReverse * geometryTerms.cosineFrom / geometryTerms.squaredDistance;

                estimate += OnHit(ray, hit, pdfFromAncestor, pdfToAncestor, throughput, depth, geometryTerms);

                // Terminate if the maximum depth has been reached
                if (depth >= maxDepth) break;

                // Every so often, the BSDF samples an invalid direction (e.g., due to shading normals or imperfect sampling)
                if (pdfNext == 0 || weight == ColorRGB.Black) break;

                // Continue the path with the next ray
                ray = scene.SpawnRay(hit.point, direction);
                throughput *= weight;
                pdfDirection = pdfNext;
                previousPoint = hit.point;
            }
           
            return estimate;
        }

        protected Scene scene;
        protected RNG rng;
        protected int maxDepth;
        protected bool isOnLightSubpath;
    }
}
