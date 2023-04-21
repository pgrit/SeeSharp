using System.Linq;

namespace SeeSharp.Experiments;

/// <summary>
/// Singleton that manages all available test scenes. Folders with test scenes can be registered as
/// "sources" and will then be available everywhere.
/// </summary>
public static class SceneRegistry {
    static readonly HashSet<DirectoryInfo> directories = new();

    static SceneRegistry() {
        string env = Environment.GetEnvironmentVariable("SEESHARP_SCENE_DIRS");
        if (string.IsNullOrEmpty(env))
            return;

        foreach (string path in env.Split(Path.PathSeparator)) {
            if (!Directory.Exists(path)) {
                Logger.Warning($"Skipping scene directory '{path}' because it does not exist.");
                continue;
            }
            AddSource(path);
            Logger.Log($"Added scene directory specified in the environment: {path}", Verbosity.Debug);
        }
    }

    /// <summary>
    /// Adds a directory to the list of sources. The contents must adhere the expected structure:
    /// Each subdirectory represents a scene, the name of the subdirectory is that of the scene.
    /// It should contain one or more .json files with the scene data, any meshes and textures, and
    /// (if available) pre-rendered reference images at various resolutions and maximum path lengths.
    /// </summary>
    /// <param name="directoryName">A full or relative path to an existing directory</param>
    public static void AddSource(string directoryName) {
        lock (directories) {
            DirectoryInfo directory = new(directoryName);
            Debug.Assert(directory.Exists);
            directories.Add(directory);
        }
    }

    /// <summary>
    /// Searches all sources for the given scene and returns the configuration retrieved from any one
    /// of the sources (not guaranteed to be a specific one in case of duplicates).
    /// </summary>
    /// <param name="name">The name of the scene. We look for a file "[source]/name/name.json</param>
    /// <param name="variant">If given, load the "name/variant/name-variant.json" file instead</param>
    /// <param name="maxDepth">If given, overrides the default maximum path length</param>
    /// <param name="minDepth">If given, overrides the default minimum path length</param>
    /// <returns>A <see cref="SceneFromFile"/> that represents the scene, if found, or null.</returns>
    public static SceneFromFile LoadScene(string name, string variant = null, int? maxDepth = null,
                                          int? minDepth = null) {
        string candidate = null;

        lock (directories) {
            foreach (DirectoryInfo dir in directories) {
                candidate = Path.Join(dir.FullName, name);
                if (Directory.Exists(candidate)) {
                    Logger.Log($"Using {name} scene from {dir.FullName}", Verbosity.Debug);
                    break;
                }
                candidate = null;
            }
        }

        if (candidate == null) {
            Logger.Log($"Scene \"{name}\" not found in any registered repository!", Verbosity.Error);
            foreach (DirectoryInfo dir in directories)
                Logger.Log($"Searched: {dir.FullName}", Verbosity.Info);
            return null;
        }

        string sceneFile = (variant == null)
            ? Path.Join(candidate, $"{name}.json")
            : Path.Join(candidate, variant, $"{name}-{variant}.json");

        return new SceneFromFile(sceneFile, minDepth ?? 1, maxDepth ?? 5, name + (variant ?? ""));
    }

    public static IEnumerable<string> FindAvailableScenes() {
        lock (directories) {
            var dirs = directories.SelectMany(dir => dir.EnumerateDirectories());
            return dirs.Select(dir => dir.Name);
        }
    }
}