
# TODO if Windows
add_compile_definitions(GROUND_DLL GROUND_IMPORTS)

add_executable(SimpleQuad)

target_sources(SimpleQuad
    PRIVATE
        simplequad.cpp
)

target_link_libraries(SimpleQuad Ground)

set_target_properties(SimpleQuad PROPERTIES
                      RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_CURRENT_SOURCE_DIR}/../../dist
                      RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_CURRENT_SOURCE_DIR}/../../dist
)