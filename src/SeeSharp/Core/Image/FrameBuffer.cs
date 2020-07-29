using SeeSharp.Core.Shading;
using System;

namespace SeeSharp.Core.Image {
    public class FrameBuffer {
        public int Width => Image.Width;
        public int Height => Image.Height;
        public Image<ColorRGB> Image;
        public int CurIteration = 0; // 1-based index of the current iteration (i.e., the total number of iterations so far)

        public string Basename {
            get {
                string dir = System.IO.Path.GetDirectoryName(filename);
                string fileBase = System.IO.Path.GetFileNameWithoutExtension(filename);
                return System.IO.Path.Combine(dir, fileBase);
            }
        }

        [Flags]
        public enum Flags {
            None = 0,
            WriteIntermediate = 1, // Write the result of each iteration into a distinct file
            WriteContinously = 2, // Continously update the rendering result after each iteration
            SendToTev = 4 // Like WriteContinously, but sends the data via a socket instead
        }

        Flags flags;
        TevIpc tevIpc;

        public FrameBuffer(int width, int height, string filename, Flags flags = Flags.None, int varianceTileSize = 4) {
            Image = new Image<ColorRGB>(width, height);
            varianceEstimator = new VarianceEstimator(varianceTileSize, width, height);
            this.filename = filename;
            this.flags = flags;

            if (flags.HasFlag(Flags.SendToTev)) {
                tevIpc = new TevIpc();
                tevIpc.CreateImage(width, height, filename);
            }
        }

        public void Splat(float x, float y, ColorRGB value) {
            Image.Splat(x, y, value / CurIteration);
            varianceEstimator.AddSample(x, y, value);
        }

        public float GetTileSecondMoment(float x, float y)
            => varianceEstimator.GetSecondMoment(x, y, CurIteration);

        public float GetTileMean(float x, float y)
            => varianceEstimator.GetMean(x, y, CurIteration);

        public float GetTileVariance(float x, float y) {
            var moment = GetTileSecondMoment(x, y);
            var mean = GetTileMean(x, y);
            return moment - mean * mean;
        }

        public void StartIteration() {
            CurIteration++;

            // Correct the division by the number of iterations from the previous iterations
            if (CurIteration > 1)
                Image.Scale((CurIteration - 1.0f) / CurIteration);
        }

        public void EndIteration() {
            if (flags.HasFlag(Flags.WriteIntermediate)) {
                string name = Basename + "-iter" + CurIteration.ToString("D3") + ".exr";
                Image<ColorRGB>.WriteToFile(Image, name);
            }

            if (flags.HasFlag(Flags.WriteContinously))
                WriteToFile();

            if (flags.HasFlag(Flags.SendToTev))
                tevIpc.UpdateImage(Image, filename);
        }

        public void Reset() {
            CurIteration = 0;
            Image.Scale(0);
        }

        public void WriteToFile() => Image<ColorRGB>.WriteToFile(Image, filename);

        string filename;
        VarianceEstimator varianceEstimator;
    }
}