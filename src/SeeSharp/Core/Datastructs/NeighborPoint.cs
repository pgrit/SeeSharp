using System.Numerics;

namespace SeeSharp.Core.Datastructs {
    public struct NeighborPoint {
        public Vector3 Position;
        public int UserId;
        public NeighborPoint(Vector3 pos, int userId) {
            Position = pos;
            UserId = userId;
        }
    }
}