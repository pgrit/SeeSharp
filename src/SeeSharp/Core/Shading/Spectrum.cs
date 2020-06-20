using System;

namespace SeeSharp.Core.Shading {
#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
    public struct ColorRGB {
#pragma warning restore CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
#pragma warning restore CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
        public float R, G, B;

        public static ColorRGB operator *(ColorRGB a, ColorRGB b)
            => new ColorRGB { R = a.R * b.R, G = a.G * b.G, B = a.B * b.B };

        public static ColorRGB operator *(ColorRGB a, float b)
            => new ColorRGB { R = a.R * b, G = a.G * b, B = a.B * b };

        public static ColorRGB operator *(float a, ColorRGB b)
            => b * a;

        public static ColorRGB operator /(ColorRGB a, float b)
            => new ColorRGB { R = a.R / b, G = a.G / b, B = a.B / b };

        public static ColorRGB operator /(ColorRGB a, ColorRGB b)
            => new ColorRGB { R = a.R / b.R, G = a.G / b.G, B = a.B / b.B };

        public static ColorRGB operator +(ColorRGB a, ColorRGB b)
            => new ColorRGB { R = a.R + b.R, G = a.G + b.G, B = a.B + b.B };

        public static ColorRGB operator -(ColorRGB a, ColorRGB b)
            => a + -1 * b;

        public static ColorRGB operator +(ColorRGB a, float b)
            => new ColorRGB { R = a.R + b, G = a.G + b, B = a.B + b };

        public static ColorRGB operator -(ColorRGB a, float b)
            => a + -b;

        public static ColorRGB Black =
            new ColorRGB { R = 0.0f, G = 0.0f, B = 0.0f };

        public static ColorRGB White =
            new ColorRGB { R = 1.0f, G = 1.0f, B = 1.0f };

        public ColorRGB(float r, float g, float b) : this() {
            R = r;
            G = g;
            B = b;
        }

        public ColorRGB(float c) {
            R = c;
            G = c;
            B = c;
        }

        public static bool operator ==(ColorRGB a, ColorRGB b)
            => a.R == b.R && a.G == b.G && a.B == b.B;

        public static bool operator !=(ColorRGB a, ColorRGB b)
            => !(a == b);

        public static ColorRGB Sqrt(ColorRGB v)
            => new ColorRGB(MathF.Sqrt(v.R), MathF.Sqrt(v.G), MathF.Sqrt(v.B));

        public static ColorRGB Lerp(float w, ColorRGB from, ColorRGB to)
            => (1 - w) * from + w * to;

        public float Luminance => 0.212671f * R + 0.715160f * G + 0.072169f * B;
    }
}
