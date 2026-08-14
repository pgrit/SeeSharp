using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SeeSharp.Blender;
public static class BlenderViewer
{
    private static string? _blender;

    private static string? blenderExecutable
    {
        get
        {
            if (_blender != null)
                return _blender;

            // Check PATH
            if (IsInPath("blender"))
                _blender = "blender";

            if (IsInPath("blender.exe"))
                _blender = "blender.exe";

            // Check default Windows installation directory
            if (OperatingSystem.IsWindows())
            {
                var parentDir = @"C:\Program Files\Blender Foundation\";

                if (Directory.Exists(parentDir))
                {
                    double bestVersion = 0;

                    foreach (var dir in Directory.EnumerateDirectories(parentDir))
                    {
                        string candidate = Path.Join(dir, "blender.exe");

                        if (!File.Exists(candidate))
                            continue;

                        var match = Regex.Match(candidate, @"(\d+)\.(\d+)");

                        if (double.TryParse(match.Value, out var version))
                        {
                            if (version > bestVersion)
                            {
                                bestVersion = version;
                                _blender = candidate;
                            }
                        }
                    }
                }
            }

            return _blender;
        }
    }

    private static bool IsInPath(string exe)
    {
        if (File.Exists(exe))
            return true;

        var pathVariable = Environment.GetEnvironmentVariable("PATH");

        if (pathVariable == null)
            return false;

        foreach (var path in pathVariable.Split(Path.PathSeparator))
        {
            if (File.Exists(Path.Combine(path, exe)))
                return true;
        }

        return false;
    }

    private static Process? blenderProcess;

    public static bool Open()
    {
        // Blender is already running
        if (blenderProcess != null && !blenderProcess.HasExited)
            return true;

        // Blender couldn't be found
        if (blenderExecutable == null)
            return false;

        string python = """
            import bpy
            bpy.context.scene.path_viewer_props.enabled = True
            bpy.context.scene.cursor_sender_props.sending_enabled = True
            """;

        blenderProcess = Process.Start(new ProcessStartInfo
        {
            FileName = blenderExecutable,
            Arguments = $"--python-expr \"{python}\"",
            UseShellExecute = true
        });

        return blenderProcess != null;
    }

    public static async Task<bool> OpenAsync(BlenderEventListener listener)
    {
        // Already running
        if (blenderProcess != null && !blenderProcess.HasExited)
            return true;

        if (blenderExecutable == null)
            return false;

        string python = """
            import bpy
            bpy.context.scene.path_viewer_props.enabled = True
            bpy.context.scene.cursor_sender_props.sending_enabled = True
            """;

        blenderProcess = Process.Start(new ProcessStartInfo
        {
            FileName = blenderExecutable,
            Arguments = $"--python-expr \"{python}\"",
            UseShellExecute = true
        });

        if (blenderProcess == null)
            return false;

        // Wait until Blender connects
        await listener.WaitForBlenderAsync();

        return true;
    }
}