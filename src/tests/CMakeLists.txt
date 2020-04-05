enable_testing()

find_package(GTest CONFIG REQUIRED)

add_executable(TestMain)

target_sources(TestMain
PRIVATE
    main.cpp
    shadingnormals.cpp
    diffuse.cpp
    furnace.cpp
    vertexattributes.cpp
)

target_link_libraries(TestMain PRIVATE GTest::gtest GTest::gtest_main GTest::gmock GTest::gmock_main)
target_link_libraries(TestMain PRIVATE Ground)

add_test(NAME TestMain
         COMMAND TestMain
         WORKING_DIRECTORY ${PROJECT_SOURCE_DIR}/src/dist)