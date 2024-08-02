using Markdig;

namespace SeeSharp.Common;

/// <summary>
/// Utility to generate a static .html page with
/// </summary>
public class HtmlReport {
    const string style = """
    <style>
        table {
            border-collapse: collapse;
        }
        td, th {
            border: none;
            padding: 4px;
        }
        tr:hover { background-color: #e7f2f1; }
        th {
            padding-top: 6px;
            padding-bottom: 6px;
            text-align: left;
            background-color: #4a96af;
            color: white;
            font-size: smaller;
        }
        body {
            font-family: system-ui;
            max-width: 1200px;
        }
    </style>
    """;

    string htmlBody = "";

    public void AddMarkdown(string text) {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        htmlBody += Markdown.ToHtml(text, pipeline);
    }

    public void AddTable(IEnumerable<IEnumerable<string>> rows) {
        htmlBody += HtmlUtil.MakeTable(rows, true);
    }

    public void AddFlipBook(FlipBook flip) {
        htmlBody += $"""<div style="display: flex;">{flip.Resize(900,800)}</div>""";
    }

    public override string ToString() {
        return HtmlUtil.MakeHTML(FlipBook.Header + style, htmlBody);
    }
}