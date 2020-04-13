using GroundWrapper;

namespace Integrators
{
    public readonly struct CachedRandomWalk {
        public readonly Scene scene;
        public readonly RNG rng;
        public readonly PathCache cache;
        public readonly bool isOnLightSubpath;
        public readonly int maxDepth;

        public CachedRandomWalk(Scene scene, RNG rng, PathCache cache,
            bool isOnLightSubpath, int maxDepth)
        {
            this.scene = scene;
            this.rng = rng;
            this.cache = cache;
            this.isOnLightSubpath = isOnLightSubpath;
            this.maxDepth = maxDepth;
        }

        /// <summary>Performs the random walk.</summary>
        /// <returns>Index of the last vertex along the path.</returns>
        public int StartWalk(SurfacePoint initialPoint, float surfaceAreaPdf,
            Ray initialRay, float directionPdf, ColorRGB initialWeight)
        {
            var originVertex = new PathVertex {
                point = initialPoint,
                pdfFromAncestor = surfaceAreaPdf,
                pdfToAncestor = -1110.0f, // cannot continue beyond an end-point (guard value used to detect this more easily)
                                          // TODO refactor: none of these values need to be stored, except for the pdf and maybe position!
                weight = ColorRGB.Black, // the first known weight is that at the first hit point
                ancestorId = -1,
                depth = 0
            };
            var originId = cache.AddVertex(originVertex);

            return ContinueWalk(originId, initialPoint, initialRay,
                initialWeight, directionPdf, 1);
        }

        int ContinueWalk(int previousVertexId, SurfacePoint previousPoint, Ray nextRay,
            ColorRGB nextWeight, float pdfNextDir, int depth)
        {
            var hit = scene.TraceRay(nextRay);
            if (!scene.IsValid(hit) || depth >= maxDepth)
                return previousVertexId;

            // Convert the PDF to surface area
            var geometryTerms = scene.ComputeGeometryTerms(previousPoint, hit.point);
            float pdfNextSurfaceArea = pdfNextDir * geometryTerms.cosineTo / geometryTerms.squaredDistance;

            // Sample the next direction from the BSDF
            float u = rng.NextFloat();
            float v = rng.NextFloat();
            var bsdfSample = scene.WrapPrimarySampleToBsdf(hit.point, -nextRay.direction, u, v,
                isOnLightSubpath);
            (var bsdfValue, float shadingCosine) = scene.EvaluateBsdf(hit.point, -nextRay.direction,
                bsdfSample.direction, false);

            // TODO the separated Wrap-Eval can cause severe outliers when grazing angles are sampled.
            //      Since we explicitely divide by the jacobian later on...


            // Compute and incorporate the pdf of next event estimation, if this is the first bounce.
            float pdfToAncestor = bsdfSample.reverseJacobian * geometryTerms.cosineFrom / geometryTerms.squaredDistance;
            if (depth == 1) { // Add the next event pdf
                // TODO this is assumes that Next Event and emission are sampled exactly the same way.
                //      find a way to separate this nicely!
                pdfToAncestor += cache[previousVertexId].pdfFromAncestor; 
            }

            // Store the vertex
            var primaryVertex = new PathVertex {
                point = hit.point,
                pdfFromAncestor = pdfNextSurfaceArea,
                pdfToAncestor = pdfToAncestor,
                weight = nextWeight,
                ancestorId = previousVertexId,
                depth = (byte)depth
            };
            var primaryId = cache.AddVertex(primaryVertex);

            // Continue the path with the next ray
            var weight = nextWeight * bsdfValue * (shadingCosine / bsdfSample.jacobian);

            var bsdfRay = scene.SpawnRay(hit.point, bsdfSample.direction);
            return ContinueWalk(primaryId, hit.point, bsdfRay, weight, bsdfSample.jacobian, depth + 1);
        }
    }
}