using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace SeeSharp.Blazor;

public partial class Experiment : ViewComponent
{
    DynamicComponent viewComponent;

    public IEnumerable<Type> AllViews =>
        AppDomain
            .CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(t => t.IsSubclassOf(typeof(ComponentBase)))
            .Where(c => c.GetCustomAttribute<ExperimentViewAttribute>() != null);

    public Type ActiveViewType
    {
        get
        {
            if (ExperimentRunner.Active == null)
                return null;

            var t = ExperimentRunner.Active.GetType();
            var view = AllViews
                .Where(c => c.GetCustomAttribute<ExperimentViewAttribute>().RunnerType == t)
                .FirstOrDefault();

            return view;
        }
    }

    void StartExperiment(Type t)
    {
        var runnerT = t.GetCustomAttribute<ExperimentViewAttribute>().RunnerType;
        ExperimentRunner.Active = (ExperimentRunner)Activator.CreateInstance(runnerT);
    }
}
