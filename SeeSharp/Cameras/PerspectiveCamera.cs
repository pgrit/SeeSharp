namespace SeeSharp.Cameras;

/// <summary>
/// A simple pin-hole camera with thin-lens field-of-view (the latter is WIP and not yet supported).
/// </summary>
public class PerspectiveCamera : Camera {
    /// <summary>
    /// The width (in pixels) of the associated frame buffer
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// The height (in pixels) of the associated frame buffer
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// The vertical field of view this camera was created with (readonly) in degrees
    /// </summary>
    public float VerticalFieldOfView { get; }

    /// <summary>
    /// Creates a new perspective camera.
    /// </summary>
    /// <param name="worldToCamera">
    /// Transformation from (right handed) world space to (right handed) camera space.
    /// The camera is centered at the origin, looking along negative Z. X is right, Y is up.
    /// </param>
    /// <param name="verticalFieldOfView">The full vertical opening angle in degrees.</param>
    /// <param name="lensRadius">Radius of the thin lens, determines the strength of depth-of-field</param>
    /// <param name="focalDistance">Distance from the camera where everything is in focus</param>
    public PerspectiveCamera(Matrix4x4 worldToCamera, float verticalFieldOfView,
                                float lensRadius = 0, float focalDistance = 0)
    : base(worldToCamera) {
        fovRadians = verticalFieldOfView * MathF.PI / 180;
        VerticalFieldOfView = verticalFieldOfView;
    }

    /// <summary>
    /// Updates the camera parameters after the frame buffer changed resolution
    /// </summary>
    public override void UpdateResolution(int width, int height) {
        Width = width;
        Height = height;
        aspectRatio = Width / (float)Height;

        cameraToView = Matrix4x4.CreatePerspectiveFieldOfView(fovRadians, aspectRatio, 0.001f, 1000.0f);
        Matrix4x4.Invert(cameraToView, out viewToCamera);

        float tanHalf = MathF.Tan(fovRadians * 0.5f);
        imagePlaneDistance = Height / (2 * tanHalf);
    }

    /// <summary>
    /// Generates a ray from a position in the image into the scene
    /// </summary>
    /// <param name="filmPos">
    ///     Position on the film plane: integer pixel coordinates and fractional position within
    /// </param>
    /// <param name="rng">
    ///     Random number generator used to sample additional decisions (lens position for depth of field)
    /// </param>
    /// <returns>The sampled camera ray and related information like PDF and contribution</returns>
    public override CameraRaySample GenerateRay(Vector2 filmPos, ref RNG rng) {
        Debug.Assert(Width != 0 && Height != 0);

        // Transform the direction from film to world space.
        // The view space is vertically flipped compared to the film.
        var view = new Vector3(2 * filmPos.X / Width - 1, 1 - 2 * filmPos.Y / Height, 0);
        var localDir = Vector3.Transform(view, viewToCamera);
        var dirHomo = Vector4.Transform(new Vector4(localDir, 0), cameraToWorld);
        var dir = new Vector3(dirHomo.X, dirHomo.Y, dirHomo.Z);

        // Compute the camera position
        var pos = Vector3.Transform(new Vector3(0, 0, 0), cameraToWorld);

        var ray = new Ray { Direction = Vector3.Normalize(dir), MinDistance = 0, Origin = pos };

        // Sample depth of field
        float pdfLens = 1;
        if (lensRadius > 0) {
            var lensSample = rng.NextFloat2D();
            var lensPos = lensRadius * SampleWarp.ToConcentricDisc(lensSample);

            // Intersect ray with focal plane
            var focalPoint = ray.ComputePoint(focalDistance / ray.Direction.Z);

            // Update the ray
            ray.Origin = new Vector3(lensPos, 0);
            ray.Direction = Vector3.Normalize(focalPoint - ray.Origin);

            pdfLens = 1 / (MathF.PI * lensRadius * lensRadius);
        }

        return new CameraRaySample {
            Ray = ray,
            Weight = RgbColor.White,
            Point = new SurfacePoint { Position = Position, Normal = Direction },
            PdfRay = SolidAngleToPixelJacobian(pos + dir) * pdfLens,
            PdfConnect = pdfLens
        };
    }

