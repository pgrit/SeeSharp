using System.Runtime.CompilerServices;

namespace SeeSharp.Common;

/// <summary>
/// Verbosity level to hide / show less important messages
/// </summary>
public enum Verbosity
{
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

public interface ILogOutput
{
    void Write(Verbosity verbosity, string message);
}

/// <summary>
/// A simple command line logger that colors and tags messages based on their type. Verbosity can be
/// set to control which types are displayed.
/// </summary>
public static class Logger
{
    /// <summary>
    /// Minimum level of verbosity a message needs to have to be displayed. Default is
    /// <see cref="Verbosity.Info"/>.
    /// </summary>
    public static Verbosity Verbosity
    {
        get => verbosity;
        set => verbosity = value;
    }
    static Verbosity verbosity = Verbosity.Info;

    static readonly List<ILogOutput> logWriters = [];

    public static void AddOutput(ILogOutput output) => logWriters.Add(output);

    public static void RemoveOutput(ILogOutput output) => logWriters.Remove(output);

    /// <summary>
    /// Prints a log message with appropriate coloring if the verbosity level matches.
    /// </summary>
    public static void Log(
        string message,
        Verbosity verbosity = Verbosity.Info,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        string caller = $"{memberName} in {Path.GetFileName(sourceFilePath)}:{sourceLineNumber}";
        foreach (var l in logWriters)
        {
            l.Write(verbosity, message + $" -- {caller}");
        }

        if (Verbosity < verbosity)
            return;

        // Reduce threading issues with color changes. Can still be problematic if a custom Console.Out
        // is used that does not synchronize on itself.
        lock (Console.Out)
        {
            switch (verbosity)
            {
                case Verbosity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("⛔ Error: ");
                    break;
                case Verbosity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("⚠️ Warning: ");
                    break;
                case Verbosity.Info:
                    Console.Write("💡 Info: ");
                    break;
                case Verbosity.Debug:
                    Console.Write("[DEBUG] ");
                    break;
            }
            Console.WriteLine(message + $" -- {caller}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Calls <see cref="Log"/> with verbosity level set to <see cref="Verbosity.Error"/>
    /// </summary>
    public static void Error(
        string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    ) => Log(message, Verbosity.Error, memberName, sourceFilePath, sourceLineNumber);

    /// <summary>
    /// Calls <see cref="Log"/> with verbosity level set to <see cref="Verbosity.Warning"/>
    /// </summary>
    public static void Warning(
        string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    ) => Log(message, Verbosity.Warning, memberName, sourceFilePath, sourceLineNumber);

    /// <summary>
    /// Calls <see cref="Log"/> with verbosity level set to <see cref="Verbosity.Info"/>
    /// </summary>
    public static void Info(
        string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    ) => Log(message, Verbosity.Info, memberName, sourceFilePath, sourceLineNumber);

    /// <summary>
    /// Calls <see cref="Log"/> with verbosity level set to <see cref="Verbosity.Debug"/>
    /// </summary>
    public static void Debug(
        string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    ) => Log(message, Verbosity.Debug, memberName, sourceFilePath, sourceLineNumber);
}
