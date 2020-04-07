# Building the project

You will need CMake and a C++14 compliant compiler to build the project.

## Dependencies

The recommended method to install dependencies is using vcpkg on all platforms.

- Embree 3
- TBB (also a dependency of Embree)
- rapidjson
- GTest (to build the unit tests)

For example, for a 64-Bit build on Windows, install all dependencies via:

```
vcpkg install embree3 tbb rapidjson gtest --triplet=x64-windows
```

## Using CMake to build

To build the project after installing the dependencies, first create a new folder, for example:

```
cd renderground
mkdir build
cd build
```

Now use CMake to generate the proper makefiles for your platform, don't forget to specify the vcpkg toolchain file:

```
cmake .. -DCMAKE_TOOLCHAIN_FILE=...
cmake --build .
```

Or, alternatively, simply open the folder in Visual Studio 2019 and it should be automatically configured correctly.