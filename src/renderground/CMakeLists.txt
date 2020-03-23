find_package(embree 3.0 REQUIRED)
INCLUDE_DIRECTORIES(${EMBREE_INCLUDE_DIRS})

# TODO if Windows
add_compile_definitions(GROUND_DLL GROUND_EXPORTS)

include_directories(${CMAKE_CURRENT_SOURCE_DIR})

add_library(Ground SHARED)

target_sources(Ground
    PUBLIC
        renderground.h

    PRIVATE
        renderground.cpp

        geometry/float3.h
        geometry/geometry.h
        geometry/geometry.cpp
        geometry/mesh.h
        geometry/mesh.cpp
        geometry/ray.h
        geometry/hit.h

        image/image.h
        image/image.cpp
        image/tinyexr.h
)

target_link_libraries(Ground ${EMBREE_LIBRARY})

set_target_properties(Ground PROPERTIES
                      RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_CURRENT_SOURCE_DIR}/../dist
                      RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_CURRENT_SOURCE_DIR}/../dist
                      RUNTIME_OUTPUT_DIRECTORY_RELWITHDEBINFO ${CMAKE_CURRENT_SOURCE_DIR}/../dist
)