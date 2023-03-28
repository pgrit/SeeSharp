namespace SeeSharp.Shading.Background;

/// <summary>
/// Background that provides image based lighting to the scene.
///
/// The image is interpreted as a lat-long map.
/// The vertical axis corresponds to the latidude, the horizontal axis to the longitude.
/// The "up" direction (top center pixel of the image) corresponds to the worldspace
/// positive y direction.
/// The longitude is measured such that an angle of zero indicates a direction along the
/// worldspace negative x axis. Rotation is CCW about the y axis, when looking along positive y.
/// </summary>
public class EnvironmentMap : Background {
    public EnvironmentMap(RgbImage image, bool useMisCompensation = false) {
        this.Image = image;
        BuildSamplingGrid(useMisCompensation);
    }

    public override RgbColor EmittedRadiance(Vector3 direction) {
        var sphericalDir = WorldToSpherical(direction);
        var pixelCoords = SphericalToPixel(sphericalDir);

        var col = pixelCoords.X * Image.Width;
        var row = pixelCoords.Y * Image.Height;
        return Image.GetPixel((int)col, (int)row);
    }

    public override RgbColor ComputeTotalPower() {
        RgbColor totalPower = RgbColor.Black;
        for (int row = 0; row < Image.Height; ++row) {
            for (int col = 0; col < Image.Width; ++col) {
                // TOOD / FIXME this is not quite correct: the latlong map does not preserve area and we
                //              are ignoring the jacobian term (sin(y)).
                totalPower += Image.GetPixel(col, row) * 4 * MathF.PI / (Image.Width * Image.Height);
            }
        }
        return totalPower;
    }

    public override BackgroundSample SampleDirection(Vector2 primary) {
        // Warp the primary sample to a position within one of the pixels.
        var pixelPrimary = directionSampler.Sample(primary);
        var pdf = directionSampler.Pdf(pixelPrimary);

        // Warp the pixel coordinates to the sphere of directions.
        var sphericalDir = PixelToSpherical(pixelPrimary);
        var direction = SphericalToWorld(sphericalDir);

        // Multiply the pdfs with the jacobian of the change of variables from unit square to sphere
        float jacobian = MathF.Sin(sphericalDir.Y) * MathF.PI * MathF.PI * 2.0f;
        if (jacobian == 0.0f) {
            // Prevent infs / nans in this rare edge case
            return new BackgroundSample {
                Direction = direction,
                Pdf = 0,
                Weight = RgbColor.Black
            };
        }
        pdf /= jacobian;
        // TODO we could (and should) pre-multiply the pdf by the sine, to avoid oversampling regions that will receive zero weight

        // Compute the sample weight
        var weight = Image.GetPixel(
            (int)(pixelPrimary.X * Image.Width),
            (int)(pixelPrimary.Y * Image.Height)
        ) / pdf;

        return new BackgroundSample {
            Direction = direction,
            Pdf = pdf,
            Weight = weight
        };
    }

    public override Vector2 SampleDirectionInverse(Vector3 direction) {
        var sphericalDir = WorldToSpherical(direction);
        var pixelPrimary = SphericalToPixel(sphericalDir);
        return directionSampler.SampleInverse(pixelPrimary);
    }

    public override float DirectionPdf(Vector3 direction) {
        var sphericalDir = WorldToSpherical(direction);
        var pixelCoords = SphericalToPixel(sphericalDir);
        return directionSampler.Pdf(pixelCoords) / (MathF.Sin(sphericalDir.Y) * MathF.PI * MathF.PI * 2.0f);
    }

