#pragma once

#include "core/math.h"

namespace axrone::physics {

struct Hull {
    int32_t vertexCount;
    Vec3 vertices[kHullMaxVertices];
    int32_t faceCount;
    int32_t faceStart[kHullMaxFaces + 1];
    int32_t faceIndices[kHullMaxIndices];
    Vec3 faceNormals[kHullMaxFaces];
    float faceOffsets[kHullMaxFaces];
};

struct HullView {
    int32_t vertexCount;
    const Vec3* vertices;
    int32_t faceCount;
    const int32_t* faceStart;
    const int32_t* faceIndices;
    const Vec3* faceNormals;
    const float* faceOffsets;
};

AXRONE_PHYSICS_INLINE HullView makeHullView(const Hull& hull) {
    HullView view;
    view.vertexCount = hull.vertexCount;
    view.vertices = hull.vertices;
    view.faceCount = hull.faceCount;
    view.faceStart = hull.faceStart;
    view.faceIndices = hull.faceIndices;
    view.faceNormals = hull.faceNormals;
    view.faceOffsets = hull.faceOffsets;
    return view;
}

void buildPrismHull(const Vec3& center, const Mat3& rotation, float radius, float halfHeight, Hull& hull);
void buildConeHull(const Vec3& center, const Mat3& rotation, float radius, float halfHeight, Hull& hull);
void buildBoxHull(const Vec3& center, const Mat3& rotation, const Vec3& half, Hull& hull);

bool buildUserHullFaces(
    const Vec3* vertices,
    int32_t vertexCount,
    int32_t* faceIndices,
    int32_t* faceStart,
    Vec3* faceNormals,
    float* faceOffsets,
    int32_t maxFaces,
    int32_t& faceCount,
    int32_t& indexTotal
);

float hullMaxProjection(const HullView& view, const Vec3& axis);
float hullMinProjection(const HullView& view, const Vec3& axis);

void hullClosestPoint(
    const HullView& view,
    const Vec3& point,
    Vec3& closest,
    float& signedDistance,
    Vec3& faceHint
);

}
