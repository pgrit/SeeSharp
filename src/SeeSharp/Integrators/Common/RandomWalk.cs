using SeeSharp.Core;
using SeeSharp.Core.Geometry;
using SeeSharp.Core.Sampling;
using SeeSharp.Core.Shading;
using SeeSharp.Core.Shading.Emitters;
using System.Numerics;

namespace SeeSharp.Integrators.Common {
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
            Ray ray = scene.Raytracer.SpawnRay(emitterSample.point, emitterSample.direction);
            return ContinueWalk(ray, emitterSample.point, emitterSample.pdf, initialWeight, 1);
        }

        public virtual ColorRGB StartFromBackground(Ray ray, ColorRGB initialWeight, float pdf) {
            isOnLightSubpath = true;

            // Find the first actual hitpoint on scene geometry
            var hit = scene.Raytracer.Trace(ray);
            if (!hit) 
                return OnInvalidHit();

            // Sample the next direction (required to know the reverse pdf)
            var (pdfNext, pdfReverse, weight, direction) = SampleNextDirection(hit, ray);

            // Both pdfs have unit sr-1
            float pdfFromAncestor = pdf;
            float pdfToAncestor = pdfReverse;

            ColorRGB estimate = OnHit(ray, hit, pdfFromAncestor, pdfToAncestor, initialWeight, 1, 1.0f, direction);

            // Terminate if the maximum depth has been reached
            if (1 >= maxDepth) 
                return estimate;

            // Every so often, the BSDF samples an invalid direction (e.g., due to shading normals or imperfect sampling)
            if (pdfNext == 0 || weight == ColorRGB.Black) 
                return estimate;

            // Continue the path with the next ray
            ray = scene.Raytracer.SpawnRay(hit, direction);
            return estimate + ContinueWalk(ray, hit, pdfNext, initialWeight * weight, 2);
        }

        // TODO implement this function for splitting support!
        //public ColorRGB StartOnSurface(bool isOnLightSubpath) {
        //    this.isOnLightSubpath = isOnLightSubpath;
        //}

        protected virtual ColorRGB OnInvalidHit() {
            return ColorRGB.Black;
        }

        protected virtual ColorRGB OnHit(Ray ray, SurfacePoint hit, float pdfFromAncestor, float pdfToAncestor,
                                         ColorRGB throughput, int depth, float toAncestorJacobian, Vector3 nextDirection) {
            return ColorRGB.Black;
        }

        protected virtual (float, float, ColorRGB, Vector3) SampleNextDirection(SurfacePoint hit, Ray ray) {
            // Sample the next direction from the BSDF
            var bsdfSample = hit.Bsdf.Sample(-ray.Direction, isOnLightSubpath, rng.NextFloat2D());
            return (
                bsdfSample.pdf,
                bsdfSample.pdfReverse,
                bsdfSample.weight,
                bsdfSample.direction
            );
        }

        ColorRGB ContinueWalk(Ray ray, SurfacePoint previousPoint, float pdfDirection, ColorRGB throughput, int initialDepth) {
            ColorRGB estimate = ColorRGB.Black;
            for (int depth = initialDepth; depth < maxDepth; ++depth) {
                var hit = scene.Raytracer.Trace(ray);
                if (!hit) {
                    estimate += OnInvalidHit();
                    break;
                }

                // Convert the PDF of the previous hemispherical sample to surface area
                float pdfFromAncestor = pdfDirection * SampleWrap.SurfaceAreaToSolidAngle(previousPoint, hit);

                // Sample the next direction (required to know the reverse pdf)
                var (pdfNext, pdfReverse, weight, direction) = SampleNextDirection(hit, ray);

                // Compute the surface area pdf of sampling the previous path segment backwards
                float pdfToAncestor = pdfReverse * SampleWrap.SurfaceAreaToSolidAngle(hit, previousPoint);

                estimate += OnHit(ray, hit, pdfFromAncestor, pdfToAncestor, throughput, depth,
                                  SampleWrap.SurfaceAreaToSolidAngle(hit, previousPoint), direction);

                // Terminate if the maximum depth has been reached
                if (depth >= maxDepth) break;

                // Every so often, the BSDF samples an invalid direction (e.g., due to shading normals or imperfect sampling)
                if (pdfNext == 0 || weight == ColorRGB.Black) break;

                // Continue the path with the next ray
                ray = scene.Raytracer.SpawnRay(hit, direction);
                throughput *= weight;
                pdfDirection = pdfNext;
                previousPoint = hit;
            }

            return estimate;
        }

        protected Scene scene;
        protected RNG rng;
        protected int maxDepth;
        protected bool isOnLightSubpath;
    }
}
