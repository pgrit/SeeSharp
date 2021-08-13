using SeeSharp.Geometry;
using SeeSharp.Sampling;
using SimpleImageIO;
using System;
using System.Numerics;
using System.Threading.Tasks;
using TinyEmbree;

namespace SeeSharp.Integrators.Bidir {
    /// <summary>
    /// A pure photon mapper in its most naive form: merging at the first camera vertex with a fixed radius
    /// computed from a fraction of the scene size.
    /// </summary>
    public class PhotonMapper : Integrator {
        /// <summary>
        /// Number of iterations to render.
        /// </summary>
        public int NumIterations = 2;

        /// <summary>
        /// Number of light paths in each iteration.
        /// </summary>
        public int NumLightPaths = 0;

        /// <summary>
        /// Seed for the random samples used to generate the photons
        /// </summary>
        public uint BaseSeedLight = 0xC030114u;

        /// <summary>
        /// Seed for the random samples used to generate the camera rays
        /// </summary>
        public uint BaseSeedCamera = 0x13C0FEFEu;

        /// <summary>
        /// The scene that is currently rendered
        /// </summary>
        protected Scene scene;

        /// <summary>
        /// Generates and stores the light paths / photons
        /// </summary>
        protected LightPathCache lightPaths;

        readonly PhotonHashGrid photonMap = new();

        /// <inheritdoc />
        public override void Render(Scene scene) {
            this.scene = scene;

            if (NumLightPaths <= 0) {
                NumLightPaths = scene.FrameBuffer.Width * scene.FrameBuffer.Height;
            }

            lightPaths = new LightPathCache {
                MaxDepth = MaxDepth,
                NumPaths = NumLightPaths,
                Scene = scene,
                BaseSeed = BaseSeedLight
            };

            for (uint iter = 0; iter < NumIterations; ++iter) {
                scene.FrameBuffer.StartIteration();
                lightPaths.TraceAllPaths(iter, null);
                ProcessPathCache();
                TraceAllCameraPaths(iter);
                scene.FrameBuffer.EndIteration();
            }
        }

        /// <summary>
        /// Builds the photon map from the cached light paths
        /// </summary>
        protected virtual void ProcessPathCache() {
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

        /// <summary>
        /// Computes the estimated radiance travelling along a sampled camera ray
        /// </summary>
        /// <param name="pixel">Position on the image plane</param>
        /// <param name="ray">Ray sampled from the camera</param>
        /// <param name="weight">Contribution of the ray to the image, multiplied with the radiance</param>
        /// <param name="rng">Random number generator</param>
        /// <returns>Pixel value estimate</returns>
        protected virtual RgbColor EstimatePixelValue(Vector2 pixel, Ray ray, RgbColor weight, RNG rng) {
            // Trace the primary ray into the scene
            var hit = scene.Raytracer.Trace(ray);
            if (!hit)
                return scene.Background?.EmittedRadiance(ray.Direction) ?? RgbColor.Black;

            // Gather nearby photons
            float radius = scene.Radius / 100.0f;
            RgbColor estimate = photonMap.Accumulate(radius, hit, -ray.Direction, Merge, radius);

            // Add contribution from directly visible light sources
            var light = scene.QueryEmitter(hit);
            if (light != null) {
                estimate += light.EmittedRadiance(hit, -ray.Direction);
            }

            return estimate;
        }

        private void RenderPixel(uint row, uint col, RNG rng) {
            // Sample a ray from the camera
            var offset = rng.NextFloat2D();
            var filmSample = new Vector2(col, row) + offset;
            var cameraRay = scene.Camera.GenerateRay(filmSample, rng);
            var value = EstimatePixelValue(filmSample, cameraRay.Ray, cameraRay.Weight, rng);
            scene.FrameBuffer.Splat(col, row, value);
        }

        private void TraceAllCameraPaths(uint iter) {
            Parallel.For(0, scene.FrameBuffer.Height,
                row => {
                    var rng = new RNG(BaseSeedCamera, (uint)row, iter);
                    for (uint col = 0; col < scene.FrameBuffer.Width; ++col) {
                        RenderPixel((uint)row, col, rng);
                    }
                }
            );
        }
    }
}
