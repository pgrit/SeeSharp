using System;
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

        protected virtual float[] Kernel(ImageBase original, int centerRow, int centerCol) {
            int top = Math.Max(0, centerRow - radius);
            int bottom = Math.Min(original.Height - 1, centerRow + radius);
            int left = Math.Max(0, centerCol - radius);
            int right = Math.Min(original.Width - 1, centerCol + radius);

            int area = (bottom - top + 1) * (right - left + 1);
            float normalization = 1.0f / area;

            float[] result = new float[original.NumChannels];
            for (int chan = 0; chan < original.NumChannels; ++chan)
                for (int row = top; row <= bottom; ++row)
                    for (int col = left; col <= right; ++col)
                        result[chan] += original.GetPixelChannel(col, row, chan) * normalization;
            return result;
        }

        public void Apply(ImageBase original, ImageBase target) {
            Parallel.For(0, original.Height, row => {
                for (int col = 0; col < original.Width; ++col) {
                    target.SetPixelChannels(col, row, Kernel(original, row, col));
                }
            });
        }

        int radius;
    }

    public class NeighborAverage : BoxFilter {
        public NeighborAverage() : base(1) {}

        protected override float[] Kernel(ImageBase original, int centerRow, int centerCol) {
            int top = Math.Max(0, centerRow - 1);
            int bottom = Math.Min(original.Height - 1, centerRow + 1);
            int left = Math.Max(0, centerCol - 1);
            int right = Math.Min(original.Width - 1, centerCol + 1);

            int area = (bottom - top + 1) * (right - left + 1) - 1;
            float normalization = 1.0f / area;

            float[] result = new float[original.NumChannels];
            for (int chan = 0; chan < original.NumChannels; ++chan) {
                for (int row = top; row <= bottom; ++row) {
                    for (int col = left; col <= right; ++col) {
                        if (row == centerRow && col == centerCol)
                            continue;
                        result[chan] += original.GetPixelChannel(col, row, chan) * normalization;
                    }
                }
            }
            return result;
        }
    }
}