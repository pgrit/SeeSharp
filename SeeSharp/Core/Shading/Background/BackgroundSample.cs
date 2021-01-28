using System.Numerics;

namespace SeeSharp.Core.Shading.Background {
    public struct BackgroundSample {
        public ColorRGB Weight;
        public Vector3 Direction;
        public float Pdf;
    }
}