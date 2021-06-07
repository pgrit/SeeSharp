#!/bin/sh

dotnet publish ./SeeSharp.PreviewRender -c Release -o ./see_blender/bin
zip -r see_blender see_blender

echo "Blender plugin built. Open Blender and go to 'Edit - Preferences - Addons - Install...'"
echo "Browse to the 'see_blender.zip' file in this directory and install it."