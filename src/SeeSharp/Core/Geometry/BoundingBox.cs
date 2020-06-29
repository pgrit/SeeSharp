using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace SeeSharp.Core.Geometry {
    public struct BoundingBox {
        public Vector3 Min, Max;

        public static BoundingBox Empty => new BoundingBox {
            Min = Vector3.One * float.MaxValue,
            Max = -Vector3.One * float.MaxValue
        };

        public static BoundingBox Full => new BoundingBox {
            Min = -Vector3.One * float.MaxValue,
            Max = Vector3.One * float.MaxValue
        };

        public void GrowToContain(Vector3 point) {
            Min = Vector3.Min(Min, point);
            Max = Vector3.Max(Max, point);
        }

        public void GrowToContain(BoundingBox box) {
            Min = Vector3.Min(Min, box.Min);
            Max = Vector3.Max(Max, box.Max);
        }

        public bool IsInside(Vector3 point) 
            => point.X >= Min.X && point.Y >= Min.Y && point.Z >= Min.Z &&
               point.X <= Max.X && point.Y <= Max.Y && point.Z <= Max.Z;
    }
}
