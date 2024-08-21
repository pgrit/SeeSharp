namespace SeeSharp.Integrators.Bidir;

/// <summary>
/// Assembles the pdf values in two arrays. The elements of each array
/// correspond to the pdf values of sampling each vertex along the path.
/// [0] is the primary vertex after the camera
/// [numPdfs] is the last vertex, the one on the light source itself.
/// </summary>
public ref struct BidirPathPdfs {
    private readonly LightPathCache lightPathCache;

    /// <summary>
    /// Number of pdfs along the path
    /// </summary>
    public int NumPdfs => PdfsLightToCamera.Length;

    /// <summary>
    /// The surface area pdfs for each vertex along the path, when traced from the light.
    /// The [i]th value is the pdf of sampling this vertex from its ancestor, which is the [i+1]th value.
    /// </summary>
    public readonly Span<float> PdfsLightToCamera;

    /// <summary>
    /// The surface area pdf for each vertex along the path, when traced from the camera.
    /// The [i]th value is the pdf of sampling this vertex from its ancestor, which is the [i-1]th value.
    /// </summary>
    public readonly Span<float> PdfsCameraToLight;

    /// <summary>
    /// PDF of doing next event estimation at the final vertex. Tracked separately so we can separately enable
    /// or disable BSDF light hits and next event
    /// </summary>
    public float PdfNextEvent;

    /// <summary>
    /// Prepares <see langword="this"/> object to compute the pdf values into two preallocated arrays.
    /// </summary>
    /// <param name="cache">The cached light paths</param>
    /// <param name="lightToCam">Pre-allocated memory for the light path pdfs</param>
    /// <param name="camToLight">Pre-allocated memory for the camera path pdfs</param>
    public BidirPathPdfs(LightPathCache cache, Span<float> lightToCam, Span<float> camToLight) {
        PdfsCameraToLight = camToLight;
        PdfsLightToCamera = lightToCam;
        lightPathCache = cache;
    }

    /// <summary>
    /// Gathers the surface area pdfs along a camera path in our look-up array
    /// </summary>
    /// <param name="cameraPath">The camera path</param>
    /// <param name="lastCameraVertexIdx">
    /// The index of the last vertex along the path. Can be smaller than the actual length to accumulate
    /// only the pdfs of a sub-path.
    /// </param>
    public void GatherCameraPdfs<T>(in BidirBase<T>.CameraPath cameraPath, int lastCameraVertexIdx) {
        for (int i = 0; i < lastCameraVertexIdx; ++i) {
            PdfsCameraToLight[i] = cameraPath.Vertices[i].PdfFromAncestor;
            if (i < lastCameraVertexIdx - 1)
                PdfsLightToCamera[i] = cameraPath.Vertices[i + 1].PdfToAncestor;
        }
    }

    /// <summary>
    /// Gathers the surface area pdfs along a light path into our look-up array
    /// </summary>
    /// <param name="lightVertex">The last vertex of the light path</param>
    /// <param name="lastCameraVertexIdx">
    /// Index of the last vertex that was sampled via a camera path. Everything after that position
    /// is filled with the forward and backward sampling pdfs along the light path.
    /// </param>
    public void GatherLightPdfs(in PathVertex lightVertex, int lastCameraVertexIdx) {
        var nextVert = lightVertex;
        for (int i = lastCameraVertexIdx + 1; i < NumPdfs - 2; ++i) {
            PdfsLightToCamera[i] = nextVert.PdfFromAncestor;
            PdfsCameraToLight[i + 2] = nextVert.PdfReverseAncestor;
            PdfNextEvent += nextVert.PdfNextEventAncestor; // All but one are zero, so we are lazy and add them up instead of picking the correct one
            nextVert = lightPathCache[nextVert.PathId, nextVert.Depth - 1];
        }
        PdfsLightToCamera[^2] = nextVert.PdfFromAncestor;
        PdfsLightToCamera[^1] = 1;
    }
}
