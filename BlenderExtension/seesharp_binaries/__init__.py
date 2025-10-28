import os
import subprocess

dllpath = os.path.dirname(__file__) + "/bin/SeeSharp.PreviewRender.dll"

def preview_render(scene, output, size_x, size_y, samples, algorithm, maxdepth, denoise):
    exe = os.path.dirname(__file__) + "/bin/SeeSharp.PreviewRender.dll"
    args = ["dotnet", exe]
    args.extend([
        "--scene", scene,
        "--output", output,
        "--resx", str(size_x),
        "--resy", str(size_y),
        "--samples", str(samples),
        "--algo", str(algorithm),
        "--maxdepth", str(maxdepth),
        "--denoise", str(denoise)
    ])
    subprocess.call(args)
