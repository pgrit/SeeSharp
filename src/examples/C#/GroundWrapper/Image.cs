using System.Runtime.InteropServices;

namespace Ground {
    public class Image {

        public uint width {
            get; private set;
        }

        public uint height {
            get; private set;
        }

        public int id {
            get; private set;
        }

        public Image(uint width, uint height) {
            this.id = CreateImageRGB((int)width, (int)height);
            this.width = width;
            this.height = height;
        }

        public void Splat(float x, float y, Ground.ColorRGB value) {
            AddSplatRGB(this.id, x, y, value);
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