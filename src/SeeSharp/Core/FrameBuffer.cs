using SeeSharp.Core.Shading;
using System;

namespace SeeSharp.Core {
    public class FrameBuffer {
        public int Width => image.Width;
        public int Height => image.Height;

        public Image image;
        public int curIteration = 0; // 1-based index of the current iteration (i.e., the total number of iterations so far)

        public string Basename {
            get {
                string dir = System.IO.Path.GetDirectoryName(filename);
                string fileBase = System.IO.Path.GetFileNameWithoutExtension(filename);
                return System.IO.Path.Combine(dir, fileBase);
            }
        }

        string filename;

        [Flags]
        public enum Flags {
            None = 0,
            WriteIntermediate = 1, // Write the result of each iteration into a distinct file
            WriteContinously = 2 // Continously update the rendering result after each iteration
        }

        Flags flags;

        public FrameBuffer(int width, int height, string filename, Flags flags = Flags.None) {
            image = new Image(width, height);
            this.filename = filename;
            this.flags = flags;
        }

        public void Splat(float x, float y, ColorRGB value)
            => image.Splat(x, y, value / curIteration);

        public void StartIteration() {
            curIteration++;

            // Correct the division by the number of iterations from the previous iterations
            if (curIteration > 1)
                image.Scale((curIteration - 1.0f) / curIteration);
        }

        public void EndIteration() {
            if (flags.HasFlag(Flags.WriteIntermediate)) {
                string name = Basename + "-iter" + curIteration.ToString("D3") + ".exr";
                image.WriteToFile(name);
            }

            if (flags.HasFlag(Flags.WriteContinously))
                WriteToFile();
        }

        public void Reset() {
            curIteration = 0;
            image.Scale(0);
        }

        public void WriteToFile() => image.WriteToFile(filename);
    }
}