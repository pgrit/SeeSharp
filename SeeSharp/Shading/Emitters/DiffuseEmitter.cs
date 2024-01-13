namespace SeeSharp.Shading.Emitters;

/// <summary>
/// Emits a constant amount of radiance in all directions of the upper hemisphere, defined by the
/// shading normal of the emissive mesh surface.
/// </summary>
public class DiffuseEmitter : Emitter {
    /// <param name="triangle">The emissive triangle</param>
    /// <param name="radiance">Emitted radiance in all directions</param>
    public DiffuseEmitter(Triangle triangle, RgbColor radiance) {
        Triangle = triangle;
        Radiance = radiance;
    }

    /// <summary>
    /// Creates emitters for all triangles in a mesh.
    /// </summary>
    /// <param name="mesh">An emissive mesh</param>
    /// <param name="radiance">The radiance emitted by every point on the surface</param>
    /// <returns>List of emitters, one for every triangle in the mesh</returns>
    public static IEnumerable<Emitter> MakeFromMesh(Mesh mesh, RgbColor radiance) {
        List<DiffuseEmitter> emitters = new(mesh.NumFaces);
        for (int i = 0; i < mesh.NumFaces; ++i) {
            try {
                emitters.Add(new(new(mesh, i), radiance));
            } catch(ArgumentException) {
                continue;
            }
        }
        return emitters;
    }

    public override RgbColor EmittedRadiance(SurfacePoint point, Vector3 direction) {
        if (Vector3.Dot(point.ShadingNormal, direction) < 0)
            return RgbColor.Black;
        return Radiance;
    }

    public override float PdfRay(SurfacePoint point, Vector3 direction) {
        float cosine = Vector3.Dot(point.ShadingNormal, direction) / direction.Length();
        return PdfUniformArea(point) * MathF.Max(cosine, 0) / MathF.PI;
    }

    public override EmitterSample SampleRay(Vector2 primaryPos, Vector2 primaryDir) {
        var posSample = SampleUniformArea(primaryPos);

        // Transform primary to cosine hemisphere (z is up)
        var local = SampleWarp.ToCosHemisphere(primaryDir);

        // Transform to world space direction
        var normal = posSample.Point.ShadingNormal;
        Vector3 tangent, binormal;
        ComputeBasisVectors(normal, out tangent, out binormal);
        Vector3 dir = local.Direction.Z * normal
                    + local.Direction.X * tangent
                    + local.Direction.Y * binormal;

        return new EmitterSample {
            Point = posSample.Point,
            Direction = dir,
            Pdf = local.Pdf * posSample.Pdf,
            Weight = Radiance / posSample.Pdf * MathF.PI // cosine cancels out with the directional pdf
        };
    }

    public override (Vector2, Vector2) SampleRayInverse(SurfacePoint point, Vector3 direction) {
        var posPrimary = SampleUniformAreaInverse(point);

        // Transform from world space to sampling space
        var normal = point.ShadingNormal;
        Vector3 tangent, binormal;
        ComputeBasisVectors(normal, out tangent, out binormal);
        float z = Vector3.Dot(normal, direction);
        float x = Vector3.Dot(tangent, direction);
        float y = Vector3.Dot(binormal, direction);

        var dirPrimary = SampleWarp.FromCosHemisphere(new(x, y, z));
        return (posPrimary, dirPrimary);
    }

    public override RgbColor ComputeTotalPower()
    => Radiance * 2.0f * MathF.PI * Mesh.SurfaceArea;

    /// <summary>
    /// The radiance that is emitted in all directions
    /// </summary>
    public readonly RgbColor Radiance;
}