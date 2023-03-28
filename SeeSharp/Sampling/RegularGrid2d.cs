namespace SeeSharp.Sampling;

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

    public Vector2 SampleInverse(Vector2 sample) {
        float col = sample.X * numCols;
        int colIdx = (int)col;

        float row = sample.Y * numRows;
        int rowIdx = (int)row;

        float x = colDistributions[rowIdx].SampleInverse(colIdx, col - colIdx);
        float y = rowDistribution.SampleInverse(rowIdx, row - rowIdx);
        return new(x, y);
    }

    public float Pdf(Vector2 pos) {
        int row = Math.Min(Math.Max((int)(pos.Y * numRows), 0), numRows - 1);
        int col = Math.Min(Math.Max((int)(pos.X * numCols), 0), numCols - 1);
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
        Splat(col, row, value);
    }

    public void Splat(int col, int row, float value) {
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

    /// <summary>
    /// Applies a clipping operation to alter the PDF to only sample the strongest regions. This only makes
    /// sense when used in an MIS / mixture combination.
    /// Applies the logic of: Karlik et al. 2019. MIS Compensation (SIGGRAPH Asia)
    /// The PDF is correctly normalized at the end.
    /// </summary>
    public void ApplyMISCompensation() {
        float avg = 0;
        for (int row = 0; row < numRows; ++row)
            avg += rowMarginals[row];
        avg /= (numRows * numCols);

        for (int row = 0; row < numRows; ++row) {
            rowMarginals[row] = 0;
            for (int col = 0; col < numCols; ++col) {
                int idx = row * numCols + col;
                density[idx] = Math.Max(density[idx] - avg, 0.0f);
                rowMarginals[row] += density[idx];
            }

        }

        Normalize();
    }

    float[] density;
    float[] rowMarginals;
    int numCols, numRows;

    PiecewiseConstant rowDistribution;
    List<PiecewiseConstant> colDistributions;
}