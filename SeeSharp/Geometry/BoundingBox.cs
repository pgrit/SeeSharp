namespace SeeSharp.Geometry;

/// <summary>
/// Represents an axis aligned bounding box
/// </summary>
public readonly struct BoundingBox {
    /// <summary>
    /// Minimum and maximum values along all axes of the points within the box
    /// </summary>
    public readonly Vector3 Min, Max;

    /// <summary>
    /// Creates a new bounding box that spans the given region
    /// </summary>
    public BoundingBox(Vector3 min, Vector3 max) {
        Min = min;
        Max = max;
    }

    /// <summary>
    /// An empty box that contains nothing (max is smaller than min)
    /// </summary>
    public static BoundingBox Empty => new(
        min: Vector3.One * float.MaxValue,
        max: -Vector3.One * float.MaxValue
    );

    /// <summary>
    /// A box that spans the entire (representable) space
    /// </summary>
    public static BoundingBox Full => new(
        min: -Vector3.One * float.MaxValue,
        max: Vector3.One * float.MaxValue
    );

    /// <summary>
    /// Computes a new box with updated minimum and maximum so the given point is within the bounds.
    /// </summary>
    /// <param name="point">Point that should be within the box</param>
    /// <returns>A new box with the updated bounds</returns>
    public BoundingBox GrowToContain(Vector3 point) => new(
        min: Vector3.Min(Min, point),
        max: Vector3.Max(Max, point)
    );

    /// <summary>
    /// Computes a new box with updated minimum and maximum so the given box is entirely within the bounds.
    /// </summary>
    /// <param name="box">Other box that should be inside</param>
    /// <returns>A new box with the updated bounds</returns>
    public BoundingBox GrowToContain(BoundingBox box) => new(
        min: Vector3.Min(Min, box.Min),
        max: Vector3.Max(Max, box.Max)
    );

    /// <summary>
    /// Checks if a point is inside the bounding box
    /// </summary>
    public bool IsInside(Vector3 point)
    => point.X >= Min.X && point.Y >= Min.Y && point.Z >= Min.Z &&
       point.X <= Max.X && point.Y <= Max.Y && point.Z <= Max.Z;

    /// <summary>
    /// Computes the diagonal vector of the box
    /// </summary>
    public Vector3 Diagonal => Max - Min;

    /// <summary>
    /// Center point of the box
    /// </summary>
    public Vector3 Center => (Max + Min) / 2;

    /// <summary>
    /// An empty box is one where Max &lt; Min along every axis
    /// </summary>
    public bool IsEmpty => Min.X >= Max.X && Min.Y >= Max.Y && Min.Z >= Max.Z;

    /// <summary>
    /// Surface area of the box
    /// </summary>
    public float SurfaceArea => 2 * (Diagonal.X * (Diagonal.Y + Diagonal.Z) + Diagonal.Y * Diagonal.Z);

    public float Volume => Diagonal.X * Diagonal.Y * Diagonal.Z;
}
