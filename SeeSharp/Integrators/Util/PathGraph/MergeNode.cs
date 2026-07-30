namespace SeeSharp.Integrators.Util;

public class MergeNode : PathGraphNode, IContribNode {
    public MergeNode(PathVertex lightVertex, float misWeight, RgbColor contrib)
    : base(lightVertex.Point.Position) {
        Contrib = contrib;
        MISWeight = misWeight;
        LightVertex = lightVertex;
    }

    public RgbColor Contrib { get; init; }
    public float MISWeight { get; init; }
    public PathVertex LightVertex { get; }

    public override RgbColor ComputeVisualizerColor() => RgbColor.SrgbToLinear(218, 152, 204);
}