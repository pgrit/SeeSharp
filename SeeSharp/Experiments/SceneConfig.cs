namespace SeeSharp.Experiments;

/// <summary>
/// Describes a scene configuration when running experiments
/// </summary>
public abstract class SceneConfig
{
    /// <summary>
    /// The name of the scene, used for the directory structure
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Maximum path length used when rendering the scene. DI only = 2
    /// </summary>
    public abstract int MaxDepth { get; set; }

    /// <summary>
    /// Minimum path length used when rendering the scene. No directly visible lights = 2
    /// </summary>
    public abstract int MinDepth { get; set; }

    /// <summary>
    /// Generates (or retrieves) the scene ready for rendering
    /// </summary>
    /// <returns>The generated scene</returns>
    public abstract Scene MakeScene();

    /// <summary>
    /// Renders a reference image, or retrieves a cached one.
    /// Use <see cref="GetReferenceImageDetails(int, int, bool)" /> to retrieve AOVs
    /// or query meta data like the render time.
    /// </summary>
    /// <param name="width">Width of the image</param>
    /// <param name="height">Height of the image</param>
    /// <param name="allowRender">If false, missing references are not rendered and null is returned instead</param>
    /// <returns>The reference image</returns>
    public abstract RgbImage GetReferenceImage(int width, int height, bool allowRender = true);

    /// <summary>
    /// Renders a reference image, or retrieves a cached one.
    /// </summary>
    /// <param name="width">Width of the image</param>
    /// <param name="height">Height of the image</param>
    /// <param name="allowRender">If false, missing references are not rendered and null is returned instead</param>
    /// <returns>
    /// All rendered layers present in the reference image and
    /// the .json metadata output by the rendering integrator
    /// </returns>
    public abstract (
        Dictionary<string, Image> Layers,
        string JsonMetadata
    ) GetReferenceImageDetails(int width, int height, bool allowRender = true);

    /// <summary>
    /// File path where the reference image for this scene is cached.
    /// Null if there is no cache.
    /// </summary>
    public abstract string ReferenceLocation { get; }

    /// <summary>
    /// The fully configured integrator used to render reference images for this scene.
    /// </summary>
    public abstract Integrator ReferenceIntegrator { get; set; }

    /// <summary>
    /// Queries all available reference image configurations for this scene
    /// </summary>
    public abstract IEnumerable<(
        int Width,
        int Height,
        int MinDepth,
        int MaxDepth
    )> AvailableReferences { get; }
}
