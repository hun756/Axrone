# draco-bridge — official google/draco for the NativeAOT importer

`NativeDracoMeshPolicy` (in `Axrone.AssetGltf.Codecs`) decodes
`KHR_draco_mesh_compression` through a thin C ABI bridge over the **official
google/draco C++ sources** — no third-party managed port. The C# side uses
`LibraryImport` (source-generated, trim/AOT-safe) and degrades to
`NoDracoCompressionPolicy` behaviour when the RID library is absent.

## Layout

- `axrone_draco_bridge.h` / `.cc` — the C ABI (`axr_draco_*`). Only mesh
  decode is exposed: decode once, pull attributes as floats, pull indices.
- `CMakeLists.txt` — fetches `google/draco` at the pinned tag and builds
  `axrone_draco` as a shared library linked against draco statically, so the
  deployed file has no draco dependency of its own.

## Pinned version

`AXR_DRACO_TAG = 1.5.7` (CMake cache variable). This matches the
`draco3dgltf@1.5.7` WASM decoders used by the web glTF pipeline, so native
and web decode the same bitstream line.

## Local build (needs a C++ toolchain: MSVC / GCC / Clang + CMake)

```sh
cmake -S native/extern/draco-bridge -B build/draco -DCMAKE_BUILD_TYPE=Release
cmake --build build/draco --config Release
# -> build/draco/axrone_draco.dll | libaxrone_draco.so | libaxrone_draco.dylib
```

Stage next to the app for local runs:

```sh
# win-x64 example
mkdir -p native/packages/Axrone.AssetGltf.Codecs/runtimes/win-x64/native
cp build/draco/Release/axrone_draco.dll \
   native/packages/Axrone.AssetGltf.Codecs/runtimes/win-x64/native/
```

(`runtimes/**/native/*` is git-ignored; CI packs it from build artifacts.)

Android cross build (NDK r27):

```sh
cmake -S native/extern/draco-bridge -B build/draco-android \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake \
  -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-24
cmake --build build/draco-android --config Release
```

## RIDs shipped by CI

`win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`, `android-arm64` — the same
set `MeshoptCompressionPolicy` supports. On other RIDs `IsSupported` stays
`false` and Draco payloads are rejected with `gltf.extension.unsupported`.

## License

- Bridge code in this directory: Apache-2.0 (Axrone).
- google/draco sources (fetched at build time, never vendored): Apache-2.0,
  including its Section 3 patent grant. See `THIRD-PARTY-NOTICES.md`.
