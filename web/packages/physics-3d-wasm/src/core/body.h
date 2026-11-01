#pragma once

#include "core/math.h"

namespace axp {

enum BodyState : int32_t {
    kBodyFree = 0,
    kBodyStatic = 1,
    kBodyDynamic = 2
};

enum ShapeKind : int32_t {
    kShapeNone = 0,
    kShapeSphere = 1,
    kShapeBox = 2
};

struct Body {
    int32_t state;
    int32_t shape;
    float invMass;
    Vec3 invInertia;
    Vec3 extents;
    Mat3 rotation;
    AABB aabb;
    float friction;
    float restitution;
};

}
