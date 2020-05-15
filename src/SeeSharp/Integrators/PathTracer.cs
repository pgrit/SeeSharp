using SeeSharp.Core;
using SeeSharp.Core.Geometry;
using SeeSharp.Core.Sampling;
using SeeSharp.Core.Shading;
using SeeSharp.Core.Shading.Emitters;
using System;
using System.Numerics;

namespace SeeSharp.Integrators {

    public class PathTracer : Integrator {
        public UInt32 BaseSeed = 0xC030114;
        public int TotalSpp = 20;
        public uint MaxDepth = 2;
        public uint MinDepth = 1;

        public override void Render(Scene scene) {
            System.Threading.Tasks.Parallel.For(0, scene.FrameBuffer.Height,
                row => {
                    for (uint col = 0; col < scene.FrameBuffer.Width; ++col) {
                        RenderPixel(scene, (uint)row, col);
                    }
                }
            );
        }

        private void RenderPixel(Scene scene, uint row, uint col) {
            for (uint sampleIndex = 0; sampleIndex < TotalSpp; ++sampleIndex) {
                // Seed the random number generator
                uint pixelIndex = row * (uint)scene.FrameBuffer.Width + col;
                var seed = RNG.HashSeed(BaseSeed, pixelIndex, sampleIndex);
                var rng = new RNG(seed);

                // Sample a ray from the camera
                var offset = rng.NextFloat2D();
                Ray primaryRay = scene.Camera.GenerateRay(new Vector2(col, row) + offset);

                var value = EstimateIncidentRadiance(scene, primaryRay, rng);
                value = value * (1.0f / TotalSpp);

                // TODO we do nearest neighbor splatting manually here, to avoid numerical
                //      issues if the primary samples are almost 1 (400 + 0.99999999f = 401)
                scene.FrameBuffer.Splat(col, row, value);
            }
        }

        private ColorRGB PerformNextEventEstimation(Scene scene, Ray ray, SurfacePoint hit, RNG rng) {
            // Select a light source
            // TODO implement multi-light support
            int idx = rng.NextInt(0, scene.Emitters.Count);
            var light = scene.Emitters[idx];
            float lightSelectProb = 1.0f / scene.Emitters.Count;

            // Sample a point on the light source
            var lightSample = light.SampleArea(rng.NextFloat2D());

            if (!scene.Raytracer.IsOccluded(hit, lightSample.point)) {
                Vector3 lightToSurface = hit.position - lightSample.point.position;
                var emission = light.EmittedRadiance(lightSample.point, lightToSurface);

                // Compute the jacobian for surface area -> solid angle
                // (Inverse of the jacobian for solid angle pdf -> surface area pdf)
                float jacobian = SampleWrap.SurfaceAreaToSolidAngle(hit, lightSample.point);

                var bsdf = hit.Bsdf;
                var bsdfCos = bsdf.EvaluateWithCosine(-ray.direction, -lightToSurface, false);

                // Compute surface area PDFs
                float pdfNextEvt = lightSample.pdf * lightSelectProb;
                float pdfBsdfSolidAngle = bsdf.Pdf(-ray.direction, -lightToSurface, false).Item1;
                float pdfBsdf = pdfBsdfSolidAngle * jacobian;

                // Compute the resulting power heuristic weights
                float pdfRatio = pdfBsdf / pdfNextEvt;
                float misWeight = 1.0f / (pdfRatio * pdfRatio + 1);

                // Compute the final sample weight, account for the change of variables from light source area
                // to the hemisphere about the shading point.

                var value = misWeight * emission * bsdfCos * (jacobian / lightSample.pdf / lightSelectProb);
                return value;
            }

            return new ColorRGB { r=0, g=0, b=0 };
        }

        private (Ray, float, ColorRGB) BsdfSample(Scene scene, Ray ray, SurfacePoint hit, RNG rng) {
            var bsdf = hit.Bsdf;

            var primary = rng.NextFloat2D();
            var bsdfSample = bsdf.Sample(-ray.direction, false, primary);

            var bsdfRay = scene.Raytracer.SpawnRay(hit, bsdfSample.direction);

            return (bsdfRay, bsdfSample.pdf, bsdfSample.weight);
        }

        private ColorRGB EstimateIncidentRadiance(Scene scene, Ray ray, RNG rng, uint depth = 1,
            SurfacePoint? previousHit = null, float previousPdf = 0.0f) {
            ColorRGB value = ColorRGB.Black;

            // Did we reach the maximum depth?
            if (depth > MaxDepth) return value;

            var hit = scene.Raytracer.Trace(ray);

            // Did the ray leave the scene?
            if (!hit) return ColorRGB.Black;

            // Check if a light source was hit.
            Emitter light = scene.QueryEmitter(hit);
            if (light != null && depth >= MinDepth) {
                float misWeight = 1.0f;
                if (depth > 1) { // directly visible emitters are not explicitely connected
                    // Compute the surface area PDFs.
                    var jacobian = SampleWrap.SurfaceAreaToSolidAngle(previousHit.Value, hit);
                    float pdfNextEvt = light.PdfArea(hit) / scene.Emitters.Count;
                    float pdfBsdf = previousPdf * jacobian;

                    // Compute MIS weights
                    float pdfRatio = pdfNextEvt / pdfBsdf;
                    misWeight = 1 / (pdfRatio * pdfRatio + 1);
                }

                var emission = light.EmittedRadiance(hit, -ray.direction);
                value += misWeight * emission;
            }

            if (depth + 1 >= MinDepth && depth < MaxDepth)
                value += PerformNextEventEstimation(scene, ray, hit, rng);

            if (depth > MaxDepth) return value;

            // Contine the random walk with a sample proportional to the BSDF
            (var bsdfRay, float bsdfPdf, var bsdfSampleWeight) =
                BsdfSample(scene, ray, hit, rng);

            if (bsdfPdf == 0 || bsdfSampleWeight == ColorRGB.Black)
                return value;

            var indirectRadiance = EstimateIncidentRadiance(scene, bsdfRay, rng,
                depth + 1, hit, bsdfPdf);            

            return value + indirectRadiance * bsdfSampleWeight;
        }
    }

}