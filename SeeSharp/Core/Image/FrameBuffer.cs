using SeeSharp.Core.Shading;
using System;

namespace SeeSharp.Core.Image {
    public class FrameBuffer {
        public int Width => Image.Width;
        public int Height => Image.Height;
        public Image<ColorRGB> Image;

        /// <summary>
        /// 1-based index of the current iteration (i.e., the total number of iterations so far)
        /// </summary>
        public int CurIteration = 0;

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

        public FrameBuffer(int width, int height, string filename, Flags flags = Flags.None,
                           int varianceTileSize = 4) {
            Image = new Image<ColorRGB>(width, height);
            varianceEstimator = new VarianceEstimator(varianceTileSize, width, height);
            this.filename = filename;
            this.flags = flags;

            if (flags.HasFlag(Flags.SendToTev)) {
                tevIpc = new TevIpc();
                // If a file with the same name is open, close it to avoid conflicts
                tevIpc.CloseImage(filename);
                tevIpc.CreateImage(width, height, filename);
            }
        }

        public virtual void Splat(float x, float y, ColorRGB value) {
            Image.Splat(x, y, value / CurIteration);
            varianceEstimator.AddSample(x, y, value);
        }

        public virtual void StartIteration() {
            CurIteration++;

            // Correct the division by the number of iterations from the previous iterations
            if (CurIteration > 1)
                Image.Scale((CurIteration - 1.0f) / CurIteration);

            stopwatch.Start();
        }

        public long RenderTimeMs => stopwatch.ElapsedMilliseconds;

        public virtual void EndIteration() {
            stopwatch.Stop();

            if (flags.HasFlag(Flags.WriteIntermediate)) {
                string name = Basename + "-iter" + CurIteration.ToString("D3")
                    + $"-{stopwatch.ElapsedMilliseconds}ms" + ".exr";
                Image<ColorRGB>.WriteToFile(Image, name);
            }

            if (flags.HasFlag(Flags.WriteContinously))
                WriteToFile();

            if (flags.HasFlag(Flags.SendToTev))
                tevIpc.UpdateImage(Image, filename);
        }

        public virtual void Reset() {
            CurIteration = 0;
            Image.Scale(0);
            varianceEstimator.Reset();
            stopwatch.Reset();
        }

        public void WriteToFile() => Image<ColorRGB>.WriteToFile(Image, filename);

        protected string filename;
        protected VarianceEstimator varianceEstimator;
        protected System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
    }
}