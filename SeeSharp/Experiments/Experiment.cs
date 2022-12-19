namespace SeeSharp.Experiments;

/// <summary>
/// Describes an experiment with a list of named integrators.
/// </summary>
public abstract class Experiment {
    /// <summary>
    /// A "method" is a named integrator with specific parameters
    /// </summary>
    public readonly struct Method {
        /// <summary>
        /// Name of the method. Determines file and directory names.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// The integrator object to run.
        /// </summary>
        public readonly Integrator Integrator;

        /// <summary>
        /// Creates a new method
        /// </summary>
        /// <param name="name">Name of the method. Determines file and directory names.</param>
        /// <param name="integrator">The integrator object to run, with the desired parameters set</param>
        public Method(string name, Integrator integrator) {
            Name = name;
            Integrator = integrator;
        }
    }

    /// <summary>
    /// Factory function for the methods.
    /// </summary>
    /// <returns>A list of all methods that should be run in a benchmark</returns>
    public abstract List<Method> MakeMethods();

    /// <summary>
    /// Called before the experiment is run on a test scene.
    /// </summary>
    /// <param name="scene">The scene that will be rendered</param>
    /// <param name="dir">Output directory</param>
    /// <param name="minDepth">Minimum path length during rendering</param>
    /// <param name="maxDepth">Maximum path length during rendering</param>
    public virtual void OnStartScene(Scene scene, string dir, int minDepth, int maxDepth) { }

    /// <summary>
    /// Called after all methods have been run on a test scene.
    /// </summary>
    /// <param name="scene">The scene that was rendered</param>
    /// <param name="dir">
    /// Output directory, each method is in a subdirectory; the method's name is the name of that subdirectory
    /// </param>
    /// <param name="minDepth">Minimum path length during rendering</param>
    /// <param name="maxDepth">Maximum path length during rendering</param>
    public virtual void OnDoneScene(Scene scene, string dir, int minDepth, int maxDepth) { }

    /// <summary>
    /// Called before the experiment is run on a set of scenes
    /// </summary>
    /// <param name="workingDirectory">Output directory</param>
    public virtual void OnStart(string workingDirectory) { }

    /// <summary>
    /// Called after the experiment run has finished for all scenes
    /// </summary>
    /// <param name="workingDirectory">Output directory</param>
    public virtual void OnDone(string workingDirectory) { }
}
