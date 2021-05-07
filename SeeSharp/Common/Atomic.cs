using System.Threading;

namespace SeeSharp.Common {
    /// <summary>
    /// Provides utility functions for atomic operations.
    /// </summary>
    public static class Atomic {
        /// <summary>
        /// Adds two floating point values in an atomic fashion, using a compare-and-swap.
        /// Thread-safe version of: target += value;
        /// </summary>
        /// <param name="target">Destination</param>
        /// <param name="value">Value to add</param>
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