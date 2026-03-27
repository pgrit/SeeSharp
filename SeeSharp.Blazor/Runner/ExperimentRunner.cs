using SeeSharp.Common;

namespace SeeSharp.Blazor;

/// <summary>
/// Background service that manages the currently running experiment.
/// </summary>
public abstract class ExperimentRunner
{
    [Flags]
    public enum RunnerState
    {
        Ready = 1,
        Running = 2,
        ResultsAvailable = 4,
    }

    public static RunnerState State { get; private set; } = RunnerState.Ready;

    public static bool IsReady => State.HasFlag(RunnerState.Ready);
    public static bool IsRunning => State.HasFlag(RunnerState.Running);
    public static bool HasResults => State.HasFlag(RunnerState.ResultsAvailable);

    protected static Lock runnerLock = new();

    /// <summary>
    /// Invoke this event to sync all connected client views to the new
    /// experiment state.
    /// </summary>
    public static event Action OnUpdate;

    public static ExperimentRunner Active
    {
        get;
        set
        {
            field = value;
            OnUpdate.Invoke();
        }
    }

    protected static void NotifyUpdate() => OnUpdate.Invoke();

    public static async Task Run()
    {
        lock (runnerLock)
        {
            if (State.HasFlag(RunnerState.Running))
                return;
            State = RunnerState.Running;
        }

        NotifyUpdate();

        await Task.Run(() =>
        {
            lock (runnerLock)
            {
                try
                {
                    Active.RunExperiment();
                    State = RunnerState.ResultsAvailable | RunnerState.Ready;
                }
                catch (Exception e)
                {
                    // If we errored out, there are no results but we are ready to re-run
                    Logger.Error($"Exception during experiment run: {e}");
                    State = RunnerState.Ready;
                }
                finally
                {
                    NotifyUpdate();
                }
            }
        });
    }

    protected abstract void RunExperiment();
}
