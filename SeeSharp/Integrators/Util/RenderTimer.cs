namespace SeeSharp.Integrators.Util;

/// <summary>
/// Utility to track the time spent rendering and the frame buffer IO overhead separately.
/// Can be used to achieve fair equal-time renderings.
/// </summary>
public class RenderTimer {
    /// <summary>
    /// The total time spent processing the frame buffer so far, in milliseconds.
    /// </summary>
    public long FrameBufferTime { get; private set; }

    /// <summary>
    /// The total time spent in the actual rendering code so far, in milliseconds.
    /// </summary>
    public long RenderTime { get; private set; }

    /// <summary>
    /// The estimated cost of a single iteration, in milliseconds.
    /// </summary>
    public long PerIterationCost { get; private set; }

    /// <summary>
    /// The total duration of the last iteration (including all overheads) in seconds. This should
    /// only be used to update progress bars etc, not for equal time rendering.
    /// </summary>
    public double CurrentIterationSeconds { get; private set; }

    Stopwatch timer = new();
    int numIter = 0;

    /// <summary>
    /// Adds the elapsed time to the frame buffer cost and resets the timer
    /// </summary>
    public void EndFrameBuffer() {
        FrameBufferTime += timer.ElapsedMilliseconds;
        CurrentIterationSeconds += timer.Elapsed.TotalSeconds;
        timer.Restart();
    }

    /// <summary>
    /// Adds the elapsed time to the rendering cost and resets the timer
    /// </summary>
    public void EndRender() {
        RenderTime += timer.ElapsedMilliseconds;
        CurrentIterationSeconds += timer.Elapsed.TotalSeconds;
        timer.Restart();
    }

    /// <summary>
    /// Starts a new timer for the next iteration
    /// </summary>
    public void StartIteration() {
        CurrentIterationSeconds = 0;
        numIter++;
        timer.Restart();
    }

    /// <summary>
    /// Updates statistics at the end of each iteration
    /// </summary>
    public void EndIteration() => PerIterationCost = RenderTime / numIter;
}
