using System.Runtime.CompilerServices;

namespace SeeSharp.Common;

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddFloat(ref float target, float value) {
        float initialValue, computedValue;
        do {
            initialValue = target;
            computedValue = initialValue + value;
        } while (
            initialValue != Interlocked.CompareExchange(ref target, computedValue, initialValue)
            // If another thread changes target to NaN in the meantime, we will be stuck forever
            // since NaN != NaN is always true, and NaN + value is also NaN
            && !float.IsNaN(initialValue)
        );
    }
}