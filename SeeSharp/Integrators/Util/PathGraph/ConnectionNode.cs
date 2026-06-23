namespace SeeSharp.Integrators.Util;

public class ConnectionNode : PathGraphNode, IContribNode {
    public ConnectionNode(PathVertex lightVertex, float misWeight, RgbColor contrib)
    : base(lightVertex.Point.Position) {
        Contrib = contrib;
        MISWeight = misWeight;
        LightVertex = lightVertex;
    }

    public RgbColor Contrib { get; init; }
    public float MISWeight { get; init; }
    public PathVertex LightVertex { get; }

    public override RgbColor ComputeVisualizerColor() => RgbColor.SrgbToLinear(167, 214, 170);
}