using System.Threading;

namespace SeeSharp.Common {
    public static class Atomic {
        public static void AddFloat(ref float target, float value) {
            float initialValue, computedValue;
            do {
                initialValue = target;
                computedValue = initialValue + value;
            } while (initialValue != Interlocked.CompareExchange(ref target,
                computedValue, initialValue));
        }
    }
}