using System.Text.Json;

public record Vec3DTO(float X, float Y, float Z);
public record Vec2DTO(float X, float Y);

public record SurfacePointDTO(Vec3DTO Position, Vec3DTO Normal, Vec2DTO BarycentricCoords,
        Mesh Mesh, uint PrimId, float ErrorOffset,
        float Distance, Vec3DTO ShadingNormal, Vec2DTO TextureCoordinates, Material Material);

public record PathVertexDTO(SurfacePointDTO Point, float PdfFromAncestor, float PdfReverseAncestor,
        float PdfNextEventAncestor, Vec3DTO DirToAncestor, float JacobianToAncestor,
        RgbColor Weight, int PathId, byte Depth, float MaximumRoughness, bool FromBackground);
public class NodeDTO
{
    public string NodeType { get; set; }
    public Vec3DTO Position { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

public static class GraphNodeSerializer
{
    public static NodeDTO Serialize(PathGraphNode node)
    {
        return node switch
        {
            NextEventNode n => SerializeNextEventNode(n),
            BSDFSampleNode n => SerializeBSDFSampleNode(n),
            LightPathNode n => SerializeLightPathNode(n),
            ConnectionNode n => SerializeConnectionNode(n),
            MergeNode n => SerializeMergeNode(n),
            BackgroundNode n => SerializeBackgroundNode(n),
            _ => SerializeBase(node)
        };
    }

    public static Vec3DTO ToDTO(this Vector3 v)
        => new(v.X, v.Y, v.Z);

    public static Vec2DTO ToDTO(this Vector2 v)
        => new(v.X, v.Y);
    public static SurfacePointDTO toDTO(this SurfacePoint s)
        => new(s.Position.ToDTO(), s.Normal.ToDTO(), s.BarycentricCoords.ToDTO(),
         s.Mesh, s.PrimId, s.ErrorOffset, s.Distance, s.ShadingNormal.ToDTO(), s.TextureCoordinates.ToDTO(), s.Material);

    public static PathVertexDTO toDTO(this PathVertex p)
        => new(p.Point.toDTO(), p.PdfFromAncestor, p.PdfReverseAncestor, p.PdfNextEventAncestor, p.DirToAncestor.ToDTO(),
        p.JacobianToAncestor, p.Weight, p.PathId, p.Depth, p.MaximumRoughness, p.FromBackground);
    private static NodeDTO SerializeBase(PathGraphNode n)
    {
        return new NodeDTO
        {
            NodeType = n.GetType().Name,
            Position = n.Position.ToDTO(),
            Data = {
                ["IsBackground"] = n.IsBackground,
                ["SuccessorCount"] = n.Successors.Count
            }
        };
    }

    private static NodeDTO SerializeNextEventNode(NextEventNode n)
    {
        return new NodeDTO
        {
            NodeType = "NextEventNode",
            Position = n.Position.ToDTO(),
            Data = {
                ["Emission"] = n.Emission,
                ["Pdf"] = n.Pdf,
                ["BsdfTimesCosine"] = n.BsdfTimesCosine,
                ["MISWeight"] = n.MISWeight,
                ["PrefixWeight"] = n.PrefixWeight,
                ["HasSurfacePoint"] = n.Point != null
            }
        };
    }

    private static NodeDTO SerializeBSDFSampleNode(BSDFSampleNode n)
    {
        return new NodeDTO
        {
            NodeType = "BSDFSampleNode",
            Position = n.Position.ToDTO(),
            Data = {
                ["ScatterWeight"] = n.ScatterWeight,
                ["SurvivalProbability"] = n.SurvivalProbability,
                ["Emission"] = n.Emission,
                ["MISWeight"] = n.MISWeight,
                ["SurfacePoint"] = n.Point.toDTO()
            }
        };
    }

    private static NodeDTO SerializeLightPathNode(LightPathNode n)
    {
        return new NodeDTO
        {
            NodeType = "LightPathNode",
            Position = n.Position.ToDTO(),
            Data = {
                ["LightVertex"] = n.LightVertex.toDTO()
            }
        };
    }

    private static NodeDTO SerializeConnectionNode(ConnectionNode n)
    {
        return new NodeDTO
        {
            NodeType = "ConnectionNode",
            Position = n.Position.ToDTO(),
            Data = {
                ["Contrib"] = n.Contrib,
                ["MISWeight"] = n.MISWeight,
                ["LightVertex"] = n.LightVertex.toDTO()
            }
        };
    }

    private static NodeDTO SerializeMergeNode(MergeNode n)
    {
        return new NodeDTO
        {
            NodeType = "MergeNode",
            Position = n.Position.ToDTO(),
            Data = {
                ["Contrib"] = n.Contrib,
                ["MISWeight"] = n.MISWeight,
                ["LightVertex"] = n.LightVertex.toDTO()
            }
        };
    }

    private static NodeDTO SerializeBackgroundNode(BackgroundNode n)
    {
        return new NodeDTO
        {
            NodeType = "BackgroundNode",
            Position = n.Position.ToDTO(),
            Data = {
                ["Emission"] = n.Emission,
                ["MISWeight"] = n.MISWeight
            }
        };
    }
}

public static class NodeIdGenerator
{
    public static string GetId(PathGraphNode node)
        => node.GetHashCode().ToString("X"); // stable enough for session
}

public static class GraphSerializer
{
    public static string SerializeGraph(PathGraphNode root)
    {
        var visited = new HashSet<PathGraphNode>();
        var nodes = new List<Dictionary<string, object>>();
        var edges = new List<(string, string)>();

        Traverse(root, visited, nodes, edges);

        var final = new
        {
            nodes,
            edges = edges.Select(e => new[] { e.Item1, e.Item2 })
        };

        return JsonSerializer.Serialize(final, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    private static void Traverse(
        PathGraphNode node,
        HashSet<PathGraphNode> visited,
        List<Dictionary<string, object>> nodes,
        List<(string, string)> edges)
    {
        if (node == null || visited.Contains(node))
            return;

        visited.Add(node);

        string id = NodeIdGenerator.GetId(node);
        var dto = GraphNodeSerializer.Serialize(node);

        // Convert DTO into raw dictionary for JSON
        var nodeDict = new Dictionary<string, object>
        {
            ["id"] = id,
            ["type"] = dto.NodeType,
            ["position"] = dto.Position,   // Vec3DTO (serializable)
            ["data"] = dto.Data            // Dictionary<string,object>
        };

        nodes.Add(nodeDict);

        // Traverse successors
        foreach (var child in node.Successors)
        {
            if (child != null)
            {
                string childId = NodeIdGenerator.GetId(child);
                edges.Add((id, childId));
            }

            Traverse(child, visited, nodes, edges);
        }
    }
}

