add_library(Ground SHARED)

find_package(embree 3.0 REQUIRED)
target_include_directories(Ground PRIVATE ${EMBREE_INCLUDE_DIRS})

find_package(TBB CONFIG REQUIRED)

find_package(RapidJSON CONFIG REQUIRED)
target_include_directories(Ground PRIVATE ${RAPIDJSON_INCLUDE_DIRS})

# TODO if Windows
add_compile_definitions(GROUND_DLL GROUND_EXPORTS)

# TODO add option to enable / disable this
add_compile_definitions(SANITY_CHECKS)

include_directories(${CMAKE_CURRENT_SOURCE_DIR})

target_sources(Ground
    PUBLIC
        api/types.h
        api/api.h
        api/camera.h
        api/imageio.h
        api/raytrace.h
        api/shading.h
        api/transforms.h
        api/cpputils.h
        api/scenefiles.h
        api/pathcache.h

        renderground.h

    PRIVATE
        api/api.cpp
        api/camera.cpp
        api/imageio.cpp
        api/raytrace.cpp
        api/shading.cpp
        api/transforms.cpp
        api/internal.h
        api/scenefiles.cpp
        api/pathcache.cpp

        geometry/scene.h
        geometry/scene.cpp
        geometry/mesh.h
        geometry/mesh.cpp
        geometry/transform.h
        geometry/transform.cpp

        image/image.h
        image/image.cpp
        image/tinyexr.h

        math/constants.h
        math/float4.h
        math/float4x4.h
        math/distribution.h
        math/wrap.h

        cameras/camera.h
        cameras/perspective.h
        cameras/perspective.cpp

        shading/shading.h
        shading/generic.h
        shading/generic.cpp

        utility/pathcache.h
        utility/pathcache.cpp
)

target_link_libraries(Ground ${EMBREE_LIBRARY} TBB::tbb)

set_target_properties(Ground PROPERTIES
                      RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_CURRENT_SOURCE_DIR}/../dist
                      RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_CURRENT_SOURCE_DIR}/../dist
                      RUNTIME_OUTPUT_DIRECTORY_RELWITHDEBINFO ${CMAKE_CURRENT_SOURCE_DIR}/../dist
)