namespace SeeSharp.Integrators.Util;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(PathGraphNode), "PathGraphNode")]
[JsonDerivedType(typeof(NextEventNode), "NextEventNode")]
[JsonDerivedType(typeof(BSDFSampleNode), "BSDFSampleNode")]
[JsonDerivedType(typeof(LightPathNode), "LightPathNode")]
[JsonDerivedType(typeof(ConnectionNode), "ConnectionNode")]
[JsonDerivedType(typeof(MergeNode), "MergeNode")]
[JsonDerivedType(typeof(BackgroundNode), "BackgroundNode")]
public class PathGraphNode(Vector3 pos, PathGraphNode ancestor = null) {
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..30];
    public Vector3 Position { get; set; } = pos;
    [JsonIgnore] public PathGraphNode Ancestor = ancestor;

    [JsonPropertyName("ancestorId")]
    public string? AncestorId => Ancestor?.Id;
    public List<PathGraphNode> Successors = [];

    public virtual bool IsBackground => false;

    public PathGraphNode AddSuccessor(PathGraphNode vertex) {
        Successors.Add(vertex);
        vertex.Ancestor = this;
        return vertex;
    }

    public virtual RgbColor ComputeVisualizerColor() {
        return RgbColor.Black;
    }

    public PathGraphNode Clone() {
        var result = MemberwiseClone() as PathGraphNode;
        result.Successors = [];
        foreach (var s in Successors) {
            var sClone = s.Clone();
            sClone.Ancestor = result;
            result.Successors.Add(sClone);
        }
        return result;
    }
}

public interface IContribNode {
    public RgbColor Contrib { get; }
    public float MISWeight { get; }
}