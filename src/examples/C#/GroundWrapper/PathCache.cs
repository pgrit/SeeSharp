using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Ground {

    public class PathCache {
        public PathCache(int capacity) {
            id = CreatePathCache(capacity);
        }

        ~PathCache() => DeletePathCache(id);

        public PathVertex this[int vertexId] => GetPathVertex(id, vertexId);

        public int AddVertex(PathVertex vertex) => AddPathVertex(id, vertex);

        public void Clear() => ClearPathCache(id);

        readonly int id;

#region C-API-IMPORTS
        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        extern static int CreatePathCache(int initialSize);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        extern static int AddPathVertex(int cacheId, PathVertex vertex);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        extern static PathVertex GetPathVertex(int cacheId, int vertexId);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        extern static void ClearPathCache(int cacheId);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        extern static void DeletePathCache(int cacheId);
#endregion C-API-IMPORTS
    }

    public class ManagedPathCache {
        public ManagedPathCache(int capacity) {
            vertices = new PathVertex[capacity];
        }

        public PathVertex this[int vertexId] => vertices[vertexId];

        public int AddVertex(PathVertex vertex) {
            int idx = System.Threading.Interlocked.Increment(ref next);

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