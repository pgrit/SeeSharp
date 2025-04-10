namespace SeeSharp.Shading.Materials;

public struct ShadingContext {
    public SurfacePoint Point;
    public bool IsOnLightSubpath;
    public Vector3 Normal;
    public Vector3 Tangent;
    public Vector3 Binormal;

    /// <summary>
    /// Outgoing direction in shading space
    /// </summary>
    public Vector3 OutDir;

    public Vector3 OutDirWorld;

    public ShadingContext(in SurfacePoint point, in Vector3 outDir, bool isOnLightSubpath) {
        Point = point;
        IsOnLightSubpath = isOnLightSubpath;
        Normal = point.ShadingNormal;
        ComputeBasisVectors(Normal, out Tangent, out Binormal);
        OutDir = WorldToShading(outDir);
        OutDirWorld = outDir;
    }

    public Vector3 WorldToShading(in Vector3 dir) => ShadingSpace.WorldToShading(Normal, Tangent, Binormal, dir);
    public Vector3 ShadingToWorld(in Vector3 dir) => ShadingSpace.ShadingToWorld(Normal, Tangent, Binormal, dir);
}
