using SeeSharp.Core.Shading;

namespace SeeSharp.Core {
    public class FrameBuffer {
        public int Width => image.Width;
        public int Height => image.Height;

        public FrameBuffer(int width, int height) {
            image = new Image(width, height);
        }

        public void Splat(float x, float y, ColorRGB value) => image.Splat(x, y, value);

        public Image image;
        public void IterationFinished() {
            
        }

        public void WriteToFile(string path) => image.WriteToFile(path);
    }
}