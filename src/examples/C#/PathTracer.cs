using System;
using Ground;

namespace Experiments {

    class PathTracer {
        public void Render(Scene scene) {
            System.Threading.Tasks.Parallel.For(0, scene.frameBuffer.height,
                row => {
                    for (uint col = 0; col < scene.frameBuffer.width; ++col) {
                        this.RenderPixel(scene, (uint)row, col);
                    }
                }
            );
        }

        private void RenderPixel(Scene scene, uint row, uint col) {
            for (uint sampleIndex = 0; sampleIndex < TotalSpp; ++sampleIndex) {
                // Seed the random number generator
                uint pixelIndex = row * scene.frameBuffer.width + col;
                var seed = RNG.HashSeed(BaseSeed, pixelIndex, sampleIndex);
                var rng = new RNG(seed);

                // Sample a ray from the camera
                (Ray primaryRay, Vector2 filmSample) = scene.SampleCamera(row, col,
                    rng.NextFloat(), rng.NextFloat());
                Hit primaryHit = scene.TraceRay(primaryRay);

                var value = ComputeOutgoingRadiance(scene, primaryRay, primaryHit, rng);
                value = value * (1.0f / TotalSpp);

                scene.frameBuffer.Splat(filmSample.x, filmSample.y, value);
            }
        }

        private ColorRGB PerformNextEventEstimation(Scene scene, Ray ray, Hit hit, RNG rng) {
            // Select a light source
            // TODO implement multi-light support
            var light = scene.Emitters[0];

            // Sample a point on the light source
            var lightSample = light.WrapPrimarySample(rng.NextFloat(), rng.NextFloat());

            if (!scene.IsOccluded(hit, lightSample.point.position)) {
                Vector3 lightDir = hit.point.position - lightSample.point.position;
                var emission = light.ComputeEmission(lightSample.point, lightDir);

                var bsdfValue = scene.EvaluateBsdf(hit.point, -ray.direction, lightDir, false);
                var geometryTerms = scene.ComputeGeometryTerms(hit.point, lightSample.point);

                // Compute MIS weights
                // TODO refactor and put in utility function
                float pdfNextEvt = lightSample.jacobian;
                float pdfBsdf = scene.ComputePrimaryToBsdfJacobian(hit.point, -ray.direction,
                    lightDir, false).jacobian * geometryTerms.cosineTo / geometryTerms.squaredDistance;
                float pdfRatio = pdfBsdf / pdfNextEvt;
                float misWeight = 1.0f / (pdfRatio * pdfRatio + 1);

                return misWeight * emission * bsdfValue * (geometryTerms.geomTerm / lightSample.jacobian);
            }

            return new ColorRGB { r=0, g=0, b=0 };
        }

        private ColorRGB DirectIllumBsdfSample(Scene scene, Ray ray, Hit hit, RNG rng) {
            // Estimate DI via BSDF importance sampling
            var bsdfSample = scene.WrapPrimarySampleToBsdf(hit.point,
                -ray.direction, rng.NextFloat(), rng.NextFloat(), false);

            var bsdfValue = scene.EvaluateBsdf(hit.point, -ray.direction,
                bsdfSample.direction, false);

            var bsdfRay = scene.SpawnRay(hit, bsdfSample.direction);
            var bsdfhit = scene.TraceRay(bsdfRay);

            var light = scene.QueryEmitter(bsdfhit.point);
            if (light != null) { // The light source was hit.
                var emission = light.ComputeEmission(bsdfhit.point, -bsdfRay.direction);

                var geometryTerms = scene.ComputeGeometryTerms(hit.point, bsdfhit.point);

                // Compute MIS weights
                float pdfNextEvt = light.Jacobian(bsdfhit.point);
                float pdfBsdf = bsdfSample.jacobian * geometryTerms.cosineTo / geometryTerms.squaredDistance;
                float pdfRatio = pdfNextEvt / pdfBsdf;
                float misWeight = 1 / (pdfRatio * pdfRatio + 1);

                return misWeight * emission * bsdfValue * (geometryTerms.cosineFrom / bsdfSample.jacobian);
            }
            return new ColorRGB { r=0, g=0, b=0 };
        }

        private ColorRGB ComputeOutgoingRadiance(Scene scene, Ray ray, Hit hit, RNG rng) {
            ColorRGB value = new ColorRGB { r=0, g=0, b=0 };
            if (hit.point.meshId < 0)
                return value;

            value = value + PerformNextEventEstimation(scene, ray, hit, rng);
            value = value + DirectIllumBsdfSample(scene, ray, hit, rng);

            return value;
        }

        const UInt32 BaseSeed = 0xC030114;
        const int TotalSpp = 8;
    }

}