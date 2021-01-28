using SeeSharp.Core;
using SeeSharp.Core.Geometry;
using SeeSharp.Core.Sampling;
using SeeSharp.Core.Shading;
using System;
using System.Numerics;
using TinyEmbree;

namespace SeeSharp.Integrators {
    public class DebugVisualizer : Integrator {
        public uint BaseSeed = 0xC030114;
        public int TotalSpp = 1;

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

        public virtual ColorRGB ComputeColor(SurfacePoint hit, Vector3 from) {
            float cosine = Math.Abs(Vector3.Dot(hit.Normal, from));
            cosine /= hit.Normal.Length();
            cosine /= from.Length();
            return ColorRGB.White * cosine;
        }

        private void RenderPixel(Scene scene, uint row, uint col, uint sampleIndex) {
            // Seed the random number generator
            uint pixelIndex = row * (uint)scene.FrameBuffer.Width + col;
            var seed = RNG.HashSeed(BaseSeed, pixelIndex, sampleIndex);
            var rng = new RNG(seed);

            // Sample a ray from the camera
            var offset = rng.NextFloat2D();
            Ray primaryRay = scene.Camera.GenerateRay(new Vector2(col, row) + offset, rng).Ray;
            var hit = scene.Raytracer.Trace(primaryRay);

            // Shade and splat
            ColorRGB value = ColorRGB.Black;
            if (hit) value = ComputeColor(hit, -primaryRay.Direction);
            scene.FrameBuffer.Splat(col, row, value);
        }
    }
}
