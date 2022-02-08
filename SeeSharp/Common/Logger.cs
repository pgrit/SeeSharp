namespace SeeSharp.Common;

/// <summary>
/// Verbosity level to hide / show less important messages
/// </summary>
public enum Verbosity {
    /// <summary>
    /// Error message, almost always means that rendering cannot continue (correctly)
    /// </summary>
    Error,

    /// <summary>
    /// A warning should be used if unexpected results can occur, but rendering can still proceed safely
    /// </summary>
    Warning,

    /// <summary>
    /// Info messages typically contain statistics, notice that an operation is done, ...
    /// </summary>
    Info,

    /// <summary>
    /// Highest verbosity level, used to output debug information
    /// </summary>
    Debug,
}

/// <summary>
/// A simple command line logger that colors and tags messages based on their type. Verbosity can be
/// set to control which types are displayed.
/// </summary>
public static class Logger {
    /// <summary>
    /// Minimum level of verbosity a message needs to have to be displayed. Default is
    /// <see cref="Verbosity.Info"/>.
    /// </summary>
    public static Verbosity Verbosity { get => verbosity; set => verbosity = value; }
    static Verbosity verbosity = Verbosity.Info;

    /// <summary>
    /// Prints a log message with appropriate coloring if the verbosity level matches.
    /// </summary>
    /// <param name="message">The message to print</param>
    /// <param name="verbosity">The verbosity level of this message</param>
    public static void Log(string message, Verbosity verbosity = Verbosity.Info) {
        if (Verbosity < verbosity)
            return;

        // Reduce threading issues with color changes. Can still be problematic if a custom Console.Out
        // is used that does not synchronize on itself.
        lock (Console.Out) {
            switch (verbosity) {
                case Verbosity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("[ERROR] ");
                    break;
                case Verbosity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("[WARN] ");
                    break;
                case Verbosity.Info:
                    Console.Write("[INFO] ");
                    break;
                case Verbosity.Debug:
                    Console.Write("[DEBUG INFO] ");
                    break;
            }
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Calls <see cref="Log"/> with verbosity level set to <see cref="Verbosity.Error"/>
    /// </summary>
    public static void Error(string message) => Log(message, Verbosity.Error);

    /// <summary>
    /// Calls <see cref="Log"/> with verbosity level set to <see cref="Verbosity.Warning"/>
    /// </summary>
    public static void Warning(string message) => Log(message, Verbosity.Warning);
}