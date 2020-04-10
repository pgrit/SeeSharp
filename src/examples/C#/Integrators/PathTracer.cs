using System;
using Ground;

namespace Experiments {

    public class PathTracer {
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
                float u = rng.NextFloat();
                float v = rng.NextFloat();
                (Ray primaryRay, Vector2 filmSample) = scene.SampleCamera(row, col, u, v);

                var value = EstimateIncidentRadiance(scene, primaryRay, rng);
                value = value * (1.0f / TotalSpp);

                // TODO we do nearest neighbor splatting manually here, to avoid numerical
                //      issues if the primary samples are almost 1 (400 + 0.99999999f = 401)
                scene.frameBuffer.Splat((float)col, (float)row, value);
            }
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

                // Compute surface area PDFs
                float pdfNextEvt = lightSample.jacobian;
                float pdfBsdfSolidAngle = scene.ComputePrimaryToBsdfJacobian(
                    hit.point, -ray.direction, -lightToSurface, false).jacobian;
                float pdfBsdf = pdfBsdfSolidAngle * geometryTerms.cosineTo / geometryTerms.squaredDistance;

                // Compute the resulting power heuristic weights
                float pdfRatio = pdfBsdf / pdfNextEvt;
                float misWeight = 1.0f / (pdfRatio * pdfRatio + 1);

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

        private (Ray, float, ColorRGB) BsdfSample(Scene scene, Ray ray, Hit hit, RNG rng) {
            float u = rng.NextFloat();
            float v = rng.NextFloat();
            var bsdfSample = scene.WrapPrimarySampleToBsdf(hit.point,
                -ray.direction, u, v, false);

            (var bsdfValue, float shadingCosine) = scene.EvaluateBsdf(hit.point, -ray.direction,
                bsdfSample.direction, false);

            var bsdfRay = scene.SpawnRay(hit.point, bsdfSample.direction);

            var weight = bsdfSample.jacobian == 0.0f ? ColorRGB.Black : bsdfValue * (shadingCosine / bsdfSample.jacobian);

            return (bsdfRay, bsdfSample.jacobian, weight);
        }

        private ColorRGB EstimateIncidentRadiance(Scene scene, Ray ray, RNG rng, uint depth = 1,
            Hit? previousHit = null, float previousPdf = 0.0f)
        {
            ColorRGB value = ColorRGB.Black;

            // Did we reach the maximum depth?
            if (depth >= MaxDepth)
                return value;

            var hit = scene.TraceRay(ray);

            // Did the ray leave the scene?
            if (!scene.IsValid(hit))
                return ColorRGB.Black;

            // Check if a light source was hit.
            Emitter light = scene.QueryEmitter(hit.point);
            if (light != null) {
                float misWeight = 1.0f;
                if (depth > 1) { // directly visible emitters are not explicitely connected
                    // Compute the surface area PDFs.
                    var geometryTerms = scene.ComputeGeometryTerms(previousHit.Value.point, hit.point);
                    float pdfNextEvt = light.Jacobian(hit.point);
                    float pdfBsdf = previousPdf * geometryTerms.cosineTo / geometryTerms.squaredDistance;

                    // Compute MIS weights
                    float pdfRatio = pdfNextEvt / pdfBsdf;
                    misWeight = 1 / (pdfRatio * pdfRatio + 1);
                }

                var emission = light.ComputeEmission(hit.point, -ray.direction);
                value += misWeight * emission;
            }

            value = value + PerformNextEventEstimation(scene, ray, hit, rng);

            // Contine the random walk with a sample proportional to the BSDF
            (var bsdfRay, float bsdfJacobian, var bsdfSampleWeight) =
                BsdfSample(scene, ray, hit, rng);

            var indirectRadiance = EstimateIncidentRadiance(scene, bsdfRay, rng,
                depth + 1, hit, bsdfJacobian);

            return value + indirectRadiance * bsdfSampleWeight;
        }

        const UInt32 BaseSeed = 0xC030114;
        const int TotalSpp = 2;

        const uint MaxDepth = 10;
    }

}