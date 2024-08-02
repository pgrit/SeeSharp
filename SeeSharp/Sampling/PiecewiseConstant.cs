namespace SeeSharp.Sampling;

/// <summary>
/// A piece-wise constant PDF / discrete probability to sample from
/// </summary>
public class PiecewiseConstant {
    /// <summary>
    /// Initializes the piece-wise constant pdf over the [0, 1] domain, where each piece has the same length.
    /// The given weights are normalized and the CDF is computed.
    /// </summary>
    /// <param name="weights">The non-normalized weights of each bin</param>
    public PiecewiseConstant(ReadOnlySpan<float> weights) {
        // Compute unnormalized cdf
        cdf = new List<float>(weights.Length);
        float sum = 0;
        foreach (float w in weights) {
            cdf.Add(sum += w);
        }

        // Normalize
        float total = cdf[^1];
        for (int i = 0; i < cdf.Count && total > 0.0f; ++i) {
            cdf[i] /= total;
            Debug.Assert(float.IsFinite(cdf[i]));
        }

        // Force the last value to one for numerical stability
        cdf[^1] = 1.0f;
    }

    /// <summary>
    /// Initializes the piece-wise constant pdf over the [0, 1] domain, where each piece has the same length.
    /// The given weights are normalized and the CDF is computed.
    /// </summary>
    /// <param name="weights">The non-normalized weights of each bin</param>
    public PiecewiseConstant(params float[] weights) : this(new ReadOnlySpan<float>(weights)) { }

    /// <summary>
    /// Transforms a primary sample to one distributed according to the
    /// piecewise constant density encoded by this object.
    /// </summary>
    /// <param name="primarySample">A primary sample in [0,1]</param>
    /// <returns>The bin index, and the relative position within the bin.</returns>
    public (int BinIndex, float RelativePosition) Sample(float primarySample) {
        // Find the index of the next greater (or exact) match in the CDF
        int idx = cdf.BinarySearch(primarySample);
        if (idx < 0) idx = ~idx;
        else // Make sure we find the first element, some might have zero probability!
            for (; idx > 0 && cdf[idx - 1] == primarySample; --idx) { }

        // Compute the relative position within the constant region
        float lo = idx == 0 ? 0 : cdf[idx - 1];
        float delta = cdf[idx] - lo;
        float relative = (primarySample - lo) / delta;

        return (idx, relative);
    }

    /// <summary>
    /// Performs the inverse of the transform used by <see cref="Sample"/>.
    /// </summary>
    /// <param name="idx">The bin index</param>
    /// <param name="relative">Position within the bin</param>
    /// <returns>The primary sample that is mapped to this position</returns>
    public float SampleInverse(int idx, float relative) {
        float lo = idx == 0 ? 0 : cdf[idx - 1];
        float delta = cdf[idx] - lo;
        return delta * relative + lo;
    }

    /// <param name="idx">Index of a bin</param>
    /// <returns>The probability that any sampled point lies within the bin</returns>
    public float Probability(int idx) {
        if (idx > 0)
            return cdf[idx] - cdf[idx - 1];
        return cdf[idx];
    }

    /// <param name="idx">Index of a bin in the piecewise distribution</param>
    /// <returns>The CDF value of the given bin</returns>
    public float CumulativeProbability(int idx) => cdf[idx];

    List<float> cdf;
}
