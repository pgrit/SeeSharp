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
        public int NumShadowRays = 1;
        public bool EnableBsdfDI = true;

        public virtual void RegisterSample(Vector2 pixel, ColorRGB weight, float misWeight, uint depth, bool isNextEvent) { }

        public override void Render(Scene scene) {
            for (uint sampleIndex = 0; sampleIndex < TotalSpp; ++sampleIndex) {
                scene.FrameBuffer.StartIteration();
                System.Threading.Tasks.Parallel.For(0, scene.FrameBuffer.Height,
                    row => {
                        for (uint col = 0; col < scene.FrameBuffer.Width; ++col) {
                            RenderPixel(scene, (uint)row, col, sampleIndex);
                        }
                    }
                );
                scene.FrameBuffer.EndIteration();
            }
        }

        private void RenderPixel(Scene scene, uint row, uint col, uint sampleIndex) {
            // Seed the random number generator
            uint pixelIndex = row * (uint)scene.FrameBuffer.Width + col;
            var seed = RNG.HashSeed(BaseSeed, pixelIndex, sampleIndex);
            var rng = new RNG(seed);

            // Sample a ray from the camera
            var offset = rng.NextFloat2D();
            var pixel = new Vector2(col, row) + offset;
            Ray primaryRay = scene.Camera.GenerateRay(pixel, rng).Ray;

            var value = EstimateIncidentRadiance(scene, primaryRay, rng, pixel, ColorRGB.White);

            // TODO / HACK we do nearest neighbor splatting manually here, to avoid numerical
            //             issues if the primary samples are almost 1 (400 + 0.99999999f = 401)
            scene.FrameBuffer.Splat(col, row, value);
        }

        private ColorRGB EstimateIncidentRadiance(Scene scene, Ray ray, RNG rng, Vector2 pixel, ColorRGB throughput,
                                                  uint depth = 1, SurfacePoint? previousHit = null, float previousPdf = 0.0f) {
            ColorRGB value = ColorRGB.Black;

            // Did we reach the maximum depth?
            if (depth > MaxDepth) return value;

            var hit = scene.Raytracer.Trace(ray);

            // Did the ray leave the scene?
            if (!hit) return OnBackgroundHit(scene, ray, pixel, throughput, depth, previousPdf);

            // Check if a light source was hit.
            Emitter light = scene.QueryEmitter(hit);
            if (light != null && depth >= MinDepth) {
                value += OnLightHit(scene, ray, pixel, throughput, depth, previousHit, previousPdf, hit, light);
            }

            if (depth + 1 >= MinDepth && depth < MaxDepth) {
                for (int i = 0; i < NumShadowRays; ++i) {
                    value += PerformBackgroundNextEvent(scene, ray, hit, rng, pixel, throughput, depth);
                    value += PerformNextEventEstimation(scene, ray, hit, rng, pixel, throughput, depth);
                }
            }

            if (depth > MaxDepth) return value;

            // Contine the random walk with a sample proportional to the BSDF
            (var bsdfRay, float bsdfPdf, var bsdfSampleWeight) = BsdfSample(scene, ray, hit, rng);

            if (bsdfPdf == 0 || bsdfSampleWeight == ColorRGB.Black)
                return value;

            var indirectRadiance = EstimateIncidentRadiance(scene, bsdfRay, rng, pixel, throughput * bsdfSampleWeight,
                                                            depth + 1, hit, bsdfPdf);
            return value + indirectRadiance * bsdfSampleWeight;
        }

        private ColorRGB OnBackgroundHit(Scene scene, Ray ray, Vector2 pixel, ColorRGB throughput, uint depth, float previousPdf) {
            if (scene.Background == null || !EnableBsdfDI)
                return ColorRGB.Black;

            float misWeight = 1.0f;
            if (depth > 1) {
                // Compute the balance heuristic MIS weight
                float pdfNextEvent = scene.Background.DirectionPdf(ray.Direction) * NumShadowRays;
                misWeight = 1 / (1 + pdfNextEvent / previousPdf);
            }

            var emission = scene.Background.EmittedRadiance(ray.Direction);
            RegisterSample(pixel, emission * throughput, misWeight, depth, false);
            return misWeight * emission;
        }

        private ColorRGB OnLightHit(Scene scene, Ray ray, Vector2 pixel, ColorRGB throughput, uint depth,
                                    SurfacePoint? previousHit, float previousPdf, SurfacePoint hit, Emitter light) {
            float misWeight = 1.0f;
            if (depth > 1) { // directly visible emitters are not explicitely connected
                             // Compute the surface area PDFs.
                var jacobian = SampleWrap.SurfaceAreaToSolidAngle(previousHit.Value, hit);
                float pdfNextEvt = light.PdfArea(hit) / scene.Emitters.Count * NumShadowRays;
                float pdfBsdf = previousPdf * jacobian;

                // Compute power heuristic MIS weights
                float pdfRatio = pdfNextEvt / pdfBsdf;
                misWeight = 1 / (pdfRatio * pdfRatio + 1);

                if (!EnableBsdfDI) misWeight = 0;
            }

            var emission = light.EmittedRadiance(hit, -ray.Direction);
            RegisterSample(pixel, emission * throughput, misWeight, depth, false);
            return misWeight * emission;
        }

        private ColorRGB PerformBackgroundNextEvent(Scene scene, Ray ray, SurfacePoint hit, RNG rng,
                                                    Vector2 pixel, ColorRGB throughput, uint depth) {
            if (scene.Background == null)
                return ColorRGB.Black; // There is no background

            var sample = scene.Background.SampleDirection(rng.NextFloat2D());
            if (scene.Raytracer.LeavesScene(hit, sample.Direction)) {
                var bsdfTimesCosine = hit.Material.EvaluateWithCosine(hit, -ray.Direction, sample.Direction, false);
                var (pdfBsdf, _)= hit.Material.Pdf(hit, -ray.Direction, sample.Direction, false);

                // Since the densities are in solid angle unit, no need for any conversions here
                float misWeight = EnableBsdfDI ? 1 / (1.0f + pdfBsdf / (sample.Pdf * NumShadowRays)) : 1;

                var contrib = sample.Weight * bsdfTimesCosine / NumShadowRays;
                RegisterSample(pixel, contrib * throughput, misWeight, depth + 1, true);
                return misWeight * contrib;
            }

            // The background is occluded
            return ColorRGB.Black;
        }

        private ColorRGB PerformNextEventEstimation(Scene scene, Ray ray, SurfacePoint hit, RNG rng, Vector2 pixel,
                                                    ColorRGB throughput, uint depth) {
            if (scene.Emitters.Count == 0)
                return ColorRGB.Black;

            // Select a light source
            int idx = rng.NextInt(0, scene.Emitters.Count);
            var light = scene.Emitters[idx];
            float lightSelectProb = 1.0f / scene.Emitters.Count;

            // Sample a point on the light source
            var lightSample = light.SampleArea(rng.NextFloat2D());

            if (!scene.Raytracer.IsOccluded(hit, lightSample.point)) {
                Vector3 lightToSurface = hit.Position - lightSample.point.Position;
                var emission = light.EmittedRadiance(lightSample.point, lightToSurface);

                // Compute the jacobian for surface area -> solid angle
                // (Inverse of the jacobian for solid angle pdf -> surface area pdf)
                float jacobian = SampleWrap.SurfaceAreaToSolidAngle(hit, lightSample.point);
                var bsdfCos = hit.Material.EvaluateWithCosine(hit, -ray.Direction, -lightToSurface, false);

                // Compute surface area PDFs
                float pdfNextEvt = lightSample.pdf * lightSelectProb * NumShadowRays;
                float pdfBsdfSolidAngle = hit.Material.Pdf(hit, -ray.Direction, -lightToSurface, false).Item1;
                float pdfBsdf = pdfBsdfSolidAngle * jacobian;

                // Compute the resulting power heuristic weights
                float pdfRatio = pdfBsdf / pdfNextEvt;
                float misWeight = EnableBsdfDI ? 1.0f / (pdfRatio * pdfRatio + 1) : 1;

                // Compute the final sample weight, account for the change of variables from light source area
                // to the hemisphere about the shading point.

                var contrib = emission * bsdfCos * (jacobian / lightSample.pdf / lightSelectProb) / NumShadowRays;
                RegisterSample(pixel, contrib * throughput, misWeight, depth + 1, true);
                return misWeight * contrib;
            }

            return ColorRGB.Black;
        }

        private (Ray, float, ColorRGB) BsdfSample(Scene scene, Ray ray, SurfacePoint hit, RNG rng) {
            var primary = rng.NextFloat2D();
            var bsdfSample = hit.Material.Sample(hit, -ray.Direction, false, primary);
            var bsdfRay = scene.Raytracer.SpawnRay(hit, bsdfSample.direction);
            return (bsdfRay, bsdfSample.pdf, bsdfSample.weight);
        }
    }

}