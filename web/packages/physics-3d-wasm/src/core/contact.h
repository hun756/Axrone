#pragma once

#include "core/math.h"

namespace axrone::physics {

struct alignas(16) Contact {
    int32_t a;
    int32_t b;
    Vec3 normal;
    float pen;
    Vec3 t1;
    Vec3 t2;
    Vec3 ra;
    Vec3 rb;
    float massN;
    float massT1;
    float massT2;
    float impN;
    float impT1;
    float impT2;
    float friction;
    float restitution;
    float velocityBias;
    int32_t normalMode;
    Vec3 localNormal;
    Vec3 localA;
    Vec3 localB;
    int32_t cacheSlot;
};

struct PackedContact {
    int32_t a;
    int32_t b;
    float nx;
    float ny;
    float nz;
    float pen;
};

struct CacheEntry {
    int32_t keyA;
    int32_t keyB;
    int32_t feature;
    float impN;
    float impT1;
    float impT2;
};

}
