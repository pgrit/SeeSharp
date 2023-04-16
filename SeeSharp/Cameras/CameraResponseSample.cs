namespace SeeSharp.Cameras;

/// <summary>
/// Stores the relevant data when sampling a connection to the camera
/// </summary>
public struct CameraResponseSample {
    /// <summary>
    /// Pixel coordinates
    /// </summary>
    public Pixel Pixel;

    /// <summary>
    /// Position of the lens point in world space, the sample only contributes if this position
    /// is visible from the scene point in question.
    /// </summary>
    public Vector3 Position;

    /// <summary>
    /// Contribution to the sampled pixel ("importance" divided by the pdf)
    /// </summary>
    public RgbColor Weight;

    /// <summary>
    /// Probability of sampling this connection
    /// </summary>
    public float PdfConnect;

    /// <summary>
    /// Probability of instead sampling a ray from the camera into the scene.
    /// Unit: surface area at primary hit point times [whatever happens on the specific camera model]
    /// </summary>
    public float PdfEmit;

    /// <summary>
    /// Checks whether this is a valid sample, i.e., non-zero and sampled with non-zero pdf
    /// </summary>
    public bool IsValid => Weight != RgbColor.Black && PdfConnect != 0 && PdfEmit != 0;

    /// <summary>
    /// An invalid sample is one where everything is set to zero.
    /// </summary>
    public static CameraResponseSample Invalid => new();
}
