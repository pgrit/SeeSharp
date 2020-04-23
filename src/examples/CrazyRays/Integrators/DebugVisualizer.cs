using GroundWrapper;
using GroundWrapper.Geometry;
using GroundWrapper.Sampling;
using GroundWrapper.Shading;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Integrators {
    public class DebugVisualizer : Integrator {
        public uint BaseSeed = 0xC030114;
        public int TotalSpp = 1;

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

                var hit = scene.Raytracer.Trace(primaryRay);
                ColorRGB value = ColorRGB.Black;
                if (hit) {
                    value = ColorRGB.White * Math.Abs(Vector3.Dot(hit.normal, primaryRay.direction) 
                        / hit.normal.Length() / primaryRay.direction.Length());
                }

                value = value * (1.0f / TotalSpp);

                // TODO we do nearest neighbor splatting manually here, to avoid numerical
                //      issues if the primary samples are almost 1 (400 + 0.99999999f = 401)
                scene.FrameBuffer.Splat(col, row, value);
            }
        }
    }
}
