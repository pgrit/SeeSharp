using SeeSharp.Geometry;
using SimpleImageIO;
using System;

namespace SeeSharp.Integrators.Common {
    public struct PathVertex {
        public SurfacePoint Point;

        // Surface area pdf to sample this vertex from the previous one,
        // i.e., the actual density this vertex was sampled from
        public float PdfFromAncestor;

        // Surface area pdf to sample the ancestor of the previous vertex.
        public float PdfReverseAncestor;

        // Surface area pdf of next event estimation at the ancestor (if applicable)
        public float PdfNextEventAncestor;

        public RgbColor Weight;

        public int AncestorId;
        public int PathId;

        // The number of edges along the path. This should be clarified,
        // conventions differ between rendereres!
        public byte Depth;
    }

    public class PathCache { // TODO no need for all the fancy pre-alloc if we are using class anyway
        public PathCache(int numPaths, int pathCapacity) {
            vertices = new PathVertex[numPaths, pathCapacity];
            next = new int[numPaths];
            this.numPaths = numPaths;
            this.pathCapacity = pathCapacity;
        }

        public ref PathVertex this[int pathIdx, int vertexId] {
            get => ref vertices[pathIdx, vertexId];
        }

        public int AddVertex(PathVertex vertex, int pathIdx) {
            int idx = next[pathIdx]++;
            if (idx >= pathCapacity)
                return -1;
            vertices[pathIdx, idx] = vertex;
            vertices[pathIdx, idx].PathId = pathIdx;
            return idx;
        }

        public void Clear() {
            int overflow = 0;
            for (int i = 0; i < numPaths; ++i) {
                overflow = Math.Max(next[i] - pathCapacity, overflow);
                next[i] = 0;
            }

            if (overflow > 0) {
                System.Console.WriteLine($"Overflow detected. Resizing to fit {overflow * 2} additional vertices.");
                pathCapacity += overflow * 2;
                vertices = new PathVertex[numPaths, pathCapacity];
            }
        }

        public int Length(int index) => next[index];
        public int NumPaths => numPaths;

        PathVertex[,] vertices;
        int[] next;
        int numPaths, pathCapacity;
    }
}