using SeeSharp.Core.Geometry;
using SeeSharp.Core.Shading;
using System.Threading;

namespace SeeSharp.Integrators.Common {
    public class PathVertex {
        public SurfacePoint point;

        // Surface area pdf to sample this vertex from the previous one,
        // i.e., the actual density this vertex was sampled from
        public float pdfFromAncestor;

        // Surface area pdf to sample the previous vertex from this one,
        // i.e., the reverse direction of the path.
        public float pdfToAncestor;

        public ColorRGB weight;

        public int ancestorId;

        // The number of edges along the path. This should be clarified,
        // conventions differ between rendereres!
        public byte depth;
    }

    public class PathCache { // TODO no need for all the fancy pre-alloc if we are using class anyway
        public PathCache(int capacity) {
            vertices = new PathVertex[capacity];
        }

        public ref PathVertex this[int vertexId] {
            get => ref vertices[vertexId];
        }

        public int Count => next;

        public int AddVertex(PathVertex vertex) {
            int idx = Interlocked.Increment(ref next) - 1;

            if (idx >= vertices.Length)
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