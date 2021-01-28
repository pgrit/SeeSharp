using SeeSharp.Core;
using SeeSharp.Core.Geometry;
using SeeSharp.Core.Sampling;
using SeeSharp.Core.Shading;
using SeeSharp.Core.Shading.Emitters;
using SeeSharp.Integrators.Bidir;
using System;
using System.Diagnostics;
using System.Numerics;
using TinyEmbree;

namespace SeeSharp.Integrators {
    public class PathTracer : Integrator {
        public UInt32 BaseSeed = 0xC030114;
        public int TotalSpp = 20;
        public uint MaxDepth = 2;
        public uint MinDepth = 1;
        public int NumShadowRays = 1;
        public bool EnableBsdfDI = true;
        public bool RenderTechniquePyramid = true;

        TechPyramid techPyramidRaw;
        TechPyramid techPyramidWeighted;

        public virtual void RegisterSample(Vector2 pixel, ColorRGB weight, float misWeight, uint depth,
                                           bool isNextEvent) {
            if (!RenderTechniquePyramid)
                return;
            weight /= TotalSpp;
            int cameraEdges = (int)depth - (isNextEvent ? 1 : 0);
            techPyramidRaw.Add(cameraEdges, 0, (int)depth, pixel, weight);
            techPyramidWeighted.Add(cameraEdges, 0, (int)depth, pixel, weight * misWeight);
        }

        protected virtual void RegisterRadianceEstimate(SurfacePoint hit, Vector3 outDir, Vector3 inDir,
                                                        ColorRGB directIllum, ColorRGB indirectIllum,
                                                        Vector2 pixel, ColorRGB throughput, float pdfInDir,
                                                        float pdfNextEvent) {}

        protected virtual void PreIteration(Scene scene, uint iterIdx) {}
        protected virtual void PostIteration(uint iterIdx) {}

        public override void Render(Scene scene) {
            if (RenderTechniquePyramid) {
                techPyramidRaw = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                    (int)MinDepth, (int)MaxDepth, false, false, false);
                techPyramidWeighted = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                    (int)MinDepth, (int)MaxDepth, false, false, false);
            }

            for (uint sampleIndex = 0; sampleIndex < TotalSpp; ++sampleIndex) {
                scene.FrameBuffer.StartIteration();
                PreIteration(scene, sampleIndex);
                System.Threading.Tasks.Parallel.For(0, scene.FrameBuffer.Height,
                    row => {
                        for (uint col = 0; col < scene.FrameBuffer.Width; ++col) {
                            RenderPixel(scene, (uint)row, col, sampleIndex);
                        }
                    }
                );
                PostIteration(sampleIndex);
                scene.FrameBuffer.EndIteration();
            }

