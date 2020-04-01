## Dependencies

The recommended method to install dependencies is using vcpkg on all platforms.

- Embree 3
- TBB (also a dependency of Embree)
- rapidjson

For example, for a 64-Bit build on Windows, install all dependencies via:

```
vcpkg install embree3 tbb rapidjson --triplet=x64-windows
```
