using System;
using System.Numerics;
using System.Threading.Tasks;
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
        public bool RenderTechniquePyramid = true;

        Scene scene;
        TechPyramid techPyramidRaw;
        TechPyramid techPyramidWeighted;

        struct PathPayload {
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
            var payloads = new PathPayload[numPixels];

            if (RenderTechniquePyramid) {
                techPyramidRaw = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                    (int)MinDepth, (int)MaxDepth, false, false, false);
                techPyramidWeighted = new TechPyramid(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                    (int)MinDepth, (int)MaxDepth, false, false, false);
            }

            for (int sampleIndex = 0; sampleIndex < TotalSpp; ++sampleIndex) {
                scene.FrameBuffer.StartIteration();
                LaunchWave(wavefront, payloads, sampleIndex);
                for (int d = 1; d <= MaxDepth; ++d) {
                    wavefront.Intersect();
                    ShadeWave(wavefront, payloads, d);
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

        void LaunchWave(Wavefront wave, PathPayload[] weights, int sampleIndex) {
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

        void ShadeWave(Wavefront wave, PathPayload[] payloads, int depth) {
            wave.Process((idx, ray, hit) => {
                ref PathPayload path = ref payloads[idx];

                // Did the ray leave the scene?
                if (!hit) {
                    OnBackgroundHit(ray, path.Pixel, path.Throughput, depth, path.PreviousPdf);
                    return false;
                }

                // Check if a light source was hit.
                Emitter light = scene.QueryEmitter(hit);
                if (light != null && depth >= MinDepth) {
                    var value = OnLightHit(ray, path.Pixel, path.Throughput, depth, path.PreviousHit, path.PreviousPdf, hit, light);
                    scene.FrameBuffer.Splat(path.Pixel.X, path.Pixel.Y, value * path.Throughput);
                }

                return true;
            });

            if (depth + 1 >= MinDepth && depth < MaxDepth) {
                if (scene.Background != null)
                    NextEventWave(PerformBackgroundNextEvent, wave, payloads, depth);
                if (scene.Emitters.Count > 0)
                    NextEventWave(PerformNextEventEstimation, wave, payloads, depth);
            }

            ContinueWave(wave, payloads);
        }

        private void ContinueWave(Wavefront wave, PathPayload[] payloads) {
            var bsdfQueries = new BsdfQuery[wave.Size];
            wave.Process((idx, ray, hit) => {
                bsdfQueries[idx].Hit = hit;
                bsdfQueries[idx].OutDir = -ray.Direction;
                bsdfQueries[idx].IsActive = true;
                bsdfQueries[idx].IsOnLightSubpath = false;
                bsdfQueries[idx].Rng = payloads[idx].Rng;
                return true;
            });

            var weights = new ColorRGB[wave.Size];
            var pdfs = new float[wave.Size];
            var directions = new Vector3[wave.Size];
            BsdfQuery.Sample(bsdfQueries, weights, pdfs, directions);

            wave.ContinuePaths((idx, ray, hit) => {
                if (pdfs[idx] == 0 || weights[idx] == ColorRGB.Black)
                    return null;

                payloads[idx].Throughput *= weights[idx];
                payloads[idx].PreviousHit = hit;
                payloads[idx].PreviousPdf = pdfs[idx];

                return scene.Raytracer.SpawnRay(hit, directions[idx]);
            });
        }

        private ColorRGB OnBackgroundHit(Ray ray, Vector2 pixel, ColorRGB throughput, int depth, float previousPdf) {
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

        private ColorRGB OnLightHit(Ray ray, Vector2 pixel, ColorRGB throughput, int depth,
                                    SurfacePoint? previousHit, float previousPdf, SurfacePoint hit, Emitter light) {
            float misWeight = 1.0f;
            if (depth > 1) { // directly visible emitters are not explicitely connected
                             // Compute the surface area PDFs.
                var jacobian = SampleWrap.SurfaceAreaToSolidAngle(previousHit.Value, hit);
                float pdfNextEvt = light.PdfArea(hit) / scene.Emitters.Count;
                float pdfBsdf = previousPdf * jacobian;

                // Compute balance heuristic MIS weights
                float pdfRatio = pdfNextEvt / pdfBsdf;
                misWeight = 1 / (pdfRatio + 1);
            }

            var emission = light.EmittedRadiance(hit, -ray.Direction);
            RegisterSample(pixel, emission * throughput, misWeight, depth, false);
            return misWeight * emission;
        }

        struct NextEventQuery {
            public BsdfQuery Bsdf;
            public ShadowRay Visibility;
            public ColorRGB Weight;
            public float PdfNextEventSolidAngle;
        }

        delegate NextEventQuery NextEventGenerator(Ray ray, SurfacePoint hit, RNG rng, Vector2 pixel,
                                                   ColorRGB throughput, int depth);
        void NextEventWave(NextEventGenerator generator, Wavefront wave, PathPayload[] payloads, int depth) {
            var bsdfQueries = new BsdfQuery[wave.Size];
            var shadowRays = new ShadowRay[wave.Size];
            var weights = new ColorRGB[wave.Size];
            var pdfs = new float[wave.Size];
            wave.Process((idx, ray, hit) => {
                ref PathPayload path = ref payloads[idx];
                var q = generator(ray, hit, path.Rng, path.Pixel, path.Throughput, depth);
                bsdfQueries[idx] = q.Bsdf;
                shadowRays[idx] = q.Visibility;
                weights[idx] = q.Weight;
                pdfs[idx] = q.PdfNextEventSolidAngle;
                return true;
            });

            var occluded = new bool[wave.Size];
            scene.Raytracer.IsOccluded(shadowRays, occluded);

            var bsdfValues = new ColorRGB[wave.Size];
            var bsdfPdfs = new float[wave.Size];
            BsdfQuery.Evaluate(bsdfQueries, bsdfValues);
            BsdfQuery.ComputePdfs(bsdfQueries, bsdfPdfs);

            // Compute the final sample weight and MIS heuristic value
            Parallel.For(0, wave.Size, idx => {
                if (!occluded[idx] && bsdfQueries[idx].IsActive) {
                    var weight = weights[idx] * bsdfValues[idx] * payloads[idx].Throughput;
                    float misWeight = 1 / (1 + bsdfPdfs[idx] / pdfs[idx]);
                    RegisterSample(payloads[idx].Pixel, weight, misWeight, depth + 1, true);
                    scene.FrameBuffer.Splat(payloads[idx].Pixel.X, payloads[idx].Pixel.Y, misWeight * weight);
                }
            });
        }

        private NextEventQuery PerformBackgroundNextEvent(Ray ray, SurfacePoint hit, RNG rng,
                                                          Vector2 pixel, ColorRGB throughput, int depth) {
            var sample = scene.Background.SampleDirection(rng.NextFloat2D());

            var bsdfQuery = new BsdfQuery {
                OutDir = -ray.Direction,
                InDir = sample.Direction,
                Hit = hit,
                IsOnLightSubpath = false,
                IsActive = true,
                Rng = rng
            };

            return new NextEventQuery {
                Bsdf = bsdfQuery,
                Weight = sample.Weight,
                PdfNextEventSolidAngle = sample.Pdf,
                Visibility = scene.Raytracer.MakeBackgroundShadowRay(hit, sample.Direction)
            };
        }

        private NextEventQuery PerformNextEventEstimation(Ray ray, SurfacePoint hit, RNG rng, Vector2 pixel,
                                                          ColorRGB throughput, int depth) {
            // Select a light source
            int idx = rng.NextInt(0, scene.Emitters.Count);
            var light = scene.Emitters[idx];
            float lightSelectProb = 1.0f / scene.Emitters.Count;

            // Sample a point on the light source
            var lightSample = light.SampleArea(rng.NextFloat2D());
            Vector3 lightToSurface = hit.Position - lightSample.point.Position;
            var emission = light.EmittedRadiance(lightSample.point, lightToSurface);

            // Compute the jacobian for surface area -> solid angle
            float jacobian = SampleWrap.SurfaceAreaToSolidAngle(hit, lightSample.point);
            float pdfNextEvtSolidAngle = lightSample.pdf * lightSelectProb / jacobian;

            var bsdfQuery = new BsdfQuery {
                OutDir = -ray.Direction,
                InDir = -lightToSurface,
                Hit = hit,
                IsOnLightSubpath = false,
                IsActive = true,
                Rng = rng
            };

            return new NextEventQuery {
                Bsdf = bsdfQuery,
                Weight = emission / pdfNextEvtSolidAngle,
                PdfNextEventSolidAngle = pdfNextEvtSolidAngle,
                Visibility = scene.Raytracer.MakeShadowRay(hit, lightSample.point)
            };
        }
    }
}