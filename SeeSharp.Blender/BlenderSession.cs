namespace SeeSharp.Blender;

public sealed class BlenderSession : IDisposable
{
    private const string RootFolderName = "SeeSharp";
    private const string LockFileName = "session.lock";
    public string Directory { get; }
    private bool _disposed;

    public BlenderSession()
    {
        var rootDirectory = Path.Combine(
            Path.GetTempPath(),
            RootFolderName);

        System.IO.Directory.CreateDirectory(rootDirectory);

        // Clean up sessions left behind by previous crashed processes.
        CleanupStaleSessions(rootDirectory);

        var sessionId = Guid.NewGuid().ToString("N");

        Directory = Path.Combine(
            rootDirectory,
            sessionId);

        System.IO.Directory.CreateDirectory(Directory);

        // Mark this session as active.
        File.WriteAllText(
            Path.Combine(Directory, LockFileName),
            Environment.ProcessId.ToString());
    }

    private static void CleanupStaleSessions(string rootDirectory)
    {
        foreach (var directory in System.IO.Directory.EnumerateDirectories(rootDirectory))
        {
            try
            {
                var lockFile = Path.Combine(directory, LockFileName);

                if (!File.Exists(lockFile))
                {
                    // No lock file → probably an incomplete/crashed session.
                    System.IO.Directory.Delete(directory, recursive: true);
                    continue;
                }

                var content = File.ReadAllText(lockFile);

                if (!int.TryParse(content, out var processId))
                {
                    System.IO.Directory.Delete(directory, recursive: true);
                    continue;
                }

                if (!IsProcessRunning(processId))
                {
                    // The process that owned this session no longer exists.
                    System.IO.Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception)
            {
                // Don't let failure to clean one directory
                // prevent SeeSharp from starting.
            }
        }
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);

            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            // Process doesn't exist.
            return false;
        }
        catch
        {
            // If we cannot determine the state, leave it alone.
            return true;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(
                    Directory,
                    recursive: true);
            }
        }
        catch
        {
            // Optional: log this.
        }
    }
}