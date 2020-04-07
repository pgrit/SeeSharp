using System.Runtime.InteropServices;

namespace Ground {
    public class PathCache {
        public PathCache(int capacity) {
            this.id = CreatePathCache(capacity);
        }

        ~PathCache()
        => DeletePathCache(this.id);

        public PathVertex this[int vertexId] {
            get => GetPathVertex(this.id, vertexId);
        }

        public int AddVertex(PathVertex vertex)
        => AddPathVertex(this.id, vertex);

        public void Clear()
        => ClearPathCache(this.id);

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
}