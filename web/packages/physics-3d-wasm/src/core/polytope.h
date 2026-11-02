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

void buildPrismHull(const Vec3& center, const Mat3& rotation, float radius, float halfHeight, Hull& hull);
void buildConeHull(const Vec3& center, const Mat3& rotation, float radius, float halfHeight, Hull& hull);
void buildBoxHull(const Vec3& center, const Mat3& rotation, const Vec3& half, Hull& hull);

float hullMaxProjection(const Hull& hull, const Vec3& axis);
float hullMinProjection(const Hull& hull, const Vec3& axis);

void hullClosestPoint(
    const Hull& hull,
    const Vec3& point,
    Vec3& closest,
    float& signedDistance,
    Vec3& faceHint
);

}
