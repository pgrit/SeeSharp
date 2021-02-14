import figuregen
from figuregen import util
import simpleimageio as sio
import sys, os

def tonemap(img):
    return figuregen.PNG(util.image.lin_to_srgb(img))

def make_figure(dirname, method_names):
    # Read the files
    ref = sio.read(os.path.join(dirname, "Reference.exr"))
    methods = [
        sio.read(os.path.join(dirname, name, "Render.exr"))
        for name in method_names
    ]
    errors = [ f"{util.image.relative_mse(m, ref):.4f}" for m in methods ]

    # Put side by side and write error values underneath
    grid = figuregen.Grid(1, len(method_names) + 1)
    grid.get_element(0, 0).set_image(tonemap(ref)).set_caption("Error (relMSE):")
    for i in range(len(methods)):
        grid.get_element(0, i + 1).set_image(tonemap(methods[i])).set_caption(errors[i])
    grid.set_col_titles("top", ["Reference"] + method_names)

    grid.get_layout().set_col_titles("top", 4, offset_mm=1, fontsize=10)
    grid.get_layout().set_caption(4, offset_mm=1, fontsize=10)

    figuregen.horizontal_figure([grid], 18, os.path.join(dirname, "Overview.pdf"))

if __name__ == "__main__":
    result_dir = sys.argv[1]
    method_names = []
    for i in range(2, len(sys.argv)):
        method_names.append(sys.argv[i])
    make_figure(result_dir, method_names)