using System.Numerics;
using System;
using GroundWrapper.Geometry;

namespace GroundWrapper {
    public struct BsdfSample {
        public Vector3 direction;
        public float pdf;
        public float pdfReverse;

        /// <summary>
        /// Sample weight of the reflectance estimate, i.e., the product of 
        /// BSDF and shading cosine divided by the pdf.
        /// </summary>
        public ColorRGB weight;
    }

    public interface Bsdf {
        ColorRGB Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);
        BsdfSample Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample);
        (float, float) Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);
    }

    public struct DiffuseBsdf : Bsdf {
        public ColorRGB reflectance;
        public SurfacePoint point;

        ColorRGB Bsdf.Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // Normalize the directions
            outDir = Vector3.Normalize(outDir);
            inDir = Vector3.Normalize(inDir);

            // Flip the normal to the correct hemisphere
            var normal = point.ShadingNormal;
            if (Vector3.Dot(outDir, normal) < 0) normal *= -1.0f;

            var cos = Vector3.Dot(normal, inDir);
            cos = MathF.Max(cos, 0); // Only reflection is possible (i.e. same hemisphere)
            return reflectance * (cos / MathF.PI);
        }

        BsdfSample Bsdf.Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            // Normalize the directions
            outDir = Vector3.Normalize(outDir);

            // Flip the normal to the correct hemisphere
            var normal = point.ShadingNormal;
            if (Vector3.Dot(outDir, normal) < 0) normal *= -1.0f;

            // Transform primary to cosine hemisphere (z is up)
            var local = GroundMath.SampleWrap.ToCosHemisphere(primarySample);

            // Transform to world space direction
            var (tangent, binormal) = GroundMath.SampleWrap.ComputeBasisVectors(normal);
            Vector3 dir = local.direction.Z * normal 
                        + local.direction.X * tangent 
                        + local.direction.Y * binormal;

            // Compute weights and pdfs
            float pdf = local.pdf;
            float pdfReverse = Vector3.Dot(normal, outDir) / MathF.PI;
            var weight = reflectance; // cosine and PI cancel out with the pdf

            // Catch corner cases if the primary sample should be exactly 0 or 1
            if (pdf == 0) weight = ColorRGB.Black;

            return new BsdfSample { direction = dir, pdf = pdf, pdfReverse = pdfReverse, weight = weight };
        }

        (float, float) Bsdf.Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // Normalize the directions
            outDir = Vector3.Normalize(outDir);
            inDir = Vector3.Normalize(inDir);

            // Flip the normal to the correct hemisphere
            var normal = point.ShadingNormal;
            if (Vector3.Dot(outDir, normal) < 0) normal *= -1.0f;

            float pdf = Vector3.Dot(normal, inDir) / MathF.PI;
            float pdfReverse = Vector3.Dot(normal, outDir) / MathF.PI;

            // Check that the directions are in the same hemisphere
            if (pdf * pdfReverse < 0) return (0, 0);

            return (pdf, pdfReverse);
        }
    }

    public interface Material {
        Bsdf GetBsdf(SurfacePoint hit);
    }

    /// <summary>
    /// Basic uber-shader surface material that should suffice for most integrator experiments.
    /// </summary>
    public class GenericMaterial : Material {
        public struct Parameters {
            public Image baseColor;
        }

        public GenericMaterial(Parameters parameters) => this.parameters = parameters;

        Bsdf Material.GetBsdf(SurfacePoint hit) {
            var tex = hit.TextureCoordinates;
            return new DiffuseBsdf { point = hit, reflectance = parameters.baseColor[tex.X, tex.Y] };
        }

        Parameters parameters;
    }
}
