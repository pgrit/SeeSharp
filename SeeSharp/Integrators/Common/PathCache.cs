using System.Linq;

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

    /// <summary>
    /// Prepares the path cache for usage during sampling
    /// </summary>
    /// <param name="deterministic">
    /// If true, vertices will be sorted by path index and depth. Ensures that - given identical path sampling -
    /// the rendering will be deterministic and unaffected by cache write order of different threads.
    /// </param>
    public void Prepare(bool deterministic) {
        List<(int StartIndex, int Amount)> gaps = [];
        int totalUnused = 0;
        foreach (var c in threadCaches.Values) {
            (int start, int num, int unused, int overflow) = c.Flush(vertices);
            this.overflow += overflow;
            totalUnused += unused;

            if (unused > 0)
                gaps.Add((start, unused));
        }
        threadCaches.Values.Clear();

        if (overflow > 0) return;

        if (deterministic)
            SortPaths(totalUnused); // sorting makes sure all connections are deterministic
        else if (gaps.Count > 0)
            CompactVertices(gaps, totalUnused); // Removing gaps allows us to sample random light vertices more easily
    }

    void SortPaths(int totalUnused) {
        var sortedVertices = new PathVertex[vertices.Length];

        int offset = 0;
        var offsets = new int[pathEndpoints.Length];
        for (int pathIdx = 0; pathIdx < pathEndpoints.Length; ++pathIdx) {
            offsets[pathIdx] = offset;
            offset += pathLengths[pathIdx];
        }

        Parallel.For(0, pathEndpoints.Length, pathIdx => {
            int idx = pathEndpoints[pathIdx];
            for (int i = offsets[pathIdx] + pathLengths[pathIdx] - 1; i >= offsets[pathIdx]; --i) {
                sortedVertices[i] = vertices[idx];
                idx = vertices[idx].AncestorId;
                sortedVertices[i].AncestorId = sortedVertices[i].AncestorId < 0 ? -1 : i - 1;
            }
            pathEndpoints[pathIdx] = offsets[pathIdx] + pathLengths[pathIdx] - 1;
        });

        vertices = sortedVertices;

        nextIdx -= totalUnused;
    }

    void CompactVertices(IEnumerable<(int StartIndex, int Amount)> gaps, int totalUnused) {
        var sortedGaps = gaps.OrderBy(gap => gap.StartIndex).ToList();

        int offset = sortedGaps[0].Amount;
        int nextGapIdx = 1;
        for (int i = sortedGaps[0].StartIndex + ThreadCache.BatchSize; i < nextIdx; i += ThreadCache.BatchSize) {
            Array.Copy(vertices, i, vertices, i - offset, ThreadCache.BatchSize);

            if (nextGapIdx < sortedGaps.Count && i == sortedGaps[nextGapIdx].StartIndex) {
                offset += sortedGaps[nextGapIdx].Amount;
                nextGapIdx++;
            }
        }

        nextIdx -= totalUnused;

        // Update the ancestor links. We can safely assume that ancestor vertices are always before the current one.
        for (int i = sortedGaps[0].StartIndex + ThreadCache.BatchSize - sortedGaps[0].Amount; i < nextIdx; ++i) {
            if (vertices[i].AncestorId < 0) continue;
            Debug.Assert(vertices[i].PathId >= 0);

            // If this vertex pointed to another one behind a gap, need to shift it
            int idxOffset = 0;
            for (int j = 0; j < sortedGaps.Count; ++j) {
                if (vertices[i].AncestorId < sortedGaps[j].StartIndex + ThreadCache.BatchSize)
                    break;
                idxOffset += sortedGaps[j].Amount;
            }
            vertices[i].AncestorId -= idxOffset;
        }

        // Correct the path endpoint links if the last vertex of a path got shifted
        for (int i = 0; i < pathEndpoints.Length; ++i) {
            int idxOffset = 0;
            for (int j = 0; j < sortedGaps.Count; ++j) {
                if (pathEndpoints[i] < sortedGaps[j].StartIndex + ThreadCache.BatchSize)
                    break;
                idxOffset += sortedGaps[j].Amount;
            }
            pathEndpoints[i] -= idxOffset;
        }
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

            if (next > 0)
                Array.ConstrainedCopy(batch, 0, vertices, insertPos, next);

            // For uncompleted batches, add a guard
            int unused = overflow > 0 ? 0 : (BatchSize - next);
            for (int i = next; i < next + unused && insertPos + i < vertices.Length; ++i)
                vertices[insertPos + i].PathId = -1;
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