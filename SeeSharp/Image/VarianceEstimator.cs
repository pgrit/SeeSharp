using SeeSharp.Shading;
using System;
using System.Diagnostics;

namespace SeeSharp.Image {
    public class VarianceEstimator {
        public VarianceEstimator(int tileSize, int width, int height) {
            this.width = width;
            this.height = height;
            this.tileSize = tileSize;
            this.tileMoments = new Image<Scalar>(Math.Max(1, width / tileSize), Math.Max(1, height / tileSize));
            this.tileMeans = new Image<Scalar>(Math.Max(1, width / tileSize), Math.Max(1, height / tileSize));
        }

        public void AddSample(float x, float y, ColorRGB value) {
            var (col, row) = ComputeTile(x, y);

            Scalar s = MakeScalar(value);
            tileMeans.Splat(col, row, s);
            tileMoments.Splat(col, row, s * s);
        }

        public (int, int) ComputeTile(float x, float y) {
            int col = (int)x / tileSize;
            col = Math.Min(Math.Max(col, 0), tileMoments.Width - 1);

            int row = (int)y / tileSize;
            row = Math.Min(Math.Max(row, 0), tileMoments.Height - 1);

            return (col, row);
        }

        public float GetVariance(float x, float y, int numSamples) {
            var (col, row) = ComputeTile(x, y);
            int n = numSamples * tileSize * tileSize;
            var moment = tileMoments[col, row].Value;
            var mean = tileMeans[col, row].Value;
            var variance = (moment - mean * mean / n) / (n - 1);
            return variance;
        }

        public float GetSecondMoment(float x, float y, int numSamples) {
            var (col, row) = ComputeTile(x, y);
            var moment = tileMoments[col, row].Value / (numSamples * tileSize * tileSize);
            return moment;
        }

        public float GetMean(float x, float y, int numSamples) {
            var (col, row) = ComputeTile(x, y);
            var mean = tileMeans[col, row].Value / (numSamples * tileSize * tileSize);
            return mean;
        }

        public void Combine(float x, float y, VarianceEstimator other, float weightOther) {
            Debug.Assert(other.width == width);
            Debug.Assert(other.height == height);

            var (col, row) = ComputeTile(x, y);
            tileMoments[col, row] = new Scalar(weightOther * other.tileMoments[col, row].Value
                + (1 - weightOther) * tileMoments[col, row].Value);
            tileMeans[col, row] = new Scalar(weightOther * other.tileMeans[col, row].Value
                + (1 - weightOther) * tileMeans[col, row].Value);
        }

        public void Reset() {
            tileMeans.Scale(0);
            tileMoments.Scale(0);
        }

        public Scalar MakeScalar(ColorRGB value)
        => new Scalar { Value = (value.R + value.G + value.B) / 3 };

        int width, height;
        int tileSize;
        Image<Scalar> tileMoments;
        Image<Scalar> tileMeans;
    }
}