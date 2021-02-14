using SeeSharp.Geometry;
using SeeSharp.Shading;
using SeeSharp.Integrators.Common;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace SeeSharp.Integrators.Bidir {
    public class PhotonHashGrid {
        struct PhotonReference {
            public int PathIndex;
            public int VertexIndex;
            public Vector3 Position;
        }

        protected virtual bool Filter(PathVertex vertex)
            => vertex.Depth >= 1 && vertex.Weight != ColorRGB.Black;

        protected virtual void AssemblePhotons(LightPathCache paths) {
            photons = new List<PhotonReference>(paths.MaxDepth * paths.NumPaths);
            for (int i = 0; i < paths.NumPaths; ++i) {
                for (int k = 1; k < paths.PathCache.Length(i); ++k) {
                    if (Filter(paths.PathCache[i, k])) {
                        photons.Add(new PhotonReference {
                            PathIndex = i,
                            VertexIndex = k,
                            Position = paths.PathCache[i, k].Point.Position
                        });
                    }
                }
            }
        }

        public void Build(LightPathCache paths, float averageRadius) {
            inverseBinSize = 0.5f / averageRadius;

            AssemblePhotons(paths);

            // Compute the bounding box of all photons
            bounds = BoundingBox.Empty;
            foreach (var p in photons) {
                bounds = bounds.GrowToContain(p.Position);
            }

            // Add an offset for numerical stability
            var extents = bounds.Max - bounds.Min;
            bounds = new BoundingBox(
                max: bounds.Max + extents * 0.001f,
                min: bounds.Min - extents * 0.001f
            );

            // Count the number of photons per grid cell
            cellCounts = new int[NextPowerOfTwo(photons.Count)];
            foreach (var p in photons) {
                var hash = HashPhoton(p.Position);
                cellCounts[hash]++;
            }

            // Compute the insertion position for each cell
            int sum = 0;
            for (int i = 0; i < cellCounts.Length; ++i) {
                cellCounts[i] = sum += cellCounts[i];
            }
            Debug.Assert(cellCounts[^1] == photons.Count);

            // Sort the photons into their cells
            photonIndices = new int[photons.Count];
            for (int i = 0; i < photons.Count; ++i) {
                var h = HashPhoton(photons[i].Position);
                photonIndices[--cellCounts[h]] = i;
            }
        }

        public delegate void Callback(int pathIdx, int vertIdx, float distanceSquared);

        public void Query(Vector3 pos, Callback callback, float radius) {
            if (!bounds.IsInside(pos)) return;

            var p = (pos - bounds.Min) * inverseBinSize;
            uint px1 = (uint)p.X;
            uint py1 = (uint)p.Y;
            uint pz1 = (uint)p.Z;
            uint px2 = (uint)(px1 + (p.X - px1 > 0.5f ? 1 : -1));
            uint py2 = (uint)(py1 + (p.Y - py1 > 0.5f ? 1 : -1));
            uint pz2 = (uint)(pz1 + (p.Z - pz1 > 0.5f ? 1 : -1));

            for (int i = 0; i < 8; i++) {
                var (start, end) = CellRange((i & 1) != 0 ? px2 : px1,
                                             (i & 2) != 0 ? py2 : py1,
                                             (i & 4) != 0 ? pz2 : pz1);

                for (int j = start; j < end; j++) {
                    var photon = photons[photonIndices[j]];
                    float distanceSqr = (pos - photon.Position).LengthSquared();
                    if (distanceSqr < radius * radius)
                        callback(photon.PathIndex, photon.VertexIndex, distanceSqr);
                }
            }
        }

        /// <summary> Returns the integer that is greater or equal to the logarithm base 2 of the argument. </summary>
        int NextLog2(int i) {
            int powerOfTwo = 1, exponent = 0;
            while (i > powerOfTwo) {
                powerOfTwo <<= 1; exponent++;
            }
            return exponent;
        }

        int NextPowerOfTwo(int i) => 1 << (NextLog2(i) + 1);

        uint BernsteinInit => 5381;

        uint BernsteinHash(uint h, uint d) {
            h = (h * 33) ^ (d & 0xFF);
            h = (h * 33) ^ ((d >> 8) & 0xFF);
            h = (h * 33) ^ ((d >> 16) & 0xFF);
            h = (h * 33) ^ ((d >> 24) & 0xFF);
            return h;
        }

        (int, int) CellRange(uint x, uint y, uint z) {
            var h = HashCell(x, y, z);
            return (cellCounts[h], h == cellCounts.Length - 1 ? photons.Count : cellCounts[h + 1]);
        }

        uint HashCell(uint x, uint y, uint z) {
            uint h = BernsteinHash(BernsteinInit, x);
            h = BernsteinHash(h, y);
            h = BernsteinHash(h, z);
            return h & (uint)(cellCounts.Length - 1);
        }

        uint HashPhoton(Vector3 pos) {
            var p = (pos - bounds.Min) * inverseBinSize;
            return HashCell((uint)p.X, (uint)p.Y, (uint)p.Z);
        }

        float inverseBinSize;
        BoundingBox bounds;
        int[] cellCounts;
        int[] photonIndices;
        List<PhotonReference> photons;
    }
}
