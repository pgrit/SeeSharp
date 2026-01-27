namespace SeeSharp.Integrators.Util;
public class NextEventNode : PathGraphNode, IContribNode {
    public NextEventNode(Vector3 direction, PathGraphNode ancestor, RgbColor emission, float pdf,
                         RgbColor bsdfCos, float misWeight, RgbColor prefixWeight)
    : base(ancestor.Position + direction, ancestor) {
        Emission = emission;
        Pdf = pdf;
        BsdfTimesCosine = bsdfCos;
        MISWeight = misWeight;
        PrefixWeight = prefixWeight;
    }

    public NextEventNode(SurfacePoint point, RgbColor emission, float pdf, RgbColor bsdfCos, float misWeight, RgbColor prefixWeight)
    : base(point.Position) {
        Emission = emission;
        Pdf = pdf;
        BsdfTimesCosine = bsdfCos;
        MISWeight = misWeight;
        Point = point;
        PrefixWeight = prefixWeight;
    }

    public readonly RgbColor Emission;
    public readonly float Pdf;
    public readonly RgbColor BsdfTimesCosine;
    public readonly float MISWeight;
    public readonly SurfacePoint? Point;
    public readonly RgbColor PrefixWeight;

    public override bool IsBackground => !Point.HasValue;

    public RgbColor Contrib => PrefixWeight * Emission / Pdf * BsdfTimesCosine;

    float IContribNode.MISWeight => MISWeight;

    public override RgbColor ComputeVisualizerColor() => RgbColor.SrgbToLinear(170, 231, 232);
}
