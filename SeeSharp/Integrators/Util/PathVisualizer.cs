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
    /// Length of the arrow head, as a fraction of the scene radius.
    /// </summary>
    public float HeadHeight = 0.02f;

    /// <summary>
    /// Number of quads used to model the cylinder / cone that make up each arrow.
    /// </summary>
    public int NumSegments = 16;

    /// <inheritdoc />
    public override void Render(Scene scene) {
        curScene = scene;

        if (Paths != null) {
            // Generate and add geometry for the selected paths
            MakePathArrows();
            scene.Prepare();
        }

        base.Render(scene);

        // Remove the marker meshes from the scene and trigger acceleration structure regeneration
        foreach (var mesh in markerTypes.Keys) {
            scene.Meshes.Remove(mesh);
        }
        scene.Prepare();
    }

    /// <returns>A grayscale color for scene geometry or the color of the intersected path marker</returns>
    /// <inheritdoc />
    public override RgbColor ComputeColor(SurfacePoint hit, Vector3 from) {
        int type;
        if (!markerTypes.TryGetValue(hit.Mesh, out type))
            return base.ComputeColor(hit, from);

        RgbColor color = new RgbColor(1, 0, 0);
        TypeToColor?.TryGetValue(type, out color);

        float cosine = Math.Abs(Vector3.Dot(hit.Normal, from));
        cosine /= hit.Normal.Length();
        cosine /= from.Length();
        return color * cosine;
    }

    void MakeArrow(Vector3 start, Vector3 end, int type) {
        float headHeight = curScene.Radius * HeadHeight;
        float radius = curScene.Radius * Radius;

        var headStart = end + headHeight * (start - end);
        var line = MeshFactory.MakeCylinder(start, headStart, radius, NumSegments);
        var head = MeshFactory.MakeCone(headStart, end, radius * 2, NumSegments);
        line.Material = new SeeSharp.Shading.Materials.DiffuseMaterial(new());
        head.Material = new SeeSharp.Shading.Materials.DiffuseMaterial(new());

        curScene.Meshes.Add(line);
        curScene.Meshes.Add(head);

        markerTypes.Add(line, type);
        markerTypes.Add(head, type);
    }

    void MakePathArrows() {
        foreach (var path in Paths) {
            // Iterate over all edges
            for (int i = 0; i < path.Vertices.Count - 1; ++i) {
                var start = path.Vertices[i];
                var end = path.Vertices[i + 1];
                int type = path.UserTypes[i];
                MakeArrow(start, end, type);
            }
        }
    }

    Dictionary<Mesh, int> markerTypes = new();
    Scene curScene;
}