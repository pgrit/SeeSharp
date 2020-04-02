namespace Experiments {

using System.Runtime.InteropServices;

class Ground {

    public class Scene {
        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        public static extern void InitScene();

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        public static extern void FinalizeScene();

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        public static extern bool LoadSceneFromFile([In] string filename, int frameBufferId);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        public static extern int GetNumberEmitters();

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        public static extern int GetEmitterMesh(int id);
    }


    public class Image {
        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        public static extern int CreateImageRGB(int width, int height);
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct Hit {
        public int meshId;
    }

    [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
    public static extern Hit TraceSingle([In] float[] _pos, [In] float[] _dir);

    [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
    public static extern void TraceMulti([In] float[] _pos, [In] float[] _dir, int _num, [Out] Hit[] _hits);

    [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
    public static extern int CreateImage(int _width, int _height, int _numChannels);

    [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
    public static extern void AddSplat(int _image, float _x, float _y, [In] float[] _value);

    [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
    public static extern void AddSplatMulti(int _image, [In] float[] _xs, [In] float[] _ys, [In] float[] _values, int _num);

    [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
    public static extern void WriteImage(int _image, string _filename);
};
}