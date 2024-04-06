import figuregen
from figuregen.util.image import Cropbox
from figuregen.util.templates import FullSizeWithCrops
import simpleimageio as sio
import sys, os

def make_figure(dirname, method_names):
    """
    Creates a simple overview figure using the FullSizeWithCrops template.
    Assumes the given directory contains a reference image and subdirectories for each method:

    - Reference.exr
    - method_names[0].exr
    - method_names[1].exr
    """
    names = ["Reference"]
    names.extend(method_names)
    return FullSizeWithCrops(
        reference_image=sio.read(os.path.join(dirname, "Reference.exr")),
        method_images=[
            sio.read(os.path.join(dirname, f"{name}.exr"))
            for name in method_names
        ],
        method_names=names,
        crops=[
            Cropbox(top=345, left=25, width=64, height=48, scale=4),
            Cropbox(top=155, left=200, width=64, height=48, scale=4),
        ]
    ).figure

if __name__ == "__main__":
    result_dir = sys.argv[1]

    method_names = []
    for i in range(2, len(sys.argv)):
        method_names.append(sys.argv[i])

    # Find all scenes by enumerating the result directory
    rows = []
    for path in os.listdir(result_dir):
        if not os.path.isdir(os.path.join(result_dir, path)):
            continue
        try:
            rows.extend(make_figure(os.path.join(result_dir, path), method_names))
        except:
            print(f"skipping scene with invalid data: {path}")

    figuregen.figure(rows, 18, os.path.join(result_dir, "Overview.pdf"))