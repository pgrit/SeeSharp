using System.Runtime.CompilerServices;

namespace SeeSharp.Template;

class MyExperiment : Experiment
{
    public override List<Method> MakeMethods() =>
        [
            new("Path tracing", new PathTracer() { TotalSpp = 4 }),
            new("VCM", new VertexConnectionAndMerging() { SampleCount = 2 }),
        ];

    public override void OnDone(
        string workingDirectory,
        IEnumerable<string> sceneNames,
        IEnumerable<float> sceneExposures
    )
    {
        // Run Python script to generate overview figure
        try
        {
            var scenes = sceneNames.Zip(sceneExposures).Select(kv => $"{kv.First};{kv.Second}");
            string args =
                $"\"{workingDirectory}\" \"{string.Join(',', scenes)}\" \"{string.Join(',', MethodNames)}\"";
            RunPythonScript("MakeFigure.py", args);
        }
        catch (Exception)
        {
            Logger.Warning("Running figuregen script with Python failed");
        }
    }

    static void RunPythonScript(
        string scriptName,
        string arguments,
        [CallerFilePath] string callerPath = null
    )
    {
        string scriptPath = Path.Join(Path.GetDirectoryName(callerPath), scriptName);
        Logger.Log($"python \"{scriptPath}\" {arguments}");
        Process.Start("python", $"{scriptPath} {arguments}").WaitForExit();
    }
}
