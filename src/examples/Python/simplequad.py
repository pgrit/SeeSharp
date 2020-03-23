from api import ground
import time
import ctypes

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

endTime = time.time()
deltaTime = (endTime - startTime) * 1000
print(f"{deltaTime}ms")

filename = ctypes.create_string_buffer(b"../../dist/renderPY.exr")
ground.WriteImage(image, filename)