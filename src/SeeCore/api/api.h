#pragma once

// Used to generate correct DLL linkage on Windows
#ifdef SEE_CORE_DLL
    #ifdef SEE_CORE_EXPORTS
        #define SEE_CORE_API __declspec(dllexport)
    #else
        #define SEE_CORE_API __declspec(dllimport)
    #endif
#else
    #define SEE_CORE_API
#endif

#include <SeeCore/api/types.h>
