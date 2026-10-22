# axrone_draco: google/draco shared library for the Axrone importer.
#
# Builds a thin C ABI bridge (axrone_draco_bridge.cc) against the official
# google/draco C++ sources fetched with CMake FetchContent. The produced
# shared library (axrone_draco.dll / libaxrone_draco.so / .dylib) is staged
# into native/packages/Axrone.AssetGltf.Codecs/runtimes/<rid>/native/ by CI
# and consumed from C# through NativeDracoInterop (LibraryImport).
#
# Pinned google/draco tag: 1.5.7 (same bitstream line as the web decoders).
# Override at configure time when deliberately moving the pin:
#   cmake -DAXR_DRACO_TAG=1.5.9 ...

cmake_minimum_required(VERSION 3.20)

project(axrone_draco_bridge CXX)

set(AXR_DRACO_TAG "1.5.7" CACHE STRING "google/draco git tag to build against")

set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Keep draco's own test/tool/plugin targets off; only the static core is needed.
# Unknown option names are harmless (CMake warns that they were not used).
foreach(opt IN ITEMS
    DRACO_TESTS DRACO_WASM DRACO_JS_GLUE DRACO_UNITY_PLUGIN
    DRACO_MAYA_PLUGIN DRACO_NODEJS DRACO_BUILD_EXECUTABLES)
  set(${opt} OFF CACHE BOOL "" FORCE)
endforeach()
set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)

include(FetchContent)
FetchContent_Declare(
  draco
  URL "https://github.com/google/draco/archive/refs/tags/${AXR_DRACO_TAG}.zip"
)
FetchContent_MakeAvailable(draco)

add_library(axrone_draco SHARED axrone_draco_bridge.cc)
target_include_directories(axrone_draco PRIVATE "${draco_SOURCE_DIR}/src")

if(TARGET draco_static)
  target_link_libraries(axrone_draco PRIVATE draco_static)
elseif(TARGET draco)
  target_link_libraries(axrone_draco PRIVATE draco)
else()
  message(FATAL_ERROR "google/draco did not provide a linkable static target")
endif()
