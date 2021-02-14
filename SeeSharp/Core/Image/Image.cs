using SeeSharp.Core.Shading;
using SimpleImageIO;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace SeeSharp.Core.Image {
    public class Image<T> where T : ISpectrum, new() {
        public int Width {
            get; private set;
        }

        public int Height {
            get; private set;
        }

        public Image(int width, int height) {
            if (width < 1 || height < 1)
                throw new ArgumentOutOfRangeException("width / height",
                    "Cannot create an image smaller than 1x1 pixels.");

            Width = width;
            Height = height;

            data = new T[width * height];
        }

        public void Splat(float x, float y, T value) {
            int idx = IndexOf(x, y);
            data[idx].AtomicAdd(value);
        }

        public void Scale(float s) {
            Parallel.For(0, Width, x => {
                for (int y = 0; y < Height; ++y) {
                    this[x, y].Scale(s);
                }
            });
        }

        public T Sum() {
            T result = new();
            for (int x = 0; x < Width; ++x) {
                for (int y = 0; y < Height; ++y) {
                    result.Add(this[x, y]);
                }
            }
            return result;
        }

        public static Image<T> operator* (Image<T> a, float b) {
            Image<T> result = new(a.Width, a.Height);

            Parallel.For(0, a.Width, x => {
                for (int y = 0; y < a.Height; ++y) {
                    result[x, y] = a[x, y];
                    result[x, y].Scale(b);
                }
            });

            return result;
        }

        public static Image<T> operator* (float b, Image<T> a) => a * b;

        public static Image<T> operator* (Image<T> a, Image<T> b) {
            if (a.Width != b.Width || a.Height != b.Height)
                throw new ArgumentException("Image dimensions do not match.");

            Image<T> result = new(a.Width, a.Height);

            Parallel.For(0, a.Width, x => {
                for (int y = 0; y < a.Height; ++y) {
                    result[x, y] = a[x, y];
                    result[x, y].Multiply(b[x, y]);
                }
            });

            return result;
        }

        public static Image<T> operator/ (Image<T> a, Image<T> b) {
            if (a.Width != b.Width || a.Height != b.Height)
                throw new ArgumentException("Image dimensions do not match.");

            Image<T> result = new(a.Width, a.Height);

            Parallel.For(0, a.Width, x => {
                for (int y = 0; y < a.Height; ++y) {
                    result[x, y] = a[x, y];
                    result[x, y].Divide(b[x, y]);
                }
            });

            return result;
        }

        public static Image<T> operator+ (Image<T> a, Image<T> b) {
            if (a.Width != b.Width || a.Height != b.Height)
                throw new ArgumentException("Image dimensions do not match.");

            Image<T> result = new(a.Width, a.Height);

            Parallel.For(0, a.Width, x => {
                for (int y = 0; y < a.Height; ++y) {
                    result[x, y] = a[x, y];
                    result[x, y].Add(b[x, y]);
                }
            });

            return result;
        }

        public static Image<T> operator+ (Image<T> a, float b) {
            Image<T> result = new(a.Width, a.Height);
            Parallel.For(0, a.Width, x => {
                for (int y = 0; y < a.Height; ++y) {
                    result[x, y] = a[x, y];
                    result[x, y].Add(b);
                }
            });
            return result;
        }

        public static Image<T> operator- (Image<T> a, Image<T> b) {
            if (a.Width != b.Width || a.Height != b.Height)
                throw new ArgumentException("Image dimensions do not match.");

            Image<T> result = new(a.Width, a.Height);

            Parallel.For(0, a.Width, x => {
                for (int y = 0; y < a.Height; ++y) {
                    result[x, y] = b[x, y];
                    result[x, y].Scale(-1);
                    result[x, y].Add(a[x, y]);
                }
            });

            return result;
        }

        int IndexOf(float x, float y) {
            int row = (int)y;
            int col = (int)x;
            return IndexOf(col, row);
        }

        int IndexOf(int col, int row) {
            // TODO make border handling mode programmable, we go with repeat as the hard-coded default.
            row = (row % Height + Height) % Height;
            col = (col % Width + Width) % Width;
            // e.g. for clamp:
            // row = System.Math.Clamp(row, 0, Height - 1);
            // col = System.Math.Clamp(col, 0, Width - 1);
            return col + row * Width;
        }

        /// <summary>
        /// Allows access to a pixel in the image. (0,0) is the top left corner and (1,0) the top right corner.
        /// Not thread-safe unless read-only. Use <see cref="Splat(float, float)"/> for thread-safe writing.
        /// </summary>
        /// <param name="x">Horizontal coordinate [0,width], left to right.</param>
        /// <param name="y">Vertical coordinate [0, height], top to bottom.</param>
        /// <returns>The pixel color.</returns>
        public ref T this[float x, float y] => ref data[IndexOf(x, y)];

        public ref T this[int x, int y] => ref data[IndexOf(x, y)];

        /// <summary>
        /// Same as the index operator, only the parameters are (0,1)^2 rather than (0, Width) x (0, Height)
        /// </summary>
        public ref T TextureLookup(Vector2 uv) => ref this[uv.X * Width, uv.Y * Height];

        readonly T[] data;

        public static void WriteToFile(Image<ColorRGB> image, string filename) {
            // Create a temporary RGB image
            RgbImage buffer = new(image.Width, image.Height);
            for (int row = 0; row < image.Height; ++row) {
                for (int col = 0; col < image.Width; ++col) {
                    var v = image[col, row];
                    buffer.SetPixel(col, row, new(v.R, v.G, v.B));
                }
            }
            buffer.WriteToFile(filename);
        }

        public static string AsBase64Png(Image<ColorRGB> image) {
            // Create a temporary RGB image
            RgbImage buffer = new(image.Width, image.Height);
            for (int row = 0; row < image.Height; ++row) {
                for (int col = 0; col < image.Width; ++col) {
                    var v = image[col, row];
                    buffer.SetPixel(col, row, new(v.R, v.G, v.B));
                }
            }
            return buffer.AsBase64Png();
        }

        public static void WriteToFile(Image<Scalar> image, string filename) {
            // Create a temporary RGB image
            RgbImage buffer = new(image.Width, image.Height);
            for (int row = 0; row < image.Height; ++row) {
                for (int col = 0; col < image.Width; ++col) {
                    var v = image[col, row].Value;
                    buffer.SetPixel(col, row, new(v, v, v));
                }
            }
            buffer.WriteToFile(filename);
        }

        public static Image<ColorRGB> LoadFromFile(string filename) {
            RgbImage buffer = new(filename);
            Image<ColorRGB> result = new(buffer.Width, buffer.Height);
            for (int row = 0; row < buffer.Height; ++row) {
                for (int col = 0; col < buffer.Width; ++col) {
                    var val = buffer.GetPixel(col, row);
                    result[col, row] = new(val.R, val.G, val.B);
                }
            }
            return result;
        }

        public static Image<ColorRGB> Constant(ColorRGB color) {
            var img = new Image<ColorRGB>(1, 1);
            img[0, 0] = color;
            return img;
        }

        public static Image<Scalar> Constant(float value) {
            var img = new Image<Scalar>(1, 1);
            img[0, 0] = new Scalar(value);
            return img;
        }
    }
}