using System;
using System.Numerics;

namespace GroundWrapper.Shading.Bsdfs {
    public class Bsdf {
        public Vector3 shadingNormal;
        public BsdfComponent[] Components;

        public ColorRGB EvaluateBsdfOnly(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // Transform directions to shading space and normalize
            var normal = shadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);
            inDir = ShadingSpace.WorldToShading(normal, inDir);

            var result = ColorRGB.Black;
            foreach (var c in Components) {
                result += c.Evaluate(outDir, inDir, isOnLightSubpath);
            }

            return result;
        }

        public ColorRGB EvaluateWithCosine(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // Transform directions to shading space and normalize
            var normal = shadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);
            inDir = ShadingSpace.WorldToShading(normal, inDir);

            var result = ColorRGB.Black;
            foreach (var c in Components) {
                result += c.Evaluate(outDir, inDir, isOnLightSubpath);
            }

            return result * ShadingSpace.AbsCosTheta(inDir);
        }

        public (float, float) Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // Transform directions to shading space and normalize
            var normal = shadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);
            inDir = ShadingSpace.WorldToShading(normal, inDir);

            var (pdfForward, pdfReverse) = (0.0f, 0.0f);
            foreach (var c in Components) {
                var (pdfFwd, pdfRev) = c.Pdf(outDir, inDir, isOnLightSubpath);
                pdfForward += pdfFwd / Components.Length;
                pdfReverse += pdfRev / Components.Length;
            }
            return (pdfForward, pdfReverse);
        }

        public BsdfSample Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            // Transform directions to shading space and normalize
            var normal = shadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);

            // Select a component to importance sample. Uniformly for now.
            var idx = (int)(primarySample.X * Components.Length);
            idx = Math.Min(idx, Components.Length - 1);

            // Remap the primary sample to [0,1]
            var remapped = primarySample;
            remapped.X = Math.Min(primarySample.X * Components.Length - idx, 1);

            // Sample the selected component
            var sampled = Components[idx].Sample(outDir, isOnLightSubpath, remapped);
            if (!sampled.HasValue)
                return BsdfSample.Invalid;
            var sampledDir = sampled.Value;

            // Compute the joint value
            var combinedValue = ColorRGB.Black;
            foreach (var c in Components) {
                combinedValue += c.Evaluate(outDir, sampledDir, isOnLightSubpath);
            }
            combinedValue *= ShadingSpace.AbsCosTheta(sampledDir);

            // Compute the joint pdf
            var (pdfForward, pdfReverse) = (0.0f, 0.0f);
            foreach (var c in Components) {
                var (pdfFwd, pdfRev) = c.Pdf(outDir, sampledDir, isOnLightSubpath);
                pdfForward += pdfFwd / Components.Length;
                pdfReverse += pdfRev / Components.Length;
            }

            // Catch edge cases where the pdf is zero
            var weight = pdfForward > 0 ? combinedValue / pdfForward : ColorRGB.Black;

            return new BsdfSample {
                pdf = pdfForward,
                pdfReverse = pdfReverse,
                weight = weight,
                direction = ShadingSpace.ShadingToWorld(normal, sampledDir)
            };
        }
    }
}
