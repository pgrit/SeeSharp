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

            data = new ColorRGB[width, height];
        }

        public ColorRGB this[float x, float y] {
            get {
                int row = (int)y;
                int col = (int)x;
                
                row = System.Math.Clamp(row, 0, Height - 1);
                col = System.Math.Clamp(col, 0, Width - 1);
                
                return data[col, row];
            }
            set {
                int row = (int)y;
                int col = (int)x;

                row = System.Math.Clamp(row, 0, Height - 1);
                col = System.Math.Clamp(col, 0, Width - 1);

                data[col, row] = value;
            }
        }

        public void WriteToFile(string filename) {
        }

        public static Image LoadFromFile(string filename) {
            return new Image(1, 1);
        }

        public static Image Constant(ColorRGB color) {
            var img = new Image(1, 1);
            img[0, 0] = color;
            return img;
        }

        readonly ColorRGB[,] data;
    }
}