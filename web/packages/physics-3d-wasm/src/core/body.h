#pragma once

#include "core/math.h"

namespace axrone::physics {

enum BodyState : int32_t {
    kBodyFree = 0,
    kBodyStatic = 1,
    kBodyDynamic = 2,
    kBodyKinematic = 3
};

enum ShapeKind : int32_t {
    kShapeNone = 0,
    kShapeSphere = 1,
    kShapeBox = 2
};

struct Shape {
    int32_t kind;
    Vec3 extents;
    Vec3 offset;
    Mat3 rotation;
    float density;

    AXRONE_PHYSICS_INLINE void setSphere(float radius, float shapeDensity, const Vec3& shapeOffset) {
        kind = kShapeSphere;
        extents = Vec3(radius, 0.0f, 0.0f);
        offset = shapeOffset;
        rotation.setIdentity();
        density = shapeDensity;
    }

    AXRONE_PHYSICS_INLINE void setBox(
        float hx,
        float hy,
        float hz,
        float shapeDensity,
        const Vec3& shapeOffset,
        float qx,
        float qy,
        float qz,
        float qw
    ) {
        kind = kShapeBox;
        extents = Vec3(hx, hy, hz);
        offset = shapeOffset;
        rotation.fromQuaternion(qx, qy, qz, qw);
        density = shapeDensity;
    }
};

struct Body {
    int32_t state;
    int32_t shapeCount;
    float invMass;
    Vec3 invInertia;
    Mat3 rotation;
    AABB aabb;
    float friction;
    float restitution;
    Shape shapes[kMaxShapesPerBody];
};

static_assert(sizeof(Shape) == 96, "shape size must stay fixed for arena sizing");
static_assert(alignof(Shape) == 16, "shape must stay vector aligned");
static_assert(sizeof(Body) == 96 * kMaxShapesPerBody + 112, "body size must stay fixed for arena sizing");
static_assert(alignof(Body) == 16, "body must stay vector aligned");

AXRONE_PHYSICS_INLINE int32_t shapeSlots(const Body& body) {
    if (body.shapeCount <= 0) {
        return 0;
    }
    return body.shapeCount < kMaxShapesPerBody ? body.shapeCount : kMaxShapesPerBody;
}

AXRONE_PHYSICS_INLINE bool shapeValid(const Shape& shape) {
    if (shape.kind == kShapeSphere) {
        return shape.extents.x > 0.0f;
    }
    if (shape.kind == kShapeBox) {
        return shape.extents.x > 0.0f && shape.extents.y > 0.0f && shape.extents.z > 0.0f;
    }
    return false;
}

AXRONE_PHYSICS_INLINE bool bodyQueryable(const Body& body) {
    return body.state != kBodyFree && shapeSlots(body) > 0;
}

AXRONE_PHYSICS_INLINE Vec3 shapeCenter(const Vec3& bodyPosition, const Mat3& bodyRotation, const Shape& shape) {
    return bodyPosition + bodyRotation.transform(shape.offset);
}

AXRONE_PHYSICS_INLINE void composeRotation(const Mat3& outer, const Mat3& inner, Mat3& result) {
    const float* o = outer.m;
    const float* i = inner.m;

    const float r00 = o[0] * i[0] + o[1] * i[3] + o[2] * i[6];
    const float r01 = o[0] * i[1] + o[1] * i[4] + o[2] * i[7];
    const float r02 = o[0] * i[2] + o[1] * i[5] + o[2] * i[8];
    const float r10 = o[3] * i[0] + o[4] * i[3] + o[5] * i[6];
    const float r11 = o[3] * i[1] + o[4] * i[4] + o[5] * i[7];
    const float r12 = o[3] * i[2] + o[4] * i[5] + o[5] * i[8];
    const float r20 = o[6] * i[0] + o[7] * i[3] + o[8] * i[6];
    const float r21 = o[6] * i[1] + o[7] * i[4] + o[8] * i[7];
    const float r22 = o[6] * i[2] + o[7] * i[5] + o[8] * i[8];

    result.m[0] = r00;
    result.m[1] = r01;
    result.m[2] = r02;
    result.m[3] = r10;
    result.m[4] = r11;
    result.m[5] = r12;
    result.m[6] = r20;
    result.m[7] = r21;
    result.m[8] = r22;
}

AXRONE_PHYSICS_INLINE void shapeWorldRotation(const Mat3& bodyRotation, const Shape& shape, Mat3& result) {
    composeRotation(bodyRotation, shape.rotation, result);
}

}
