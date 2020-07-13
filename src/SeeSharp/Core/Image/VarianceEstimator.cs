using SeeSharp.Core.Shading;
using System;

namespace SeeSharp.Core.Image {
    public class VarianceEstimator {
        public VarianceEstimator(int tileSize, int width, int height) {
            this.width = width;
            this.height = height;
            this.tileSize = tileSize;
            this.tileMoments = new Image<Scalar>(width / tileSize, height / tileSize);
            this.tileMeans = new Image<Scalar>(width / tileSize, height / tileSize);
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
            var moment = tileMoments[col, row].Value / numSamples;
            var mean = tileMeans[col, row].Value / numSamples;
            return moment - mean * mean;
        }

        public float GetSecondMoment(float x, float y, int numSamples) {
            var (col, row) = ComputeTile(x, y);
            var moment = tileMoments[col, row].Value / numSamples;
            return moment;
        }

        public float GetMean(float x, float y, int numSamples) {
            var (col, row) = ComputeTile(x, y);
            var mean = tileMeans[col, row].Value / numSamples;
            return mean;
        }

        public Scalar MakeScalar(ColorRGB value) => new Scalar { Value = (value.R + value.G + value.B) / 3 };

        int width, height;
        int tileSize;
        Image<Scalar> tileMoments;
        Image<Scalar> tileMeans;
    }
}