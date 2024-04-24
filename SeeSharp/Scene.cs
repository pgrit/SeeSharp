using System.Collections.Frozen;

namespace SeeSharp;

/// <summary>
/// Manages all information required to render a scene. Each object is meant to be used to render one
/// image. But shallow copies can be made (with replaced frame buffers) to re-render the same scene.
/// </summary>
public class Scene : IDisposable {
    /// <summary>
    /// Name of the scene - if available. If loaded from file, this is the basename of the file.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The default exposure value, i.e., when mapped to LDR, pixel values of a rendered image of this
    /// scene should be multiplied by 2^this by default.
    /// </summary>
    public float RecommendedExposure { get; set; }

    /// <summary>
    /// The frame buffer that will receive the rendered image. Ownership of the framebuffer is
    /// transferred to this object, i.e., the framebuffer will be disposed along with this scene or
    /// when it is replaced.
    /// </summary>
    public FrameBuffer FrameBuffer {
        get => frameBuffer;
        set {
            frameBuffer?.Dispose();
            frameBuffer = value;
        }
    }
    FrameBuffer frameBuffer;

    /// <summary>
    /// The camera, which models how the frame buffer receives light from the scene
    /// </summary>
    public Camera Camera;

    /// <summary>
    /// All meshes in the scene. There needs to be at least one.
    /// </summary>
    public List<Mesh> Meshes = new();

    /// <summary>
    /// Acceleration structure for ray tracing the meshes
    /// </summary>
    public Raytracer Raytracer { get; private set; }

    /// <summary>
    /// All emitters in the scene. There needs to be at least one, unless a background is given.
    /// </summary>
    public List<Emitter> Emitters { get; private set; } = new();

    /// <summary>
    /// Defines radiance from rays that leave the scene. Must be given if no emitters are present.
    /// </summary>
    public Background Background;

    /// <summary>
    /// Center of the geometry in the scene. Computed by <see cref="Prepare"/>
    /// </summary>
    public Vector3 Center { get; private set; }

    /// <summary>
    /// Radius of the scene bounding sphere. Computed by <see cref="Prepare"/>
    /// </summary>
    public float Radius { get; private set; }

    /// <summary>
    /// Axis aligned bounding box of the scene. Computed by <see cref="Prepare"/>
    /// </summary>
    public BoundingBox Bounds { get; private set; }

    /// <summary>
    /// If <see cref="IsValid"/> is false, this list will contain all error messages found during validation.
    /// </summary>
    public List<string> ValidationErrorMessages { get; private set; } = new();

    /// <summary>
    /// Creates a semi-deep copy of the scene. That is, a shallow copy except that all lists of references
    /// are copied into new lists of references. So meshes in the new scene can be removed or added.
    /// The <see cref="FrameBuffer" /> and <see cref="Raytracer" /> are not copied and set to null, to
    /// avoid any conflicts. The scene is in an invalid state until <see cref="Prepare" /> is called.
    /// </summary>
    /// <returns>A copy of the scene</returns>
    public Scene Copy() {
        Scene cpy = (Scene)MemberwiseClone();
        cpy.Meshes = new(Meshes);
        cpy.Emitters = new(Emitters);
        cpy.ValidationErrorMessages = new();
        cpy.Camera = Camera.Copy();
        cpy.FrameBuffer = null;
        cpy.Raytracer = null;
        cpy.Name = Name;
        return cpy;
    }

