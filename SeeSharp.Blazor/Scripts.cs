using Microsoft.JSInterop;
using System.Reflection;

namespace SeeSharp.Blazor;

public static class Scripts
{
    static string ReadResourceText(string filename)
    {
        var assembly = typeof(Scripts).GetTypeInfo().Assembly;
        var stream = assembly.GetManifestResourceStream("SeeSharp.Blazor." + filename)
            ?? throw new FileNotFoundException("resource file not found", filename);
        return new StreamReader(stream).ReadToEnd();
    }

    /// <summary>
    /// Required in the &lt; head &gt; so that the SimpleImageIO.FlipBook can be rendered
    /// </summary>
    public static readonly string FlipBookScript =
    $$"""
    <script>
        {{SimpleImageIO.FlipBook.HeaderScript}}
        function makeFlipBook(jsonArgs, onClickObj, onClickMethodName) {
            let onClick = null;
            if (onClickObj && onClickMethodName) {
                onClick = (col, row, evt) =>
                    onClickObj.invokeMethodAsync(onClickMethodName, col, row, { ctrlKey: evt.ctrlKey })
            }

            window['flipbook']['MakeFlipBook'](jsonArgs, onClick);
        }
    </script>
    """;

    /// <summary>
    /// Utility script to add to the &lt; head &gt; to download arbitrary streams from the server
    /// </summary>
    public static readonly string DownloadScript =
    $$"""
    <script>
        window.downloadFileFromStream = async (fileName, contentStreamReference) => {
            const arrayBuffer = await contentStreamReference.arrayBuffer();
            const blob = new Blob([arrayBuffer]);
            const url = URL.createObjectURL(blob);
            const anchorElement = document.createElement('a');
            anchorElement.href = url;
            anchorElement.download = fileName ?? '';
            anchorElement.click();
            anchorElement.remove();
            URL.revokeObjectURL(url);
        }
    </script>
    """;

    public static readonly string WidgetScripts =
    $$"""
    <script>
        {{ReadResourceText("Scripts.rotationInput.js")}}
    </script>
    """;

    public static readonly string AllScripts = FlipBookScript + DownloadScript + WidgetScripts;

    /// <summary>
    /// Downloads a stream to the client with the given file name. Requires that <see cref="DownloadScript" />
    /// was added to the &lt; head &gt;
    /// </summary>
    public static async Task DownloadAsFile(IJSRuntime js, string filename, Stream stream) {
        using var r = new DotNetStreamReference(stream: stream);
        await js.InvokeVoidAsync("downloadFileFromStream", filename, r);
    }

    public static async Task DownloadAsFile(IJSRuntime js, string filename, string content) {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        await DownloadAsFile(js, filename, stream);
    }

    public static async Task DownloadAsFile(IJSRuntime js, string filename, SeeSharp.Common.HtmlReport report)
    => await DownloadAsFile(js, filename, report.ToString());

    public static async Task DownloadAsFile(this SeeSharp.Common.HtmlReport report, IJSRuntime js, string filename)
    => await DownloadAsFile(js, filename, report.ToString());
}