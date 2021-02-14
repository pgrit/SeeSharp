using System.Numerics;

namespace SeeSharp.Geometry {
    public readonly struct BoundingBox {
        public readonly Vector3 Min, Max;

        public BoundingBox(Vector3 min, Vector3 max) {
            Min = min;
            Max = max;
        }

        public static BoundingBox Empty => new BoundingBox(
            min: Vector3.One * float.MaxValue,
            max: -Vector3.One * float.MaxValue
        );

        public static BoundingBox Full => new BoundingBox(
            min: -Vector3.One * float.MaxValue,
            max: Vector3.One * float.MaxValue
        );

        public BoundingBox GrowToContain(Vector3 point) => new BoundingBox(
            min: Vector3.Min(Min, point),
            max: Vector3.Max(Max, point)
        );

        public void GrowToContain(BoundingBox box) => new BoundingBox(
            min: Vector3.Min(Min, box.Min),
            max: Vector3.Max(Max, box.Max)
        );

        public bool IsInside(Vector3 point)
            => point.X >= Min.X && point.Y >= Min.Y && point.Z >= Min.Z &&
               point.X <= Max.X && point.Y <= Max.Y && point.Z <= Max.Z;

        public Vector3 Diagonal => Max - Min;
    }
}
