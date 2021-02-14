using System.Collections.Generic;

namespace SeeSharp.Datastructs {
    class NeighborCandidates {
        List<float> squaredDistances;
        List<int> indices;
        int numPoints;
        float maxSquaredDistance;

        public NeighborCandidates(int max, float radius) {
            squaredDistances = new List<float>(max);
            indices = new List<int>(max);
            numPoints = max;
            maxSquaredDistance = radius * radius;
        }

        public int[] Result => indices.ToArray();

        public bool CheckAndAdd(float squaredDistance, int index) {
            if (squaredDistance > maxSquaredDistance)
                return false;

            int nextGreater = squaredDistances.BinarySearch(squaredDistance);
            if (nextGreater < 0) nextGreater = ~nextGreater;
            else for (; nextGreater > 0 && squaredDistances[nextGreater-1] == squaredDistance; --nextGreater) { }

            if (nextGreater < numPoints) {
                squaredDistances.Insert(nextGreater, squaredDistance);
                indices.Insert(nextGreater, index);
            }

            // Trim any value that exceeds the maxium number of candidate points
            if (squaredDistances.Count > numPoints) {
                squaredDistances.RemoveAt(numPoints);
                indices.RemoveAt(numPoints);
            }

            return false;
        }

        public bool WithinRange(float squaredDistance)
        => (squaredDistances.Count < numPoints || squaredDistances[^1] > squaredDistance)
            && squaredDistance <= maxSquaredDistance;
    }
}