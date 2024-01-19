namespace SeeSharp.Integrators.Common;

/// <summary>
/// Stores a set of paths consisting of vertices. The capacity is pre-determined. If not all vertices of a
/// path fit in the cache, they are discared and the next iteration uses a bigger cache.
/// </summary>
public class PathCache {
    /// <summary>
    /// Initializes the cache
    /// </summary>
    /// <param name="numPaths">The number of paths to store</param>
    /// <param name="pathCapacity">The (expected) maximum number of vertices along each path</param>
    public PathCache(int numPaths, int pathCapacity) {
        vertices = new PathVertex[numPaths * pathCapacity];
        next = new int[numPaths];
        this.numPaths = numPaths;
        this.pathCapacity = pathCapacity;
    }

    /// <summary>
    /// By-reference access to a vertex stored in the cache
    /// </summary>
    /// <param name="pathIdx">0-based index of the path</param>
    /// <param name="vertexId">0-based index of the vertex along the path</param>
    /// <returns>Reference to the path vertex</returns>
    public ref PathVertex this[int pathIdx, int vertexId] {
        get => ref vertices[pathIdx * pathCapacity + vertexId];
    }

    /// <summary>
    /// Extends a path by adding a new vertex
    /// </summary>
    /// <param name="vertex">The next vertex</param>
    /// <param name="pathIdx">Index of the path to extend</param>
    /// <returns>Index of the new vertex along the path</returns>
    public int AddVertex(PathVertex vertex, int pathIdx) {
        int idx = next[pathIdx]++;
        if (idx >= pathCapacity)
            return -1;
        vertices[pathIdx * pathCapacity + idx] = vertex;
        vertices[pathIdx * pathCapacity + idx].PathId = pathIdx;
        return idx;
    }

    /// <summary>
    /// Deletes all paths
    /// </summary>
    public void Clear() {
        int overflow = 0;
        for (int i = 0; i < numPaths; ++i) {
            overflow = Math.Max(next[i] - pathCapacity, overflow);
            next[i] = 0;
        }

        if (overflow > 0) {
            Logger.Warning($"Path cache overflow. Resizing to fit {overflow * 2} additional vertices.");
            pathCapacity += overflow * 2;
            vertices = new PathVertex[numPaths * pathCapacity];
        }
    }

    /// <param name="index">Index of a path</param>
    /// <returns>The number of vertices along the path</returns>
    public int Length(int index) => Math.Min(next[index], pathCapacity);

    /// <summary>
    /// The number of paths the cache can store
    /// </summary>
    public int NumPaths => numPaths;

    PathVertex[] vertices;
    int[] next;
    int numPaths, pathCapacity;
}