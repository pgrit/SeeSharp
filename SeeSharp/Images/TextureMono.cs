namespace SeeSharp.Images;

/// <summary>
/// Monochromatic image texture
/// </summary>
public class TextureMono : ImageTexture {
    /// <summary>
    /// Creates a single pixel texture set to a constant value
    /// </summary>
    public TextureMono(float color) => constColor = color;

    /// <summary>
    /// Creates a texture from a monochromatic image
    /// </summary>
    public TextureMono(MonochromeImage img) => Image = img;

    // TODO not yet supported in SimpleImageIO
    // public TextureMono(string filename) => image = new MonochromeImage(filename);

    /// <summary>
    /// True if the texture is just a single constant value
    /// </summary>
    public bool IsConstant => Image == null;

    /// <returns>The texture value for the given uv-coordinates.</returns>
    public float Lookup(Vector2 uv) {
        if (Image == null)
            return constColor;

        (int col, int row) = ComputeTexel(uv);
        return (Image as MonochromeImage).GetPixel(col, row);
    }

    float constColor;
}