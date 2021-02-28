using SimpleImageIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
            public virtual void OnEndIteration(int curIteration) { }
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

        /// <summary>
        /// Estimates the pixel variance in the rendered image.
        /// "Pixel variance" is defined as the squared deviation of the pixel value from each iteration
        /// from the mean pixel value across all iterations.
        /// Note that this does not necessarily equal the variance of the underlying Monte Carlo estimator,
        /// especially not if MIS is used. It is a lower-bound approximation of that.
        /// </summary>
        public class VarianceLayer : Layer {
            MonochromeImage momentImage;
            MonochromeImage meanImage;
            MonochromeImage bufferImage;

            public float Average;

            public override void Init(int width, int height) {
                Image = new MonochromeImage(width, height);
                momentImage = new MonochromeImage(width, height);
                meanImage = new MonochromeImage(width, height);
                bufferImage = new MonochromeImage(width, height);
            }

            public override void Reset() {
                base.Reset();
                momentImage.Scale(0);
                meanImage.Scale(0);
                bufferImage.Scale(0);
            }

            public virtual void Splat(float x, float y, RgbColor value)
            => bufferImage.AtomicAdd((int)x, (int)y, value.Average);

            public override void OnStartIteration(int curIteration) {
                if (curIteration > 1) {
                    momentImage.Scale((curIteration - 1.0f) / curIteration);
                    meanImage.Scale((curIteration - 1.0f) / curIteration);
                }
                this.curIteration = curIteration;

                // Each iteration needs to store the final pixel value of that iteration
                bufferImage.Scale(0);
            }

            public override void OnEndIteration(int curIteration) {
                // Update the mean and moment based on the buffered image of the current iteration
                Parallel.For(0, momentImage.Height, row => {
                    for (int col = 0; col < momentImage.Width; ++col) {
                        float val = bufferImage.GetPixel(col, row);
                        momentImage.AtomicAdd(col, row, val * val / curIteration);
                        meanImage.AtomicAdd(col, row, val / curIteration);
                    }
                });

                // Blur both buffers to get a more stable estimate.
                // TODO this could be done in-place by directly splatting in multiple pixels above
                BoxFilter filter = new(1);
                MonochromeImage blurredMean = new(meanImage.Width, meanImage.Height);
                MonochromeImage blurredMoment = new(meanImage.Width, meanImage.Height);
                filter.Apply(meanImage, blurredMean);
                filter.Apply(momentImage, blurredMoment);

                // Compute the final variance and update the main image
                Average = 0;
                Parallel.For(0, momentImage.Height, row => {
                    for (int col = 0; col < momentImage.Width; ++col) {
                        float mean = blurredMean.GetPixel(col, row);
                        float variance = blurredMoment.GetPixel(col, row) - mean * mean;
                        variance /= (mean * mean + 0.001f);
                        Image.SetPixelChannel(col, row, 0, variance);
                        Common.Atomic.AddFloat(ref Average, variance);
                    }
                });
            }
        }

        public readonly int Width;
        public readonly int Height;
        public RgbImage Image { get; private set; }
        public readonly VarianceLayer PixelVariance = new();
        public readonly Dictionary<string, dynamic> MetaData = new();

        public void AddLayer(string name, Layer layer) => layers.Add(name, layer);

        /// <summary>
        /// 1-based index of the current iteration (i.e., the total number of iterations so far)
        /// </summary>
        public int CurIteration = 0;

        public string Basename {
            get {
                string dir = Path.GetDirectoryName(filename);
                string fileBase = Path.GetFileNameWithoutExtension(filename);
                return Path.Combine(dir, fileBase);
            }
        }

        public string Extension => Path.GetExtension(filename);

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
            Width = width;
            Height = height;
            AddLayer("variance", PixelVariance);
        }

        public virtual void Splat(float x, float y, RgbColor value) {
            Image.AtomicAdd((int)x, (int)y, value / CurIteration);
            PixelVariance.Splat(x, y, value);

            // Catch invalid values in long running Release mode renderings.
            // Ideally can be reproduced with a single sample from a correctly seeded RNG.
            if (!float.IsFinite(value.Average)) {
                Console.WriteLine("NaN or Inf written to frame buffer! " +
                    $"Iteration: {CurIteration}, Pixel: ({x},{y})");
            }
        }

        public virtual void StartIteration() {
            if (CurIteration == 0) {
                Image = new RgbImage(Width, Height);
                foreach (var (_, layer) in layers)
                    layer.Init(Width, Height);

                if (flags.HasFlag(Flags.SendToTev)) {
                    tevIpc = new TevIpc();
                    // If a file with the same name is open, close it to avoid conflicts
                    tevIpc.CloseImage(filename);
                    tevIpc.CreateImageSync(filename, Width, Height,
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

            MetaData["RenderTime"] = stopwatch.ElapsedMilliseconds;

            foreach (var (_, layer) in layers)
                layer.OnEndIteration(CurIteration);

            if (flags.HasFlag(Flags.WriteIntermediate)) {
                string name = Basename + "-iter" + CurIteration.ToString("D3")
                    + $"-{stopwatch.ElapsedMilliseconds}ms" + Extension;
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

            string dir = Path.GetDirectoryName(fname);
            string fileBase = Path.GetFileNameWithoutExtension(fname);
            string basename = Path.Combine(dir, fileBase);

            if (Path.GetExtension(fname).ToLower() == ".exr") {
                ImageBase.WriteLayeredExr(fname,
                    layers.Select(kv => (kv.Key, kv.Value.Image))
                        .Append(("default", Image))
                        .ToArray()
                );
            } else {
                // write all layers into individual files
                Image.WriteToFile(fname);

                string ext = Path.GetExtension(fname);
                foreach (var (name, layer) in layers) {
                    layer.Image.WriteToFile(basename + "-" + name + ext);
                }
            }

            // Write the metadata as json
            string json = System.Text.Json.JsonSerializer.Serialize(MetaData, options: new() {
                WriteIndented = true
            });
            File.WriteAllText(basename + ".json", json);
        }

        Flags flags;
        TevIpc tevIpc;

        protected string filename;
        protected Dictionary<string, Layer> layers = new();
        protected System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
    }
}