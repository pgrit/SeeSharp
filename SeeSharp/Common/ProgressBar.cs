using System.Runtime.CompilerServices;
using System.Text;

namespace SeeSharp.Common;

/// <summary>
/// A simple ASCII progress bar for the console. Estimates and displays the remaining time.
/// Automatically detects if other parts of the application wrote to the console and makes sure not to
/// overwrite anything. If the console does not support changing previous output (e.g., a file or the
/// VS code debug console), the bar is written in a new line whenever it grew visibly (like from [#---]
/// to [##--]).
/// </summary>
public class ProgressBar {
    readonly int numBlocks = 50;
    readonly bool displayTime;
    readonly bool displayWork;
    readonly string prefix;

    int total;
    int done;

    double accumSeconds;
    double estimTotalSeconds;
    double secondsPerUnit;
    int numUpdates;

    Stopwatch timer = new();

    readonly ConsoleWatchdog watcher;
    bool outputIsDirty;
    bool dirtEndsInNewline;

    string curText = "";
    int activeBlocks;
    readonly static bool supportsRewrite;

    public static bool Silent = false;

    static ProgressBar() {
        // Check if the console / output stream supports altering previous output
        // e.g., not (always) the case if its forwarded to a file or the VS Code Debug Console is used.
        try {
            (int left, int top) = Console.GetCursorPosition();
            Console.SetCursorPosition(left, top);

            if (Console.WindowHeight == 0 || Console.WindowWidth == 0)
                supportsRewrite = false;
            else
                supportsRewrite = true;
        } catch (Exception) {
            supportsRewrite = false;
        }
    }

    /// <summary>
    /// Initializes a new progress bar for a given amount of work. The amount of work should be specified
    /// as a number of (roughly) equally expensive steps.
    /// </summary>
    /// <param name="numBlocks">Number of blocks to display in the ASCII bar</param>
    /// <param name="displayWork">If true, displays the amount of total steps and the performed steps</param>
    /// <param name="displayTime">If true, displays the elapsed time and predicted total time</param>
    /// <param name="prefix">Text displayed in front of the progress bar</param>
    public ProgressBar(int numBlocks = 20, bool displayWork = true, bool displayTime = true, string prefix = "") {
        this.numBlocks = numBlocks;
        this.displayWork = displayWork;
        this.displayTime = displayTime;
        this.prefix = prefix;

        // Detect if our output has been "polluted" by other printed messages
        watcher = new(Console.Out);
        Console.SetOut(watcher);
        watcher.WriteCharEvent += OnOtherOutput;
    }

    /// <summary>
    /// Notifies that the process has started.
    /// </summary>
    /// <param name="totalWork">Amount of steps that are performed in total (e.g., render iterations)</param>
    public void Start(int totalWork) {
        total = totalWork;
        done = 0;
        accumSeconds = 0.0;
        estimTotalSeconds = 0.0;
        secondsPerUnit = 0.0;
        numUpdates = 0;
        timer.Restart();

        lock (Console.Out) UpdateText();
    }

    /// <summary>
    /// Updates the progress bar after some work has been performed. Thread-safe.
    /// </summary>
    /// <param name="amount">How many steps have been performed</param>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void ReportDone(int amount) {
        done += amount;

        Debug.Assert(timer.IsRunning, "ProgressBar.Start() has not been called");

        double elapsedSeconds = timer.ElapsedMilliseconds / 1000.0f;
        timer.Restart();
        accumSeconds += elapsedSeconds;

        // Update the cost statistics
        secondsPerUnit *= numUpdates / (numUpdates + 1.0f);
        numUpdates++;
        secondsPerUnit += (elapsedSeconds / amount) / numUpdates;

        estimTotalSeconds = secondsPerUnit * total;

        lock (Console.Out) UpdateText();
    }

    static string MakeTimeString(double seconds) {
        if (seconds > 60 * 120) { // hours
            return $"{seconds / (60 * 60):0.##}h";
        } else if (seconds > 120) { // minutes
            return $"{seconds / 60:0.##}min";
        } else { // seconds
            return $"{seconds:0.##}s";
        }
    }

    void OnOtherOutput(char value) {
        outputIsDirty = true;
        if (value == '\n') dirtEndsInNewline = true;
        else dirtEndsInNewline = false;
    }

    void UpdateText() {
        if (Silent) return;

        double fractionDone = done / (double)total;
        int nextActiveBlocks = (int)(fractionDone * numBlocks);
        nextActiveBlocks = Math.Min(nextActiveBlocks, numBlocks);

        // If rewriting is not possible, only write the next line if we added a new block in the bar
        // or after the first work item was completed
        if (!supportsRewrite && activeBlocks == nextActiveBlocks && done > 1 && curText.Length > 0)
            return;

        activeBlocks = nextActiveBlocks;

        // Suspend our event handler
        watcher.WriteCharEvent -= OnOtherOutput;

        // Remove the current text
        if (!outputIsDirty && curText.Length > 0 && supportsRewrite) {
            (_, int top) = Console.GetCursorPosition();

            if (top <= 0) {
                // Some nasty consoles (VS code debug on Linux for example) report incorrectly that
                // you can write multiple lines when in fact you can't. In that case, the top position
                // reported here will be 0 (which should never happen as we wrote an entire line).
                if (outputIsDirty && !dirtEndsInNewline) {
                    Console.WriteLine();
                }
            } else {
                // If the last output got split over multiple lines, accomodate for that.
                int numLines = (curText.Length / Console.WindowWidth + 1);
                Console.SetCursorPosition(0, top - numLines);
            }
        } else if (outputIsDirty && !dirtEndsInNewline)
            Console.WriteLine();

        // Create the progress bar
        StringBuilder next = new();
        next.Append(prefix);
        next.Append(" |");
        next.Append('█', activeBlocks);
        next.Append(' ', numBlocks - activeBlocks);
        next.Append('|');
        next.Append($" {(int)(fractionDone * 100)}%");

        if (displayWork)
            next.Append($" ({done}/{total})");

        if (displayTime) {
            next.Append(" (" + MakeTimeString(accumSeconds) + " / ");
            next.Append(MakeTimeString(estimTotalSeconds) + ")");
        }

        // If we previously had a longer text: add spaces to "delete" it
        int delta = curText.Length - next.Length;
        if (delta > 0)
            next.Append(' ', delta);

        curText = next.ToString();
        Console.WriteLine(curText);

        // Re-enable our console watchdog
        outputIsDirty = false;
        watcher.WriteCharEvent += OnOtherOutput;
    }
}