#pragma once

#include <renderground/api/api.h>

extern "C" {

// Attempts to create a scene from the given .json scene file.
// Returns false if the scene file contained an error.
// The given "frameBufferId" is used to setup the camera.
GROUND_API bool LoadSceneFromFile(const char* filename, int frameBufferId);

GROUND_API void WriteSceneToFile(const char* filename);

}