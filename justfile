set windows-powershell # uses powershell instead of cygwin on Windows

default: blender


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
  @echo ""
  @echo "Blender plugin built. Open Blender and go to 'Edit - Preferences - Addons - Install from Disk' (dropdown menu in the top-right corner)"
  @echo "Browse to the 'BlenderExtension/see_sharp_renderer-VERSION.zip' file in this directory and install it."
