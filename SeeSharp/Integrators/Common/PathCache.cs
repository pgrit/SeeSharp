using SeeSharp.Geometry;
using SimpleImageIO;
using System;

namespace SeeSharp.Integrators.Common {
    /// <summary>
    /// Stores the info of a single vertex of a cached light path
    /// </summary>
    public struct PathVertex {
        /// <summary>
        /// The surface interesection
        /// </summary>
        public SurfacePoint Point;

        /// <summary>
        /// Surface area pdf to sample this vertex from the previous one, i.e., the actual density this vertex
        /// was sampled from
        /// </summary>
        public float PdfFromAncestor;

        /// <summary> Surface area pdf to sample the ancestor of the previous vertex. </summary>
        public float PdfReverseAncestor;

        /// <summary> Surface area pdf of next event estimation at the ancestor (if applicable) </summary>
        public float PdfNextEventAncestor;

        /// <summary>
        /// Accumulated Monte Carlo weight of the sub-path up to and including this vertex
        /// </summary>
        public RgbColor Weight;

        /// <summary>
        /// 0-based index of the previous vertex along the path, or -1 if there is none
        /// </summary>
        public int AncestorId;

        /// <summary>
        /// 0-based index of the path this vertex belongs to
        /// </summary>
        public int PathId;

        /// <summary>
        /// The number of edges along the path.
        /// </summary>
        public byte Depth;

        /// <summary>
        /// Maximum roughness of the materials at any of the previous vertices and this one.
        /// </summary>
        public float MaximumRoughness;
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