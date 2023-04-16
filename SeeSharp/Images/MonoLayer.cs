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
    public virtual void Splat(int x, int y, float value)
    => (Image as MonochromeImage).AtomicAdd(x, y, value / curIteration);

    /// <summary>
    /// Adds a new sample contribution to the layer
    /// </summary>
    public virtual void Splat(Pixel pixel, float value) => Splat(pixel.Col, pixel.Row, value);
}