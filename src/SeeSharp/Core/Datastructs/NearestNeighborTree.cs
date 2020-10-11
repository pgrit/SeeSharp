using System.Collections.Generic;
using System.Numerics;

namespace SeeSharp.Core.Datastructs {
    public class NearestNeighborTree {
        public void AddPoint(Vector3 position, int userId) => records.Add(new Record(position, userId));

        public void Clear() {
            records.Clear();
            root = null;
        }

        public void Build() {
            root = Split(0, 0, records.Count);
        }

        public int[] QueryNearest(Vector3 position, int maxCount, float maxRadius) {
            var candidates = new Candidates(maxCount, maxRadius);
            FindNearest(position, candidates, root);

            // Retrieve the user ids associated with the nearest records
            var indices = candidates.Result;
            for (int i = 0; i < indices.Length; ++i)
                indices[i] = records[indices[i]].UserId;
            return indices;
        }

        void FindNearest(Vector3 position, Candidates candidates, Node curNode) {
            if (curNode == null)
                return;

            float distSquared = (records[curNode.Index].Position - position).LengthSquared();
            candidates.CheckAndAdd(distSquared, curNode.Index);

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

        class Candidates {
            List<float> squaredDistances;
            List<int> indices;
            int numPoints;
            float maxSquaredDistance;

            public Candidates(int max, float radius) {
                squaredDistances = new List<float>(max);
                indices = new List<int>(max);
                numPoints = max;
                maxSquaredDistance = radius * radius;
            }

            public int[] Result => indices.ToArray();

            public bool CheckAndAdd(float squaredDistance, int index) {
                if (squaredDistance > maxSquaredDistance)
                    return false;

                int nextGreater = squaredDistances.FindIndex(a => a > squaredDistance);

                // If all elements are smaller, we may still allow the candidate if there are too few right now
                if (nextGreater == -1) {
                    if (squaredDistances.Count >= numPoints)
                        return false;
                    else
                        nextGreater = squaredDistances.Count;
                }

                squaredDistances.Insert(nextGreater, squaredDistance);
                indices.Insert(nextGreater, index);

                // Trim any value that exceeds the maxium number of candidate points
                if (squaredDistances.Count > numPoints) {
                    squaredDistances.RemoveAt(numPoints);
                    indices.RemoveAt(numPoints);
                }

                return true;
            }

            public bool WithinRange(float squaredDistance)
            => (squaredDistances.Count < numPoints || squaredDistances[^1] > squaredDistance)
                && squaredDistance <= maxSquaredDistance;
        }

        float GetAxisValue(Vector3 vec, int axis) => (axis%3) == 0 ? vec.X : ((axis%3) == 1 ? vec.Y : vec.Z);

        Node Split(int axis, int first, int count) {
            if (count == 1)
                return new Node {
                    Axis = axis,
                    Position = GetAxisValue(records[first].Position, axis),
                    Left = null,
                    Right = null
                };
            else if (count <= 0)
                return null;

            // Sort along the split axis
            records.Sort(first, count, Comparer<Record>.Create((a, b) =>
                GetAxisValue(a.Position, axis).CompareTo(GetAxisValue(b.Position, axis))
            ));

            int medianIndex = first + count / 2;
            var medianPos = GetAxisValue(records[medianIndex].Position, axis);

            return new Node {
                Axis = axis,
                Position = medianPos,
                Index = medianIndex,
                Left = Split(axis + 1, first, count / 2),
                Right = Split(axis + 1, medianIndex + 1, count - medianIndex - 1)
            };
        }

        struct Record {
            public Vector3 Position;
            public int UserId;
            public Record(Vector3 pos, int userId) {
                Position = pos;
                UserId = userId;
            }
        }

        List<Record> records = new List<Record>();

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