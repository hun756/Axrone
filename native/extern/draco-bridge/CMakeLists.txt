# axrone_draco: google/draco shared library for the Axrone importer.
#
# Builds a thin C ABI bridge (axrone_draco_bridge.cc) against the official
# google/draco C++ sources fetched with CMake FetchContent. The produced
# shared library (axrone_draco.dll / libaxrone_draco.so / .dylib) is staged
# into native/packages/Axrone.AssetGltf.Codecs/runtimes/<rid>/native/ by CI
# and consumed from C# through NativeDracoInterop (LibraryImport).
#
# Pinned google/draco tag: 1.5.7 (same bitstream line as the web decoders).
# Override at configure time when deliberately moving the pin (both values):
#   cmake -DAXR_DRACO_TAG=1.5.9 -DAXR_DRACO_SHA256=<archive sha256> ...

cmake_minimum_required(VERSION 3.20)

project(axrone_draco_bridge CXX)

set(AXR_DRACO_TAG "1.5.7" CACHE STRING "google/draco git tag to build against")
set(AXR_DRACO_SHA256
    "27b72ba2d5ff3d0a9814ad40d4cb88f8dc89a35491c0866d952473f8f9416b77"
    CACHE STRING "SHA-256 of the google/draco tag archive (update with AXR_DRACO_TAG)")

set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Keep draco's optional targets off; only the static core is needed. Draco
# 1.5.7 has no switch for its CLI tools, so build the axrone_draco target
# explicitly instead of relying on the default ALL target.
foreach(opt IN ITEMS
    DRACO_TESTS DRACO_WASM DRACO_JS_GLUE DRACO_UNITY_PLUGIN
    DRACO_MAYA_PLUGIN DRACO_INSTALL)
  set(${opt} OFF CACHE BOOL "" FORCE)
endforeach()
set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)

include(FetchContent)
FetchContent_Declare(
  draco
  URL "https://github.com/google/draco/archive/refs/tags/${AXR_DRACO_TAG}.zip"
  URL_HASH "SHA256=${AXR_DRACO_SHA256}"
  DOWNLOAD_EXTRACT_TIMESTAMP TRUE
)
FetchContent_MakeAvailable(draco)

add_library(axrone_draco SHARED axrone_draco_bridge.cc)

# Draco exposes its include paths as PRIVATE on the draco target, so they do
# not propagate. The generated draco/draco_features.h lands under the
# top-level binary directory (draco_build == CMAKE_BINARY_DIR).
target_include_directories(axrone_draco PRIVATE
  "${draco_SOURCE_DIR}/src"
  "${CMAKE_BINARY_DIR}"
)

# Only axr_draco_* may leave the shared object; static draco symbols stay hidden.
set_target_properties(axrone_draco PROPERTIES
  CXX_VISIBILITY_PRESET hidden
  VISIBILITY_INLINES_HIDDEN ON
)

# The static Draco archive keeps default visibility on ELF; exclude it so the
# deployed library does not export (or interpose) draco symbols.
if(UNIX AND NOT APPLE)
  target_link_options(axrone_draco PRIVATE "-Wl,--exclude-libs,ALL")
endif()

if(TARGET draco_static)
  target_link_libraries(axrone_draco PRIVATE draco_static)
elseif(TARGET draco)
  target_link_libraries(axrone_draco PRIVATE draco)
else()
  message(FATAL_ERROR "google/draco did not provide a linkable static target")
endif()
