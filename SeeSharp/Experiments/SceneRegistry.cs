using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

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
            if (!directory.Exists)
                Logger.Error($"Tried to add non-existing directory as a scene source. Check if the path is correct: {directoryName}");
            else
                directories.Add(directory);
        }
    }

    /// <summary>
    /// Adds a scene directory with a path relative to the <b>source code file that this method was called from</b>.
    /// This method should only be used when running directly from source. Allows to specify asset locations
    /// within a repository, independent of the working directory during debugging / execution.
    /// </summary>
    /// <param name="relativePath">The relative path from the caller's code file</param>
    /// <param name="scriptPath">(compiler provided) path to the code file that called this method</param>
    public static void AddSourceRelativeToScript(string relativePath, [CallerFilePath] string scriptPath = null) {
        AddSource(Path.Join(Path.GetDirectoryName(scriptPath), relativePath));
    }

    /// <summary>
    /// Searches all sources for the given scene and returns the configuration retrieved from any one
    /// of the sources (not guaranteed to be a specific one in case of duplicates).
    /// </summary>
    /// <param name="name">The name of the scene. We look for a file "[source]/name/name.json</param>
    /// <param name="variant">If given, load the "name/variant/name-variant.json" file instead</param>
    /// <param name="maxDepth">Maximum path length in edges (2 = direct illumination)</param>
    /// <param name="minDepth">Minimum path length in edges (2 = direct illumination)</param>
    /// <returns>A <see cref="SceneFromFile"/> that represents the scene, if found, or null.</returns>
    public static SceneFromFile LoadScene(string name, string variant = null, int maxDepth = 100, int minDepth = 1) {
        string candidate = null;

        lock (directories) {
            foreach (DirectoryInfo dir in directories) {
                candidate = Path.Join(dir.FullName, name);
                if (Directory.Exists(candidate)) {
                    Logger.Log($"Using {name} scene from {dir.FullName}", Verbosity.Info);
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

        string blendFile = sceneFile[0..^4] + "blend";
        if (File.Exists(blendFile)) {
            ImportBlendFile(name, blendFile, sceneFile);
        }

        return new SceneFromFile(sceneFile, minDepth, maxDepth, name + (variant ?? ""));
    }

    public static IEnumerable<string> FindAvailableScenes() {
        lock (directories) {
            var dirs = directories.SelectMany(dir => dir.EnumerateDirectories());
            return dirs.Select(dir => dir.Name);
        }
    }

    static void ImportBlendFile(string name, string blendFile, string sceneFile) {
        string importDescFile = blendFile + ".import";

        bool NeedsReimport() {
            if (!File.Exists(importDescFile)) {
                Logger.Log($"Scene {name}: importing .blend for the first time", Verbosity.Info);
                return true;
            }

            var lines = File.ReadAllLines(importDescFile);
            if (lines.Length < 2) {
                Logger.Log($"Scene {name}: corrupted import file, reimporting .blend", Verbosity.Warning);
                return true;
            }

            long timestamp = long.Parse(lines[0]);
            string md5 = lines[1];

            long newTime = File.GetLastWriteTime(blendFile).ToFileTime();
            if (newTime == timestamp) {
                Logger.Log($"Scene {name}: no reimport, last known write time is current", Verbosity.Debug);
                return false;
            }

            var newMd5 = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(blendFile)));
            if (md5 == newMd5) {
                Logger.Log($"Scene {name}: no reimport, md5 matches last known value", Verbosity.Debug);
                return false;
            }

            Logger.Log($"Scene {name}: reimporting .blend - md5 changed", Verbosity.Info);
            return true;
        }

        bool needsReimport = NeedsReimport();
        if (!needsReimport && File.Exists(sceneFile))
            return;

        if (!BlenderImporter.Import(blendFile, sceneFile)) {
            Logger.Error($"Scene {name}: Blender import failed. Check if Blender is in PATH, " +
                "the correct version is used, and the SeeSharp Plugin is installed. " +
                "Resuming with old .json (if it exists)...");
            return;
        }

        if (!File.Exists(sceneFile)) {
            Logger.Error($"Scene {name}: Blender did not create a new scene file. Likely due to an error in the exporter, " +
                "try manual export and check for error messages.");
        }

        long time = File.GetLastWriteTime(blendFile).ToFileTime();
        var md5 = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(blendFile)));
        File.WriteAllLines(importDescFile, [
            time.ToString(),
            md5
        ]);
    }
}