using System.Numerics;

namespace GroundWrapper.Geometry {
    public struct Ray {
        public Vector3 origin;
        public Vector3 direction;
        public float minDistance;
    }
}
