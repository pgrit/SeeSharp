using SimpleImageIO;
using System.Numerics;
using SeeSharp.Image;

namespace SeeSharp.Integrators.Util {
    /// <summary>
    /// Convenience wrapper to add common feature buffers to a frame buffer that are useful for denoising.
    /// </summary>
    public class DenoiseBuffers {
        public DenoiseBuffers(FrameBuffer frameBuffer) {
            frameBuffer.AddLayer("albedo", albedo);
            frameBuffer.AddLayer("normal", normal);
            frameBuffer.AddLayer("denoised", denoised);
            this.frameBuffer = frameBuffer;
        }

        public void LogPrimaryHit(Vector2 pixel, RgbColor albedo, Vector3 normal) {
            this.albedo.Splat(pixel.X, pixel.Y, albedo);
            this.normal.Splat(pixel.X, pixel.Y, normal);
        }

        public void Denoise() {
            ImageBase.Move(
                denoiser.Denoise(frameBuffer.Image, (RgbImage)albedo.Image, (RgbImage)normal.Image),
                denoised.Image);
        }

        FrameBuffer.RgbLayer albedo = new();
        FrameBuffer.RgbLayer normal = new();
        FrameBuffer.MonoLayer depth = new();
        FrameBuffer.RgbLayer denoised = new();
        FrameBuffer frameBuffer;
        Denoiser denoiser = new();
    }

}