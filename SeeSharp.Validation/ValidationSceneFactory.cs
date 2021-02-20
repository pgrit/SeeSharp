using SeeSharp;

namespace SeeSharp.Validation {
    abstract class ValidationSceneFactory {
        public abstract int SamplesPerPixel { get; }
        public abstract int MaxDepth { get; }
        public abstract string Name { get; }
        public abstract Scene MakeScene();
    }
}