using SeeSharp.Geometry;
using SimpleImageIO;
using SeeSharp.Integrators.Common;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace SeeSharp.Integrators.Bidir {
    public class PhotonHashGrid {
        struct PhotonReference {
            public int PathIndex;
            public int VertexIndex;
            public Vector3 Position;
        }

        protected virtual bool Filter(PathVertex vertex)
        => vertex.Depth >= 1 && vertex.Weight != RgbColor.Black;

        protected virtual void AssemblePhotons(LightPathCache paths) {
            if (photons == null || photons.Length < paths.MaxDepth * paths.NumPaths)
                photons = new PhotonReference[paths.MaxDepth * paths.NumPaths];
            photonCount = 0;
            for (int i = 0; i < paths.NumPaths; ++i) {
                for (int k = 1; k < paths.PathCache.Length(i); ++k) {
                    if (Filter(paths.PathCache[i, k])) {
                        photons[photonCount++] = new PhotonReference {
                            PathIndex = i,
                            VertexIndex = k,
                            Position = paths.PathCache[i, k].Point.Position
                        };
                    }
                }
            }
        }

        public void Build(LightPathCache paths, float averageRadius) {
            inverseBinSize = 0.5f / averageRadius;

            AssemblePhotons(paths);

            // Compute the bounding box of all photons
            bounds = BoundingBox.Empty;
            for (int i = 0; i < photonCount; ++i) {
                var p = photons[i];
                bounds = bounds.GrowToContain(p.Position);
            }

            // Add an offset for numerical stability
            var extents = bounds.Max - bounds.Min;
            bounds = new BoundingBox(
                max: bounds.Max + extents * 0.001f,
                min: bounds.Min - extents * 0.001f
            );

            // Count the number of photons per grid cell
            cellCounts = new int[NextPowerOfTwo(photonCount)];
            Parallel.For(0, photonCount, i => {
                var p = photons[i];
                var hash = HashPhoton(p.Position);
                Interlocked.Increment(ref cellCounts[hash]);
            });

            // Compute the insertion position for each cell
            int sum = 0;
            for (int i = 0; i < cellCounts.Length; ++i) {
                cellCounts[i] = sum += cellCounts[i];
            }
            Debug.Assert(cellCounts[^1] == photonCount);

            // Sort the photons into their cells
            photonIndices = new int[photonCount];
            Parallel.For(0, photonCount, i => {
                var h = HashPhoton(photons[i].Position);
                int idx = Interlocked.Decrement(ref cellCounts[h]);
                photonIndices[idx] = i;
            });
        }

        public delegate RgbColor Callback<in T>(T userData, SurfacePoint hit, Vector3 outDir,
                                                int pathIdx, int vertIdx, float distanceSquared,
                                                float radiusSquared);

        public RgbColor Accumulate<T>(T userData, SurfacePoint hit, Vector3 outDir, Callback<T> callback, float radius) {
            if (!bounds.IsInside(hit.Position))
                return RgbColor.Black;

            var p = (hit.Position - bounds.Min) * inverseBinSize;
            uint px1 = (uint)p.X;
            uint py1 = (uint)p.Y;
            uint pz1 = (uint)p.Z;
            uint px2 = (uint)(px1 + (p.X - px1 > 0.5f ? 1 : -1));
            uint py2 = (uint)(py1 + (p.Y - py1 > 0.5f ? 1 : -1));
            uint pz2 = (uint)(pz1 + (p.Z - pz1 > 0.5f ? 1 : -1));

            RgbColor result = RgbColor.Black;
            for (int i = 0; i < 8; i++) {
                (int start, int end) = CellRange((i & 1) != 0 ? px2 : px1,
                                                 (i & 2) != 0 ? py2 : py1,
                                                 (i & 4) != 0 ? pz2 : pz1);

                for (int j = start; j < end; j++) {
                    var photon = photons[photonIndices[j]];
                    float distanceSqr = (hit.Position - photon.Position).LengthSquared();
                    if (distanceSqr < radius * radius) {
                        result += callback(userData, hit, outDir, photon.PathIndex, photon.VertexIndex,
                            distanceSqr, radius * radius);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the integer that is greater or equal to the logarithm base 2 of the argument.
        /// </summary>
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
            return (cellCounts[h], h == cellCounts.Length - 1 ? photonCount : cellCounts[h + 1]);
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
        PhotonReference[] photons;
        int photonCount;
    }
}
