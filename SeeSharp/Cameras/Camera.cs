namespace SeeSharp.Cameras;

/// <summary>
/// Base class for all camera models
/// </summary>
public abstract class Camera {
    /// <summary>
    /// The position of the camera in world space (computed from the <see cref="WorldToCamera"/> matrix)
    /// </summary>
    public Vector3 Position => Vector3.Transform(new Vector3(0, 0, 0), cameraToWorld);

    /// <summary>
    /// The principal view direction of the camera in world space (computed from the
    /// <see cref="WorldToCamera"/> matrix)
    /// </summary>
    public Vector3 Direction {
        get {
            var dir = Vector4.Transform(new Vector4(0, 0, -1, 0), cameraToWorld);
            return new(dir.X, dir.Y, dir.Z);
        }
    }

    /// <summary>Initializes the common camera parameters based on a world matrix</summary>
    /// <exception cref="System.ArgumentException">
    ///     If the world to camera transform is not invertible.
    /// </exception>
    public Camera(Matrix4x4 worldToCamera) {
        WorldToCamera = worldToCamera;
    }

    /// <summary>
    /// Transformation from world space to camera space. Specifies position, view direction, and up vector.
    /// </summary>
    public Matrix4x4 WorldToCamera {
        get => worldToCamera;
        set {
            worldToCamera = value;
            var succ = Matrix4x4.Invert(worldToCamera, out cameraToWorld);
            if (!succ)
                throw new ArgumentException("World to camera transform must be invertible.", nameof(value));
        }
    }

    /// <summary>
    /// Transformation from camera space to world space; inverse of <see cref="WorldToCamera" />.
    /// Readonly, automatically computed when <see cref="WorldToCamera" /> is updated
    /// </summary>
    public Matrix4x4 CameraToWorld => cameraToWorld;

    /// <summary>
    /// Updates camera parameters based on changed resolution in a frame buffer.
    /// </summary>
    /// <param name="width">The new width of the frame buffer in pixels</param>
    /// <param name="height">The new height of the frame buffer in pixels</param>
    public abstract void UpdateResolution(int width, int height);

    /// <summary>
    /// Samples a ray from the camera into the scene
    /// </summary>
    /// <param name="filmPos">Position on the image in pixel coordinates</param>
    /// <param name="rng">Random number generator</param>
    /// <returns>The sampled ray, pdf, and importance weight</returns>
    public abstract CameraRaySample GenerateRay(Vector2 filmPos, ref RNG rng);

    /// <summary>
    /// Computes a Monte Carlo estimate of the contribution a scene point makes to the camera film
    /// </summary>
    /// <param name="scenePoint">A point on a surface, visible to the camera</param>
    /// <param name="rng">Random number generator</param>
    /// <returns>Importance estimate, sampled pixel coordinates, and pdf</returns>
    public abstract CameraResponseSample SampleResponse(SurfacePoint scenePoint, ref RNG rng);

    /// <summary>
    /// Computes the change of area when mapping the hemisphere of directions around the camera onto
    /// pixels on the image.
    /// </summary>
    /// <param name="pos">A point in the scene that the camera is looking at</param>
    /// <returns>
    ///     The jacobian. The factor by which the differential area on the image plane is larger than the
    ///     differential solid angle corresponding to the given direction.
    /// </returns>
    public abstract float SolidAngleToPixelJacobian(Vector3 pos);

    /// <summary>
    /// Computes the jacobian determinant for the mapping from surface area in the scene to solid angle
    /// about the camera
    /// </summary>
    /// <param name="point">A point in the scene that is visible to the camera</param>
    /// <param name="normal">Surface normal at the scene point</param>
    /// <returns>Jacobian determinant</returns>
    public virtual float SurfaceAreaToSolidAngleJacobian(Vector3 point, Vector3 normal) {
        var dirToCam = Position - point;
        float distToCam = dirToCam.Length();
        float cosToCam = Math.Abs(Vector3.Dot(normal, dirToCam)) / distToCam;
        if (distToCam == 0) // prevent NaN / Inf
            return 0;
        return cosToCam / (distToCam * distToCam);
    }

    /// <returns>A deep copy of this camera object</returns>
    public virtual Camera Copy() {
        return (Camera)MemberwiseClone();
    }

    /// <summary>
    /// World space to camera space transform
    /// </summary>
    protected Matrix4x4 worldToCamera;

    /// <summary>
    /// Automatically computed inverse of <see cref="worldToCamera" />
    /// </summary>
    protected Matrix4x4 cameraToWorld;
}
