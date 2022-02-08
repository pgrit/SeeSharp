namespace SeeSharp.IO;

/// <summary>
/// Represents errors that occur during loading of external meshes.
/// </summary>
public class MeshLoadException : Exception {
    /// <summary>
    /// Path to the file which was attempted to be loaded.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Initializes a new instance of the MeshLoadException class with a specified error message and file path.
    /// </summary>
    /// <param name="message">The error message string.</param>
    /// <param name="path">A path to the file which was attempted to be loaded.</param>
    public MeshLoadException(string message, string path) : base(message + $" ({path})") {
        Path = path;
    }

    /// <summary>
    /// Initializes a new instance of the MeshLoadException class with a specified error message, a file path
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message string.</param>
    /// <param name="path">A path to the file which was attempted to be loaded.</param>
    /// <param name="inner">The exception that is the cause of the current exception,
    /// or a null reference if no inner exception is specified.</param>
    public MeshLoadException(string message, string path, Exception inner) : base(message + $" ({path})", inner) {
        Path = path;
    }
}