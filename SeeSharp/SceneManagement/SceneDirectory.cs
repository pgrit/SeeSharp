namespace SeeSharp.SceneManagement;

/// <summary>
/// Tracks a scene including its cached reference images and potentially
/// a .blend source file.
/// </summary>
public class SceneDirectory
{
    public ReferenceCache References { get; }

    /// <summary>
    /// The Blender source file for this scene, if available
    /// </summary>
    public FileInfo BlenderFile { get; }

    public string Name => Directory.Name;

    public SceneLoader SceneLoader { get; }

    public DirectoryInfo Directory { get; }

    /// <param name="file">
    /// The .json for this scene. All other data is assumed to be in the same folder
    /// </param>
    public SceneDirectory(FileInfo file)
    {
        this.Directory = file.Directory;
        BlenderFile = new(Path.ChangeExtension(file.FullName, ".blend"));
        SceneLoader = new(file, BlenderFile, Name);
        References = new(Directory, SceneLoader);
    }
}