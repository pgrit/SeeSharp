namespace SeeSharp.Integrators {
    /// <summary>
    /// Base class for all rendering algorithms.
    /// </summary>
    public abstract class Integrator {
        /// <summary>
        /// Maximum path length for global illumination algorithms. Default is 5.
        /// </summary>
        public int MaxDepth { get => maxDepth; set => maxDepth = value; }
        int maxDepth = 5;

        /// <summary>
        /// Minimum length (in edges) of a path that can contribute to the image. If set to 2, e.g., directly
        /// visible lights are not rendered. Default is 1.
        /// </summary>
        public int MinDepth { get => minDepth; set => minDepth = value; }
        int minDepth = 1;

        /// <summary>
        /// Renders a scene to the frame buffer that is specified by the <see cref="Scene" /> object.
        /// </summary>
        /// <param name="scene">The scene to render</param>
        public abstract void Render(Scene scene);
    }
}
