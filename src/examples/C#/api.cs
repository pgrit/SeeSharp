namespace Experiments {

    using System.Runtime.InteropServices;

    class Ground {
        [DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)]
        public static extern void InitScene();

        [DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)]
        public static extern void FinalizeScene();

        [DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)]
        public static extern int AddTriangleMesh([In] float[] _vertices, int _numVerts,
            [In] int[] _indices, int _numIndices);

        [StructLayout(LayoutKind.Sequential)]
        public struct Hit {
            public int meshId;
        }

        [DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)]
        public static extern Hit TraceSingle([In] float[] _pos, [In] float[] _dir);

        [DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)]
        public static extern void TraceMulti([In] float[] _pos, [In] float[] _dir, int _num, [Out] Hit[] _hits);

        [DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)]
        public static extern int CreateImage(int _width, int _height, int _numChannels);

        [DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)]
        public static extern void AddSplat(int _image, float _x, float _y, [In] float[] _value);

        [DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)]
        public static extern void AddSplatMulti(int _image, [In] float[] _xs, [In] float[] _ys, [In] float[] _values, int _num);

        [DllImport("../../dist/Ground", CallingConvention=CallingConvention.Cdecl)]
        public static extern void WriteImage(int _image, string _filename);
    };
}