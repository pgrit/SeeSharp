using System;
using System.Numerics;
using SeeSharp.Core;
using SeeSharp.Core.Geometry;
using SeeSharp.Core.Sampling;
using SeeSharp.Core.Shading;
using SeeSharp.Core.Shading.Emitters;
using SeeSharp.Integrators.Bidir;

namespace SeeSharp.Integrators.Wavefront {
    public class WavePathTracer : Integrator {
        public UInt32 BaseSeed = 0xC030114;
        public int TotalSpp = 20;
        public uint MaxDepth = 2;
        public uint MinDepth = 1;
        public bool RenderTechniquePyramid = false;

        Scene scene;
        TechPyramid techPyramidRaw;
        TechPyramid techPyramidWeighted;

        struct PathWeight {
            public Vector2 Pixel;
            public ColorRGB Throughput;
            public RNG Rng;
            public SurfacePoint PreviousHit;
            public float PreviousPdf;
        }

        public virtual void RegisterSample(Vector2 pixel, ColorRGB weight, float misWeight, int depth, bool isNextEvent) {
            if (!RenderTechniquePyramid)
                return;
            weight /= TotalSpp;
            techPyramidRaw.Add(depth - (isNextEvent ? 1 : 0), 0, depth, pixel, weight);
            techPyramidWeighted.Add(depth - (isNextEvent ? 1 : 0), 0, depth, pixel, weight * misWeight);
        }

        public override void Render(Scene scene) {
            this.scene = scene;

            var numPixels = scene.FrameBuffer.Width * scene.FrameBuffer.Height;
            var wavefront = new Wavefront(scene, numPixels);
            var pathWeights = new PathWeight[numPixels];

            if (RenderTechniquePyramid) {
                techPyramidRaw = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                    (int)MinDepth, (int)MaxDepth, false, false, false);
                techPyramidWeighted = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                    (int)MinDepth, (int)MaxDepth, false, false, false);
            }

            for (int sampleIndex = 0; sampleIndex < TotalSpp; ++sampleIndex) {
                scene.FrameBuffer.StartIteration();
                LaunchWave(wavefront, pathWeights, sampleIndex);
                for (int d = 1; d <= MaxDepth; ++d) {
                    wavefront.Intersect();
                    ShadeWave(wavefront, pathWeights, d);
                }
                scene.FrameBuffer.EndIteration();
            }

