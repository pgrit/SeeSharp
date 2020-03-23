import ctypes
import os

libPath = "../../dist"
libName = "Ground.dll" # TODO platform specific

# The 'dist' folder must be in the path for Python to find also the other
# dependencies correctly (Embree, tbb, ...)
os.environ['PATH'] = ';'.join([os.environ['PATH'], libPath])

ground = ctypes.cdll.LoadLibrary(libName)

ground.InitScene.restype = None
ground.InitScene.argtypes = []

ground.FinalizeScene.restype = None
ground.FinalizeScene.argtypes = []

ground.AddTriangleMesh.restype = ctypes.c_int
ground.AddTriangleMesh.argtypes = [
    ctypes.POINTER(ctypes.c_float),
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int
]

class Hit(ctypes.Structure):
    _fields_ = [
        ("meshId", ctypes.c_int)
    ]

ground.TraceSingle.restype = Hit
ground.TraceSingle.argtypes = [
    ctypes.POINTER(ctypes.c_float),
    ctypes.POINTER(ctypes.c_float)
]

ground.CreateImage.restype = ctypes.c_int
ground.CreateImage.argtypes = [
    ctypes.c_int,
    ctypes.c_int,
    ctypes.c_int
]

ground.AddSplat.restype = None
ground.AddSplat.argtypes = [
    ctypes.c_int,
    ctypes.c_float,
    ctypes.c_float,
    ctypes.POINTER(ctypes.c_float)
]

ground.WriteImage.restype = None
ground.WriteImage.argtypes = [
    ctypes.c_int,
    ctypes.c_char_p
]