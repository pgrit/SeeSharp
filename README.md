<img src="logo.png" width=120 height=120 alt="SeeSharp Logo" />

# SeeSharp 

Traditional rendering frameworks are almost exclusively written in C++. They offer great
performance, but require quite some implementation effort. Hence, quickly testing various
ideas, both large and small, can be quite a chore. The SeeSharp rendering framwork tackles
that problem.

In principle, SeeSharp is yet another rendering framework. It combines the Embree traversal
kernels with a PBRT-style material system and a variety of rendering algorithms (integrators).
The key difference is the choice of programming language: C#. Thanks to C# and .NET core, 
rendering experiments can benefit from short compile times, far easier debuging, no nasty 
segfaults, and even a REPL.

At the cost of a surprisingly small reduction in performance, experiments can be done much 
quicker, and without wasting time on debugging undefined behaviour only because you yet again forgot a -1 somewhere.

The following sections explain how to get the framework up and running. Additionally, a simple
example experiment is discussed, to demonstrate the benefits of SeeSharp.

## Getting started

## Building the C++ libraries

You will need CMake and a C++14 compliant compiler to build the project.

### Dependencies

The recommended method to install dependencies is using vcpkg on all platforms.

- Embree 3
- TBB (also a dependency of Embree)

For example, for a 64-Bit build on Windows, install all dependencies via:

```
vcpkg install embree3 tbb --triplet=x64-windows
```

### Using CMake to build

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

## Building the SeeSharp framework

### Building

Building is trivial. From anywhere, simply run:

```
dotnet build [PATH_TO_ROOT_DIR]/src/SeeSharp
```

### Testing

To run the unit test, you first need to add the folder `dist` to the path (as it contains the shared libraries).
Then, run the following commands:

```
cd dist
dotnet test ../src/SeeSharp/
```

## Rendering a scene from Blender

SeeSharp comes with a simple export script for Blender, that can export triangle meshes, cameras,
and a small subset of Cycles materials, to SeeSharp's file format. The script is called 
`src/BlendToSeeSharp.py`
To use the script, simply run it within Blender.

### Exporting an example scene

### Rendering with the interactive viewer

### Performance comparison to Mitsuba

## Conducting an experiment

SeeSharp is designed to be used as a library, to write rendering experiments with. To get started, you should first create a new console application that will contain you experiment set-up, as well as any additional algorithms or other changes you will introduce.
To get started, run the following commands:

```
dotnet new console -o MyFirstExperiment
dotnet add ./MyFirstExperiment reference [...]/SeeSharp/src/SeeSharp/Experiments
```

Currently, you only need to add the `Experiments` project as a reference, it automatically references all other components as well.

Now, you can write your own experiment setup, for instance by deriving a class from `ExperimentFactory`.
To compile and run, simply type:

```
dotnet run ./MyFirstExperiment
```

Note that the `dist` directory, which contains the .so / .dll files of the C++ libraries, needs to be in the path (or in the current working directory).
