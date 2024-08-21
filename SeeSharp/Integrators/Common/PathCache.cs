namespace SeeSharp.Integrators.Common;

/// <summary>
/// Stores a set of paths consisting of vertices. The capacity is pre-determined. If not all vertices of a
/// path fit in the cache, they are discared and the next iteration uses a bigger cache.
/// </summary>
public class PathCache {
    PathVertex[] memory;
    int next = 0;
    int[] pathIndices;
    int[] pathLengths;
    int[] cumPathLen;

    public PathCache(int numPaths, int expectedPathLength) {
        NumPaths = numPaths;
        pathIndices = new int[numPaths];
        pathLengths = new int[numPaths];
        cumPathLen = new int[numPaths];
        memory = new PathVertex[numPaths * expectedPathLength];
    }

    /// <returns>
    /// A reference to the vertexIdx'th vertex along the pathIdx'th path
    /// </returns>
    public ref PathVertex GetPathVertex(int pathIdx, int vertexIdx) {
        int p = pathIndices[pathIdx];
        return ref memory[p + vertexIdx];
    }

    /// <returns>
    /// The global vertex index of the vertexIdx'th vertex along the pathIdx'th path
    /// </returns>
    public int GetPathVertexIndex(int pathIdx, int vertexIdx) => pathIndices[pathIdx] + vertexIdx;

    /// <returns>
    /// A reference to the a vertex identified by its global index in the entire cache
    /// </returns>
    public ref PathVertex GetVertex(int globalVertexIdx) {
        int idx = Array.BinarySearch(cumPathLen, globalVertexIdx);
        int vertexMemoryIdx;
        if (idx < 0) {
            idx = ~idx;
            int offset = globalVertexIdx - (idx == 0 ? 0 : cumPathLen[idx - 1]);
            vertexMemoryIdx = pathIndices[idx] + offset;
        } else {
            // Skip empty paths in-between
            do idx++;
            while (pathIndices[idx] == -1);
            vertexMemoryIdx = pathIndices[idx];
        }
        return ref memory[vertexMemoryIdx];
    }

    public int NumVertices => cumPathLen[NumPaths - 1];
    public int NumPaths { get; init; }

    bool overflow = false;

    public int Length(int pathIdx) => pathLengths[pathIdx];

    public void Commit(int pathIdx, ReadOnlySpan<PathVertex> vertices) {
        if (vertices.Length > 0) {
            pathIndices[pathIdx] = Interlocked.Add(ref next, vertices.Length) - vertices.Length;
            pathLengths[pathIdx] = vertices.Length;
            if (pathIndices[pathIdx] + vertices.Length >= memory.Length) {
                overflow = true;
                pathLengths[pathIdx] = 0;
                pathIndices[pathIdx] = -1;
            } else
                vertices.CopyTo(memory.AsSpan(pathIndices[pathIdx], vertices.Length));
        } else {
            pathIndices[pathIdx] = -1;
            pathLengths[pathIdx] = 0;
        }
    }

    public void Clear() {
        next = 0;
        if (overflow) {
            Logger.Warning("Overflow occured in the path cache, consider using a larger initial size.");
            memory = new PathVertex[memory.Length * 2];
        }
    }

    public void Prepare() {
        int sum = 0;
        for (int i = 0; i < NumPaths; ++i) {
            sum += pathLengths[i];
            cumPathLen[i] = sum;
        }
    }
}
