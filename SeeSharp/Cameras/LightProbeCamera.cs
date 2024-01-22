namespace SeeSharp.Cameras;

/// <summary>
/// Visualizes the illumination at a surface point in the scene in spherical coordinates.
/// </summary>
public class LightProbeCamera : Camera {
    Vector3 upVector;
    Vector3 position;
    Vector3 normal;
    float errorOffset;
    int width, height;

    /// <summary>
    /// Initializes a light probe camera at a surface point
    /// </summary>
    /// <param name="position">Point on a scene surface</param>
    /// <param name="normal">Surface normal at the query point, must point "outside" so it can be used
    /// to avoid self intersection problems with shadow rays</param>
    /// <param name="errorOffset">How far to move rays from the surface to avoid self-intersection</param>
    /// <param name="upVector">Defines the up direction of the probe in world space</param>
    public LightProbeCamera(Vector3 position, Vector3 normal, float errorOffset, Vector3 upVector)
    : base(Matrix4x4.Identity) {
        this.upVector = upVector;
        this.position = position;
        this.normal = normal;
        this.errorOffset = errorOffset;

        // Define the world to camera transform (image center is the forward direction)
        var forward = SampleWarp.SphericalToCartesian(new(MathF.PI, MathF.PI / 2.0f));
        WorldToCamera = Matrix4x4.CreateLookAt(position + errorOffset * normal, position + forward, upVector);
    }

    /// <summary>
    /// Generates a ray from the camera into the scene.
    /// </summary>
    /// <param name="filmPos">Position (in pixels) on the image</param>
    /// <param name="rng">Unused, can be null</param>
    /// <returns>The chosen ray and associated weights</returns>
    public override CameraRaySample GenerateRay(Vector2 filmPos, ref RNG rng) {
        Debug.Assert(width != 0 && height != 0);

        // Convert image position to spherical coordinates
        float phi = 2 * MathF.PI * (filmPos.X / width);
        float theta = MathF.PI * (filmPos.Y / height);

        var dir = SampleWarp.SphericalToCartesian(new(phi, theta));
        dir = ShadingToWorld(upVector, dir);

        float sign = Vector3.Dot(dir, normal) < 0.0f ? -1.0f : 1.0f;
        Ray ray = new() {
            Origin = position + sign * errorOffset * normal,
            Direction = dir,
            MinDistance = errorOffset,
        };

        return new() {
            Point = new() { Position = position, Normal = normal, ErrorOffset = errorOffset },
            Ray = ray,
            Weight = RgbColor.White,
            PdfRay = SolidAngleToPixelJacobian(position + dir),
            PdfConnect = 1,
        };
    }

    /// <summary>
    /// Maps the point to the deterministic location on the image.
    /// </summary>
    /// <param name="scenePoint">A point on a scene surface</param>
    /// <param name="rng">Unused, can be null</param>
    /// <returns>Contribution, pixel, and sampling PDFs</returns>
    public override CameraResponseSample SampleResponse(SurfacePoint scenePoint, ref RNG rng) {
        var filmPoint = WorldToFilm(scenePoint.Position);

        float jacobian = SolidAngleToPixelJacobian(scenePoint.Position);
        jacobian *= SurfaceAreaToSolidAngleJacobian(scenePoint.Position, scenePoint.Normal);

        return new() {
            Position = Position,
            Pixel = new((int)filmPoint.X, (int)filmPoint.Y),
            Weight = jacobian * RgbColor.White,
            PdfConnect = 1,
            PdfEmit = jacobian
        };
    }

    /// <summary>
    /// Computes the change of area when mapping a direction from the hemisphere around the camera
    /// to the image. Given by our transformation to spherical coordinates, followed by the scaling to the
    /// desired resolution.
    /// </summary>
    /// <param name="pos">Position in world space of a point towards which the direction points</param>
    /// <returns>
    ///     Jacobian determinant that describes how much larger an area on the image plane is than
    ///     the corresponding solid angle.
    /// </returns>
    public override float SolidAngleToPixelJacobian(Vector3 pos) {
        var dir = Vector3.Normalize(pos - position);
        dir = WorldToShading(upVector, dir);
        float theta = SampleWarp.CartesianToSpherical(dir).Y;
        return 1 / (2 * MathF.PI * MathF.PI * MathF.Sin(theta)) * width * height;
    }

    /// <summary>
    /// Updates the camera parameters after the frame buffer changed resolution.
    /// Must be called at least once, and with the correct values.
    /// </summary>
    public override void UpdateResolution(int width, int height) {
        this.width = width;
        this.height = height;
    }

    Vector3 WorldToFilm(Vector3 pos) {
        Debug.Assert(width != 0 && height != 0);

        var dir = pos - position;
        float distance = dir.Length();
        dir = WorldToShading(upVector, dir / distance);
        var spherical = SampleWarp.CartesianToSpherical(dir);
        return new(
            width * spherical.X / (2 * MathF.PI),
            height * spherical.Y / MathF.PI,
            distance
        );
    }
}