            if (RenderTechniquePyramid) {
                string pathRaw = System.IO.Path.Join(scene.FrameBuffer.Basename, "techs-raw");
                techPyramidRaw.WriteToFiles(pathRaw);
                string pathWeighted = System.IO.Path.Join(scene.FrameBuffer.Basename, "techs-weighted");
                techPyramidWeighted.WriteToFiles(pathWeighted);
            }
        }

        void LaunchWave(Wavefront wave, PathWeight[] weights, int sampleIndex) {
            wave.Init(idx => {
                // Seed the random number generator
                var seed = RNG.HashSeed(BaseSeed, (uint)idx, (uint)sampleIndex);
                weights[idx].Rng = new RNG(seed);

                // Sample pixel coordinate
                var offset = weights[idx].Rng.NextFloat2D();
                int row = idx / scene.FrameBuffer.Width;
                int col = idx % scene.FrameBuffer.Width;
                weights[idx].Pixel = new Vector2(col, row) + offset;

                weights[idx].Throughput = ColorRGB.White;
                return scene.Camera.GenerateRay(weights[idx].Pixel, weights[idx].Rng).Ray;
            });
        }

        void ShadeWave(Wavefront wave, PathWeight[] weights, int depth) {
            wave.Process((idx, ray, hit) => {
                ref PathWeight path = ref weights[idx];

                // Did the ray leave the scene?
                if (!hit) {
                    OnBackgroundHit(scene, ray, path.Pixel, path.Throughput, depth, path.PreviousPdf);
                    return null;
                }

                // Check if a light source was hit.
                Emitter light = scene.QueryEmitter(hit);
                if (light != null && depth >= MinDepth) {
                    var value = OnLightHit(scene, ray, path.Pixel, path.Throughput, depth, path.PreviousHit, path.PreviousPdf, hit, light);
                    scene.FrameBuffer.Splat(path.Pixel.X, path.Pixel.Y, value * path.Throughput);
                }

                // TODO generate a wave of shadow rays
                //      batched eval of bsdfs
                //      batched trace of shadow rays

                if (depth + 1 >= MinDepth && depth < MaxDepth) {
                    var value = PerformBackgroundNextEvent(scene, ray, hit, path.Rng, path.Pixel, path.Throughput, depth);
                    value += PerformNextEventEstimation(scene, ray, hit, path.Rng, path.Pixel, path.Throughput, depth);
                    scene.FrameBuffer.Splat(path.Pixel.X, path.Pixel.Y, value * path.Throughput);
                }

                // TODO batched sampling of the BSDF

                // Contine the random walk with a sample proportional to the BSDF
                var (bsdfRay, bsdfPdf, bsdfSampleWeight) = BsdfSample(scene, ray, hit, path.Rng);
                if (bsdfPdf == 0 || bsdfSampleWeight == ColorRGB.Black)
                    return null;

                path.Throughput *= bsdfSampleWeight;
                path.PreviousHit = hit;
                path.PreviousPdf = bsdfPdf;

                return bsdfRay;
            });
        }

        private ColorRGB OnBackgroundHit(Scene scene, Ray ray, Vector2 pixel, ColorRGB throughput, int depth, float previousPdf) {
            if (scene.Background == null)
                return ColorRGB.Black;

            float misWeight = 1.0f;
            if (depth > 1) {
                // Compute the balance heuristic MIS weight
                float pdfNextEvent = scene.Background.DirectionPdf(ray.Direction);
                misWeight = 1 / (1 + pdfNextEvent / previousPdf);
            }

            var emission = scene.Background.EmittedRadiance(ray.Direction);
            RegisterSample(pixel, emission * throughput, misWeight, depth, false);
            return misWeight * emission;
        }

        private ColorRGB OnLightHit(Scene scene, Ray ray, Vector2 pixel, ColorRGB throughput, int depth,
                                    SurfacePoint? previousHit, float previousPdf, SurfacePoint hit, Emitter light) {
            float misWeight = 1.0f;
            if (depth > 1) { // directly visible emitters are not explicitely connected
                             // Compute the surface area PDFs.
                var jacobian = SampleWrap.SurfaceAreaToSolidAngle(previousHit.Value, hit);
                float pdfNextEvt = light.PdfArea(hit) / scene.Emitters.Count;
                float pdfBsdf = previousPdf * jacobian;

                // Compute power heuristic MIS weights
                float pdfRatio = pdfNextEvt / pdfBsdf;
                misWeight = 1 / (pdfRatio * pdfRatio + 1);
            }

            var emission = light.EmittedRadiance(hit, -ray.Direction);
            RegisterSample(pixel, emission * throughput, misWeight, depth, false);
            return misWeight * emission;
        }

        private ColorRGB PerformBackgroundNextEvent(Scene scene, Ray ray, SurfacePoint hit, RNG rng,
                                                    Vector2 pixel, ColorRGB throughput, int depth) {
            if (scene.Background == null)
                return ColorRGB.Black; // There is no background

            var sample = scene.Background.SampleDirection(rng.NextFloat2D());
            if (scene.Raytracer.LeavesScene(hit, sample.Direction)) {
                var bsdfTimesCosine = hit.Material.EvaluateWithCosine(hit, -ray.Direction, sample.Direction, false);
                var (pdfBsdf, _)= hit.Material.Pdf(hit, -ray.Direction, sample.Direction, false);

                // Since the densities are in solid angle unit, no need for any conversions here
                float misWeight = 1 / (1.0f + pdfBsdf / (sample.Pdf));

                var contrib = sample.Weight * bsdfTimesCosine;
                RegisterSample(pixel, contrib * throughput, misWeight, depth + 1, true);
                return misWeight * contrib;
            }

            // The background is occluded
            return ColorRGB.Black;
        }

        private ColorRGB PerformNextEventEstimation(Scene scene, Ray ray, SurfacePoint hit, RNG rng, Vector2 pixel,
                                                    ColorRGB throughput, int depth) {
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
                float pdfNextEvt = lightSample.pdf * lightSelectProb;
                float pdfBsdfSolidAngle = hit.Material.Pdf(hit, -ray.Direction, -lightToSurface, false).Item1;
                float pdfBsdf = pdfBsdfSolidAngle * jacobian;

                // Compute the resulting power heuristic weights
                float pdfRatio = pdfBsdf / pdfNextEvt;
                float misWeight = 1.0f / (pdfRatio * pdfRatio + 1);

                // Compute the final sample weight, account for the change of variables from light source area
                // to the hemisphere about the shading point.

                var contrib = emission * bsdfCos * (jacobian / lightSample.pdf / lightSelectProb);
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