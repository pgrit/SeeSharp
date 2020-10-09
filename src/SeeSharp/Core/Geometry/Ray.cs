using System.Numerics;

namespace SeeSharp.Core.Geometry {
    public struct Ray {
        public Vector3 Origin;
        public Vector3 Direction;
        public float MinDistance;

        public Vector3 ComputePoint(float t) => Origin + t * Direction;
    }

    public readonly struct ShadowRay {
        public readonly Ray Ray;
        public readonly float MaxDistance;
        public ShadowRay(Ray ray, float maxDistance) {
            Ray = ray;
            MaxDistance = maxDistance;
        }
    }
}
