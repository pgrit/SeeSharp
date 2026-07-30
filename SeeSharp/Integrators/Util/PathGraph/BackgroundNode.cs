namespace SeeSharp.Integrators.Util;

public class BackgroundNode : PathGraphNode {
    public BackgroundNode(Vector3 direction, PathGraphNode ancestor, RgbColor contrib, float misWeight) : base(ancestor.Position + direction) {
        Emission = contrib;
        MISWeight = misWeight;
    }
    public readonly RgbColor Emission;
    public readonly float MISWeight;
    public override bool IsBackground => true;

    public override RgbColor ComputeVisualizerColor() => RgbColor.SrgbToLinear(170, 231, 232);
}