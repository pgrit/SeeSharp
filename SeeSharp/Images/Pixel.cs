namespace SeeSharp.Images;

/// <summary>
/// Tracks the integer coordinates of a pixel in an image
/// </summary>
/// <param name="Col">Horizontal position, 0 is leftmost, Width-1 is rightmost</param>
/// <param name="Row">Vertical position, 0 is topmost, Height-1 is bottommost</param>
public record struct Pixel(int Col, int Row) {}
