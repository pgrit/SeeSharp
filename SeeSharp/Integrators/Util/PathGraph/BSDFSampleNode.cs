namespace SeeSharp.Integrators.Util;

public class BSDFSampleNode : PathGraphNode {
    public BSDFSampleNode(SurfacePoint point, RgbColor scatterWeight, float survivalProb) : base(point.Position) {
        ScatterWeight = scatterWeight;
        SurvivalProbability = survivalProb;
        Point = point;
    }

    public BSDFSampleNode(SurfacePoint point, RgbColor scatterWeight, float survivalProb, RgbColor emission, float misWeight) : base(point.Position) {
        ScatterWeight = scatterWeight;
        SurvivalProbability = survivalProb;
        Emission = emission;
        MISWeight = misWeight;
        Point = point;
    }

    public readonly RgbColor ScatterWeight;
    public readonly float SurvivalProbability;
    public readonly RgbColor Emission;
    public readonly float MISWeight;
    public SurfacePoint Point { get; }

    public override RgbColor ComputeVisualizerColor() => RgbColor.SrgbToLinear(41, 107, 177);
}
