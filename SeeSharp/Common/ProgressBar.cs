using System;
using System.Text;

namespace SeeSharp.Common {
    /// <summary>
    /// A simple ASCII progress bar for the console. Estimates and displays the remaining time.
    /// Automatically detects if other parts of the application wrote to the console and makes sure not to
    /// overwrite anything. If the console does not support changing previous output (e.g., a file or the
    /// VS code debug console), the bar is written in a new line whenever it grew visibly (like from [#---]
    /// to [##--]).
    /// </summary>
    public class ProgressBar {
        int numBlocks = 50;
        bool displayTime;
        bool displayWork;

        int total;
        int done;

        double accumSeconds;
        double estimTotalSeconds;
        double secondsPerUnit;
        int numUpdates;

        ConsoleWatchdog watcher;
        bool outputIsDirty;
        bool dirtEndsInNewline;

        string curText = "";
        int activeBlocks;
        bool supportsRewrite;

        public ProgressBar(int totalWork, int numBlocks = 20, bool displayWork = true, bool displayTime = true) {
            total = totalWork;
            this.numBlocks = numBlocks;
            this.displayWork = displayWork;
            this.displayTime = displayTime;

            // Detect if our output has been "polluted" by other printed messages
            watcher = new(Console.Out);
            Console.SetOut(watcher);
            watcher.WriteCharEvent += OnOtherOutput;

            // Check if the console / output stream supports altering previous output
            // e.g., not (always) the case if its forwarded to a file or the VS Code Debug Console is used.
            try {
                (int left, int top) = Console.GetCursorPosition();
                Console.SetCursorPosition(left, top);
                supportsRewrite = true;
            } catch (Exception) {
                supportsRewrite = false;
            }

            if (Console.WindowHeight == 0 || Console.WindowWidth == 0)
                supportsRewrite = false;
        }

        public void ReportDone(int amount, double elapsedSeconds) {
            done += amount;
            accumSeconds += elapsedSeconds;

            // Update the cost statistics
            secondsPerUnit *= numUpdates / (numUpdates + 1.0f);
            numUpdates++;
            secondsPerUnit += (elapsedSeconds / amount) / numUpdates;

            estimTotalSeconds = secondsPerUnit * total;

            UpdateText();
        }

        string MakeTimeString(double seconds) {
            if (seconds > 60 * 120) { // hours
                return $"{seconds / (60 * 60) :0.##}h";
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
            double fractionDone = done / (double)total;
            int nextActiveBlocks = (int)(fractionDone * numBlocks);
            nextActiveBlocks = Math.Min(nextActiveBlocks, numBlocks);

            // Only write the next line if we added a new block in the bar
            if (!supportsRewrite && activeBlocks == nextActiveBlocks && curText.Length > 0)
                return;

            activeBlocks = nextActiveBlocks;

            // Suspend our event handler
            watcher.WriteCharEvent -= OnOtherOutput;

            // Remove the current text
            if (!outputIsDirty && curText.Length > 0 && supportsRewrite) {
                (_, int top) = Console.GetCursorPosition();

                // If the last output got split over multiple lines, accomodate for that.
                int numLines = (curText.Length / Console.WindowWidth + 1);

                Console.SetCursorPosition(0, top - numLines);
            } else if (outputIsDirty && !dirtEndsInNewline)
                Console.WriteLine();

            // Create the progress bar
            StringBuilder next = new();
            next.Append('[');
            next.Append('#', activeBlocks);
            next.Append('-', numBlocks - activeBlocks);
            next.Append(']');
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
}