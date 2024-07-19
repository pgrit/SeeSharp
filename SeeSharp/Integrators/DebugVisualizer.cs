namespace SeeSharp.Integrators;

/// <summary>
/// Renders a simple and fast grayscale visualization of a scene
/// </summary>
public class DebugVisualizer : Integrator {
    /// <summary>
    /// Base seed used for anti-aliasing
    /// </summary>
    public uint BaseSeed = 0xC030114;

    /// <summary>
    /// Number of anti-aliasing samples to take in each pixel
    /// </summary>
    public int TotalSpp = 1;

    /// <summary>
    /// Renders the given scene.
    /// </summary>
    public override void Render(Scene scene) {
        for (uint sampleIndex = 0; sampleIndex < TotalSpp; ++sampleIndex) {
            scene.FrameBuffer.StartIteration();
            System.Threading.Tasks.Parallel.For(0, scene.FrameBuffer.Height,
                row => {
                    for (uint col = 0; col < scene.FrameBuffer.Width; ++col) {
                        RenderPixel(scene, (uint)row, col, sampleIndex);
                    }
                }
            );
            scene.FrameBuffer.EndIteration();
        }
    }

    /// <summary>
    /// The shading value at a primary hit point. The default implementation uses "eye light shading",
    /// i.e., the cosine between the outgoing direction and the normal.
    /// </summary>
    public virtual RgbColor ComputeColor(SurfacePoint hit, Vector3 from, uint row, uint col) {
        float cosine = Math.Abs(Vector3.Dot(hit.Normal, from));
        cosine /= hit.Normal.Length();
        cosine /= from.Length();
        return RgbColor.White * cosine;
    }

    public virtual void RenderPixel(Scene scene, uint row, uint col, uint sampleIndex) {
        // Seed the random number generator
        uint pixelIndex = row * (uint)scene.FrameBuffer.Width + col;
        var rng = new RNG(BaseSeed, pixelIndex, sampleIndex);

        // Sample a ray from the camera
        var offset = rng.NextFloat2D();
        Ray primaryRay = scene.Camera.GenerateRay(new Vector2(col, row) + offset, ref rng).Ray;
        var hit = scene.Raytracer.Trace(primaryRay);

        // Shade and splat
        RgbColor value = RgbColor.Black;
        if (hit) value = ComputeColor(hit, -primaryRay.Direction, row, col);
        scene.FrameBuffer.Splat((int)col, (int)row, value);
    }
}