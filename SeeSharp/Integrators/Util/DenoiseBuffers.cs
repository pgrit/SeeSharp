using SimpleImageIO;
using System.Numerics;
using SeeSharp.Image;

namespace SeeSharp.Integrators.Util {
    /// <summary>
    /// Convenience wrapper to add common feature buffers to a frame buffer that are useful for denoising.
    /// </summary>
    public class DenoiseBuffers {
        /// <summary>
        /// Adds the required layers to a frame buffer and tracks there references in this object
        /// </summary>
        public DenoiseBuffers(FrameBuffer frameBuffer) {
            frameBuffer.AddLayer("albedo", albedo);
            frameBuffer.AddLayer("normal", normal);
            frameBuffer.AddLayer("denoised", denoised);
            this.frameBuffer = frameBuffer;
        }

        /// <summary>
        /// Logs the features at a primary hit point
        /// </summary>
        public void LogPrimaryHit(Vector2 pixel, RgbColor albedo, Vector3 normal) {
            this.albedo.Splat(pixel.X, pixel.Y, albedo);
            this.normal.Splat(pixel.X, pixel.Y, normal);
        }

        /// <summary>
        /// Runs the denoiser on the current rendered image. The result is stored in the "denoised" layer.
        /// </summary>
        public void Denoise() {
            ImageBase.Move(
                denoiser.Denoise(frameBuffer.Image, (RgbImage)albedo.Image, (RgbImage)normal.Image),
                denoised.Image);
        }

        RgbLayer albedo = new();
        RgbLayer normal = new();
        RgbLayer denoised = new();
        FrameBuffer frameBuffer;
        Denoiser denoiser = new();
    }

}