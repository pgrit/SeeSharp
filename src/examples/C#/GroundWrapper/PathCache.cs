using System.Threading;

namespace Ground {

    public class PathCache {
        public PathCache(int capacity) {
            vertices = new PathVertex[capacity];
        }

        public PathVertex this[int vertexId] => vertices[vertexId];

        public int AddVertex(PathVertex vertex) {
            int idx = Interlocked.Increment(ref next);

            if (idx > vertices.Length)
                return -1;

            vertices[idx] = vertex;
            return idx;
        }

        public void Clear() {
            int overflow = next - vertices.Length;
            if (overflow > 0) {
                System.Console.WriteLine($"Overflow detected. Resizing to fit {overflow * 2} additional vertices.");
                vertices = new PathVertex[vertices.Length + overflow * 2];
            }

            next = 0;
        }

        PathVertex[] vertices;
        int next = 0;
    }

}