            if (RenderTechniquePyramid) {
                string pathRaw = System.IO.Path.Join(scene.FrameBuffer.Basename, "techs-raw");
                techPyramidRaw.WriteToFiles(pathRaw);
                string pathWeighted = System.IO.Path.Join(scene.FrameBuffer.Basename, "techs-weighted");
                techPyramidWeighted.WriteToFiles(pathWeighted);
            }
        }

        protected struct RadianceEstimate {
            public ColorRGB Emitted { get; init; }
            public ColorRGB Reflected { get; init; }
            public float NextEventPdf { get; init; }

            public ColorRGB Outgoing => Emitted + Reflected;

            public static RadianceEstimate Absorbed
            => new RadianceEstimate();
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

            var estimate = EstimateIncidentRadiance(scene, primaryRay, rng, pixel, ColorRGB.White);

            // TODO / HACK we do nearest neighbor splatting manually here, to avoid numerical
            //             issues if the primary samples are almost 1 (400 + 0.99999999f = 401)
            scene.FrameBuffer.Splat(col, row, estimate.Outgoing);
        }

        private RadianceEstimate EstimateIncidentRadiance(Scene scene, Ray ray, RNG rng, Vector2 pixel,
                                                          ColorRGB throughput, uint depth = 1,
                                                          SurfacePoint? previousHit = null,
                                                          float previousPdf = 0.0f) {
            // Trace the next ray
            if (depth > MaxDepth)
                return RadianceEstimate.Absorbed;
            var hit = scene.Raytracer.Trace(ray);

            ColorRGB directHitContrib = ColorRGB.Black;
            float nextEventPdf = 0;

            if (!hit && depth >= MinDepth) {
                (directHitContrib, nextEventPdf) = OnBackgroundHit(scene, ray, pixel, throughput, depth, previousPdf);
                return new RadianceEstimate {
                    Emitted = directHitContrib,
                    NextEventPdf = nextEventPdf
                };
            } else if (!hit) {
                return RadianceEstimate.Absorbed;
            }

            // Check if a light source was hit.
            Emitter light = scene.QueryEmitter(hit);
            if (light != null && depth >= MinDepth) {
                (directHitContrib, nextEventPdf) = OnLightHit(scene, ray, pixel, throughput, depth,
                    previousHit, previousPdf, hit, light);
            }

            ColorRGB nextEventContrib = ColorRGB.Black;
            // Perform next event estimation
            if (depth + 1 >= MinDepth && depth < MaxDepth) {
                for (int i = 0; i < NumShadowRays; ++i) {
                    nextEventContrib += PerformBackgroundNextEvent(scene, ray, hit, rng, pixel, throughput, depth);
                    nextEventContrib += PerformNextEventEstimation(scene, ray, hit, rng, pixel, throughput, depth);
                }
            }

            // Sample a direction to continue the random walk
            (var bsdfRay, float bsdfPdf, var bsdfSampleWeight) = SampleDirection(scene, ray, hit, rng);
            if (bsdfPdf == 0 || bsdfSampleWeight == ColorRGB.Black)
                return new RadianceEstimate {
                    Emitted = directHitContrib,
                    NextEventPdf = nextEventPdf,
                    Reflected = nextEventContrib
                };

            // Recursively estimate the incident radiance and log the result
            var nested = EstimateIncidentRadiance(scene, bsdfRay, rng, pixel, throughput * bsdfSampleWeight,
                depth + 1, hit, bsdfPdf);
            RegisterRadianceEstimate(hit, -ray.Direction, bsdfRay.Direction, nested.Emitted, nested.Reflected,
                pixel, throughput * bsdfSampleWeight * bsdfPdf, bsdfPdf, nested.NextEventPdf);
            return new RadianceEstimate {
                Emitted = directHitContrib,
                NextEventPdf = nextEventPdf,
                Reflected = nextEventContrib + nested.Outgoing * bsdfSampleWeight
            };
        }

        private (ColorRGB, float) OnBackgroundHit(Scene scene, Ray ray, Vector2 pixel, ColorRGB throughput,
                                                  uint depth, float previousPdf) {
            if (scene.Background == null || !EnableBsdfDI)
                return (ColorRGB.Black, 0);

            float misWeight = 1.0f;
            float pdfNextEvent = 0;
            if (depth > 1) {
                // Compute the balance heuristic MIS weight
                pdfNextEvent = scene.Background.DirectionPdf(ray.Direction) * NumShadowRays;
                misWeight = 1 / (1 + pdfNextEvent / previousPdf);
            }

            var emission = scene.Background.EmittedRadiance(ray.Direction);
            RegisterSample(pixel, emission * throughput, misWeight, depth, false);
            return (misWeight * emission, pdfNextEvent);
        }

        private (ColorRGB, float) OnLightHit(Scene scene, Ray ray, Vector2 pixel, ColorRGB throughput,
                                             uint depth, SurfacePoint? previousHit, float previousPdf,
                                             SurfacePoint hit, Emitter light) {
            float misWeight = 1.0f;
            float pdfNextEvt = 0;
            if (depth > 1) { // directly visible emitters are not explicitely connected
                // Compute the solid angle pdf of next event
                var jacobian = SampleWarp.SurfaceAreaToSolidAngle(previousHit.Value, hit);
                pdfNextEvt = light.PdfArea(hit) / scene.Emitters.Count * NumShadowRays / jacobian;

                // Compute power heuristic MIS weights
                float pdfRatio = pdfNextEvt / previousPdf;
                misWeight = 1 / (pdfRatio * pdfRatio + 1);

                if (!EnableBsdfDI) misWeight = 0;
            }

            var emission = light.EmittedRadiance(hit, -ray.Direction);
            RegisterSample(pixel, emission * throughput, misWeight, depth, false);
            return (misWeight * emission, pdfNextEvt);
        }

        private ColorRGB PerformBackgroundNextEvent(Scene scene, Ray ray, SurfacePoint hit, RNG rng,
                                                    Vector2 pixel, ColorRGB throughput, uint depth) {
            if (scene.Background == null)
                return ColorRGB.Black; // There is no background

            var sample = scene.Background.SampleDirection(rng.NextFloat2D());
            if (scene.Raytracer.LeavesScene(hit, sample.Direction)) {
                var bsdfTimesCosine = hit.Material.EvaluateWithCosine(
                    hit, -ray.Direction, sample.Direction, false);
                var pdfBsdf = DirectionPdf(hit, -ray.Direction, sample.Direction);

                // Prevent NaN / Inf
                if (pdfBsdf == 0 || sample.Pdf == 0) {
                    RegisterRadianceEstimate(hit, -ray.Direction, sample.Direction, ColorRGB.Black,
                        ColorRGB.Black, pixel, ColorRGB.Black, 0, 0);
                    return ColorRGB.Black;
                }

                // Since the densities are in solid angle unit, no need for any conversions here
                float misWeight = EnableBsdfDI ? 1 / (1.0f + pdfBsdf / (sample.Pdf * NumShadowRays)) : 1;
                var contrib = sample.Weight * bsdfTimesCosine / NumShadowRays;

                Debug.Assert(float.IsFinite(contrib.Average));
                Debug.Assert(float.IsFinite(misWeight));

                RegisterSample(pixel, contrib * throughput, misWeight, depth + 1, true);
                RegisterRadianceEstimate(hit, -ray.Direction, sample.Direction,
                    misWeight * sample.Weight * sample.Pdf, ColorRGB.Black, pixel,
                    throughput * bsdfTimesCosine, sample.Pdf * NumShadowRays, sample.Pdf);

                return misWeight * contrib;
            }

            RegisterRadianceEstimate(hit, -ray.Direction, sample.Direction, ColorRGB.Black, ColorRGB.Black,
                pixel, ColorRGB.Black, 0, 0);
            return ColorRGB.Black;
        }

        private ColorRGB PerformNextEventEstimation(Scene scene, Ray ray, SurfacePoint hit, RNG rng,
                                                    Vector2 pixel, ColorRGB throughput, uint depth) {
            if (scene.Emitters.Count == 0)
                return ColorRGB.Black;

            // Select a light source
            int idx = rng.NextInt(0, scene.Emitters.Count);
            var light = scene.Emitters[idx];
            float lightSelectProb = 1.0f / scene.Emitters.Count;

            // Sample a point on the light source
            var lightSample = light.SampleArea(rng.NextFloat2D());
            Vector3 lightToSurface = hit.Position - lightSample.Point.Position;

            if (!scene.Raytracer.IsOccluded(hit, lightSample.Point)) {
                var emission = light.EmittedRadiance(lightSample.Point, lightToSurface);

                // Compute the jacobian for surface area -> solid angle
                // (Inverse of the jacobian for solid angle pdf -> surface area pdf)
                float jacobian = SampleWarp.SurfaceAreaToSolidAngle(hit, lightSample.Point);
                var bsdfCos = hit.Material.EvaluateWithCosine(hit, -ray.Direction, -lightToSurface, false);

                // Compute surface area PDFs
                float pdfNextEvt = lightSample.Pdf * lightSelectProb * NumShadowRays;
                float pdfBsdfSolidAngle = DirectionPdf(hit, -ray.Direction, -lightToSurface);
                float pdfBsdf = pdfBsdfSolidAngle * jacobian;

                // Avoid Inf / NaN
                if (pdfBsdf == 0 || jacobian == 0) {
                    RegisterRadianceEstimate(hit, -ray.Direction, -lightToSurface, ColorRGB.Black,
                        ColorRGB.Black, pixel, ColorRGB.Black, 0, 0);
                    return ColorRGB.Black;
                }

                // Compute the resulting power heuristic weights
                float pdfRatio = pdfBsdf / pdfNextEvt;
                float misWeight = EnableBsdfDI ? 1.0f / (pdfRatio * pdfRatio + 1) : 1;

                // Compute the final sample weight, account for the change of variables from light source area
                // to the hemisphere about the shading point.
                var pdf = lightSample.Pdf / jacobian * lightSelectProb * NumShadowRays;
                RegisterSample(pixel, emission / pdf * bsdfCos * throughput, misWeight, depth + 1, true);
                RegisterRadianceEstimate(hit, -ray.Direction, -lightToSurface, misWeight * emission,
                    ColorRGB.Black, pixel, throughput * bsdfCos, pdf, pdfNextEvt / jacobian);
                return misWeight * emission / pdf * bsdfCos;
            }

            // We register zero-valued samples, as they are used by, e.g., path guiding and optimal MIS
            RegisterRadianceEstimate(hit, -ray.Direction, -lightToSurface, ColorRGB.Black, ColorRGB.Black,
                pixel, ColorRGB.Black, 0, 0);
            return ColorRGB.Black;
        }

        protected virtual float DirectionPdf(SurfacePoint hit, Vector3 outDir, Vector3 sampledDir)
        => hit.Material.Pdf(hit, outDir, sampledDir, false).Item1;

        protected virtual (Ray, float, ColorRGB) SampleDirection(Scene scene, Ray ray, SurfacePoint hit,
                                                                 RNG rng) {
            var primary = rng.NextFloat2D();
            var bsdfSample = hit.Material.Sample(hit, -ray.Direction, false, primary);
            var bsdfRay = scene.Raytracer.SpawnRay(hit, bsdfSample.direction);
            return (bsdfRay, bsdfSample.pdf, bsdfSample.weight);
        }
    }

}