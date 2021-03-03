using System;

namespace SeeSharp.Common {
    public enum Verbosity {
        Error,
        Warning,
        Info,
    }

    public static class Logger {
        public static Verbosity Verbosity { get => verbosity; set => verbosity = value; }
        static Verbosity verbosity = Verbosity.Info;

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
                }
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }
    }
}