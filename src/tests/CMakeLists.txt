enable_testing()

find_package(GTest CONFIG REQUIRED)

# Adds a test executable for a named set of tests
function(AddNamedTest TestName TestSources)
    add_executable(${TestName})

    target_sources(${TestName}
    PRIVATE
        "main.cpp"
        "testutil.h"
        ${TestSources}
    )

    target_link_libraries(${TestName} PRIVATE GTest::gtest GTest::gtest_main GTest::gmock GTest::gmock_main)
    target_link_libraries(${TestName} PRIVATE Ground)

    add_test(NAME ${TestName}
            COMMAND ${TestName}
            WORKING_DIRECTORY ${PROJECT_SOURCE_DIR}/src/dist)
endfunction()

AddNamedTest(GeometryTests
    "geometry/shadingnormals.cpp"
    "geometry/vertexattributes.cpp"
)

AddNamedTest(ShadingTests
    "shading/diffuse.cpp"
    "shading/furnace.cpp"
)

AddNamedTest(EmitterTests
    "emitter/diffuse.cpp"
)

AddNamedTest(CameraTests
    "camera/perspective.cpp"
)

AddNamedTest(UtilityTests
    "utility/pathcache.cpp"
)