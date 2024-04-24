"""
This script can serve as a starting point to generate overview .pdf figures for the rendered images of all scenes.
"""

import figuregen as fig
import simpleimageio as sio
import sys, os, json

colors = [
    [232, 181, 88],
    [5, 142, 78],
    [94, 163, 188],
    [181, 63, 106],
    [255, 255, 255],
]

def make_figure(scene_name, dirname, method_names, exposure):
    """
    Creates the figure for one scene
    Assumes the given directory contains a reference image and subdirectories for each method:

    - Reference.exr (optional)
    - method_names[0].exr
    - method_names[1].exr
    """

    # Add the reference image only if it exists
    names = []
    ref_name = os.path.join(dirname, "Reference.exr")
    has_reference = False
    if os.path.isfile(ref_name):
        names.append("Reference")
        has_reference = True
    names.extend(method_names)

    images = [ sio.read(os.path.join(dirname, f"{name}.exr")) for name in names ]
    reference = images[0] if has_reference else None

    grid = fig.Grid(1, len(names))
    for i in range(len(names)):
        grid[0, i].image = fig.JPEG(sio.lin_to_srgb(sio.exposure(images[i], exposure)), 95)

    # Generate the column titles: method name and render time - if available: error values and speed-up
    if has_reference:
        titles = ["Reference\\vspace{1mm}\\\\relMSE, time\\\\inefficiency"]
    else:
        titles = []

    baseline = None
    for i in range(1 if has_reference else 0, len(names)):
        with open(os.path.join(dirname, f"{names[i]}.json")) as fp:
            meta_data = json.load(fp)
        time_sec = meta_data["RenderTime"] / 1000.0

        if has_reference:
            error = sio.relative_mse_outlier_rejection(images[i], reference)
            inefficiency = error * time_sec

            if baseline is None: # the first method (after the reference) serves as the baseline
                baseline = inefficiency
                speedup = "base"
            else:
                speedup = f"${baseline / inefficiency:.3g}\\times$"

            caption = f"{names[i]}\\vspace{{1mm}}\\\\${error:.3g}$, ${time_sec:.3g}$s\\\\${inefficiency:.3g}$ ({speedup})"
        else:
            caption = f"{names[i]}\\vspace{{1mm}}\\\\${time_sec:.3g}$s"
        titles.append(caption)
    grid.set_col_titles(fig.BOTTOM, titles)
    grid.set_row_titles(fig.LEFT, [f"\\textsc{{{scene_name}}}"])

    num_lines = 3 if has_reference else 2
    fontsize = 8
    grid.layout.column_titles[fig.BOTTOM] = fig.TextFieldLayout(
        fontsize=fontsize,
        size=0.3527777778 * (fontsize + 0.5) * num_lines + 1.0, # heuristic, benefits from manual fine-tuning
        offset=0.25,
    )

    return [[grid]]

if __name__ == "__main__":
    result_dir = sys.argv[1]
    scene_names = str.split(sys.argv[2], ",")
    method_names = str.split(sys.argv[3], ",")

    rows = []
    for path in scene_names:
        # Support syntax like "MyScene;2.5" to optionally specify an exposure value
        p = path.split(";", 1)
        try:
            exposure = float(p[1])
            path = p[0]
        except:
            exposure = 0.0

        try:
            rows.extend(make_figure(path, os.path.join(result_dir, path), method_names, exposure))
        except Exception as exc:
            print(exc)
            print(f"skipping scene with invalid data: {path}")

    fig.figure(rows, 18, os.path.join(result_dir, "Overview.pdf"), fig.PdfBackend(preamble_lines=[
        "\\usepackage[utf8]{inputenc}",
        "\\usepackage[T1]{fontenc}",

        # fonts used by ACM TOG:
        "\\usepackage{libertine}",
        "\\usepackage[libertine]{newtxmath}",

        # use sans-serif font in all captions (remove to use serifs instead)
        "\\renewcommand{\\familydefault}{\\sfdefault}"
    ]))