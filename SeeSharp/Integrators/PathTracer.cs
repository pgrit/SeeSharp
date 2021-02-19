using SeeSharp.Geometry;
using SeeSharp.Sampling;
using SimpleImageIO;
using SeeSharp.Shading.Emitters;
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
        public bool RenderTechniquePyramid = false;

        TechPyramid techPyramidRaw;
        TechPyramid techPyramidWeighted;

        Util.FeatureLogger features;

        protected Scene scene;

        /// <summary>
        /// Called once for each complete path from the camera to a light.
        /// The default implementation generates a technique pyramid for the MIS samplers.
        /// </summary>
        public virtual void RegisterSample(Vector2 pixel, RgbColor weight, float misWeight, uint depth,
                                           bool isNextEvent) {
            if (!RenderTechniquePyramid)
                return;
            weight /= TotalSpp;
            int cameraEdges = (int)depth - (isNextEvent ? 1 : 0);
            techPyramidRaw.Add(cameraEdges, 0, (int)depth, pixel, weight);
            techPyramidWeighted.Add(cameraEdges, 0, (int)depth, pixel, weight * misWeight);
        }

        /// <summary> Called for each surface point, after incident radiance was estimated. </summary>
        protected virtual void RegisterRadianceEstimate(SurfacePoint hit, Vector3 outDir, Vector3 inDir,
                                                        RgbColor directIllum, RgbColor indirectIllum,
                                                        Vector2 pixel, RgbColor throughput, float pdfInDir,
                                                        float pdfNextEvent) {}

        /// <summary> Called after the scene was submitted, before rendering starts. </summary>
        protected virtual void OnPrepareRender() {}

        /// <summary>
        /// Called before each iteration (one sample per pixel), after the frame buffer was updated.
        /// </summary>
        protected virtual void OnPreIteration(uint iterIdx) {}

        /// <summary>
        /// Called at the end of each iteration (one sample per pixel), before the frame buffer is updated.
        /// </summary>
        protected virtual void OnPostIteration(uint iterIdx) {}

        /// <summary>
        /// Called for every surface hit, before any sampling takes place.
        /// </summary>
        protected virtual void OnHit(Ray ray, RNG rng, Vector2 pixel, RgbColor throughput, uint depth,
                                     SurfacePoint? previousHit, float previousPdf) {}

        public override void Render(Scene scene) {
            this.scene = scene;

            OnPrepareRender();

            if (RenderTechniquePyramid) {
                techPyramidRaw = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                    (int)MinDepth, (int)MaxDepth, false, false, false);
                techPyramidWeighted = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                    (int)MinDepth, (int)MaxDepth, false, false, false);
            }

            // Add custom frame buffer layers
            features = new(scene.FrameBuffer);

            for (uint sampleIndex = 0; sampleIndex < TotalSpp; ++sampleIndex) {
                scene.FrameBuffer.StartIteration();
                OnPreIteration(sampleIndex);
                System.Threading.Tasks.Parallel.For(0, scene.FrameBuffer.Height,
                    row => {
                        for (uint col = 0; col < scene.FrameBuffer.Width; ++col) {
                            RenderPixel((uint)row, col, sampleIndex);
                        }
                    }
                );
                OnPostIteration(sampleIndex);
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
            public RgbColor Emitted { get; init; }
            public RgbColor Reflected { get; init; }
            public float NextEventPdf { get; init; }

            public RgbColor Outgoing => Emitted + Reflected;

            public static RadianceEstimate Absorbed
            => new RadianceEstimate();
        }

        private void RenderPixel(uint row, uint col, uint sampleIndex) {
            // Seed the random number generator
            uint pixelIndex = row * (uint)scene.FrameBuffer.Width + col;
            var seed = RNG.HashSeed(BaseSeed, pixelIndex, sampleIndex);
            var rng = new RNG(seed);

            // Sample a ray from the camera
            var offset = rng.NextFloat2D();
            var pixel = new Vector2(col, row) + offset;
            Ray primaryRay = scene.Camera.GenerateRay(pixel, rng).Ray;

            var estimate = EstimateIncidentRadiance(primaryRay, rng, pixel, RgbColor.White);

            scene.FrameBuffer.Splat(col, row, estimate.Outgoing);
        }

        private RadianceEstimate EstimateIncidentRadiance(Ray ray, RNG rng, Vector2 pixel,
                                                          RgbColor throughput, uint depth = 1,
                                                          SurfacePoint? previousHit = null,
                                                          float previousPdf = 0.0f) {
            // Trace the next ray
            if (depth > MaxDepth)
                return RadianceEstimate.Absorbed;
            var hit = scene.Raytracer.Trace(ray);

            RgbColor directHitContrib = RgbColor.Black;
            float nextEventPdf = 0;

            if (!hit && depth >= MinDepth) {
                (directHitContrib, nextEventPdf) = OnBackgroundHit(ray, pixel, throughput, depth, previousPdf);
                return new RadianceEstimate {
                    Emitted = directHitContrib,
                    NextEventPdf = nextEventPdf
                };
            } else if (!hit) {
                return RadianceEstimate.Absorbed;
            }

            OnHit(ray, rng, pixel, throughput, depth, previousHit, previousPdf);

            if (depth == 1) {
                var albedo = ((SurfacePoint)hit).Material.GetScatterStrength(hit);
                features.LogPrimaryHit(pixel, albedo, hit.Normal, hit.Distance);
            }

            // Check if a light source was hit.
            Emitter light = scene.QueryEmitter(hit);
            if (light != null && depth >= MinDepth) {
                (directHitContrib, nextEventPdf) = OnLightHit(ray, pixel, throughput, depth,
                    previousHit, previousPdf, hit, light);
            }

            RgbColor nextEventContrib = RgbColor.Black;
            // Perform next event estimation
            if (depth + 1 >= MinDepth && depth < MaxDepth) {
                for (int i = 0; i < NumShadowRays; ++i) {
                    nextEventContrib += PerformBackgroundNextEvent(ray, hit, rng, pixel, throughput, depth);
                    nextEventContrib += PerformNextEventEstimation(ray, hit, rng, pixel, throughput, depth);
                }
            }

            // Terminate early if this is the last desired bounce
            if (depth >= MaxDepth) {
                return new RadianceEstimate {
                    Emitted = directHitContrib,
                    NextEventPdf = nextEventPdf,
                    Reflected = nextEventContrib
                };
            }

            // Sample a direction to continue the random walk
            (var bsdfRay, float bsdfPdf, var bsdfSampleWeight) = SampleDirection(ray, hit, rng);
            if (bsdfPdf == 0 || bsdfSampleWeight == RgbColor.Black)
                return new RadianceEstimate {
                    Emitted = directHitContrib,
                    NextEventPdf = nextEventPdf,
                    Reflected = nextEventContrib
                };

            // Recursively estimate the incident radiance and log the result
            var nested = EstimateIncidentRadiance(bsdfRay, rng, pixel, throughput * bsdfSampleWeight,
                depth + 1, hit, bsdfPdf);
            RegisterRadianceEstimate(hit, -ray.Direction, bsdfRay.Direction, nested.Emitted, nested.Reflected,
                pixel, throughput * bsdfSampleWeight * bsdfPdf, bsdfPdf, nested.NextEventPdf);
            return new RadianceEstimate {
                Emitted = directHitContrib,
                NextEventPdf = nextEventPdf,
                Reflected = nextEventContrib + nested.Outgoing * bsdfSampleWeight
            };
        }

        private (RgbColor, float) OnBackgroundHit(Ray ray, Vector2 pixel, RgbColor throughput,
                                                  uint depth, float previousPdf) {
            if (scene.Background == null || !EnableBsdfDI)
                return (RgbColor.Black, 0);

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

        private (RgbColor, float) OnLightHit(Ray ray, Vector2 pixel, RgbColor throughput,
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

        private RgbColor PerformBackgroundNextEvent(Ray ray, SurfacePoint hit, RNG rng,
                                                    Vector2 pixel, RgbColor throughput, uint depth) {
            if (scene.Background == null)
                return RgbColor.Black; // There is no background

            var sample = scene.Background.SampleDirection(rng.NextFloat2D());
            if (scene.Raytracer.LeavesScene(hit, sample.Direction)) {
                var bsdfTimesCosine = hit.Material.EvaluateWithCosine(
                    hit, -ray.Direction, sample.Direction, false);
                var pdfBsdf = DirectionPdf(hit, -ray.Direction, sample.Direction);

                // Prevent NaN / Inf
                if (pdfBsdf == 0 || sample.Pdf == 0) {
                    RegisterRadianceEstimate(hit, -ray.Direction, sample.Direction, RgbColor.Black,
                        RgbColor.Black, pixel, RgbColor.Black, 0, 0);
                    return RgbColor.Black;
                }

                // Since the densities are in solid angle unit, no need for any conversions here
                float misWeight = EnableBsdfDI ? 1 / (1.0f + pdfBsdf / (sample.Pdf * NumShadowRays)) : 1;
                var contrib = sample.Weight * bsdfTimesCosine / NumShadowRays;

                Debug.Assert(float.IsFinite(contrib.Average));
                Debug.Assert(float.IsFinite(misWeight));

                RegisterSample(pixel, contrib * throughput, misWeight, depth + 1, true);
                RegisterRadianceEstimate(hit, -ray.Direction, sample.Direction,
                    misWeight * sample.Weight * sample.Pdf, RgbColor.Black, pixel,
                    throughput * bsdfTimesCosine, sample.Pdf * NumShadowRays, sample.Pdf);

                return misWeight * contrib;
            }

            RegisterRadianceEstimate(hit, -ray.Direction, sample.Direction, RgbColor.Black, RgbColor.Black,
                pixel, RgbColor.Black, 0, 0);
            return RgbColor.Black;
        }

        private RgbColor PerformNextEventEstimation(Ray ray, SurfacePoint hit, RNG rng,
                                                    Vector2 pixel, RgbColor throughput, uint depth) {
            if (scene.Emitters.Count == 0)
                return RgbColor.Black;

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
                    RegisterRadianceEstimate(hit, -ray.Direction, -lightToSurface, RgbColor.Black,
                        RgbColor.Black, pixel, RgbColor.Black, 0, 0);
                    return RgbColor.Black;
                }

                // Compute the resulting power heuristic weights
                float pdfRatio = pdfBsdf / pdfNextEvt;
                float misWeight = EnableBsdfDI ? 1.0f / (pdfRatio * pdfRatio + 1) : 1;

                // Compute the final sample weight, account for the change of variables from light source area
                // to the hemisphere about the shading point.
                var pdf = lightSample.Pdf / jacobian * lightSelectProb * NumShadowRays;
                RegisterSample(pixel, emission / pdf * bsdfCos * throughput, misWeight, depth + 1, true);
                RegisterRadianceEstimate(hit, -ray.Direction, -lightToSurface, misWeight * emission,
                    RgbColor.Black, pixel, throughput * bsdfCos, pdf, pdfNextEvt / jacobian);
                return misWeight * emission / pdf * bsdfCos;
            }

            // We register zero-valued samples, as they are used by, e.g., path guiding and optimal MIS
            RegisterRadianceEstimate(hit, -ray.Direction, -lightToSurface, RgbColor.Black, RgbColor.Black,
                pixel, RgbColor.Black, 0, 0);
            return RgbColor.Black;
        }

        protected virtual float DirectionPdf(SurfacePoint hit, Vector3 outDir, Vector3 sampledDir)
        => hit.Material.Pdf(hit, outDir, sampledDir, false).Item1;

        protected virtual (Ray, float, RgbColor) SampleDirection(Ray ray, SurfacePoint hit, RNG rng) {
            var primary = rng.NextFloat2D();
            var bsdfSample = hit.Material.Sample(hit, -ray.Direction, false, primary);
            var bsdfRay = scene.Raytracer.SpawnRay(hit, bsdfSample.direction);
            return (bsdfRay, bsdfSample.pdf, bsdfSample.weight);
        }
    }

}