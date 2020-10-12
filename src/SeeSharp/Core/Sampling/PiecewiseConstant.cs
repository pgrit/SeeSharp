using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SeeSharp.Core.Sampling {
    public class PiecewiseConstant {
        public PiecewiseConstant(ReadOnlySpan<float> weights) {
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
                Debug.Assert(float.IsFinite(cdf[i]));
            }

            // Force the last value to one for numerical stability
            cdf[^1] = 1.0f;
        }

        public PiecewiseConstant(float[] weights) : this(new ReadOnlySpan<float>(weights)) { }

        /// <summary>
        /// Transforms a primary sample to one distributed according to the
        /// piecewise constant density encoded by this object.
        /// </summary>
        /// <param name="primarySample">A primary sample in [0,1]</param>
        /// <returns>The bin index, and the relative position within the bin.</returns>
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
