using System.Linq;

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

public class LightPathNode(PathVertex lightVertex) : PathGraphNode(lightVertex.Point.Position) {
    public PathVertex LightVertex { get; } = lightVertex;

    public override RgbColor ComputeVisualizerColor() => RgbColor.SrgbToLinear(228, 135, 17);
}

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

public class PathGraph {
    public List<PathGraphNode> Roots = [];

    public PathGraph Clone() {
        var result = MemberwiseClone() as PathGraph;
        result.Roots = [];
        foreach (var r in Roots) {
            result.Roots.Add(r);
        }
        return result;
    }

    struct PlyVertex {
        public Vector3 Position;
        public byte R, G, B;
    }
    struct PlyEdge {
        public int V1, V2;
    }

    static void AddNode(PathGraphNode node, List<PlyVertex> vertices, List<PlyEdge> edges) {
        foreach (var s in node.Successors) {
            var clr = s.ComputeVisualizerColor();
            var (r, g, b) = RgbColor.LinearToSrgb(clr);
            vertices.Add(new() {
                Position = node.Position,
                R = (byte)(r * 255),
                G = (byte)(g * 255),
                B = (byte)(b * 255),
            });
            vertices.Add(new() {
                Position = s.Position,
                R = (byte)(r * 255),
                G = (byte)(g * 255),
                B = (byte)(b * 255),
            });

            edges.Add(new() {
                V1 = vertices.Count - 2,
                V2 = vertices.Count - 1,
            });

            AddNode(s, vertices, edges);
        }
    }

    public static string ConvertToPLY(PathGraphNode startNode) {
        List<PlyVertex> vertices = [];
        List<PlyEdge> edges = [];
        AddNode(startNode, vertices, edges);

        string vertexStr =
            string.Join('\n', vertices.Select(v => $"{v.Position.X} {v.Position.Y} {v.Position.Z} {v.R} {v.G} {v.B}"));
        string edgeStr =
            string.Join('\n', edges.Select(v => $"{v.V1} {v.V2}"));

        string ply = $"""
        ply
        format ascii 1.0
        element vertex {vertices.Count}
        property float x
        property float y
        property float z
        property uchar red
        property uchar green
        property uchar blue
        element edge {edges.Count}
        property int vertex1
        property int vertex2
        end_header
        {vertexStr}
        {edgeStr}
        """;

        return ply;
    }

    public string ConvertToPLY() {
        List<PlyVertex> vertices = [];
        List<PlyEdge> edges = [];
        foreach (var node in Roots)
            AddNode(node, vertices, edges);

        string vertexStr =
            string.Join('\n', vertices.Select(v => $"{v.Position.X} {v.Position.Y} {v.Position.Z} {v.R} {v.G} {v.B}"));
        string edgeStr =
            string.Join('\n', edges.Select(v => $"{v.V1} {v.V2}"));

        string ply = $"""
        ply
        format ascii 1.0
        element vertex {vertices.Count}
        property float x
        property float y
        property float z
        property uchar red
        property uchar green
        property uchar blue
        element edge {edges.Count}
        property int vertex1
        property int vertex2
        end_header
        {vertexStr}
        {edgeStr}
        """;

        return ply;
    }
}