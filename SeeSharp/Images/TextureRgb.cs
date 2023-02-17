namespace SeeSharp.Images;

/// <summary>
/// An RGB image texture
/// </summary>
public class TextureRgb : ImageTexture {
    /// <summary>
    /// Creates a single-pixel image texture with a constant color
    /// </summary>
    public TextureRgb(RgbColor color) => constColor = color;

    /// <summary>
    /// Creates a texture from an RGB image
    /// </summary>
    /// <param name="filename">Full path to the RGB image</param>
    public TextureRgb(string filename) => Image = new RgbImage(filename);

    /// <summary>
    /// Creates a texture from an RGB image
    /// </summary>
    public TextureRgb(RgbImage image) => Image = image;

    /// <summary>
    /// True if the texture is just a single constant value
    /// </summary>
    public bool IsConstant => Image == null;

    /// <returns>Color value at the given uv-coordinates</returns>
    public RgbColor Lookup(Vector2 uv) {
        if (Image == null)
            return constColor;

        (int col, int row) = ComputeTexel(uv);
        return (Image as RgbImage).GetPixel(col, row);
    }

    RgbColor constColor;
}