using System;
using SeeSharp.Core.Shading;

namespace SeeSharp.Core.Image {
    public abstract class Filter {
        public abstract void Apply(Image<ColorRGB> original, Image<ColorRGB> target);
        public abstract void Apply(Image<Scalar> original, Image<Scalar> target);
    }

    public class BoxFilter {
        public BoxFilter(int radius) {
            this.radius = radius;
        }

        ColorRGB Kernel(Image<ColorRGB> original, int centerRow, int centerCol) {
            int top = Math.Max(0, centerRow - radius);
            int bottom = Math.Min(original.Height, centerRow + radius);
            int left = Math.Max(0, centerCol - radius);
            int right = Math.Min(original.Width, centerCol + radius);

            int area = (bottom - top) * (right - left);
            float normalization = 1.0f / area;

            ColorRGB result = ColorRGB.Black;
            for (int row = top; row <= bottom; ++row) {
                for (int col = left; col <= right; ++col) {
                    result += original[col, row] * normalization;
                }
            }
            return result;
        }

        Scalar Kernel(Image<Scalar> original, int centerRow, int centerCol) {
            int top = Math.Max(0, centerRow - radius);
            int bottom = Math.Min(original.Height, centerRow + radius);
            int left = Math.Max(0, centerCol - radius);
            int right = Math.Min(original.Width, centerCol + radius);

            int area = (bottom - top + 1) * (right - left + 1);
            float normalization = 1.0f / area;

            Scalar result = new Scalar(0);
            for (int row = top; row <= bottom; ++row) {
                for (int col = left; col <= right; ++col) {
                    result += original[col, row] * normalization;
                }
            }
            return result;
        }

        public void Apply(Image<ColorRGB> original, Image<ColorRGB> target) {
            System.Threading.Tasks.Parallel.For(0, original.Height, row => {
                for (int col = 0; col < original.Width; ++col) {
                    target[col, row] = Kernel(original, row, col);
                }
            });
        }

        public void Apply(Image<Scalar> original, Image<Scalar> target) {
            System.Threading.Tasks.Parallel.For(0, original.Height, row => {
                for (int col = 0; col < original.Width; ++col) {
                    target[col, row] = Kernel(original, row, col);
                }
            });
        }

        int radius;
    }
}