    /// <summary>
    /// Samples a point on the camera lens that sees the given surface point. Returns an invalid
    /// sample if there is no such point.
    /// </summary>
    /// <param name="scenePoint">A point on a scene surface that might be seen by the camera</param>
    /// <param name="rng">RNG used to sample the lens. Can be null if the lens radius is zero.</param>
    /// <returns>The pixel coordinates and weights, or an invalid sample</returns>
    public override CameraResponseSample SampleResponse(SurfacePoint scenePoint, ref RNG rng) {
        Debug.Assert(Width != 0 && Height != 0);

        // Sample a point on the lens
        Vector3 lensPoint = Position;
        if (lensRadius > 0) {
            var lensSample = rng.NextFloat2D();
            var lensPosCam = lensRadius * SampleWarp.ToConcentricDisc(lensSample);
            var lensPosWorld = Vector4.Transform(new Vector4(lensPosCam.X, lensPosCam.Y, 0, 1), cameraToWorld);
            lensPoint = new(lensPosWorld.X, lensPosWorld.Y, lensPosWorld.Z);
        }

        // Map the scene point to the film
        var filmPos = WorldToFilm(scenePoint.Position);
        if (!filmPos.HasValue)
            return CameraResponseSample.Invalid;

        // Compute the change of variables from scene surface to pixel area
        float jacobian = SolidAngleToPixelJacobian(scenePoint.Position);
        jacobian *= SurfaceAreaToSolidAngleJacobian(scenePoint.Position, scenePoint.Normal);

        // Compute the pdfs
        float invLensArea = 1;
        if (lensRadius > 0)
            invLensArea = 1 / (MathF.PI * lensRadius * lensRadius);
        float pdfConnect = invLensArea;
        float pdfEmit = invLensArea * jacobian;

        return new CameraResponseSample {
            Pixel = new((int)filmPos.Value.X, (int)filmPos.Value.Y),
            Position = lensPoint,
            Weight = jacobian * RgbColor.White,
            PdfConnect = pdfConnect,
            PdfEmit = pdfEmit
        };
    }

    Vector3? WorldToFilm(Vector3 pos) {
        Debug.Assert(Width != 0 && Height != 0);

        var local = Vector3.Transform(pos, worldToCamera);

        // Check that the point is on the correct side of the camera
        if (local.Z > 0) return null;

        var view = Vector4.Transform(local, cameraToView);
        var film = new Vector3(
            (view.X / view.W + 1) / 2 * Width,
            (1 - view.Y / view.W) / 2 * Height,
            local.Length());

        // Check that the point is within the frustum
        if (film.X < 0 || film.X > Width || film.Y < 0 || film.Y > Height)
            return null;

        return film;
    }

    /// <summary>
    /// Computes the change of area when mapping the hemisphere of directions around the camera onto
    /// pixels on the image.
    /// </summary>
    /// <param name="pos">A reference point in the scene that the camera is looking at</param>
    /// <returns>
    ///     Factor by which the differential area on the image plane is larger than the differential solid
    ///     angle corresponding to the given direction
    /// </returns>
    public override float SolidAngleToPixelJacobian(Vector3 pos) {
        Debug.Assert(Width != 0 && Height != 0);

        // Compute the cosine
        var local = Vector3.Transform(pos, worldToCamera);
        var cosine = local.Z / local.Length();

        // Distance to the image plane point:
        // computed based on the right-angled triangle it forms with the view direction
        // cosine = adjacentSide / d <=> d = adjacentSide / cosine
        float d = imagePlaneDistance / cosine;

        // The jacobian from solid angle to surface area is:
        float jacobian = d * d / MathF.Abs(cosine);

        return jacobian;
    }

    public Matrix4x4 ViewToCamera => viewToCamera;

    Matrix4x4 cameraToView;
    Matrix4x4 viewToCamera;
    float aspectRatio;
    float fovRadians;
    float focalDistance = 0;
    float lensRadius = 0;

    // Distance from the camera position to the virtual image plane s.t. each pixel has area one
    float imagePlaneDistance;
}
