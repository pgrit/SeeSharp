using System.Runtime.InteropServices;

namespace GroundWrapper {
    public class Image {
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

            data = new ColorRGB[width * height];
        }

        /// <summary>
        /// Allows access to a pixel in the image. (0,0) is the top left corner.
        /// </summary>
        /// <param name="x">Horizontal coordinate [0,width], left to right.</param>
        /// <param name="y">Vertical coordinate [0, height], top to bottom.</param>
        /// <returns>The pixel color.</returns>
        public ColorRGB this[float x, float y] {
            get {
                int row = (int)y;
                int col = (int)x;
                
                row = System.Math.Clamp(row, 0, Height - 1);
                col = System.Math.Clamp(col, 0, Width - 1);
                
                return data[col + row * Width];
            }
            set {
                int row = (int)y;
                int col = (int)x;

                row = System.Math.Clamp(row, 0, Height - 1);
                col = System.Math.Clamp(col, 0, Width - 1);

                data[col + row * Width] = value;
            }
        }

        public void WriteToFile(string filename) {
            TinyExr.WriteImageToExr(data, Width, Height, 3, filename);
        }

        public static Image LoadFromFile(string filename) {
            // Read the image from the file, it is cached in nativ memory
            int width, height;
            int id = TinyExr.CacheExrImage(out width, out height, filename);
            if (id < 0) throw new System.IO.IOException($"could not load .exr file '{filename}'");

            // Copy to managed memory array and return
            var img = new Image(width, height);
            TinyExr.CopyCachedImage(id, img.data);
            return img;

            // TODO performance could probably be improved by using the memory allocated by the managed code, instead of copying.
            //      However, the tinyexr library uses a different structure, so there is a benefit to copying as well.
        }

        public static Image Constant(ColorRGB color) {
            var img = new Image(1, 1);
            img[0, 0] = color;
            return img;
        }

        readonly ColorRGB[] data;

        private static class TinyExr {
            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern void WriteImageToExr(ColorRGB[] data, int width, int height, int numChannels,
                                                      string filename);

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern int CacheExrImage(out int width, out int height, string filename);

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern void CopyCachedImage(int id, [Out] ColorRGB[] buffer); 
        }
    }
}