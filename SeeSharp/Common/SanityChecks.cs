namespace SeeSharp.Common;

/// <summary>
/// Provides common sanity checks like checking if a vector is normalized
/// </summary>
public static class SanityChecks {
    /// <summary>
    /// Asserts that the given direction is normalized, i.e., has a length of one.
    /// </summary>
    public static void IsNormalized(Vector3 dir, float threshold = 0.001f) {
#if DEBUG
        float len = dir.Length();
        bool normalized = MathF.Abs(len - 1) < 0.001f;
        Debug.Assert(normalized, "Vector is not normalized!");
#endif
    }
}