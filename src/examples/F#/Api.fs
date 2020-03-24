module Ground

open System.Runtime.InteropServices

[<DllImport("Ground", CallingConvention=CallingConvention.Cdecl)>]
extern void InitScene()

[<DllImport("Ground", CallingConvention=CallingConvention.Cdecl)>]
extern void FinalizeScene()

[<DllImport("Ground", CallingConvention=CallingConvention.Cdecl)>]
extern int AddTriangleMesh([<In>] float32[] _vertices, int _numVerts,
    [<In>] int[] _indices, int _numIndices)

[<Struct>]
[<StructLayout(LayoutKind.Sequential)>]
type Hit =
    val mutable meshId : int

[<DllImport("Ground", CallingConvention=CallingConvention.Cdecl)>]
extern Hit TraceSingle([<In>] float32[] _pos, [<In>] float32[] _dir)

[<DllImport("Ground", CallingConvention=CallingConvention.Cdecl)>]
extern void TraceMulti([<In>] float32[] _pos, [<In>] float32[] _dir, int _num, [<Out>] Hit[] _hits)



[<DllImport("Ground", CallingConvention=CallingConvention.Cdecl)>]
extern int CreateImage(int _width, int _height, int _numChannels)

[<DllImport("Ground", CallingConvention=CallingConvention.Cdecl)>]
extern void AddSplat(int _image, float32 _x, float32 _y, [<In>] float32[] _value)

[<DllImport("Ground", CallingConvention=CallingConvention.Cdecl)>]
extern void AddSplatMulti(int _image, [<In>] float32[] _xs, [<In>] float32[] _ys, [<In>] float32[] _values, int _num)

[<DllImport("Ground", CallingConvention=CallingConvention.Cdecl)>]
extern void WriteImage(int _image, string _filename)


[<DllImport("Ground", CallingConvention=CallingConvention.Cdecl)>]
extern void InitShadingSystem(int _spectralResolution)

[<Struct>]
[<StructLayout(LayoutKind.Sequential)>]
type UberShaderParams =
    val mutable baseColorTexture : int
    val mutable emissionTexture : int

[<DllImport("Ground", CallingConvention=CallingConvention.Cdecl)>]
extern int AddUberMaterial(UberShaderParams& _params)

[<DllImport("Ground", CallingConvention=CallingConvention.Cdecl)>]
extern void AssignMaterial(int _mesh, int _material)