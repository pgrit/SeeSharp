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
}
