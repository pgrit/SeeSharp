namespace SeeSharp.Images;

/// <summary>
/// Convenience layer for images storing monochromatic values
/// </summary>
public class MonoLayer : Layer {
    /// <summary>
    /// Called once before the first rendering iteration
    /// </summary>
    /// <param name="width">The width of the frame buffer</param>
    /// <param name="height">The height of the frame buffer</param>
    public override void Init(int width, int height) => Image = new MonochromeImage(width, height);

    /// <summary>
    /// Adds a new sample contribution to the layer
    /// </summary>
    public virtual void Splat(float x, float y, float value)
    => (Image as MonochromeImage).AtomicAdd((int)x, (int)y, value / curIteration);
}