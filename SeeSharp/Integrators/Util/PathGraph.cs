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

    public void Render(Scene scene, PathGraph graph) {
        float radius = scene.Radius * 0.005f; // TODO better initialization? -- maybe based on the median vertex distance to the camera?

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

public class PathGraphNode(Vector3 pos, PathGraphNode ancestor = null) {
    public Vector3 Position = pos;
    public PathGraphNode Ancestor = ancestor;
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
}

public class NextEventNode : PathGraphNode {
    public NextEventNode(Vector3 direction, PathGraphNode ancestor, RgbColor emission, float pdf,
                         RgbColor bsdfCos, float misWeight)
    : base(ancestor.Position + direction, ancestor) {
        Emission = emission;
        Pdf = pdf;
        BsdfTimesCosine = bsdfCos;
        MISWeight = misWeight;
    }

    public NextEventNode(SurfacePoint point, RgbColor emission, float pdf, RgbColor bsdfCos, float misWeight)
    : base(point.Position) {
        Emission = emission;
        Pdf = pdf;
        BsdfTimesCosine = bsdfCos;
        MISWeight = misWeight;
        Point = point;
    }

    public readonly RgbColor Emission;
    public readonly float Pdf;
    public readonly RgbColor BsdfTimesCosine;
    public readonly float MISWeight;
    public readonly SurfacePoint? Point;

    public override bool IsBackground => !Point.HasValue;
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
    public readonly SurfacePoint Point;

    public override RgbColor ComputeVisualizerColor() => RgbColor.SrgbToLinear(41, 107, 177);
}

public class LightPathNode(PathVertex lightVertex) : PathGraphNode(lightVertex.Point.Position) {
    public readonly PathVertex LightVertex = lightVertex;

    public override RgbColor ComputeVisualizerColor() => RgbColor.SrgbToLinear(228, 135, 17);
}

public class ConnectionNode : PathGraphNode {
    public ConnectionNode(PathVertex lightVertex, float misWeight, RgbColor contrib)
    : base(lightVertex.Point.Position) {
        Contrib = contrib;
        MISWeight = misWeight;
        LightVertex = lightVertex;
    }

    public readonly RgbColor Contrib;
    public readonly float MISWeight;
    public readonly PathVertex LightVertex;

    public override RgbColor ComputeVisualizerColor() => RgbColor.SrgbToLinear(167, 214, 170);
}

public class MergeNode : PathGraphNode {
    public MergeNode(PathVertex lightVertex, float misWeight, RgbColor contrib)
    : base(lightVertex.Point.Position) {
        Contrib = contrib;
        MISWeight = misWeight;
        LightVertex = lightVertex;
    }

    public readonly RgbColor Contrib;
    public readonly float MISWeight;
    public readonly PathVertex LightVertex;

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
}