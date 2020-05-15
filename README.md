# Components

## Blender exporter

## C++ libraries

## SeeSharp rendering framework

# Building the C++ libraries

You will need CMake and a C++14 compliant compiler to build the project.

## Dependencies

The recommended method to install dependencies is using vcpkg on all platforms.

- Embree 3
- TBB (also a dependency of Embree)

For example, for a 64-Bit build on Windows, install all dependencies via:

```
vcpkg install embree3 tbb --triplet=x64-windows
```

## Using CMake to build

To build the project after installing the dependencies, first create a new folder, for example:

```
cd SeeSharp
mkdir build
cd build
```

Now use CMake to generate the proper makefiles for your platform, don't forget to specify the vcpkg toolchain file:

```
cmake .. -DCMAKE_TOOLCHAIN_FILE=...
cmake --build .
```

Or, alternatively, simply open the folder in Visual Studio 2019 and it should be automatically configured correctly.

# Building the SeeSharp framework

## Building

Building is trivial. From anywhere, simply run:

```
dotnet build [PATH_TO_ROOT_DIR]/src/SeeSharp
```

## Testing

To run the unit test, you first need to add the folder `dist` to the path (as it contains the shared libraries).
Then, run the following commands:

```
cd dist
dotnet test ../src/SeeSharp/
```