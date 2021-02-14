using System;
using System.Numerics;

namespace SeeSharp.Sampling {
    /// <summary>
    /// A regular grid on the unit cube.
    /// Useful for describing 3D pdfs in primary sample space.
    /// </summary>
    public class RegularGrid3d {
        public RegularGrid3d(int resx, int resy, int resz) {
            this.zRes = resz;
            grid = new RegularGrid2d[resz];
            for (int i = 0; i < resz; ++i)
                grid[i] = new RegularGrid2d(resx, resy);
            depthMarginals = new float[resz];
        }

        public Vector3 Sample(Vector3 primary) {
            var (depthIdx, relDepth) = depthDistribution.Sample(primary.Z);
            float z = (depthIdx + relDepth) / zRes;
            var pos = grid[depthIdx].Sample(new Vector2(primary.X, primary.Y));
            return new Vector3(pos.X, pos.Y, z);
        }

        public float Pdf(Vector3 pos) {
            int d = Math.Min((int)(pos.Z * zRes), zRes - 1);
            float pz = depthDistribution.Probability(d) * zRes;
            if (pz == 0) return 0;
            float pxy = grid[d].Pdf(new Vector2(pos.X, pos.Y));
            return pz * pxy;
        }

        public void Splat(float x, float y, float z, float value) {
            int d = (int)(z * zRes);
            depthMarginals[d] += value;
            grid[d].Splat(x, y, value);
        }

        public void Normalize() {
            depthDistribution = new PiecewiseConstant(depthMarginals);
            for (int i = 0; i < zRes; ++i) {
                if (depthMarginals[i] > 0) grid[i].Normalize();
            }
        }

        RegularGrid2d[] grid;
        int zRes;
        float[] depthMarginals;
        PiecewiseConstant depthDistribution;
    }
}