using SeeSharp.Shading.Materials;
using System.Numerics;
using TinyEmbree;

namespace SeeSharp.Geometry {
    public struct SurfacePoint {
        public Vector3 Position { get => hit.Position; set => hit.Position = value; }
        public Vector3 Normal { get => hit.Normal; set => hit.Normal = value; }
        public Vector2 BarycentricCoords { get => hit.BarycentricCoords; set => hit.BarycentricCoords = value; }
        public Mesh Mesh { get => hit.Mesh as Mesh; set => hit.Mesh = value; }
        public uint PrimId { get => hit.PrimId; set => hit.PrimId = value; }
        public float ErrorOffset { get => hit.ErrorOffset; set => hit.ErrorOffset = value; }
        public float Distance { get => hit.Distance; set => hit.Distance = value; }

        public static implicit operator bool(SurfacePoint point) => point.hit;

        public static implicit operator Hit(SurfacePoint point) => point.hit;

        public static implicit operator SurfacePoint(Hit hit) {
            return new SurfacePoint { hit = hit };
        }

        public Vector3 ShadingNormal => Mesh.ComputeShadingNormal((int)PrimId, BarycentricCoords);

        public Vector2 TextureCoordinates => Mesh.ComputeTextureCoordinates((int)PrimId, BarycentricCoords);

        public Material Material => Mesh.Material;

        Hit hit;
    }
}
