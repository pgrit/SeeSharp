
find_package(TBB CONFIG REQUIRED)

# TODO if Windows
add_compile_definitions(GROUND_DLL GROUND_IMPORTS)

add_executable(SimpleQuad)

target_sources(SimpleQuad
    PRIVATE
        simplequad.cpp
)

target_link_libraries(SimpleQuad Ground TBB::tbb)

set_target_properties(SimpleQuad PROPERTIES
                      RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_CURRENT_SOURCE_DIR}/../../dist
                      RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_CURRENT_SOURCE_DIR}/../../dist
)


add_executable(SimplePath)

target_sources(SimplePath
    PRIVATE
        simplepath.cpp
)

target_link_libraries(SimplePath Ground TBB::tbb)

set_target_properties(SimplePath PROPERTIES
                      RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_CURRENT_SOURCE_DIR}/../../dist"
                      RUNTIME_OUTPUT_DIRECTORY_DEBUG "${CMAKE_CURRENT_SOURCE_DIR}/../../dist"
                      RUNTIME_OUTPUT_DIRECTORY_RELWITHDEBINFO "${CMAKE_CURRENT_SOURCE_DIR}/../../dist"
)