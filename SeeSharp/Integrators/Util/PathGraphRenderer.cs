namespace SeeSharp.Integrators.Util;

public class PathGraphRenderer : DebugVisualizer {
    void AddNode(PathGraphNode node, Scene scene, float radius) {
        if (node.Ancestor != null) { // TODO-HACK to avoid having a sphere around the camera
            var m = MeshFactory.MakeSphere(node.Position, radius, 16);
            m.UserData = node;
            m.Material = new DiffuseMaterial(new()); // only needed to prevent scene validation errors
            scene.Meshes.Add(m);
        }

        foreach (var s in node.Successors) {
            if (node.Ancestor != null) { // TODO-HACK to avoid having a sphere around the camera
                var m = MeshFactory.MakeCylinder(node.Position, s.Position, radius, 16);
                m.UserData = s;
                m.Material = new DiffuseMaterial(new()); // only needed to prevent scene validation errors
                scene.Meshes.Add(m); // TODO-POLISH remove duplicate code
            }

            AddNode(s, scene, radius);
        }
    }

    float ComputeMedianNodeDistance(Vector3 from, PathGraphNode root) {
        Stack<PathGraphNode> stack = new();
        stack.Push(root);
        List<float> distances = [];
        while (stack.Count > 0) {
            var node = stack.Pop();
            distances.Add((node.Position - from).Length());
            foreach (var s in node.Successors)
                stack.Push(s);
        }
        distances.Sort();

        int n = distances.Count;
        if (n % 2 == 0)
            return (distances[n/2] + distances[n/2 + 1]) * 0.5f;
        else
            return distances[(n+1)/2];
    }

    float ComputeRadius(Scene scene, PathGraph graph) {
        float medianDist = ComputeMedianNodeDistance(scene.Camera.Position, graph.Roots[0]); // TODO if we ever actually need multiple roots, this needs updating
        // Set radius so the median point covers desired angle
        return float.Tan(float.DegreesToRadians(0.1f)) * medianDist;
    }

    public void Render(Scene scene, PathGraph graph) {
        float radius = ComputeRadius(scene, graph);

        // Create geometry for the paths nodes and edges
        var sceneCpy = scene.Copy();
        foreach (var node in graph.Roots)
            AddNode(node, sceneCpy, radius);
        sceneCpy.FrameBuffer = scene.FrameBuffer;
        sceneCpy.Prepare();

        base.Render(sceneCpy);
    }

    public override void RenderPixel(Scene scene, uint row, uint col, uint sampleIndex) {
        // Seed the random number generator
        uint pixelIndex = row * (uint)scene.FrameBuffer.Width + col;
        var rng = new RNG(BaseSeed, pixelIndex, sampleIndex);

        // Sample a ray from the camera
        var offset = rng.NextFloat2D();
        Ray ray = scene.Camera.GenerateRay(new Vector2(col, row) + offset, ref rng).Ray;

        RgbColor value = RgbColor.Black;
        RgbColor firstHitValue = RgbColor.Black;
        for (int i = 0; ; i++) {
            SurfacePoint hit = scene.Raytracer.Trace(ray);

            if (!hit) {
                // Only use transparency if there is actually something underneath
                value = firstHitValue;
                break;
            }

            value *= i / (float)(i + 1);

            if (hit.Mesh.UserData is PathGraphNode node) {
                var nodeColor = node.ComputeVisualizerColor();
                value += nodeColor / (i + 1);
                break;
            }

            var surfaceColor = ComputeColor(hit, -ray.Direction, row, col);
            value += surfaceColor / (i + 1);
            ray = Raytracer.SpawnRay(hit, ray.Direction);
            if (i == 0) firstHitValue = surfaceColor;
        }

        // Shade and splat
        scene.FrameBuffer.Splat((int)col, (int)row, value);
    }
}
