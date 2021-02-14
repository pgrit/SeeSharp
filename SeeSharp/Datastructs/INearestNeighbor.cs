using System.Numerics;

namespace SeeSharp.Datastructs {
    public interface INearestNeighbor {
        void AddPoint(Vector3 position, int userId);
        void Clear();
        void Build();
        int[] QueryNearest(Vector3 position, int maxCount, float maxRadius);
    }
}