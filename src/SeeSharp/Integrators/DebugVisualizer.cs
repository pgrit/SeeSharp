using SeeSharp.Core;
using SeeSharp.Core.Geometry;
using SeeSharp.Core.Sampling;
using SeeSharp.Core.Shading;
using System;
using System.Numerics;

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

        private void RenderPixel(Scene scene, uint row, uint col, uint sampleIndex) {
            // Seed the random number generator
            uint pixelIndex = row * (uint)scene.FrameBuffer.Width + col;
            var seed = RNG.HashSeed(BaseSeed, pixelIndex, sampleIndex);
            var rng = new RNG(seed);

            // Sample a ray from the camera
            var offset = rng.NextFloat2D();
            Ray primaryRay = scene.Camera.GenerateRay(new Vector2(col, row) + offset);

            var hit = scene.Raytracer.Trace(primaryRay);
            ColorRGB value = ColorRGB.Black;
            if (hit) {
                float cosine = Math.Abs(Vector3.Dot(hit.normal, primaryRay.Direction));
                cosine /= hit.normal.Length();
                cosine /= primaryRay.Direction.Length();
                value = ColorRGB.White * cosine;
            }

            // TODO we do nearest neighbor splatting manually here, to avoid numerical
            //      issues if the primary samples are almost 1 (400 + 0.99999999f = 401)
            scene.FrameBuffer.Splat(col, row, value);
        }
    }
}
