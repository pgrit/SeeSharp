add_library(SeeCore SHARED)

find_package(embree 3.0 CONFIG REQUIRED)
find_package(TBB CONFIG REQUIRED)

if (WIN32)
    add_compile_definitions(SEE_CORE_DLL SEE_CORE_EXPORTS)
endif()

target_include_directories(SeeCore PRIVATE ${EMBREE_INCLUDE_DIRS})
target_include_directories(SeeCore PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})

target_sources(SeeCore
    PUBLIC
        "api/types.h"
        "api/api.h"
        "api/raytrace.h"
        "api/image.h"
        "seecore.h"

    PRIVATE
        "geometry/raytrace.cpp"
        "geometry/scene.h"
        "geometry/scene.cpp"
        "image/image.cpp"
        "image/tinyexr.h"
)

target_link_libraries(SeeCore ${EMBREE_LIBRARIES} TBB::tbb)

set_target_properties(SeeCore PROPERTIES
                      RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_CURRENT_SOURCE_DIR}/../../dist
                      RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_CURRENT_SOURCE_DIR}/../../dist
                      RUNTIME_OUTPUT_DIRECTORY_RELWITHDEBINFO ${CMAKE_CURRENT_SOURCE_DIR}/../../dist
)