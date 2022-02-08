using System.Text;

namespace SeeSharp.IO;

/// <summary>
/// Simple class allowing to mix ascii text and binary data reading
/// </summary>
internal class MixReader : BinaryReader {
    public MixReader(string path, Encoding encoding) : base(new FileStream(path, FileMode.Open, FileAccess.Read), encoding) {
        Path = path;
    }

    public string ReadLineAsString(ref bool eos) {
        StringBuilder stringBuffer = new(1024);

        eos = false;
        try {
            while (true) {
                char ch = base.ReadChar();
                if (ch == '\r') { // Windows style
                    ch = base.ReadChar();
                    if (ch == '\n') {
                        break;
                    } else {
                        stringBuffer.Append(ch);
                    }
                } else if (ch == '\n') { // Unix style
                    break;
                } else {
                    stringBuffer.Append(ch);
                }
            }
        } catch (EndOfStreamException) {
            eos = true;
        }

        if (stringBuffer.Length == 0)
            return "";
        else
            return stringBuffer.ToString();
    }

    public string Path { get; }
}
