using SimpleImageIO;

namespace SeeSharp.Experiments {
    /// <summary>
    /// Describes a scene configuration when running experiments
    /// </summary>
    public abstract class SceneConfig {
        /// <summary>
        /// The name of the scene, used for the directory structure
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Maximum path length used when rendering the scene
        /// </summary>
        public abstract int MaxDepth { get; }

        /// <summary>
        /// Generates (or retrieves) the scene ready for rendering
        /// </summary>
        /// <returns>The generated scene</returns>
        public abstract Scene MakeScene();

        /// <summary>
        /// Renders a reference image, or retrieves a cached one
        /// </summary>
        /// <param name="width">Width of the image</param>
        /// <param name="height">Height of the image</param>
        /// <returns>The reference image</returns>
        public abstract RgbImage GetReferenceImage(int width, int height);
    }
}