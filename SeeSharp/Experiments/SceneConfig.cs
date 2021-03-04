using SimpleImageIO;

namespace SeeSharp.Experiments {
    public abstract class SceneConfig {
        public abstract string Name { get; }
        public abstract int MaxDepth { get; }
        public abstract Scene MakeScene();
        public abstract RgbImage GetReferenceImage(int width, int height);
    }
}