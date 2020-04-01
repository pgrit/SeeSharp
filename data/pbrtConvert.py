import re
import sys
import json
import math
import numpy as np

# Common regex patterns used while parsing
decimal = r"-?\d+\.?\d*[e]?[+-]?\d*"
triplet = fr"\[\s*({decimal})\s*({decimal})\s*({decimal})\s*\]"
intList = r'\[[\s*-?\d+\s*]+\]'
decimalList = rf'\[(?:\s*{decimal}\s*)+\]'

materialLink = r"NamedMaterial\s+\"(.*?)\""
trimeshShape = r"Shape\s+\"trianglemesh\""

indices = rf'"integer indices"\s*({intList})'
vertices = rf'"point P"\s*({decimalList})'
normals = rf'"normal N"\s*({decimalList})'
uvs = rf'"float uv"\s*({decimalList})'

def extractMaterials(content): # We only support a limited set of materials following very specific patterns
    materials = []

    # Diffuse materials of the following pattern:
    # example = 'MakeNamedMaterial "Painting3" "string type" [ "matte" ] "rgb Kd" [ 0.058187 0.034230 0.017936 ]'
    namedMaterial = fr"MakeNamedMaterial \"(.*?)\""
    rgbKd = fr"\"rgb Kd\"\s*{triplet}"
    rgbMatteMaterial = fr"{namedMaterial}\s*\"string type\"\s*\[\s*\"matte\"\s*\]\s*{rgbKd}"

    groups = re.findall(rgbMatteMaterial, content)
    content = re.sub(rgbMatteMaterial, '', content)

    for g in groups:
        material = {
            "name": g[0],
            "baseColor": {
                "type": "rgb",
                "value": [float(g[1]), float(g[2]), float(g[3])]
            }
        }
        materials.append(material)
    return content, materials

def convertFloatList(string):
    groups = re.findall(rf"{decimal}", string)
    return [float(v) for v in groups]

def convertIntList(string):
    groups = re.findall(rf"{decimal}", string)
    return [int(v) for v in groups]

def extractShapes(content):
    shapes = []

    # Triangle meshes of the following pattern:
    # example = 'NamedMaterial "RightWall" \n'
    # example += 'Shape "trianglemesh" "integer indices" [ 0 1 2 0 2 3 ] "point P" [ 1 0 -1 1 2 -1 1 2 1 1 0 1 ]'\
    #            '"normal N" [ 1 -4.37114e-008 1.31134e-007 1 -4.37114e-008 1.31134e-007 1 -4.37114e-008 1.31134e-007'\
    #            ' 1 -4.37114e-008 1.31134e-007 ] "float uv" [ 0 0 1 0 1 1 0 1 ]'
    # TODO allow repetitions of shape definitions (with one shared material)
    # TODO allow different permutations of vertices / indices / normals / uvs
    shapeWithMaterial = rf'{materialLink}\s*{trimeshShape}\s*{indices}\s*{vertices}\s*{normals}\s*{uvs}'

    # Read and remove area light sources first, with the following pattern:
    # AttributeBegin
	# 	AreaLightSource "diffuse" "rgb L" [ 17.000000 12.000000 4.000000 ]
	# 	NamedMaterial ...
	# 	Shape ...
	# AttributeEnd
    areaLight = rf'AreaLightSource\s*"diffuse"\s*"rgb L"\s*{triplet}'
    areaLightShape = rf'AttributeBegin\s+{areaLight}\s*{shapeWithMaterial}\s*AttributeEnd\s+'
    groups = re.findall(areaLightShape, content, flags=re.MULTILINE)
    content = re.sub(areaLightShape, '', content)
    for g in groups:
        shape = {
            "name": f"mesh{len(shapes)}",
            "emission": {
                "type": "rgb",
                "unit": "radiance",
                "value": [float(g[0]), float(g[1]), float(g[2])]
            },
            "material": g[3],
            "type": "trimesh",
            "indices": convertIntList(g[4]),
            "vertices": convertFloatList(g[5]),
            "normals": convertFloatList(g[6]),
            "uv": convertFloatList(g[7])
        }
        shapes.append(shape)

    groups = re.findall(shapeWithMaterial, content, flags=re.MULTILINE)
    content = re.sub(shapeWithMaterial, '', content)
    for g in groups:
        shape = {
            "name": f"mesh{len(shapes)}",
            "material": g[0],

            "type": "trimesh",
            "indices": convertIntList(g[1]),
            "vertices": convertFloatList(g[2]),
            "normals": convertFloatList(g[3]),
            "uv": convertFloatList(g[4])
        }
        shapes.append(shape)

    return content, shapes


def rotationMatrixToEulerAngles(R) :
    sy = math.sqrt(R[0,0] * R[0,0] + R[1,0] * R[1,0])

    singular = sy < 1e-6

    if not singular:
        x = math.atan2(R[2,1], R[2,2])
        y = math.atan2(-R[2,0], sy)
        z = math.atan2(R[1,0], R[0,0])
    else:
        x = math.atan2(-R[1,2], R[1,1])
        y = math.atan2(-R[2,0], sy)
        z = 0

    return np.array([x, y, z]) * 180.0 / np.pi

def matrixToTransform(flatMatrix):
    # .pbrt transform matrices are in colom-major order
    matrix = np.reshape(flatMatrix, [4,4], order="Fortran")

    # Extract translation (last column)
    translate = matrix[:-1,3]

    # Extract scale (diagonal)
    scale = np.diag(matrix)[:-1]

    # Compute euler angles from the rotation values
    euler = rotationMatrixToEulerAngles(matrix)

    return translate, euler, scale

def extractCamera(content):
    # TODO also support LookAt transforms and other types
    cameraTransform = rf'Transform\s*({decimalList})'
    perspectiveCamera = rf'Camera "perspective"\s*"float fov"\s*\[\s*({decimal})\s*\]'

    transformGroups = re.findall(cameraTransform + '.*WorldBegin', content, re.MULTILINE | re.DOTALL)
    cameraGroups = re.findall(perspectiveCamera, content)

    content = re.sub(perspectiveCamera, "", content)
    content = re.sub(cameraTransform, "", content)

    transform = matrixToTransform(convertFloatList(transformGroups[0]))
    # The global "Transform" in .pbrt defines the mapping from world space to camera, we need the inverse
    translate = -transform[0] # translation
    rotate = -transform[1] # euler angles
    scale = 1.0 / transform[2] # scale

    transform = {
        "name": "camera",
        "position": translate.tolist(),
        "rotation": rotate.tolist(),
        "scale": scale.tolist()
    }

    camera = {
        "name": "default",
        "type": "perspective",
        "fov": float(cameraGroups[0]),
        "transform": "camera"
    }

    return content, transform, camera

def convert(filename):
    scene = {
        "name": "Converted Scene"
    }

    with open(filename, "r") as f:
        content = f.read()

    content, camTransform, camera = extractCamera(content)
    scene['transforms'] = [camTransform]
    scene['cameras'] = [camera]

    content, scene['materials'] = extractMaterials(content)
    content, scene['objects'] = extractShapes(content)

    with open('output.json', 'w') as fp:
        json.dump(scene, fp, indent=2)

    print("We ignored the following portion of the input file:")
    content = ">>>>  " + "\n>>>>  ".join([line for line in content.splitlines() if not re.match(r'^\s*$', line)])
    print(content)

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Missing command line argument: expected path to .pbrt file.")
        exit(1)
    convert(sys.argv[1])