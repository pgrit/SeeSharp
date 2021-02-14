using SimpleImageIO;
using System.Numerics;
using SeeSharp.Image;

namespace SeeSharp.Integrators.Util {
    /// <summary>
    /// Convenience wrapper to add common feature buffers to a frame buffer that are useful for denoising.
    /// </summary>
    public class FeatureLogger {
        public FeatureLogger(FrameBuffer frameBuffer) {
            frameBuffer.AddLayer("albedo", albedo);
            frameBuffer.AddLayer("depth", depth);
            frameBuffer.AddLayer("normal", normal);
        }

        public void LogPrimaryHit(Vector2 pixel, RgbColor albedo, Vector3 normal, float depth) {
            this.albedo.Splat(pixel.X, pixel.Y, albedo);
            this.normal.Splat(pixel.X, pixel.Y, normal);
            this.depth.Splat(pixel.X, pixel.Y, depth);
        }

        FrameBuffer.RgbLayer albedo = new();
        FrameBuffer.RgbLayer normal = new();
        FrameBuffer.MonoLayer depth = new();
    }

}