using SeeSharp.Geometry;
using SeeSharp.Sampling;
using SimpleImageIO;
using System;
using System.Numerics;
using System.Threading.Tasks;
using TinyEmbree;

namespace SeeSharp.Integrators.Bidir {
    public class PhotonMapper : Integrator {
        public int NumIterations = 2;
        public int NumLightPaths = 0;
        public int MaxDepth = 10;
        public uint BaseSeedLight = 0xC030114u;
        public uint BaseSeedCamera = 0x13C0FEFEu;

        protected Scene scene;
        protected LightPathCache lightPaths;

        PhotonHashGrid photonMap = new PhotonHashGrid();

        public override void Render(Scene scene) {
            this.scene = scene;

            if (NumLightPaths <= 0) {
                NumLightPaths = scene.FrameBuffer.Width * scene.FrameBuffer.Height;
            }

            lightPaths = new LightPathCache { MaxDepth = MaxDepth, NumPaths = NumLightPaths, Scene = scene };

            for (uint iter = 0; iter < NumIterations; ++iter) {
                scene.FrameBuffer.StartIteration();
                lightPaths.TraceAllPaths(iter, null);
                ProcessPathCache();
                TraceAllCameraPaths(iter);
                scene.FrameBuffer.EndIteration();
            }
        }

        public virtual void ProcessPathCache() {
            photonMap.Build(lightPaths, scene.Radius / 100);
        }

        RgbColor Merge(float radius, SurfacePoint hit, Vector3 outDir, int pathIdx, int vertIdx, float distSqr) {
            // Compute the contribution of the photon
            var photon = lightPaths.PathCache[pathIdx, vertIdx];
            var ancestor = lightPaths.PathCache[pathIdx, photon.AncestorId];
            var dirToAncestor = ancestor.Point.Position - photon.Point.Position;
            var bsdfValue = photon.Point.Material.Evaluate(hit, outDir, dirToAncestor, false);
            var photonContrib = photon.Weight * bsdfValue / NumLightPaths;

            // Epanechnikov kernel
            float radiusSquared = radius * radius;
            photonContrib *= (radiusSquared - distSqr) * 2.0f / (radiusSquared * radiusSquared * MathF.PI);

            return photonContrib;
        }
        
        public virtual RgbColor EstimatePixelValue(SurfacePoint cameraPoint, Vector2 filmPosition, Ray primaryRay,
                                                   float pdfFromCamera, RgbColor initialWeight, RNG rng) {
            // Trace the primary ray into the scene
            var hit = scene.Raytracer.Trace(primaryRay);
            if (!hit)
                return scene.Background?.EmittedRadiance(primaryRay.Direction) ?? RgbColor.Black;

            // Gather nearby photons
            float radius = scene.Radius / 100.0f;
            RgbColor estimate = photonMap.Accumulate(radius, hit, -primaryRay.Direction, Merge, radius);

            // Add contribution from directly visible light sources
            var light = scene.QueryEmitter(hit);
            if (light != null) {
                estimate += light.EmittedRadiance(hit, -primaryRay.Direction);
            }

            return estimate;
        }

        private void RenderPixel(uint row, uint col, RNG rng) {
            // Sample a ray from the camera
            var offset = rng.NextFloat2D();
            var filmSample = new Vector2(col, row) + offset;
            var cameraRay = scene.Camera.GenerateRay(filmSample, rng);
            var value = EstimatePixelValue(cameraRay.Point, filmSample, cameraRay.Ray,
                                           cameraRay.PdfRay, cameraRay.Weight, rng);

            // TODO we do nearest neighbor splatting manually here, to avoid numerical
            //      issues if the primary samples are almost 1 (400 + 0.99999999f = 401)
            scene.FrameBuffer.Splat((float)col, (float)row, value);
        }

        private void TraceAllCameraPaths(uint iter) {
            Parallel.For(0, scene.FrameBuffer.Height,
                row => {
                    var seed = RNG.HashSeed(BaseSeedCamera, (uint)row, iter);
                    var rng = new RNG(seed);
                    for (uint col = 0; col < scene.FrameBuffer.Width; ++col) {
                        RenderPixel((uint)row, col, rng);
                    }
                }
            );
        }
    }
}
