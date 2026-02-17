namespace SeeSharp.Experiments;

/// <summary>
/// Describes a scene configuration when running experiments
/// </summary>
public class SceneConfig
{
    /// <summary>
    /// The name of the scene, used for the directory structure in the experiment results
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Maximum path length used when rendering the scene. DI only = 2
    /// </summary>
    public int MaxDepth { get; set; }

    /// <summary>
    /// Minimum path length used when rendering the scene. No directly visible lights = 2
    /// </summary>
    public int MinDepth { get; set; }

    /// <summary>
    /// Shortcut for <see cref="SceneLoader.Scene" /> to import / load the scene
    /// </summary>
    public Scene Scene => SceneDirectory.SceneLoader.Scene;

    public SceneDirectory SceneDirectory { get; set; }

    public SceneConfig(SceneDirectory sceneDir, int maxDepth = 100, int minDepth = 1, string name = null)
    {
        MinDepth = minDepth;
        MaxDepth = maxDepth;
        SceneDirectory = sceneDir;
        if (string.IsNullOrEmpty(name))
        {
            name = SceneDirectory.Name;
        }
    }
}