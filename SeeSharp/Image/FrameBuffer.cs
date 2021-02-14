using SimpleImageIO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SeeSharp.Image {
    public class FrameBuffer {
        public abstract class Layer {
            public ImageBase Image { get; protected set; }
            public abstract void Init(int width, int height);
            public virtual void Reset() => Image.Scale(0);
            public virtual void OnStartIteration(int curIteration) {
                if (curIteration > 1)
                    Image.Scale((curIteration - 1.0f) / curIteration);
                this.curIteration = curIteration;
            }
            protected int curIteration;
        }

        public class RgbLayer : Layer {
            public override void Init(int width, int height) => Image = new RgbImage(width, height);
            public virtual void Splat(float x, float y, RgbColor value)
            => (Image as RgbImage).AtomicAdd((int)x, (int)y, value / curIteration);
        }

        public class MonoLayer : Layer {
            public override void Init(int width, int height) => Image = new MonochromeImage(width, height);
            public virtual void Splat(float x, float y, float value)
            => (Image as MonochromeImage).AtomicAdd((int)x, (int)y, value / curIteration);
        }

        public int Width => width;
        public int Height => height;
        public RgbImage Image;

        public void AddLayer(string name, Layer layer) => layers.Add(name, layer);

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

        public FrameBuffer(int width, int height, string filename, Flags flags = Flags.None) {
            this.filename = filename;
            this.flags = flags;
            this.width = width;
            this.height = height;
        }

        public virtual void Splat(float x, float y, RgbColor value) {
            Image.AtomicAdd((int)x, (int)y, value / CurIteration);
        }

        public virtual void StartIteration() {
            if (CurIteration == 0) {
                Image = new RgbImage(width, height);
                foreach (var (_, layer) in layers)
                    layer.Init(width, height);

                if (flags.HasFlag(Flags.SendToTev)) {
                    tevIpc = new TevIpc();
                    // If a file with the same name is open, close it to avoid conflicts
                    tevIpc.CloseImage(filename);
                    tevIpc.CreateImageSync(filename, width, height,
                        layers.Select(kv => (kv.Key, kv.Value.Image))
                            .Append(("default", Image))
                            .ToArray()
                    );
                }
            }

            CurIteration++;

            // Correct the division by the number of iterations from the previous iterations
            if (CurIteration > 1)
                Image.Scale((CurIteration - 1.0f) / CurIteration);

            foreach (var (_, layer) in layers)
                layer.OnStartIteration(CurIteration);

            stopwatch.Start();
        }

        public long RenderTimeMs => stopwatch.ElapsedMilliseconds;

        public virtual void EndIteration() {
            stopwatch.Stop();

            if (flags.HasFlag(Flags.WriteIntermediate)) {
                string name = Basename + "-iter" + CurIteration.ToString("D3")
                    + $"-{stopwatch.ElapsedMilliseconds}ms" + ".exr";
                WriteToFile(name);
            }

            if (flags.HasFlag(Flags.WriteContinously))
                WriteToFile();

            if (flags.HasFlag(Flags.SendToTev))
                tevIpc.UpdateImage(filename);
        }

        public virtual void Reset() {
            CurIteration = 0;
            stopwatch.Reset();
        }

        public void WriteToFile(string fname = null) {
            if (fname == null) fname = filename;
            ImageBase.WriteLayeredExr(fname,
                layers.Select(kv => (kv.Key, kv.Value.Image))
                      .Append(("default", Image))
                      .ToArray()
            );
        }

        Flags flags;
        TevIpc tevIpc;
        int width, height;

        protected string filename;
        protected Dictionary<string, Layer> layers = new();
        protected System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
    }
}