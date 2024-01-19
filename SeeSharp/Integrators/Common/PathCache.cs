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
    /// <param name="expectedPathLength">The (expected) average number of vertices along each path</param>
    public PathCache(int numPaths, int expectedPathLength) {
        vertices = new PathVertex[numPaths * expectedPathLength];
        pathEndpoints = new int[numPaths];
        pathLengths = new int[numPaths];
    }

    /// <summary>
    /// By-reference access to a vertex stored in the cache
    /// </summary>
    /// <param name="pathIdx">0-based index of the path</param>
    /// <param name="vertexIdx">0-based index of the vertex along the path</param>
    /// <returns>Reference to the path vertex</returns>
    public ref PathVertex this[int pathIdx, int vertexIdx] {
        get {
            var idx = pathEndpoints[pathIdx];
            int offset = pathLengths[pathIdx] - vertexIdx - 1;
            while (offset > 0) {
                idx = vertices[idx].AncestorId;
                offset--;
            }
            return ref vertices[idx];
        }
    }

    /// <summary>
    /// Direct acces to an arbitrary vertex in the cache, independent of path structure
    /// </summary>
    /// <param name="vertexIdx">Global index of the vertex</param>
    /// <returns>The requested path vertex</returns>
    public ref PathVertex this[int vertexIdx] => ref vertices[Math.Clamp(vertexIdx, 0, vertices.Length - 1)];

    /// <summary>
    /// Extends a path by adding a new vertex
    /// </summary>
    /// <param name="vertex">The next vertex</param>
    /// <returns>Index of the new vertex along the path</returns>
    public int AddVertex(in PathVertex vertex) {
        int idx = threadCaches.Value.AddVertex(vertex, vertices, ref nextIdx);
        pathEndpoints[vertex.PathId] = idx;
        pathLengths[vertex.PathId]++;
        return idx;
    }

    /// <summary>
    /// Deletes all paths
    /// </summary>
    public void Clear() {
        if (overflow > 0) {
            Logger.Warning($"Path cache overflow. Resizing to fit {overflow * 2} additional vertices.");
            vertices = new PathVertex[vertices.Length + overflow * 2];
        }

        nextIdx = 0;
        overflow = 0;
        pathEndpoints = new int[pathEndpoints.Length];
        pathLengths = new int[pathEndpoints.Length];
    }

    int overflow = 0;

    class VertexOrder : IComparer<PathVertex> {
        public int Compare(PathVertex x, PathVertex y) {
            return (x.PathId, x.Depth).CompareTo((y.PathId, y.Depth));
        }
    }

    public void Prepare() {
        int totalUnused = 0;
        foreach (var c in threadCaches.Values) {
            (int start, int num, int unused, int overflow) = c.Flush(vertices);
            this.overflow += overflow;
            totalUnused += unused;
        }
        // TODO compact the holes after flushing incomplete caches
    }

    /// <param name="index">Index of a path</param>
    /// <returns>The number of vertices along the path</returns>
    public int Length(int index) {
        if (pathEndpoints[index] > vertices.Length)
            return 0;
        return pathLengths[index];
    }

    /// <summary>
    /// The number of paths the cache can store
    /// </summary>
    public int NumPaths => pathEndpoints.Length;

    /// <summary>
    /// The total number of path vertices stored in the cache
    /// </summary>
    public int NumVertices => nextIdx;

    PathVertex[] vertices;
    int nextIdx;
    int[] pathEndpoints;
    int[] pathLengths;

    ThreadLocal<ThreadCache> threadCaches = new(() => new(), true);

    class ThreadCache {
        int next = 0;
        int insertPos = -1;
        PathVertex[] batch = new PathVertex[BatchSize];
        public const int BatchSize = 32;

        public (int Start, int Num, int Unused, int Overflow) Flush(PathVertex[] vertices) {
            if (insertPos < 0) return (0, 0, 0, 0); // Cache is empty

            // Drop the overflowing vertices
            int overflow = next - (vertices.Length - insertPos);
            if (overflow > 0) next -= overflow;

            Array.ConstrainedCopy(batch, 0, vertices, insertPos, next);

            // For uncompleted batches, add a guard
            int unused = overflow > 0 ? 0 : (BatchSize - next);
            for (int i = next; i < next + unused && insertPos + i < vertices.Length; ++i) {
                vertices[insertPos + i].PathId = -1;
            }
            int start = insertPos;
            int num = next;

            next = 0;
            insertPos = -1;

            return (start, num, unused, overflow > 0 ? overflow : 0);
        }

        void Reserve(PathVertex[] vertices, ref int globalNext) {
            if (next == BatchSize)
                Flush(vertices);

            if (insertPos == -1) {
                int rangeEnd = Interlocked.Add(ref globalNext, BatchSize);
                insertPos = rangeEnd - BatchSize;
                next = 0;
            }
        }

        public int AddVertex(in PathVertex vertex, PathVertex[] vertices, ref int globalNext) {
            Reserve(vertices, ref globalNext);

            int idx = next++;
            batch[idx] = vertex;
            return idx + insertPos;
        }
    }
}
