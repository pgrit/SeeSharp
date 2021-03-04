using SimpleImageIO;
using System.Numerics;

namespace SeeSharp.Image {
    public class ImageTexture {
        public enum BorderHandling {
            Repeat, Clamp
        }

        public BorderHandling Border = BorderHandling.Repeat;

        (int, int) IndexOf(float x, float y) {
            int row = (int)y;
            int col = (int)x;
            return ApplyBorderHandling(col, row);
        }

        (int, int) ApplyBorderHandling(int col, int row) {
            if (Border == BorderHandling.Repeat) {
                row = (row % image.Height + image.Height) % image.Height;
                col = (col % image.Width + image.Width) % image.Width;
            } else if (Border == BorderHandling.Clamp) {
                row = System.Math.Clamp(row, 0, image.Height - 1);
                col = System.Math.Clamp(col, 0, image.Width - 1);
            }

            return (col, row);
        }

        /// <returns> The (x,y) / (col,row) coordinate of the texel. </returns>
        public (int, int) ComputeTexel(Vector2 uv) {
            int col = (int)(uv.X * image.Width);
            int row = (int)(uv.Y * image.Height);
            return ApplyBorderHandling(col, row);
        }

        protected ImageBase image;
    }

    public class TextureRgb : ImageTexture {
        public TextureRgb(RgbColor color) => constColor = color;
        public TextureRgb(string filename) => image = new RgbImage(filename);

        public RgbColor Lookup(Vector2 uv) {
            if (image == null)
                return constColor;

            (int col, int row) = ComputeTexel(uv);
            return (image as RgbImage).GetPixel(col, row);
        }

        RgbColor constColor;
    }

    public class TextureMono : ImageTexture {
        public TextureMono(float color) => constColor = color;
        public TextureMono(MonochromeImage img) => image = img;

        // TODO not yet supported in SimpleImageIO
        // public TextureMono(string filename) => image = new MonochromeImage(filename);

        public float Lookup(Vector2 uv) {
            if (image == null)
                return constColor;

            (int col, int row) = ComputeTexel(uv);
            return (image as MonochromeImage).GetPixel(col, row);
        }

        float constColor;
    }
}