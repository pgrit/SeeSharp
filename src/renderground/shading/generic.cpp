#include "shading/generic.h"
#include "math/constants.h"
#include "math/wrap.h"

namespace ground
{

GenericMaterial::GenericMaterial(const Scene* scene,
    const GenericMaterialParameters& params)
: Material(scene), parameters(params)
{
}

Float3 GenericMaterial::EvaluateBsdf(const SurfacePoint& point,
    const Float3& inDir, const Float3& outDir, bool isOnLightSubpath) const
{
    auto texCoords = scene->GetMesh(point.geomId).ComputeTextureCoordinates(
        point.primId, point.barycentricCoords);
    auto shadingNormal = scene->GetMesh(point.geomId).ComputeShadingNormal(
        point.primId, point.barycentricCoords);

    // TODO proper wavelength etc
    Float3 reflectance = 0.0f;
    if (parameters.baseColor)
        // TODO this is unsafe as hell, use a GetValue that returns an RGB explicitely
        //      (and converts as necessary, handled by the image)
        parameters.baseColor->GetValue(texCoords.x, texCoords.y, &reflectance.x);

    return reflectance * 1.0f / PI;
}

BsdfSampleInfo GenericMaterial::WrapPrimarySampleToBsdf(const SurfacePoint& point,
    Float3* inDir, const Float3& outDir, bool isOnLightSubpath, const Float2& primarySample) const
{
    auto texCoords = scene->GetMesh(point.geomId).ComputeTextureCoordinates(
        point.primId, point.barycentricCoords);
    auto shadingNormal = scene->GetMesh(point.geomId).ComputeShadingNormal(
        point.primId, point.barycentricCoords);

    // Flip the shadingNormal to the same side of the surface as the outgoing direction
    if (Dot(shadingNormal, outDir) < 0) shadingNormal *= -1.0f;

    // TODO MIS sample all active components once this is a proper combined shader

    // Wrap the primary sample to a hemisphere in "shading space": centered in the
    // origin and oriented about the positive z-axis.
    auto dirSample = WrapToCosHemisphere(primarySample);

    // Transform the "shading space" hemisphere coordinates to world space.
    Float3 tangent, binormal;
    ComputeBasisVectors(shadingNormal, tangent, binormal);
    *inDir = shadingNormal * dirSample.direction.z
        + tangent * dirSample.direction.x
        + binormal * dirSample.direction.y;

    return BsdfSampleInfo {
        dirSample.jacobian,
        dirSample.jacobian // TODO those are only equal for diffuse BSDFs
    };
}

Float3 GenericMaterial::ComputeEmission(const SurfacePoint& point, const Float3& outDir) const {
    auto texCoords = scene->GetMesh(point.geomId).ComputeTextureCoordinates(
        point.primId, point.barycentricCoords);
    auto shadingNormal = scene->GetMesh(point.geomId).ComputeShadingNormal(
        point.primId, point.barycentricCoords);

    // TODO we need some conventions / proper handling of textures with different color
    //      conventions (at least: upsampled sRGB and luminance)
    //      the wavelength will have to somehow propagate to the texture if the upsampled
    //      result is stored in the image rather than computed on-the-fly
    Float3 emission = 0.0f;
    if (parameters.emission)
        parameters.emission->GetValue(texCoords.x, texCoords.y, &emission.x);

    return emission;
}

BsdfSampleInfo GenericMaterial::ComputeJacobians(const SurfacePoint& point,
        const Float3& inDir, const Float3& outDir, bool isOnLightSubpath) const
{
    auto shadingNormal = scene->GetMesh(point.geomId).ComputeShadingNormal(
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
