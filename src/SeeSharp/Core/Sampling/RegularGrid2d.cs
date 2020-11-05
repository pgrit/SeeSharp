using System;
using System.Collections.Generic;
using System.Numerics;

namespace SeeSharp.Core.Sampling {
    /// <summary>
    /// A regular grid on the unit square.
    /// Useful for describing 2D pdfs in primary sample space.
    /// </summary>
    public class RegularGrid2d {
        public RegularGrid2d(int resX, int resY) {
            density = new float[resX * resY];
            rowMarginals = new float[resY];
            numCols = resX;
            numRows = resY;
        }

        /// <summary>
        /// Applies the primary space sample warp that is described by this grid.
        /// </summary>
        /// <param name="primary">Primary space sample location</param>
        /// <returns>The sample position in the 2d unit square</returns>
        public Vector2 Sample(Vector2 primary) {
            var (rowIdx, relRowPos) = rowDistribution.Sample(primary.Y);
            var (colIdx, relColPos) = colDistributions[rowIdx].Sample(primary.X);

            return new Vector2((colIdx + relColPos) / numCols,
                               (rowIdx + relRowPos) / numRows);
        }

        public float Pdf(Vector2 pos) {
            int row = Math.Min((int)(pos.Y * numRows), numRows - 1);
            int col = Math.Min((int)(pos.X * numCols), numCols - 1);

            float probability = rowDistribution.Probability(row);
            if (probability == 0) return 0;
            probability *= colDistributions[row].Probability(col);
            return probability * (numRows * numCols);
        }

        /// <summary>
        /// Records a density value at a given position.
        /// The tabulated distribution will no longer be normalized after calling this function.
        /// </summary>
        /// <param name="x">Horizontal position on the 2d unit square</param>
        /// <param name="y">Vertical position on the 2d unit square</param>
        /// <param name="value">Pdf value to record</param>
        public void Splat(float x, float y, float value) {
            int row = (int)(y * numRows);
            int col = (int)(x * numCols);
            density[row * numCols + col] += value;
            rowMarginals[row] += value;
        }

        public void Normalize() {
            // Build a 1D CDF to select a row based on the marginals
            rowDistribution = new PiecewiseConstant(rowMarginals);

            // Build a 1D CDF to select a column within each row
            colDistributions = new List<PiecewiseConstant>(numRows);
            for (int i = 0; i < numRows; ++i) {
                if (rowMarginals[i] == 0) {
                    colDistributions.Add(null);
                    continue;
                }
                var row = new Span<float>(density, i * numCols, numCols);
                colDistributions.Add(new PiecewiseConstant(row));
            }
        }

        float[] density;
        float[] rowMarginals;
        int numCols, numRows;

        PiecewiseConstant rowDistribution;
        List<PiecewiseConstant> colDistributions;
    }
}