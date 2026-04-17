using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using SeeSharp.Common;

namespace SeeSharp.Blazor;

public partial class FunctionScript : ComponentBase
{
    [Parameter]
    public string Prompt { get; set; } = "";

    [Parameter]
    public string Script { get; set; } = "";

    [Parameter]
    public EventCallback<string> ScriptChanged { get; set; }

    string usings = """
        using System.Numerics;
        using static System.MathF;
        """;

    public Func<TGlobals, TResult> Compile<TGlobals, TResult>()
    {
        var script = CSharpScript.Create<TResult>(usings + Script, globalsType: typeof(TGlobals));
        try
        {
            script.Compile();
            var runner = script.CreateDelegate();
            return globals =>
            {
                var t = runner.Invoke(globals);
                t.Wait();
                return t.Result;
            };
        }
        catch (CompilationErrorException e)
        {
            Logger.Error(string.Join(Environment.NewLine, e.Diagnostics));
            return null;
        }
    }

    private async Task OnValueChanged(ChangeEventArgs e)
    {
        Script = (string)e.Value;
        await ScriptChanged.InvokeAsync(Script);
    }
}