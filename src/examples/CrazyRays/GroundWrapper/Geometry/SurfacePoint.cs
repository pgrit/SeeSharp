using GroundWrapper.Shading.Bsdfs;
using System.Numerics;

namespace GroundWrapper.Geometry {
    public struct SurfacePoint {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 barycentricCoords;
        public Mesh mesh;
        public uint primId;
        public float errorOffset;
        public float distance;

        public static implicit operator bool(SurfacePoint hit)
            => hit.mesh != null;

        public Vector3 ShadingNormal {
            get {
                if (!hasNormal) {
                    shadingNormal = mesh.ComputeShadingNormal((int)primId, barycentricCoords);
                    hasNormal = true;
                }
                return shadingNormal;
            }
        }
        bool hasNormal; Vector3 shadingNormal;

        public Vector2 TextureCoordinates {
            get {
                if (!hasTexCoords) {
                    texCoords = mesh.ComputeTextureCoordinates((int)primId, barycentricCoords);
                    hasTexCoords = true;
                }
                return texCoords;
            }
        }
        bool hasTexCoords; Vector2 texCoords;

        public Bsdf Bsdf {
            get {
                if (!hasBsdf) {
                    bsdf = mesh.Material.GetBsdf(this);
                    hasBsdf = true;
                }
                return bsdf;
            }
        }
        bool hasBsdf; private Bsdf bsdf;
    }
}
