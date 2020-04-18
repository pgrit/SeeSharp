namespace GroundWrapper {
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

        public static ColorRGB operator +(ColorRGB a, ColorRGB b)
            => new ColorRGB { r = a.r + b.r, g = a.g + b.g, b = a.b + b.b };

        public static ColorRGB Black =
            new ColorRGB { r = 0.0f, g = 0.0f, b = 0.0f };

        public static ColorRGB White =
            new ColorRGB { r = 1.0f, g = 1.0f, b = 1.0f };

        public ColorRGB(float r, float g, float b) : this() {
            this.r = r;
            this.g = g;
            this.b = b;
        }

        public static bool operator ==(ColorRGB a, ColorRGB b)
            => a.r == b.r && a.g == b.g && a.b == b.b;

        public static bool operator !=(ColorRGB a, ColorRGB b)
            => !(a == b);
    }
}
