#include "core/polytope.h"

namespace axrone::physics {

namespace {

const float kHullRingCos[kHullRingVertices] = {
    1.0f,
    0.70710678f,
    0.0f,
    -0.70710678f,
    -1.0f,
    -0.70710678f,
    0.0f,
    0.70710678f
};

const float kHullRingSin[kHullRingVertices] = {
    0.0f,
    0.70710678f,
    1.0f,
    0.70710678f,
    0.0f,
    -0.70710678f,
    -1.0f,
    -0.70710678f
};

void finalizeHull(Hull& hull) {
    Vec3 centroid(0.0f, 0.0f, 0.0f);
    for (int32_t i = 0; i < hull.vertexCount; ++i) {
        centroid += hull.vertices[i];
    }
    centroid *= 1.0f / static_cast<float>(hull.vertexCount);

    for (int32_t f = 0; f < hull.faceCount; ++f) {
        const int32_t start = hull.faceStart[f];
        const int32_t count = hull.faceStart[f + 1] - start;
        const Vec3& v0 = hull.vertices[hull.faceIndices[start]];
        const Vec3& v1 = hull.vertices[hull.faceIndices[start + 1]];
        const Vec3& v2 = hull.vertices[hull.faceIndices[start + 2]];

        Vec3 normal = normalize(cross(v1 - v0, v2 - v0));
        if (dot(normal, v0 - centroid) < 0.0f) {
            normal = -normal;
            for (int32_t i = 0; i < count / 2; ++i) {
                const int32_t low = hull.faceIndices[start + i];
                const int32_t high = hull.faceIndices[start + count - 1 - i];
                hull.faceIndices[start + i] = high;
                hull.faceIndices[start + count - 1 - i] = low;
            }
        }

        hull.faceNormals[f] = normal;
        hull.faceOffsets[f] = dot(normal, hull.vertices[hull.faceIndices[start]]);
    }
}

Vec3 closestPointOnHullSegment(const Vec3& point, const Vec3& a, const Vec3& b) {
    const Vec3 direction = b - a;
    const float span = lengthSq(direction);
    if (span <= kEps) {
        return a;
    }
    const float t = clampf(dot(point - a, direction) / span, 0.0f, 1.0f);
    return a + direction * t;
}

bool hullFaceContains(const Hull& hull, int32_t face, const Vec3& point) {
    const int32_t start = hull.faceStart[face];
    const int32_t count = hull.faceStart[face + 1] - start;
    const Vec3& normal = hull.faceNormals[face];

    for (int32_t i = 0; i < count; ++i) {
        const Vec3& a = hull.vertices[hull.faceIndices[start + i]];
        const Vec3& b = hull.vertices[hull.faceIndices[start + (i + 1) % count]];
        const Vec3 side = cross(b - a, normal);
        if (dot(side, point - a) > 0.0f) {
            return false;
        }
    }
    return true;
}

}

void buildPrismHull(const Vec3& center, const Mat3& rotation, float radius, float halfHeight, Hull& hull) {
    hull.vertexCount = 2 * kHullRingVertices;
    for (int32_t i = 0; i < kHullRingVertices; ++i) {
        hull.vertices[i] = center + rotation.transform(
            Vec3(radius * kHullRingCos[i], -halfHeight, radius * kHullRingSin[i]));
        hull.vertices[kHullRingVertices + i] = center + rotation.transform(
            Vec3(radius * kHullRingCos[i], halfHeight, radius * kHullRingSin[i]));
    }

    hull.faceCount = kHullRingVertices + 2;
    int32_t cursor = 0;
    for (int32_t i = 0; i < kHullRingVertices; ++i) {
        const int32_t next = (i + 1) % kHullRingVertices;
        hull.faceStart[i] = cursor;
        hull.faceIndices[cursor++] = i;
        hull.faceIndices[cursor++] = kHullRingVertices + i;
        hull.faceIndices[cursor++] = kHullRingVertices + next;
        hull.faceIndices[cursor++] = next;
    }

    hull.faceStart[kHullRingVertices] = cursor;
    for (int32_t i = 0; i < kHullRingVertices; ++i) {
        hull.faceIndices[cursor++] = kHullRingVertices + i;
    }

    hull.faceStart[kHullRingVertices + 1] = cursor;
    for (int32_t i = 0; i < kHullRingVertices; ++i) {
        hull.faceIndices[cursor++] = i;
    }

    hull.faceStart[kHullRingVertices + 2] = cursor;
    finalizeHull(hull);
}

void buildConeHull(const Vec3& center, const Mat3& rotation, float radius, float halfHeight, Hull& hull) {
    hull.vertexCount = kHullRingVertices + 1;
    hull.vertices[0] = center + rotation.transform(Vec3(0.0f, halfHeight, 0.0f));
    for (int32_t i = 0; i < kHullRingVertices; ++i) {
        hull.vertices[1 + i] = center + rotation.transform(
            Vec3(radius * kHullRingCos[i], -halfHeight, radius * kHullRingSin[i]));
    }

    hull.faceCount = kHullRingVertices + 1;
    int32_t cursor = 0;
    for (int32_t i = 0; i < kHullRingVertices; ++i) {
        const int32_t next = (i + 1) % kHullRingVertices;
        hull.faceStart[i] = cursor;
        hull.faceIndices[cursor++] = 0;
        hull.faceIndices[cursor++] = 1 + next;
        hull.faceIndices[cursor++] = 1 + i;
    }

    hull.faceStart[kHullRingVertices] = cursor;
    for (int32_t i = 0; i < kHullRingVertices; ++i) {
        hull.faceIndices[cursor++] = 1 + i;
    }

    hull.faceStart[kHullRingVertices + 1] = cursor;
    finalizeHull(hull);
}

void buildBoxHull(const Vec3& center, const Mat3& rotation, const Vec3& half, Hull& hull) {
    const Vec3 axes[3] = {
        Vec3(rotation.m[0], rotation.m[3], rotation.m[6]),
        Vec3(rotation.m[1], rotation.m[4], rotation.m[7]),
        Vec3(rotation.m[2], rotation.m[5], rotation.m[8])
    };

    hull.vertexCount = 8;
    for (int32_t i = 0; i < 8; ++i) {
        const Vec3 delta(
            (i & 1) != 0 ? half.x : -half.x,
            (i & 2) != 0 ? half.y : -half.y,
            (i & 4) != 0 ? half.z : -half.z
        );
        hull.vertices[i] = center +
            axes[0] * delta.x +
            axes[1] * delta.y +
            axes[2] * delta.z;
    }

    static const int32_t boxFaces[6][4] = {
        { 1, 3, 7, 5 },
        { 4, 6, 2, 0 },
        { 2, 6, 7, 3 },
        { 1, 5, 4, 0 },
        { 4, 5, 7, 6 },
        { 2, 3, 1, 0 }
    };

    hull.faceCount = 6;
    int32_t cursor = 0;
    for (int32_t f = 0; f < 6; ++f) {
        hull.faceStart[f] = cursor;
        for (int32_t i = 0; i < 4; ++i) {
            hull.faceIndices[cursor++] = boxFaces[f][i];
        }
    }
    hull.faceStart[6] = cursor;
    finalizeHull(hull);
}

float hullMaxProjection(const Hull& hull, const Vec3& axis) {
    float best = dot(axis, hull.vertices[0]);
    for (int32_t i = 1; i < hull.vertexCount; ++i) {
        const float value = dot(axis, hull.vertices[i]);
        if (value > best) {
            best = value;
        }
    }
    return best;
}

float hullMinProjection(const Hull& hull, const Vec3& axis) {
    float best = dot(axis, hull.vertices[0]);
    for (int32_t i = 1; i < hull.vertexCount; ++i) {
        const float value = dot(axis, hull.vertices[i]);
        if (value < best) {
            best = value;
        }
    }
    return best;
}

void hullClosestPoint(
    const Hull& hull,
    const Vec3& point,
    Vec3& closest,
    float& signedDistance,
    Vec3& faceHint
) {
    float maxSep = -kBig;
    int32_t maxFace = 0;
    for (int32_t f = 0; f < hull.faceCount; ++f) {
        const float sep = dot(hull.faceNormals[f], point) - hull.faceOffsets[f];
        if (sep > maxSep) {
            maxSep = sep;
            maxFace = f;
        }
    }

    faceHint = hull.faceNormals[maxFace];

    if (maxSep <= 0.0f) {
        signedDistance = maxSep;
        closest = point + faceHint * (-maxSep);
        return;
    }

    float bestSq = kBig;
    Vec3 best = point;

    for (int32_t f = 0; f < hull.faceCount; ++f) {
        const float sep = dot(hull.faceNormals[f], point) - hull.faceOffsets[f];
        if (sep <= 0.0f) {
            continue;
        }
        if (sep * sep >= bestSq) {
            continue;
        }
        const Vec3 projection = point - hull.faceNormals[f] * sep;
        if (hullFaceContains(hull, f, projection)) {
            bestSq = sep * sep;
            best = projection;
        }
    }

    for (int32_t f = 0; f < hull.faceCount; ++f) {
        const int32_t start = hull.faceStart[f];
        const int32_t count = hull.faceStart[f + 1] - start;
        for (int32_t i = 0; i < count; ++i) {
            const Vec3& a = hull.vertices[hull.faceIndices[start + i]];
            const Vec3& b = hull.vertices[hull.faceIndices[start + (i + 1) % count]];
            const Vec3 candidate = closestPointOnHullSegment(point, a, b);
            const float distanceSq = lengthSq(point - candidate);
            if (distanceSq < bestSq) {
                bestSq = distanceSq;
                best = candidate;
            }
        }
    }

    signedDistance = sqrtf(bestSq);
    closest = best;
}

}
