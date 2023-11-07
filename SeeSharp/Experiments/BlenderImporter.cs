using System.Text.RegularExpressions;

namespace SeeSharp.Experiments;

static class BlenderImporter {
    static string _blender;
    static string blenderExecutable {
        get {
            if (_blender != null) return _blender;

            if (IsInPath("blender")) _blender = "blender";
            if (IsInPath("blender.exe")) _blender = "blender.exe";

            // Check if it is in any of the default installation directories
            if (OperatingSystem.IsWindows()) {
                var parentDir = @"C:\Program Files\Blender Foundation\";
                if (Directory.Exists(parentDir)) {
                    double bestVersion = 0;
                    foreach (var dir in Directory.EnumerateDirectories(parentDir)) {
                        string candidate = Path.Join(dir, "blender.exe");
                        if (File.Exists(candidate)) {
                            if (double.TryParse(Regex.Match(candidate, @"(\d+)\.(\d+)").Value, out var version)) {
                                if (version > bestVersion) {
                                    bestVersion = version;
                                    _blender = candidate;
                                }
                            }
                        }
                    }
                }
            }
            // TODO add default directories for Linux / Mac

            if (_blender != null)
                Logger.Log("Using Blender from: " + _blender);

            return _blender;
        }
    }

    static bool IsInPath(string exe) {
        if (File.Exists(exe))
            return true;

        var paths = Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator);
        foreach (var path in paths) {
            if (File.Exists(Path.Combine(path, exe)))
                return true;
        }

        return false;
    }

    public static bool Import(string blendFile, string jsonFile) {
        string python =
            $"""
            import bpy
            bpy.ops.wm.open_mainfile(filepath='{blendFile}')
            bpy.ops.export.to_seesharp(filepath='{jsonFile}')
            """;

        if (blenderExecutable == null)
            return false;

        var p = Process.Start(blenderExecutable, new string[] {
            "--background",
            "--python-expr",
            python
        });
        p.WaitForExit();
        return p.ExitCode == 0;
    }
}