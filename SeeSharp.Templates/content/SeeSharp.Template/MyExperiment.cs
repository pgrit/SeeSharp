using System.Runtime.CompilerServices;

namespace SeeSharp.Template;

class MyExperiment : Experiment {
    public override List<Method> MakeMethods() => [
        new("PathTracer", new PathTracer() { TotalSpp = 4 }),
        new("Vcm", new VertexConnectionAndMerging() { NumIterations = 2 })
    ];

    public override void OnDone(string workingDirectory, IEnumerable<string> sceneNames) {
        // Run Python script to generate overview figure
        try {
            RunPythonScript("MakeFigure.py", $"{workingDirectory} {string.Join(',', sceneNames)} {string.Join(',', MethodNames)}");
        } catch(Exception) {
            Logger.Warning("Running figuregen script with Python failed");
        }
    }

    static void RunPythonScript(string scriptName, string arguments, [CallerFilePath] string callerPath = null) {
        string scriptPath = Path.Join(Path.GetDirectoryName(callerPath), scriptName);
        Process.Start("python", scriptPath + " " + arguments).WaitForExit();
    }
}