namespace SeeSharp.Integrators;

/// <summary>
/// Base class for all rendering algorithms.
/// </summary>
public abstract class Integrator {
    /// <summary>
    /// Maximum path length for global illumination algorithms. Default is 100.
    /// </summary>
    public int MaxDepth { get; set; } = 100;

    /// <summary>
    /// Minimum length (in edges) of a path that can contribute to the image. If set to 2, e.g., directly
    /// visible lights are not rendered. Default is 1.
    /// </summary>
    public int MinDepth { get; set; } = 1;

    /// <summary>
    /// Renders a scene to the frame buffer that is specified by the <see cref="Scene" /> object.
    /// </summary>
    /// <param name="scene">The scene to render</param>
    public abstract void Render(Scene scene);

    /// <summary>
    /// Re-renders a pixel as it was rendered in a specific iteration.
    /// </summary>
    /// <returns>The paths that contributed to this pixel as a connected graph</returns>
    public virtual (PathGraph Graph, RgbColor Estimate) ReplayPixel(Scene scene, Pixel pixel, int iteration)
    => throw new NotSupportedException("This integrator does not implement path replay");
}
