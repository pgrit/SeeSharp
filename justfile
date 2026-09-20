set windows-powershell # uses powershell instead of cygwin on Windows

default: blender_install

_build_dotnet:
  dotnet publish ./SeeSharp.PreviewRender -c Release -o ./BlenderExtension/seesharp_binaries/bin

[working-directory: "./BlenderExtension"]
_blender_binaries:
  python -m build --wheel
  cp -r ./dist ./see_blender/wheels

# Builds the Blender add-on .zip
[working-directory: "./BlenderExtension/see_blender/"]
blender: _build_dotnet _blender_binaries
  blender --command extension build --output-dir ..

[working-directory: "./BlenderExtension/"]
blender_install: blender
  blender --command extension install-file ./see_sharp_renderer-1.0.0.zip -r user_default

template:
  dotnet new install ./SeeSharp.Templates --force
