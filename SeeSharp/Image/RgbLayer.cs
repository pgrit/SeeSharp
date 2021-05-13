using SimpleImageIO;

namespace SeeSharp.Image {
    /// <summary>
    /// Convenience layer for images storing RGB values
    /// </summary>
    public class RgbLayer : Layer {
        /// <summary>
        /// Called once before the first rendering iteration
        /// </summary>
        /// <param name="width">The width of the frame buffer</param>
        /// <param name="height">The height of the frame buffer</param>
        public override void Init(int width, int height) => Image = new RgbImage(width, height);

        /// <summary>
        /// Adds a new sample contribution to the layer
        /// </summary>
        public virtual void Splat(float x, float y, RgbColor value)
        => (Image as RgbImage).AtomicAdd((int)x, (int)y, value / curIteration);
    }
}