    /// <summary>
    /// Prepares the scene for rendering. Checks that there is no missing data, builds acceleration
    /// structures, etc.
    /// </summary>
    public void Prepare() {
        if (!IsValid)
            throw new InvalidOperationException("Cannot finalize an invalid scene.");

        // Prepare the scene geometry for ray tracing.
        Raytracer?.Dispose();
        Raytracer = new();
        for (int idx = 0; idx < Meshes.Count; ++idx) {
            Raytracer.AddMesh(Meshes[idx]);
        }
        Raytracer.CommitScene();

        // Compute the bounding sphere and bounding box of the scene.
        // 1) Compute the center (the average of all vertex positions), and bounding box
        Bounds = BoundingBox.Empty;
        var center = Vector3.Zero;
        ulong totalVertices = 0;
        for (int idx = 0; idx < Meshes.Count; ++idx) {
            foreach (var vert in Meshes[idx].Vertices) {
                center += vert;
                Bounds = Bounds.GrowToContain(vert);
            }
            totalVertices += (ulong)Meshes[idx].Vertices.Length;
        }
        Center = center / totalVertices;

        // 2) Compute the radius of the tight bounding box: the distance to the furthest vertex
        float radius = 0;
        for (int idx = 0; idx < Meshes.Count; ++idx) {
            foreach (var vert in Meshes[idx].Vertices) {
                radius = MathF.Max((vert - Center).LengthSquared(), radius);
            }
        }
        Radius = MathF.Sqrt(radius);

        // If a background is set, pass the scene center and radius to it
        if (Background != null) {
            Background.SceneCenter = Center;
            Background.SceneRadius = Radius;
        }

        // Make sure the camera is set for the correct resolution.
        Camera.UpdateResolution(FrameBuffer.Width, FrameBuffer.Height);

        // Build the mesh to emitter mapping
        Dictionary<Mesh, Dictionary<int, Emitter>> meshToEmitterTemp = [];
        foreach (var emitter in Emitters) {
            if (!meshToEmitterTemp.ContainsKey(emitter.Mesh))
                meshToEmitterTemp.Add(emitter.Mesh, new());
            meshToEmitterTemp[emitter.Mesh].Add(emitter.Triangle.FaceIndex, emitter);
        }
        Dictionary<Mesh, FrozenDictionary<int, Emitter>> frozenMeshes = [];
        foreach (var kv in meshToEmitterTemp) {
            frozenMeshes[kv.Key] = kv.Value.ToFrozenDictionary();
        }
        meshToEmitter = frozenMeshes.ToFrozenDictionary();

        Dictionary<Emitter, int> emitterToIdxTemp = [];
        for (int i = 0; i < Emitters.Count; ++i)
            emitterToIdxTemp.Add(Emitters[i], i);
        emitterToIdx = emitterToIdxTemp.ToFrozenDictionary();
    }

    /// <summary>
    /// True, if the scene is valid. Any errors found whil accessing the property will be
    /// reported in the <see cref="ValidationErrorMessages"/> list.
    /// </summary>
    public bool IsValid {
        get {
            ValidationErrorMessages.Clear();

            if (FrameBuffer == null)
                ValidationErrorMessages.Add("Framebuffer not set.");
            if (Camera == null)
                ValidationErrorMessages.Add("Camera not set.");
            if (Meshes == null || Meshes.Count == 0)
                ValidationErrorMessages.Add("No meshes in the scene.");

            int idx = 0;
            foreach (var m in Meshes) {
                if (m.Material == null)
                    ValidationErrorMessages.Add($"Mesh[{idx}] does not have a material.");
                idx++;
            }

            if (Emitters.Count == 0 && Background == null)
                ValidationErrorMessages.Add("No emitters and no background in the scene.");

            foreach (string msg in ValidationErrorMessages) {
                Logger.Log(msg, Verbosity.Error);
            }

            return ValidationErrorMessages.Count == 0;
        }
    }

    /// <summary>
    /// Returns the emitter attached to the mesh on which a <see cref="SurfacePoint"/> lies.
    /// </summary>
    /// <param name="point">A point on a mesh surface.</param>
    /// <returns>The attached emitter reference, or null.</returns>
    public Emitter QueryEmitter(SurfacePoint point) {
        if (!meshToEmitter.TryGetValue(point.Mesh, out var meshEmitters))
            return null;
        if (!meshEmitters.TryGetValue((int)point.PrimId, out var emitter))
            return null;
        return emitter;
    }

    public FrozenDictionary<int, Emitter> GetMeshEmitters(Mesh mesh) {
        // TODO do we even want to support only _some_ triangles of a mesh being emissive?
        if (!meshToEmitter.TryGetValue(mesh, out var meshEmitters))
            return null;
        return meshEmitters;
    }

    /// <param name="emitter">An emitter object</param>
    /// <returns>Index of this emitter in the <see cref="Emitters"/> list</returns>
    public int GetEmitterIndex(Emitter emitter) => emitterToIdx[emitter];

    /// <summary>
    /// Loads a .json file and parses it as a scene. Assumes the file has been validated against
    /// the correct schema.
    /// </summary>
    /// <param name="path">Path to the .json scene file.</param>
    /// <returns>The scene created from the file.</returns>
    public static Scene LoadFromFile(string path) {
        return IO.JsonScene.LoadFromFile(path);
    }

    /// <summary>
    /// Frees all unmanaged resources in the `FrameBuffer` and `Raytracer`
    /// </summary>
    public void Dispose() {
        FrameBuffer?.Dispose();
        FrameBuffer = null;
        Raytracer?.Dispose();
        Raytracer = null;
    }

    FrozenDictionary<Mesh, FrozenDictionary<int, Emitter>> meshToEmitter;
    FrozenDictionary<Emitter, int> emitterToIdx;

    /// <summary>
    /// Convenience function to cast a ray through the center of a pixel and query its primary hit point.
    /// </summary>
    public SurfacePoint RayCast(Pixel pixel) {
        RNG rng = new();
        var ray = Camera.GenerateRay(new Vector2(pixel.Col + 0.5f, pixel.Row + 0.5f), ref rng).Ray;
        return (SurfacePoint)Raytracer.Trace(ray);
    }
}
