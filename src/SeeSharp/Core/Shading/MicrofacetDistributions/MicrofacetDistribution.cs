using System.Numerics;

namespace SeeSharp.Core.Shading.MicrofacetDistributions {
    public abstract class MicrofacetDistribution {
        /// <summary>
        /// Computes the distribution of microfacets with the given normal.
        /// </summary>
        /// <param name="normal">The normal vector of the microfacets, in shading space.</param>
        /// <returns>The fraction of microfacets that are oriented with the given normal.</returns>
        public abstract float NormalDistribution(Vector3 normal);

        /// <summary>
        /// Computes the masking-shadowing function: 
        /// The ratio of visible microfacet area to the total area of all correctly oriented microfacets.
        /// </summary>
        /// <param name="normal">
        /// The normal vector of the microfacets, in shading space.
        /// </param>
        /// <returns>The masking shadowing function value ("G" in most papers).</returns>
        public float MaskingShadowing(Vector3 normal) {
            return 1 / (1 + MaskingRatio(normal));
        }

        public float MaskingShadowing(Vector3 outDir, Vector3 inDir) {
            return 1 / (1 + MaskingRatio(outDir) + MaskingRatio(inDir));
        }

        /// <summary>
        /// Computes the ratio of self-masked area to visible area. Used by <see cref="MaskingShadowing(Vector3)"/>.
        /// </summary>
        /// <param name="normal">Normal of the microfacets, in shading space.</param>
        /// <returns>Ratio of self-masked area to visible area.</returns>
        protected abstract float MaskingRatio(Vector3 normal);

        /// <summary>
        /// Wraps the given primary sample to follow the pdf computed by <see cref="Pdf(Vector3, Vector3)"/>.
        /// </summary>
        /// <returns>The direction that corresponds to the given primary sample.</returns>
        public abstract Vector3 Sample(Vector3 outDir, Vector2 primary);

        /// <summary>
        /// The Pdf that is used for importance sampling microfacet normals from this distribution.
        /// This usually importance samples the portion of normals that are in the hemisphere of the outgoing direction.
        /// </summary>
        /// <param name="outDir">The outgoing direction in shading space.</param>
        /// <param name="inDir">The incoming direction in shading space.</param>
        /// <returns>The pdf value.</returns>
        public abstract float Pdf(Vector3 outDir, Vector3 inDir);
    }
}
