using SeeSharp.Core.Shading;
using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace SeeSharp.Core.Image {
    public class Image<T> where T : ISpectrum {
        public int Width {
            get; private set;
        }

        public int Height {
            get; private set;
        }

        public Image(int width, int height) {
            if (width < 1 || height < 1)
                throw new System.ArgumentOutOfRangeException("width / height",
                    "Cannot create an image smaller than 1x1 pixels.");

            Width = width;
            Height = height;

            data = new T[width * height];
        }

        public void Splat(float x, float y, T value) {
            lock (this) {
                this[x, y].Add(value);
            }
        }

        public void Scale(float s) {
            for (int x = 0; x < Width; ++x) {
                for (int y = 0; y < Height; ++y) {
                    this[x, y].Scale(s);
                }
            }
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

        public static void WriteToFile(Image<ColorRGB> image, string filename) {
            var ext = System.IO.Path.GetExtension(filename);
            if (ext.ToLower() == ".exr") {
                // First, make sure that the full path exists
                var dirname = System.IO.Path.GetDirectoryName(filename);
                if (dirname != "")
                    System.IO.Directory.CreateDirectory(dirname);

                TinyExr.WriteImageToExr(image.data, image.Width, image.Height, 3, filename);
            } else {
                WriteImageToLDR(image, filename);
            }
        }

        public static Image<ColorRGB> LoadFromFile(string filename) {
            var ext = System.IO.Path.GetExtension(filename);
            if (ext.ToLower() == ".exr")
                return LoadImageFromExr(filename);
            else {
                return LoadImageFromLDR(filename);
            }
        }

        public static Image<ColorRGB> Constant(ColorRGB color) {
            var img = new Image<ColorRGB>(1, 1);
            img[0, 0] = color;
            return img;
        }

        readonly T[] data;

        static void WriteImageToLDR(Image<ColorRGB> img, string filename) {
            int width = img.Width;
            int height = img.Height;
            using (var b = new Bitmap(width, height)) {
                for (int x = 0; x < width; ++x) {
                    for (int y = 0; y < height; ++y) {
                        var px = img[x,y];
                        int ToInt(float chan) {
                            chan = MathF.Pow(chan, 1/2.2f);
                            return Math.Clamp((int)(chan * 255), 0, 255);
                        }
                        b.SetPixel(x, y, Color.FromArgb(ToInt(px.R), ToInt(px.G), ToInt(px.B)));
                    }
                }
                b.Save(filename);
            }
        }

        static Image<ColorRGB> LoadImageFromLDR(string filename) {
            Image<ColorRGB> image;
            try {
                using (var b = (Bitmap)System.Drawing.Image.FromFile(filename)) {
                    image = new Image<ColorRGB>(b.Width, b.Height);

                    var bits = b.LockBits(new Rectangle(0, 0, b.Width, b.Height),
                        System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    var depth = Bitmap.GetPixelFormatSize(bits.PixelFormat) / 8;

                    for (int x = 0; x < b.Width; ++x) {
                        for (int y = 0; y < b.Height; ++y) {
                            byte red, green, blue;
                            unsafe {
                                byte* scan0 = (byte*)bits.Scan0.ToPointer();
                                var addr = scan0 + y * bits.Stride + x * depth;
                                blue = *addr++;
                                green = *addr++;
                                red = *addr++;
                            }

                            var rgb = new ColorRGB(red / (float)255, green / (float)255, blue / (float)255);

                            // perform inverse gamma correction
                            rgb.R = MathF.Pow(rgb.R, 2.2f);
                            rgb.G = MathF.Pow(rgb.G, 2.2f);
                            rgb.B = MathF.Pow(rgb.B, 2.2f);

                            image[x, y] = rgb;
                        }
                    }
                }
            } catch (System.OutOfMemoryException) {
                Console.WriteLine($"Unsupported image file format: {filename}. Replaced by a white 1x1 image.");
                image = Image<ColorRGB>.Constant(ColorRGB.White);
            }
            return image;
        }

        static Image<ColorRGB> LoadImageFromExr(string filename) {
            // Read the image from the file, it is cached in nativ memory
            int width, height;
            int id = TinyExr.CacheExrImage(out width, out height, filename);
            if (id < 0)
                throw new System.IO.IOException($"could not load .exr file '{filename}'");

            // Copy to managed memory array and return
            var img = new Image<ColorRGB>(width, height);
            TinyExr.CopyCachedImage(id, img.data);
            return img;
        }
    }

    static class TinyExr {
        [DllImport("SeeCore", CallingConvention = CallingConvention.Cdecl)]
        public static extern void WriteImageToExr(ColorRGB[] data, int width, int height, int numChannels,
                                                    string filename);

        [DllImport("SeeCore", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CacheExrImage(out int width, out int height, string filename);

        [DllImport("SeeCore", CallingConvention = CallingConvention.Cdecl)]
        public static extern void CopyCachedImage(int id, [Out] ColorRGB[] buffer);
    }
}