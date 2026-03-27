using Microsoft.AspNetCore.Components;

namespace SeeSharp.Blazor;

/// <summary>
/// Use this as the base class of an <see cref="ExperimentRunner" />'s frontend
/// component to automatically re-render if the backend data changed.
/// </summary>
public class ViewComponent : ComponentBase
{
    public ViewComponent()
    {
        ExperimentRunner.OnUpdate += () =>
        {
            InvokeAsync(StateHasChanged);
        };
    }
}
