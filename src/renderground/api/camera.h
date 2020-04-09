#pragma once

#include <renderground/api/api.h>

extern "C" {

/*! 
Creates a pinhole camera, the orientation and position of which is modified by the passed transform.
The default orientation and mappings are as follows:

- The camera is positioned at the origin (0, 0, 0).

- The camera is defined in a left-handed coordinate system,
    * looking along the positive z-axis, 
    * with the x-axis pointing to the right, 
    * and the y-axis pointing upwards.

\param frameBufferId    The id of the image that will be used as the frame buffer. Only used to infer
                        the aspect ratio and resolution of the image plane.

\param transformId      The id of the transformation applied to the camera. It determines how the camera
                        space is mapped to world space. That is, the position of the transformation determines the 
                        position of the camera in world space.
                        Note: We assume that each diagonal entry is either 1 or -1.

\returns The id of the newly created camera.
*/
GROUND_API int CreatePerspectiveCamera(int transformId, float verticalFieldOfView, int frameBufferId);

/**
Generates a ray from the camera for a given pixel and primary sample on the lens.

\see CameraSampleInfo for details on the used conventions.
*/
GROUND_API Ray GenerateCameraRay(int camera, CameraSampleInfo sampleInfo);

/**
Transforms a point in world space to camera space and projects it on the image plane.

\returns A 3d vector where x and y are the 2d film coordinates.
         Z stores the distance of the world space point to the camera.

\see CameraSampleInfo for the conventions the returned vector is following.
*/
GROUND_API Vector3 MapWorldSpaceToCameraFilm(int camera, Vector3 worldSpacePoint);

}
