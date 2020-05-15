using System.Collections.Generic;

namespace SeeSharp.Core.Sampling {
    public class PiecewiseConstant {
        public PiecewiseConstant(float[] weights) {
            // Compute unnormalized cdf
            cdf = new List<float>(weights.Length);
            float sum = 0;
            foreach (float w in weights) {
                cdf.Add(sum += w);
            }

            // Normalize
            float total = cdf[^1];
            for (int i = 0; i < cdf.Count; ++i) {
                cdf[i] /= total;
            }

            // Force the last value to one for numerical stability
            cdf[^1] = 1.0f;
        }

        public (int, float) Sample(float primarySample) {
            // Find the index of the next greater (or exact) match in the CDF
            int idx = cdf.BinarySearch(primarySample);
            if (idx < 0) idx = ~idx;

            // Compute the relative position within the constant region
            float lo = idx == 0 ? 0 : cdf[idx - 1];
            float delta = cdf[idx] - lo;
            float relative = (primarySample - lo) / delta;

            return (idx, relative);
        }

        public float Probability(int idx) {
            if (idx > 0)
                return cdf[idx] - cdf[idx - 1];
            return cdf[idx];
        }

        List<float> cdf;
    }
}
