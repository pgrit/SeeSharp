using System;

namespace GroundWrapper.GroundMath {
    public struct Vector3 {
        public float x, y, z;

        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public static Vector3 operator +(Vector3 a, Vector3 b)
            => new Vector3 { x = a.x + b.x, y = a.y + b.y, z = a.z + b.z };

        public static Vector3 operator -(Vector3 a)
            => new Vector3 { x = -a.x, y = -a.y, z = -a.z };

        public static Vector3 operator -(Vector3 a, Vector3 b)
            => a + (-b);

        public static Vector3 operator *(Vector3 a, float s)
            => new Vector3 { x = a.x * s, y = a.y * s, z = a.z * s };

        public static Vector3 operator *(float s, Vector3 a)
            => a * s;

        public static Vector3 operator /(Vector3 a, float s)
            => a * (1 / s);

        public static float Dot(Vector3 a, Vector3 b)
            => a.x * b.x + a.y * b.y + a.z * b.z;

        public float LengthSquared() => Dot(this, this);

        public float Length() => (float)System.Math.Sqrt(LengthSquared());

        public Vector3 Normalized() => this / Length();

        public static Vector3 Cross(Vector3 a, Vector3 b)
            => new Vector3(a.y * b.z - a.z * b.y,
                           a.z * b.x - a.x * b.z,
                           a.x * b.y - a.y * b.x);

        public static Vector3 Abs(Vector3 v)
            => new Vector3(MathF.Abs(v.x), MathF.Abs(v.y), MathF.Abs(v.z));

        public float this[int idx] {
            get {
                if (idx == 0) return x;
                else if (idx == 1) return y;
                else return z;
            }
            set {
                if (idx == 0) x = value;
                else if (idx == 1) y = value;
                else z = value;
            }
        }
    }

    public struct Vector2 {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }

        public static Vector2 operator +(Vector2 a, Vector2 b)
            => new Vector2 { x = a.x + b.x, y = a.y + b.y };

        public static Vector2 operator -(Vector2 a)
            => new Vector2 { x = -a.x, y = -a.y };

        public static Vector2 operator -(Vector2 a, Vector2 b)
            => a + (-b);

        public static Vector2 operator *(Vector2 a, float s)
            => new Vector2 { x = a.x * s, y = a.y * s };

        public static Vector2 operator *(float s, Vector2 a)
            => a * s;
    }
}
