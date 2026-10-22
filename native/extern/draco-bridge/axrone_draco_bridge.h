// Copyright 2026 Axrone contributors. SPDX-License-Identifier: Apache-2.0.
//
// C ABI bridge over the official google/draco C++ sources.
// Built per RID from native/extern/draco-bridge; consumed from C# through
// NativeDracoInterop (LibraryImport, NativeAOT-safe, no string marshaling).
//
// Status codes: 0 is success, anything else is failure.
//   1  bad arguments (null pointers, out-of-range indices, undersized buffers)
//   2  input is not a Draco triangular mesh or the bitstream is invalid
//   3  attribute layout or conversion unsupported (for example > 4 components)

#pragma once

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
#define AXR_DRACO_API __declspec(dllexport)
#else
#define AXR_DRACO_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

// Opaque handle. Backed by a heap-allocated draco::Mesh.
typedef struct AxrDracoMesh AxrDracoMesh;

// Decodes one Draco bitstream into a heap-allocated mesh.
// The caller owns *out_mesh and must release it with axr_draco_free.
AXR_DRACO_API int32_t axr_draco_decode(const uint8_t *data, size_t size,
                                       AxrDracoMesh **out_mesh);

// Releases a mesh previously returned by axr_draco_decode. Null-safe.
AXR_DRACO_API void axr_draco_free(AxrDracoMesh *mesh);

// Vertex and triangle counts. Returns 0 for a null mesh.
AXR_DRACO_API uint32_t axr_draco_point_count(const AxrDracoMesh *mesh);
AXR_DRACO_API uint32_t axr_draco_face_count(const AxrDracoMesh *mesh);

// Number of point attributes. Returns 0 for a null mesh.
AXR_DRACO_API int32_t axr_draco_attribute_count(const AxrDracoMesh *mesh);

// Describes one attribute: its Draco unique id, component count (1-4), and
// attribute type (0 POSITION, 1 NORMAL, 2 COLOR, 3 TEX_COORD, 4 GENERIC).
AXR_DRACO_API int32_t axr_draco_attribute_info(const AxrDracoMesh *mesh,
                                              int32_t attribute_index,
                                              uint32_t *out_unique_id,
                                              int32_t *out_components,
                                              int32_t *out_attribute_type);

// Converts every point of one attribute to float and writes float_count
// values (point count times component count) into out_values.
AXR_DRACO_API int32_t axr_draco_attribute_floats(const AxrDracoMesh *mesh,
                                                 int32_t attribute_index,
                                                 float *out_values,
                                                 int32_t float_count);

// Writes index_count (face count times 3) triangle indices into out_indices.
AXR_DRACO_API int32_t axr_draco_face_indices(const AxrDracoMesh *mesh,
                                             uint32_t *out_indices,
                                             int32_t index_count);

#ifdef __cplusplus
}  // extern "C"
#endif
