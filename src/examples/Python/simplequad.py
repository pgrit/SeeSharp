from api import ground, Hit
import time
import ctypes
import numpy as np

ground.InitScene()

vertices = [
    0.0, 0.0, 0.0,
    1.0, 0.0, 0.0,
    1.0, 1.0, 0.0,
    0.0, 1.0, 0.0
]

indices = [
    0, 1, 2,
    0, 2, 3
]

vertC = (ctypes.c_float * len(vertices))(*vertices)
indeC = (ctypes.c_int * len(indices))(*indices)
ground.AddTriangleMesh(vertC, 4, indeC, 6)

ground.FinalizeScene()

imageWidth = 512
imageHeight = 512
image = ground.CreateImage(imageWidth, imageHeight, 1)

topLeft = (-1, -1, 5)
diag = (3, 3, 0)

startTime = time.time()

SINGLE_RAY_MODE = False

if SINGLE_RAY_MODE:
    for y in range(imageHeight):
        for x in range(imageWidth):
            org = (
                topLeft[0] + float(x) / float(imageWidth) * diag[0],
                topLeft[1] + float(y) / float(imageHeight) * diag[1],
                5.0
            )
            dir = (0.0, 0.0, -1.0)

            orgC = (ctypes.c_float * len(org))(*org)
            dirC = (ctypes.c_float * len(dir))(*dir)
            hit = ground.TraceSingle(orgC, dirC)

            value = [ hit.meshId ] # how to access by name "meshId"?
            valC = (ctypes.c_float * len(value))(*value)
            ground.AddSplat(image, x, y, valC)
else:
    # Create coordinate array
    x = np.linspace(-1, 2, imageWidth)
    y = np.linspace(-1, 2, imageHeight)
    ox, oy = np.meshgrid(x, y, indexing="ij")

    # Compute ray origins
    oz = np.ones(ox.shape) * 5

    # Stack and flatten to obtain array of floats in AoS layout
    origins = np.stack((ox,oy,oz), axis=2).flatten()

    # Directions are equal everywhere
    dirs = np.tile([0,0,-1], imageWidth * imageHeight)

    orgC = (ctypes.c_float * len(origins))(*origins)
    dirC = (ctypes.c_float * len(dirs))(*dirs)
    hits = (Hit * (imageWidth * imageHeight))()
    ground.TraceMulti(orgC, dirC, imageWidth * imageHeight, hits)

    # Splat into the image: create coordinate vectors
    x = np.arange(0, imageWidth)
    y = np.arange(0, imageHeight)
    xs, ys = np.meshgrid(x, y, indexing="ij")
    xs = xs.flatten()
    ys = ys.flatten()
    xC = (ctypes.c_float * len(xs))(*xs)
    yC = (ctypes.c_float * len(ys))(*ys)

    # Assemble values (based on meshId for now)
    values = np.array(hits).astype(float)
    valC = (ctypes.c_float * len(values))(*values)

    ground.AddSplatMulti(image, xC, yC, valC, imageHeight * imageWidth)

endTime = time.time()
deltaTime = (endTime - startTime) * 1000
print(f"{deltaTime}ms")

filename = ctypes.create_string_buffer(b"../../dist/renderPY.exr")
ground.WriteImage(image, filename)