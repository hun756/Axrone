// Copyright 2026 Axrone contributors. SPDX-License-Identifier: Apache-2.0.
//
// C ABI bridge over the official google/draco C++ sources (Apache-2.0).
// Only the mesh-decode path used by the Axrone glTF importer is exposed:
// decode once, then pull attributes as floats and triangle indices.
// The caller (C#) owns all output buffers; this file never allocates
// caller-visible memory except the AxrDracoMesh handle itself.

#include "axrone_draco_bridge.h"

#include <memory>

#include "draco/compression/decode.h"
#include "draco/mesh/mesh.h"

namespace {

inline const draco::Mesh *AsMesh(const AxrDracoMesh *handle) {
  return reinterpret_cast<const draco::Mesh *>(handle);
}

inline draco::Mesh *AsMesh(AxrDracoMesh *handle) {
  return reinterpret_cast<draco::Mesh *>(handle);
}

}  // namespace

extern "C" {

AXR_DRACO_API int32_t axr_draco_decode(const uint8_t *data, size_t size,
                                       AxrDracoMesh **out_mesh) {
  if (data == nullptr || size == 0 || out_mesh == nullptr) {
    return 1;
  }
  *out_mesh = nullptr;

  // The type query consumes buffer position, so decode from a fresh buffer.
  draco::DecoderBuffer type_buffer;
  type_buffer.Init(reinterpret_cast<const char *>(data), size);
  const auto geometry_type =
      draco::Decoder::GetEncodedGeometryType(&type_buffer);
  if (!geometry_type.ok() ||
      geometry_type.value() != draco::TRIANGULAR_MESH) {
    return 2;
  }

  draco::Decoder decoder;
  draco::DecoderBuffer buffer;
  buffer.Init(reinterpret_cast<const char *>(data), size);
  auto mesh = std::unique_ptr<draco::Mesh>(new draco::Mesh());
  const draco::Status status =
      decoder.DecodeBufferToGeometry(&buffer, mesh.get());
  if (!status.ok()) {
    return 2;
  }

  *out_mesh = reinterpret_cast<AxrDracoMesh *>(mesh.release());
  return 0;
}

AXR_DRACO_API void axr_draco_free(AxrDracoMesh *mesh) {
  delete AsMesh(mesh);
}

AXR_DRACO_API uint32_t axr_draco_point_count(const AxrDracoMesh *mesh) {
  if (mesh == nullptr) {
    return 0;
  }
  return AsMesh(mesh)->num_points();
}

AXR_DRACO_API uint32_t axr_draco_face_count(const AxrDracoMesh *mesh) {
  if (mesh == nullptr) {
    return 0;
  }
  return AsMesh(mesh)->num_faces();
}

AXR_DRACO_API int32_t axr_draco_attribute_count(const AxrDracoMesh *mesh) {
  if (mesh == nullptr) {
    return 0;
  }
  return AsMesh(mesh)->num_attributes();
}

AXR_DRACO_API int32_t axr_draco_attribute_info(const AxrDracoMesh *mesh,
                                              int32_t attribute_index,
                                              uint32_t *out_unique_id,
                                              int32_t *out_components,
                                              int32_t *out_attribute_type) {
  if (mesh == nullptr || out_unique_id == nullptr || out_components == nullptr ||
      out_attribute_type == nullptr) {
    return 1;
  }
  const draco::Mesh *m = AsMesh(mesh);
  if (attribute_index < 0 || attribute_index >= m->num_attributes()) {
    return 1;
  }
  const draco::PointAttribute *attribute = m->attribute(attribute_index);
  if (attribute == nullptr) {
    return 2;
  }
  const uint8_t components = attribute->num_components();
  if (components == 0 || components > 4) {
    return 3;
  }
  *out_unique_id = attribute->unique_id();
  *out_components = static_cast<int32_t>(components);
  *out_attribute_type =
      static_cast<int32_t>(attribute->attribute_type());
  return 0;
}

AXR_DRACO_API int32_t axr_draco_attribute_floats(const AxrDracoMesh *mesh,
                                                 int32_t attribute_index,
                                                 float *out_values,
                                                 int32_t float_count) {
  if (mesh == nullptr || out_values == nullptr) {
    return 1;
  }
  const draco::Mesh *m = AsMesh(mesh);
  if (attribute_index < 0 || attribute_index >= m->num_attributes()) {
    return 1;
  }
  const draco::PointAttribute *attribute = m->attribute(attribute_index);
  if (attribute == nullptr) {
    return 2;
  }
  const uint8_t components = attribute->num_components();
  if (components == 0 || components > 4) {
    return 3;
  }
  const uint32_t point_count = m->num_points();
  if (float_count < static_cast<int32_t>(point_count * components)) {
    return 1;
  }
  // Decoded Draco attributes are per-point values reached through the
  // attribute's point-to-value map (identity or explicit).
  for (uint32_t i = 0; i < point_count; ++i) {
    const draco::AttributeValueIndex value_index =
        attribute->mapped_index(draco::PointIndex(i));
    if (!attribute->ConvertValue(value_index,
                                 static_cast<int8_t>(components),
                                 out_values + i * components)) {
      return 3;
    }
  }
  return 0;
}

AXR_DRACO_API int32_t axr_draco_face_indices(const AxrDracoMesh *mesh,
                                             uint32_t *out_indices,
                                             int32_t index_count) {
  if (mesh == nullptr || out_indices == nullptr) {
    return 1;
  }
  const draco::Mesh *m = AsMesh(mesh);
  const uint32_t face_count = m->num_faces();
  if (index_count < static_cast<int32_t>(face_count * 3u)) {
    return 1;
  }
  for (uint32_t i = 0; i < face_count; ++i) {
    const draco::Mesh::Face &face = m->face(draco::FaceIndex(i));
    out_indices[i * 3 + 0] = face[0].value();
    out_indices[i * 3 + 1] = face[1].value();
    out_indices[i * 3 + 2] = face[2].value();
  }
  return 0;
}

}  // extern "C"
