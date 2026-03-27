namespace SeeSharp.Blazor;

/// <summary>
/// Must be added to the Razor component that is responsible for
/// rendering an <see cref="ExperimentRunner" />'s frontend
/// </summary>
/// <param name="runnerType">
/// The class derived from <see cref="ExperimentRunner" /> that this frontend can render
/// </param>
public class ExperimentViewAttribute(Type runnerType) : Attribute
{
    public Type RunnerType { get; init; } = runnerType;
}