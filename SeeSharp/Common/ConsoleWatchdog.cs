using System.Runtime.CompilerServices;
using System.Text;

namespace SeeSharp.Common;

/// <summary>
/// A TextWriter that raises an event for each character written to the stream.
/// Can be attached to the Console.Out to monitor output from all parts of the program.
/// </summary>
internal class ConsoleWatchdog : TextWriter {
    readonly TextWriter output;
    public delegate void WriteCharEventHandler(char value);
    public event WriteCharEventHandler WriteCharEvent;
    public ConsoleWatchdog(TextWriter original) => output = original;
    public override Encoding Encoding => output.Encoding;

    [MethodImpl(MethodImplOptions.Synchronized)]
    public override void Write(char value) {
        output.Write(value);
        WriteCharEvent?.Invoke(value);
    }
}