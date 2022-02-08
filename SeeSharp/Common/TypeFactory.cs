using System.Linq;

namespace SeeSharp.Common;

public class TypeFactory<T> where T : class {
    public static T[] All {
        get {
            // Find all non-abstract classes in the currently loaded assemblies that implement T
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsClass && !type.IsAbstract && typeof(T).IsAssignableFrom(type));

            // Instantiate a new (default constructed) object of each implementation
            List<T> result = new();
            foreach (var type in types) {
                result.Add(Activator.CreateInstance(type) as T);
            }

            return result.ToArray();
        }
    }
}