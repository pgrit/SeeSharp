using Microsoft.AspNetCore.Components;

namespace SeeSharp.ReferenceManager.Pages;

public partial class IntegratorSelector : ComponentBase
{
    public Integrator CurrentIntegrator
    {
        get;
        set
        {
            TrySelectIntegrator(integratorTypes.First().FullName);
            field = value;
            StateHasChanged();
        }
    }

    Type[] integratorTypes = Array.Empty<Type>();
    string selectedIntegrator => CurrentIntegrator?.GetType().FullName;

    protected override void OnInitialized()
    {
        var types = AppDomain
            .CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
                type.IsClass
                && !type.IsAbstract
                && typeof(Integrator).IsAssignableFrom(type)
                && !type.ContainsGenericParameters
                && !typeof(DebugVisualizer).IsAssignableFrom(type)
            );
        integratorTypes = types.Where(t => !types.Any(other => other.IsSubclassOf(t))).ToArray();

        if (integratorTypes.Length > 0)
        {
            ReplaceIntegrator(integratorTypes.First().FullName);
        }
        DocumentationReader.LoadXmlDocumentation(typeof(Integrator).Assembly);
    }

    void OnSelectionChanged(ChangeEventArgs e) => ReplaceIntegrator(e.Value?.ToString());

    void ReplaceIntegrator(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return;
        var type = integratorTypes.FirstOrDefault(t => t.FullName == typeName);
        if (type == null)
            return;

        CurrentIntegrator = (Integrator)Activator.CreateInstance(type);
        StateHasChanged();
    }

    protected List<ParameterGroup> GetParameterGroups(Integrator integrator) =>
        IntegratorUtils.GetParameterGroups(integrator);

    protected string FormatClassName(Type t) => IntegratorUtils.FormatClassName(t);

    public bool TrySelectIntegrator(string simpleName)
    {
        var targetType = integratorTypes.FirstOrDefault(t =>
            t.Name == simpleName || t.Name == simpleName + "`1"
        );

        if (targetType != null && targetType.FullName != selectedIntegrator)
        {
            ReplaceIntegrator(targetType.FullName);
            return true;
        }
        return targetType != null;
    }

    public Type GetIntegratorType(string simpleName)
    {
        if (string.IsNullOrEmpty(simpleName))
            return null;
        return integratorTypes.FirstOrDefault(t =>
            t.Name.Equals(simpleName, StringComparison.OrdinalIgnoreCase)
            || t.Name.Equals(simpleName + "`1", StringComparison.OrdinalIgnoreCase)
        );
    }
}