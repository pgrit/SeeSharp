using SimpleImageIO;
using System.Numerics;

namespace SeeSharp.Image {
    /// <summary>
    /// An RGB image texture
    /// </summary>
    public class TextureRgb : ImageTexture {
        /// <summary>
        /// Creates a single-pixel image texture with a constant color
        /// </summary>
        public TextureRgb(RgbColor color) => constColor = color;

        /// <summary>
        /// Creates a texture from an RGB image
        /// </summary>
        /// <param name="filename">Full path to the RGB image</param>
        public TextureRgb(string filename) => image = new RgbImage(filename);

        /// <returns>Color value at the given uv-coordinates</returns>
        public RgbColor Lookup(Vector2 uv) {
            if (image == null)
                return constColor;

            (int col, int row) = ComputeTexel(uv);
            return (image as RgbImage).GetPixel(col, row);
        }

        RgbColor constColor;
    }
}