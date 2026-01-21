using Microsoft.AspNetCore.Components;

namespace SeeSharp.ReferenceManager.Pages;

public partial class IntegratorSelector : ComponentBase
{
    [Parameter] public Scene scene { get; set; } = default!;
    
    public List<Integrator> addedIntegrators { get; private set; } = new();
    
    public Integrator? CurrentIntegrator => addedIntegrators.FirstOrDefault();

    Type[] integratorTypes = Array.Empty<Type>();
    string? selectedIntegrator;
    private string lastIntegrator;

    protected override void OnInitialized()
    {
        var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsClass && !type.IsAbstract && typeof(Integrator).IsAssignableFrom(type) && 
                !type.ContainsGenericParameters && !typeof(DebugVisualizer).IsAssignableFrom(type));
        integratorTypes = types.Where(t => !types.Any(other => other.IsSubclassOf(t))).ToArray();

        if (integratorTypes.Length > 0) {
            selectedIntegrator = integratorTypes[0].FullName;
            lastIntegrator = selectedIntegrator;
            ReplaceIntegrator();
        }
        DocumentationReader.LoadXmlDocumentation(typeof(Integrator).Assembly);
    }

    void OnSelectionChanged(ChangeEventArgs e)
    {
        selectedIntegrator = e.Value?.ToString();
        if (!string.IsNullOrEmpty(selectedIntegrator))
        {
            lastIntegrator = selectedIntegrator;
            ReplaceIntegrator();
        }
    }

    void ReplaceIntegrator()
    {
        if (string.IsNullOrEmpty(selectedIntegrator)) return;
        var type = integratorTypes.FirstOrDefault(t => t.FullName == selectedIntegrator);
        if (type == null) return;

        addedIntegrators.Clear();

        var integrator = (Integrator)Activator.CreateInstance(type)!;
        addedIntegrators.Add(integrator);

        StateHasChanged();
    }

    protected List<ParameterGroup> GetParameterGroups(Integrator integrator) 
        => IntegratorUtils.GetParameterGroups(integrator);

    protected string FormatClassName(Type t) => IntegratorUtils.FormatClassName(t);

    public void TriggerReset()
    {
        selectedIntegrator = lastIntegrator;
        ReplaceIntegrator();
    }

    public bool TrySelectIntegrator(string simpleName)
    {
        var targetType = integratorTypes.FirstOrDefault(t => t.Name == simpleName || t.Name == simpleName + "`1");
        
        if (targetType != null && targetType.FullName != selectedIntegrator)
        {
            selectedIntegrator = targetType.FullName;
            ReplaceIntegrator();
            return true;
        }
        return targetType != null;
    }

    public Type? GetIntegratorType(string simpleName)
    {
        if (string.IsNullOrEmpty(simpleName)) return null;
        return integratorTypes.FirstOrDefault(t => 
            t.Name.Equals(simpleName, StringComparison.OrdinalIgnoreCase) || 
            t.Name.Equals(simpleName + "`1", StringComparison.OrdinalIgnoreCase));
    }
}