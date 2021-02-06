using System;
using System.Threading;

namespace SeeSharp.Core.Shading {
    static class AtomicFloat {
        public static void Add(ref float target, float value) {
            float initialValue, computedValue;
            do {
                initialValue = target;
                computedValue = initialValue + value;
            } while (initialValue != Interlocked.CompareExchange(ref target,
                computedValue, initialValue));
        }
    }

    public interface ISpectrum {
        void Add(ISpectrum other);
        void Add(float other);
        void Scale(float factor);
        void Multiply(ISpectrum other);
        void Divide(ISpectrum other);
        void AtomicAdd(ISpectrum other);
    }

    public struct Scalar : ISpectrum {
        public float Value;

        public Scalar(float v = 0) { Value = v; }

        public static Scalar operator *(Scalar a, Scalar b)
            => new Scalar { Value = a.Value * b.Value };

        public static Scalar operator +(Scalar a, Scalar b)
            => new Scalar { Value = a.Value + b.Value };

        public static Scalar operator *(Scalar a, float b)
            => new Scalar { Value = a.Value * b };

        public static Scalar operator /(Scalar a, float b)
            => new Scalar { Value = a.Value / b };

        public void Add(ISpectrum other) {
            this += (Scalar) other;
        }

        public void AtomicAdd(ISpectrum other) {
            AtomicFloat.Add(ref Value, ((Scalar)other).Value);
        }

        public void Add(float other) {
            this.Value += other;
        }

        public void Scale(float factor) {
            Value *= factor;
        }

        public void Multiply(ISpectrum other) {
            this *= (Scalar) other;
        }

        public void Divide(ISpectrum other) {
            this /= ((Scalar) other).Value;
        }
    }

#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
    public struct ColorRGB : ISpectrum {
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

        public void Add(ISpectrum other) {
            ColorRGB o = (ColorRGB) other;
            this += o;
        }

        public void Add(float other) {
            this += other;
        }

        public void AtomicAdd(ISpectrum other) {
            AtomicFloat.Add(ref R, ((ColorRGB)other).R);
            AtomicFloat.Add(ref G, ((ColorRGB)other).G);
            AtomicFloat.Add(ref B, ((ColorRGB)other).B);
        }

        public void Scale(float factor) {
            this *= factor;
        }

        public void Multiply(ISpectrum other) {
            this *= (ColorRGB) other;
        }

        public void Divide(ISpectrum other) {
            this /= (ColorRGB) other;
        }

        public float Luminance => 0.212671f * R + 0.715160f * G + 0.072169f * B;
        public float Average => (R + G + B) / 3.0f;

        public override string ToString() => $"({R}, {G}, {B})";
    }
}
