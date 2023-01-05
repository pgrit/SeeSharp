namespace SeeSharp.Shading.Emitters;

/// <summary>
/// Emits light from all points on a surface. The directional component of the emission is scaled by a
/// cosine lobe centered around the surface normal.
/// </summary>
public class GlossyEmitter : Emitter {
    public GlossyEmitter(Triangle triangle, RgbColor radiance, float exponent) {
        Triangle = triangle;
        this.radiance = radiance;
        this.exponent = exponent;

        // The total power should be the same as that of a diffuse emitter
        normalizationFactor = (exponent + 1) / (2 * MathF.PI);
    }

    /// <summary>
    /// Creates emitters for all triangles in a mesh.
    /// </summary>
    /// <param name="mesh">An emissive mesh</param>
    /// <param name="radiance">The radiance emitted by every point on the surface</param>
    /// <param name="exponent">Exponent of the cosine lobe that distorts the directional emission</param>
    /// <returns>List of emitters, one for every triangle in the mesh</returns>
    public static IEnumerable<Emitter> MakeFromMesh(Mesh mesh, RgbColor radiance, float exponent) {
        List<Emitter> emitters = new(mesh.NumFaces);
        for (int i = 0; i < mesh.NumFaces; ++i) {
            try {
                emitters.Add(new GlossyEmitter(new(mesh, i), radiance, exponent));
            } catch(ArgumentException) {
                continue;
            }
        }
        return emitters;
    }

    public override RgbColor EmittedRadiance(SurfacePoint point, Vector3 direction) {
        float cosine = Vector3.Dot(point.ShadingNormal, direction) / direction.Length();
        if (cosine < 0) return RgbColor.Black;
        return radiance * MathF.Pow(cosine, exponent) * normalizationFactor;
    }

    public override float PdfRay(SurfacePoint point, Vector3 direction) {
        float cosine = Vector3.Dot(point.ShadingNormal, direction) / direction.Length();
        return PdfUniformArea(point) * SampleWarp.ToCosineLobeJacobian(exponent + 1, cosine);
    }

    public override EmitterSample SampleRay(Vector2 primaryPos, Vector2 primaryDir) {
        var posSample = SampleUniformArea(primaryPos);

        // Transform primary to cosine hemisphere (z is up)
        // We add one to the exponent, to importance sample the cosine term from the jacobian also
        var local = SampleWarp.ToCosineLobe(exponent + 1, primaryDir);

        // Transform to world space direction
        var normal = posSample.Point.ShadingNormal;
        Vector3 tangent, binormal;
        ComputeBasisVectors(normal, out tangent, out binormal);
        Vector3 dir = local.Direction.Z * normal
                    + local.Direction.X * tangent
                    + local.Direction.Y * binormal;

        float cosine = local.Direction.Z;
        var weight = radiance * MathF.Pow(cosine, exponent + 1) * normalizationFactor;

        return new EmitterSample {
            Point = posSample.Point,
            Direction = dir,
            Pdf = local.Pdf * posSample.Pdf,
            Weight = weight / posSample.Pdf / local.Pdf
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

        var dirPrimary = SampleWarp.FromCosineLobe(exponent + 1, new(x, y, z));
        return (posPrimary, dirPrimary);
    }

    public override RgbColor ComputeTotalPower()
    => radiance * 2.0f * MathF.PI * Mesh.SurfaceArea;

    RgbColor radiance;
    float exponent;
    float normalizationFactor;
}