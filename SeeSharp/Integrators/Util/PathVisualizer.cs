namespace SeeSharp.Integrators.Util;

/// <summary>
/// Renders an eye-light shaded version of the scene and visualizes a set of paths with one arrow per edge.
/// </summary>
public class PathVisualizer : DebugVisualizer {
    /// <summary>
    /// If set, determines the color of each path type that has an entry in the dictionary.
    /// </summary>
    public Dictionary<int, RgbColor> TypeToColor;

    /// <summary>
    /// The set of paths to display
    /// </summary>
    public List<LoggedPath> Paths;

    /// <summary>
    /// Radius of the arrow's cylinder segment, as a fraction of the scene radius.
    /// </summary>
    public float Radius = 0.01f;

    /// <summary>
    /// Length of the arrow head, as a fraction of the  radius.
    /// </summary>
    public float HeadHeight = 4.0f;

    /// <summary>
    /// Number of quads used to model the cylinder / cone that make up each arrow.
    /// </summary>
    public int NumSegments = 16;

    /// <inheritdoc />
    public override void Render(Scene scene) {
        curScene?.Dispose();
        curScene = scene.Copy();
        curScene.FrameBuffer = scene.FrameBuffer;

        markerTypes = new();
        meshToPath = new();

        if (Paths != null) {
            MakePathArrows();
        }
        curScene.Prepare();
        base.Render(curScene);
    }

    /// <returns>A grayscale color for scene geometry or the color of the intersected path marker</returns>
    /// <inheritdoc />
    public override RgbColor ComputeColor(SurfacePoint hit, Vector3 from, uint row, uint col) {
        int type;
        if (!markerTypes.TryGetValue(hit.Mesh, out type))
            return base.ComputeColor(hit, from, row, col);

        RgbColor color = new RgbColor(1, 0, 0);
        TypeToColor?.TryGetValue(type, out color);

        float cosine = Math.Abs(Vector3.Dot(hit.Normal, from));
        cosine /= hit.Normal.Length();
        cosine /= from.Length();
        return color * cosine;
    }

    void MakeArrow(Vector3 start, Vector3 end, int type, LoggedPath path) {
        float radius = curScene.Radius * Radius;
        float headHeight = radius * HeadHeight;

        var headStart = end + headHeight * Vector3.Normalize(start - end);
        var line = MeshFactory.MakeCylinder(start, headStart, radius, NumSegments);
        var head = MeshFactory.MakeCone(headStart, end, radius * 2, NumSegments);
        line.Material = new SeeSharp.Shading.Materials.DiffuseMaterial(new());
        head.Material = new SeeSharp.Shading.Materials.DiffuseMaterial(new());

        curScene.Meshes.Add(line);
        curScene.Meshes.Add(head);

        markerTypes.Add(line, type);
        markerTypes.Add(head, type);
        meshToPath.Add(line, path);
        meshToPath.Add(head, path);
    }

    void MakePathArrows() {
        foreach (var path in Paths) {
            // Iterate over all edges
            for (int i = 0; i < path.Vertices.Count - 1; ++i) {
                var start = path.Vertices[i];
                var end = path.Vertices[i + 1];
                int type = path.UserTypes[i];
                MakeArrow(start, end, type, path);
            }
        }
    }

    public LoggedPath QueryPath(Pixel pixel) {
        var hit = curScene.RayCast(pixel);
        if (!hit || !meshToPath.TryGetValue(hit.Mesh, out var path))
            return null;
        return path;
    }

    protected Dictionary<Mesh, int> markerTypes = new();
    protected Dictionary<Mesh, LoggedPath> meshToPath = new();
    protected Scene curScene;
}