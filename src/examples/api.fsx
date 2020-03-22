module Ground

open System.Runtime.InteropServices

[<DllImport("../dist/Ground", CallingConvention=CallingConvention.Cdecl)>]
extern void InitScene()

[<DllImport("../dist/Ground", CallingConvention=CallingConvention.Cdecl)>]
extern void FinalizeScene()

[<DllImport("../dist/Ground", CallingConvention=CallingConvention.Cdecl)>]
extern int AddTriangleMesh(float32[] vertices, int numVerts,
    int[] indices, int numIndices)

[<Struct>]
[<StructLayout(LayoutKind.Sequential)>]
type Hit =
    val mutable meshId : int

[<DllImport("../dist/Ground", CallingConvention=CallingConvention.Cdecl)>]
extern Hit TraceSingle(float32[] pos, float32[] dir)