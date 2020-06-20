using System.Numerics;

namespace SeeSharp.Core.Geometry {
    public struct Ray {
        public Vector3 Origin;
        public Vector3 Direction;
        public float MinDistance;
    }
}
