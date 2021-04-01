namespace SeeSharp.Integrators {
    /// <summary>
    /// Base class for all rendering algorithms.
    /// </summary>
    public abstract class Integrator {
        /// <summary>
        /// Maximum path length for global illumination algorithms.
        /// </summary>
        public int MaxDepth { get => maxDepth; set => maxDepth = value; }
        int maxDepth = 5;

        /// <summary>
        /// Renders a scene to the frame buffer that is specified by the <see cref="Scene" /> object.
        /// </summary>
        /// <param name="scene">The scene to render</param>
        public abstract void Render(Scene scene);
    }
}
