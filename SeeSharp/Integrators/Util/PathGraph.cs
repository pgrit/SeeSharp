using System.Linq;

namespace SeeSharp.Integrators.Util;
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