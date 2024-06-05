namespace SeeSharp.Integrators.Bidir;

/// <summary>
/// Helper class to select random vertices from a path vertex cache.
/// Ignores the first vertices of all light paths (the ones on the lights).
/// </summary>
/// <param name="cache">The light subpath cache</param>
public struct VertexSelector(LightPathCache cache) {
    /// <summary>
    /// Randomly selects a light subpath vertex
    /// </summary>
    /// <param name="rng">RNG to use</param>
    /// <returns>Index of the path, index of the vertex along the path</returns>
    public (int, int) Select(ref RNG rng) {
        int idx = rng.NextInt(Count);
        return (-1, idx);
    }

    /// <summary>
    /// Number of light subpath vertices that can be connected to
    /// </summary>
    public int Count => cache.NumVertices;

    LightPathCache cache = cache;
}
