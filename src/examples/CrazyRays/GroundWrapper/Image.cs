using System.Runtime.InteropServices;

namespace GroundWrapper {
    public class Image {

        public int width {
            get; private set;
        }

        public int height {
            get; private set;
        }

        public int id {
            get; private set;
        }

        public Image(int width, int height) {
            id = CreateImageRGB(width, height);
            this.width = width;
            this.height = height;
        }

        public void Splat(float x, float y, ColorRGB value) {
            AddSplatRGB(id, x, y, value);
        }

        public ColorRGB Get(float x, float y) {
            return ColorRGB.Black;
        }

        public void WriteToFile(string filename) {
            WriteImage(this.id, filename);
        }

#region C-API-IMPORTS
        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        protected static extern int CreateImageRGB(int width, int height);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        protected static extern void AddSplatRGB(int imgId, float x, float y,
            [In] ColorRGB value);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        protected static extern void WriteImage(int imgId, string filename);
#endregion C-API-IMPORTS
    }
}