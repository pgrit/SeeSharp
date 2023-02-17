namespace SeeSharp.Images;

/// <summary>
/// An image texture
/// </summary>
public class ImageTexture {
    /// <summary>
    /// How texture coordinates outside the [0,1] range are mapped to pixels in the image
    /// </summary>
    public enum BorderHandling {
        /// <summary>
        /// Repeat the image in all directions, 1.1 is mapped to 0.1
        /// </summary>
        Repeat,

        /// <summary>
        /// Maps coordinates outside the image to the closest pixel that is within the image
        /// </summary>
        Clamp
    }

    /// <summary>
    /// The border handling mode to be used, defaults to "Repeat"
    /// </summary>
    public BorderHandling Border = BorderHandling.Repeat;

    (int, int) ApplyBorderHandling(int col, int row) {
        if (Border == BorderHandling.Repeat) {
            row = (row % Image.Height + Image.Height) % Image.Height;
            col = (col % Image.Width + Image.Width) % Image.Width;
        } else if (Border == BorderHandling.Clamp) {
            row = System.Math.Clamp(row, 0, Image.Height - 1);
            col = System.Math.Clamp(col, 0, Image.Width - 1);
        }

        return (col, row);
    }

    /// <returns> The (x,y) / (col,row) coordinate of the texel. </returns>
    public (int, int) ComputeTexel(Vector2 uv) {
        int col = (int)(uv.X * Image.Width);
        int row = (int)(uv.Y * Image.Height);
        return ApplyBorderHandling(col, row);
    }

    /// <summary>
    /// The texture image
    /// </summary>
    public Image Image;
}