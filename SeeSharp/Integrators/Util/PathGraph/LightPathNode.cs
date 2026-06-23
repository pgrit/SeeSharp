namespace SeeSharp.Integrators.Util;

public class LightPathNode(PathVertex lightVertex) : PathGraphNode(lightVertex.Point.Position) {
    public PathVertex LightVertex { get; } = lightVertex;

    public override RgbColor ComputeVisualizerColor() => RgbColor.SrgbToLinear(228, 135, 17);
}