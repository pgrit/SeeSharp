#include "shading/generic.h"
#include "math/constants.h"
#include "math/wrap.h"

namespace ground
{

bool AreInSameHemisphere(const SurfacePoint& point,
    const Vector3& inDir, const Vector3& outDir)
{
    return Dot(inDir, point.normal) * Dot(outDir, point.normal) > 0;
}

GenericMaterial::GenericMaterial(const Scene* scene,
    const GenericMaterialParameters& params)
: Material(scene), parameters(params)
{
}

Vector3 GenericMaterial::EvaluateBsdf(const SurfacePoint& point,
    const Vector3& inDir, const Vector3& outDir, bool isOnLightSubpath) const
{
    auto texCoords = scene->GetMesh(point.meshId)->ComputeTextureCoordinates(
        point.primId, point.barycentricCoords);
    auto shadingNormal = scene->GetMesh(point.meshId)->ComputeShadingNormal(
        point.primId, point.barycentricCoords);

    bool isReflectance = AreInSameHemisphere(point, inDir, outDir);

    Vector3 reflectance { 0, 0, 0 };
    if (parameters.baseColor && isReflectance) {
        // TODO this is unsafe as hell, use a GetValue that returns an RGB explicitely
        //      (and converts as necessary, handled by the image)
        parameters.baseColor->GetValue(texCoords.x, texCoords.y, &reflectance.x);
    }

    return reflectance * 1.0f / PI;
}

float GenericMaterial::ShadingCosine(const SurfacePoint& point, const Vector3& inDir,
        const Vector3& outDir, bool isOnLightSubpath) const
{
    auto shadingNormal = scene->GetMesh(point.meshId)->ComputeShadingNormal(
        point.primId, point.barycentricCoords);

    // Flip the shading normal to be on the same hemisphere as the outgoing direction.
    if (Dot(shadingNormal, outDir) < 0)
        shadingNormal = -shadingNormal;

    return Dot(shadingNormal, Normalize(inDir));
}

BsdfSampleInfo GenericMaterial::WrapPrimarySampleToBsdf(const SurfacePoint& point,
    Vector3* inDir, const Vector3& outDir, bool isOnLightSubpath, const Vector2& primarySample) const
{
    auto texCoords = scene->GetMesh(point.meshId)->ComputeTextureCoordinates(
        point.primId, point.barycentricCoords);
    auto shadingNormal = scene->GetMesh(point.meshId)->ComputeShadingNormal(
        point.primId, point.barycentricCoords);

    // Flip the shadingNormal to the same side of the surface as the outgoing direction
    auto normal = point.normal;
    if (Dot(normal, outDir) < 0)
        normal = -normal; // TODO refactor code duplication

    // TODO MIS sample all active components once this is a proper combined shader

    // Wrap the primary sample to a hemisphere in "shading space": centered in the
    // origin and oriented about the positive z-axis.
    auto dirSample = WrapToCosHemisphere(primarySample);

    // Transform the "shading space" hemisphere coordinates to world space.
    Vector3 tangent, binormal;
    ComputeBasisVectors(normal, tangent, binormal);
    *inDir = normal * dirSample.direction.z
        + tangent * dirSample.direction.x
        + binormal * dirSample.direction.y;

    return BsdfSampleInfo {
        dirSample.jacobian,
        dirSample.jacobian // TODO those are only equal for diffuse BSDFs
    };
}

BsdfSampleInfo GenericMaterial::ComputeJacobians(const SurfacePoint& point,
        const Vector3& inDir, const Vector3& outDir, bool isOnLightSubpath) const
{
    auto shadingNormal = scene->GetMesh(point.meshId)->ComputeShadingNormal(
        point.primId, point.barycentricCoords);
    CheckNormalized(shadingNormal);

    const auto normalizedInDir = Normalize(inDir);

    // TODO compute actual jacobians of a more complex material once it is implemented

    float diffuseJacobian = ComputeCosHemisphereJacobian(Dot(normalizedInDir, shadingNormal));
    return BsdfSampleInfo {
        diffuseJacobian, diffuseJacobian
    };
}

} // namespace ground
