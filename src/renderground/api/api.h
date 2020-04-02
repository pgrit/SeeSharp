#pragma once

// Used to generate correct DLL linkage on Windows
#ifdef GROUND_DLL
    #ifdef GROUND_EXPORTS
        #define GROUND_API __declspec(dllexport)
    #else
        #define GROUND_API __declspec(dllimport)
    #endif
#else
    #define GROUND_API
#endif

#include <renderground/api/types.h>
