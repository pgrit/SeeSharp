module Ground

open System.Runtime.InteropServices

[<DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)>]
extern void InitScene()

[<DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)>]
extern void FinalizeScene()

[<DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)>]
extern int AddTriangleMesh([<In>] float32[] _vertices, int _numVerts,
    [<In>] int[] _indices, int _numIndices)

[<Struct>]
[<StructLayout(LayoutKind.Sequential)>]
type Hit =
    val mutable meshId : int

[<DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)>]
extern Hit TraceSingle([<In>] float32[] _pos, [<In>] float32[] _dir)

[<DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)>]
extern void TraceMulti([<In>] float32[] _pos, [<In>] float32[] _dir, int _num, [<Out>] Hit[] _hits)

[<DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)>]
extern int CreateImage(int _width, int _height, int _numChannels)

[<DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)>]
extern void AddSplat(int _image, float32 _x, float32 _y, [<In>] float32[] _value)

[<DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)>]
extern void AddSplatMulti(int _image, [<In>] float32[] _xs, [<In>] float32[] _ys, [<In>] float32[] _values, int _num)

[<DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)>]
extern void WriteImage(int _image, string _filename)