using SeeSharp;
using SeeSharp.SceneManagement;

namespace SeeSharp.Blazor;

/// <summary>
/// Experiment runner that operates on a scene loaded from the <see cref="SceneRegistry" />
/// </summary>
public abstract class SceneBasedRunner : ExperimentRunner
{
    public static Scene Scene;

    public static async Task LoadScene(SceneDirectory sceneDir)
    {
        await Task.Run(() =>
        {
            lock (runnerLock)
                Scene = sceneDir.SceneLoader.Scene;
        });
        State = RunnerState.Ready;
        NotifyUpdate();
    }
}