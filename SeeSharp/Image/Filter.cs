using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SimpleImageIO;

namespace SeeSharp.Image {
    public abstract class Filter {
        public abstract void Apply(ImageBase original, ImageBase target);
    }

    public class BoxFilter {
        public BoxFilter(int radius) {
            this.radius = radius;
        }

        protected virtual void Kernel(ImageBase original, ImageBase target, int centerRow, int centerCol) {
            int top = Math.Max(0, centerRow - radius);
            int bottom = Math.Min(original.Height - 1, centerRow + radius);
            int left = Math.Max(0, centerCol - radius);
            int right = Math.Min(original.Width - 1, centerCol + radius);

            int area = (bottom - top + 1) * (right - left + 1);
            float normalization = 1.0f / area;

            for (int chan = 0; chan < original.NumChannels; ++chan) {
                float result = 0;
                for (int row = top; row <= bottom; ++row)
                for (int col = left; col <= right; ++col)
                    result += original.GetPixelChannel(col, row, chan) * normalization;
                target.SetPixelChannel(centerCol, centerRow, chan, result);
            }
        }

        public void Apply(ImageBase original, ImageBase target) {
            Debug.Assert(target.NumChannels == original.NumChannels);
            Debug.Assert(target.Width == original.Width);
            Debug.Assert(target.Height == original.Height);

            Parallel.For(0, original.Height, row => {
                for (int col = 0; col < original.Width; ++col) {
                    Kernel(original, target, row, col);
                }
            });
        }

        int radius;
    }
}