using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace SeeSharp.Core.Datastructs {
    public class NearestNeighborTree : INearestNeighbor {
        public void AddPoint(Vector3 position, int userId) {
            lock(records) {
                records.Add(new NeighborPoint(position, userId));
            }
        }

        public void Clear() {
            indices.Clear();
            records.Clear();
            root = null;
        }

        public void Build() {
            for (int i = 0; i < records.Count; ++i)
                indices.Add(i);
            root = Split(0, 0, records.Count);
        }

        public int[] QueryNearest(Vector3 position, int maxCount, float maxRadius) {
            var candidates = new NeighborCandidates(maxCount, maxRadius);
            FindNearest(position, candidates, root);
            return candidates.Result;
        }

        void FindNearest(Vector3 position, NeighborCandidates candidates, Node curNode) {
            if (curNode == null)
                return;

            float distSquared = (records[curNode.Index].Position - position).LengthSquared();
            candidates.CheckAndAdd(distSquared, records[curNode.Index].UserId);

            // compute the shortest distance to the splitting plane
            distSquared = GetAxisValue(position, curNode.Axis) - curNode.Position;
            distSquared *= distSquared;

            // traverse the node containing the query, and also the sibling if it is not too far away
            if (GetAxisValue(position, curNode.Axis) < curNode.Position) {
                FindNearest(position, candidates, curNode.Left);
                if (candidates.WithinRange(distSquared))
                    FindNearest(position, candidates, curNode.Right);
            } else {
                FindNearest(position, candidates, curNode.Right);
                if (candidates.WithinRange(distSquared))
                    FindNearest(position, candidates, curNode.Left);
            }
        }

        float GetAxisValue(Vector3 vec, int axis) => (axis%3) == 0 ? vec.X : ((axis%3) == 1 ? vec.Y : vec.Z);

        Node Split(int axis, int first, int count) {
            if (count == 1)
                return new Node {
                    Axis = axis,
                    Index = indices[first],
                    Position = GetAxisValue(records[indices[first]].Position, axis),
                    Left = null,
                    Right = null
                };
            else if (count <= 0)
                return null;

            // Sort along the split axis
            indices.Sort(first, count, Comparer<int>.Create((a, b) =>
                GetAxisValue(records[a].Position, axis).CompareTo(GetAxisValue(records[b].Position, axis))
            ));

            int medianIndex = first + count / 2;
            var medianPos = GetAxisValue(records[indices[medianIndex]].Position, axis);

            Node left = null, right = null;
            var tLeft = Task.Run(() => left = Split(axis + 1, first, count / 2));
            var tRight = Task.Run(() => right = Split(axis + 1, medianIndex + 1, count - count / 2 - 1));
            tLeft.Wait();
            tRight.Wait();

            return new Node {
                Axis = axis,
                Position = medianPos,
                Index = indices[medianIndex],
                Left = left,
                Right = right
            };
        }

        List<NeighborPoint> records = new List<NeighborPoint>();
        List<int> indices = new List<int>();

        class Node {
            public int Axis;
            public float Position;
            public int Index;
            public Node Left;
            public Node Right;
        }

        Node root;
    }
}