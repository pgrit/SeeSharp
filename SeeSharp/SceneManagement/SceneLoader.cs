using System.Security.Cryptography;

namespace SeeSharp.SceneManagement;

/// <summary>
/// Reference to a scene file that gets loaded the first time it is used
/// and then cached for future reuse
/// </summary>
public class SceneLoader(FileInfo sceneFile, FileInfo blendFile, string name) : IDisposable
{
    Scene scene;
    private bool disposedValue;
    string importFile = blendFile + ".import";

    /// <summary>
    /// Loads the scene from file or retrieves the cached version.
    /// On each access, a check is performed for changes in the blender source file.
    /// </summary>
    public Scene Scene
    {
        get
        {
            // Reload if there are changes in the .blend file
            if (blendFile.Exists && ImportBlendFile())
            {
                scene.Dispose();
                scene = null;
            }

            scene = scene ?? Scene.LoadFromFile(sceneFile.FullName);
            scene.Name = name;
            return scene;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                scene.Dispose();
                scene = null;
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    bool NeedsReimport()
    {
        if (!File.Exists(importFile))
        {
            Logger.Log($"Scene {name}: importing .blend for the first time", Verbosity.Info);
            return true;
        }

        var lines = File.ReadAllLines(importFile);
        if (lines.Length < 2)
        {
            Logger.Log(
                $"Scene {name}: corrupted import file, reimporting .blend",
                Verbosity.Warning
            );
            return true;
        }

        long timestamp = long.Parse(lines[0]);
        string md5 = lines[1];

        long newTime = blendFile.LastWriteTime.ToFileTime();
        if (newTime == timestamp)
        {
            Logger.Log(
                $"Scene {name}: no reimport, last known write time is current",
                Verbosity.Debug
            );
            return false;
        }

        var newMd5 = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(blendFile.FullName)));
        if (md5 == newMd5)
        {
            Logger.Log($"Scene {name}: no reimport, md5 matches last known value", Verbosity.Debug);
            return false;
        }

        Logger.Log($"Scene {name}: reimporting .blend - md5 changed", Verbosity.Info);
        return true;
    }

    bool ImportBlendFile()
    {
        bool needsReimport = NeedsReimport();
        if (!needsReimport && sceneFile.Exists)
            return false;

        if (!BlenderImporter.Import(blendFile.FullName, sceneFile.FullName))
        {
            Logger.Error(
                $"Scene {name}: Blender import failed. Check if Blender is in PATH, "
                    + "the correct version is used, and the SeeSharp Plugin is installed. "
                    + "Resuming with old .json (if it exists)..."
            );
            return false;
        }

        if (!sceneFile.Exists)
        {
            Logger.Error(
                $"Scene {name}: Blender did not create a new scene file. Likely due to an error in the exporter, "
                    + "try manual export and check for error messages."
            );
        }

        long time = blendFile.LastWriteTime.ToFileTime();
        var md5 = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(blendFile.FullName)));
        File.WriteAllLines(importFile, [time.ToString(), md5]);

        return true;
    }
}