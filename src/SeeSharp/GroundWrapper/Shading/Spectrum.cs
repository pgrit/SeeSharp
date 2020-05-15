using System;

namespace GroundWrapper.Shading {
    public struct ColorRGB {
        public float r, g, b;

        public static ColorRGB operator *(ColorRGB a, ColorRGB b)
            => new ColorRGB { r = a.r * b.r, g = a.g * b.g, b = a.b * b.b };

        public static ColorRGB operator *(ColorRGB a, float b)
            => new ColorRGB { r = a.r * b, g = a.g * b, b = a.b * b };

        public static ColorRGB operator *(float a, ColorRGB b)
            => b * a;

        public static ColorRGB operator /(ColorRGB a, float b)
            => new ColorRGB { r = a.r / b, g = a.g / b, b = a.b / b };

        public static ColorRGB operator /(ColorRGB a, ColorRGB b)
            => new ColorRGB { r = a.r / b.r, g = a.g / b.g, b = a.b / b.b };

        public static ColorRGB operator +(ColorRGB a, ColorRGB b)
            => new ColorRGB { r = a.r + b.r, g = a.g + b.g, b = a.b + b.b };

        public static ColorRGB operator -(ColorRGB a, ColorRGB b)
            => a + -1 * b;

        public static ColorRGB operator +(ColorRGB a, float b)
            => new ColorRGB { r = a.r + b, g = a.g + b, b = a.b + b };

        public static ColorRGB operator -(ColorRGB a, float b)
            => a + -b;

        public static ColorRGB Black =
            new ColorRGB { r = 0.0f, g = 0.0f, b = 0.0f };

        public static ColorRGB White =
            new ColorRGB { r = 1.0f, g = 1.0f, b = 1.0f };

        public ColorRGB(float r, float g, float b) : this() {
            this.r = r;
            this.g = g;
            this.b = b;
        }

        public ColorRGB(float c) {
            r = c;
            g = c;
            b = c;
        }

        public static bool operator ==(ColorRGB a, ColorRGB b)
            => a.r == b.r && a.g == b.g && a.b == b.b;

        public static bool operator !=(ColorRGB a, ColorRGB b)
            => !(a == b);

        public static ColorRGB Sqrt(ColorRGB v)
            => new ColorRGB(MathF.Sqrt(v.r), MathF.Sqrt(v.g), MathF.Sqrt(v.b));

        public static ColorRGB Lerp(float w, ColorRGB from, ColorRGB to)
            => (1 - w) * from + w * to;

        public float Luminance => 0.212671f * r + 0.715160f * g + 0.072169f * b;
    }
}
