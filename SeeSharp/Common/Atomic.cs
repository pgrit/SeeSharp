using System.Threading;

namespace SeeSharp.Common {
    public static class Atomic {
        public static void AddFloat(ref float target, float value) {
            float initialValue, computedValue;

            // Prevent infinite loop if a pixel value is NaN
            if (!float.IsFinite(target))
                return;

            do {
                initialValue = target;
                computedValue = initialValue + value;
            } while (initialValue != Interlocked.CompareExchange(ref target,
                computedValue, initialValue));
        }
    }
}