    public override (Ray, RgbColor, float) SampleRay(Vector2 primaryPos, Vector2 primaryDir) {
        // Sample a direction from the scene to the background
        var dirSample = SampleDirection(primaryDir);

        // Sample a point on the unit disc
        var unitDiscPoint = SampleWarp.ToConcentricDisc(primaryPos);

        // And transform it to the scene spanning disc orthogonal to the selected direction
        Vector3 tangent, binormal;
        ComputeBasisVectors(dirSample.Direction, out tangent, out binormal);
        var pos = SceneCenter + SceneRadius * (dirSample.Direction // offset outside of the scene
                                               + tangent * unitDiscPoint.X // remap unit disc x coordinate
                                               + binormal * unitDiscPoint.Y); // remap unit disc y coordinate

        // Compute the pdf: uniform sampling of a disc with radius "SceneRadius"
        float discJacobian = SampleWarp.ToConcentricDiscJacobian();
        float posPdf = discJacobian / (SceneRadius * SceneRadius);

        // Compute the final result
        var ray = new Ray { Origin = pos, Direction = -dirSample.Direction, MinDistance = 0 };
        var weight = dirSample.Weight / posPdf;
        var pdf = posPdf * dirSample.Pdf;

        return (ray, weight, pdf);
    }

    public override (Vector2, Vector2) SampleRayInverse(Vector3 dir, Vector3 pos) {
        var primaryDir = SampleDirectionInverse(-dir);

        // Project the point onto the plane with normal "dir"
        Vector3 tangent, binormal;
        ComputeBasisVectors(-dir, out tangent, out binormal);
        var offset = pos - SceneCenter;
        float x = Vector3.Dot(offset, tangent) / SceneRadius;
        float y = Vector3.Dot(offset, binormal) / SceneRadius;
        var primaryPos = SampleWarp.FromConcentricDisc(new(x, y));

        return (primaryPos, primaryDir);
    }

    public override float RayPdf(Vector3 point, Vector3 direction) {
        float dirPdf = DirectionPdf(-direction);
        float discJacobian = SampleWarp.ToConcentricDiscJacobian();
        float posPdf = discJacobian / (SceneRadius * SceneRadius);
        return posPdf * dirPdf;
    }

    /// <summary>
    /// Forms a tabulated pdf to importance sample the environment map.
    /// </summary>
    /// <param name="useMisCompensation">If true, applies MIS compensation to the PDF (Karlík et al. 2019)</param>
    /// <returns>Tabulated pdf for the image.</returns>
    public virtual RegularGrid2d BuildSamplingGrid(bool useMisCompensation) {
        var result = new RegularGrid2d(Image.Width, Image.Height);

        for (int row = 0; row < Image.Height; ++row) {
            for (int col = 0; col < Image.Width; ++col) {
                result.Splat(col, row, Image.GetPixel(col, row).Luminance);
            }
        }

        if (useMisCompensation)
            result.ApplyMISCompensation();
        else
            result.Normalize();

        directionSampler = result;

        return result;
    }

    Vector2 WorldToSpherical(Vector3 dir) {
        dir = Vector3.Normalize(dir);
        var sp = new Vector2(
            MathF.Atan2(dir.Z, dir.X),
            MathF.Atan2(MathF.Sqrt(dir.X * dir.X + dir.Z * dir.Z), dir.Y)
        );
        if (sp.X < 0) sp.X += MathF.PI * 2.0f;
        return sp;
    }

    Vector3 SphericalToWorld(Vector2 spherical) {
        float sinTheta = MathF.Sin(spherical.Y);
        return new Vector3(
            sinTheta * MathF.Cos(spherical.X),
            MathF.Cos(spherical.Y),
            sinTheta * MathF.Sin(spherical.X)
        );
    }

    Vector2 SphericalToPixel(Vector2 sphericalDir)
    => new Vector2(sphericalDir.X / (2 * MathF.PI), sphericalDir.Y / MathF.PI);

    Vector2 PixelToSpherical(Vector2 pixel)
    => new Vector2(pixel.X * 2 * MathF.PI, pixel.Y * MathF.PI);

    /// <summary>
    /// The image that illuminates the scene from all directions
    /// </summary>
    public readonly RgbImage Image;

    RegularGrid2d directionSampler;
}