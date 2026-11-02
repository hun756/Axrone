#pragma once

#include <stdint.h>

#include "core/math.h"
#include "core/world.h"

namespace axrone::physics {

struct RayHit {
    int32_t body;
    int32_t shape;
    float t;
    float px, py, pz;
    float nx, ny, nz;
    float reserved;
};

int32_t raycastAll(
    const World& world,
    Vec3 origin,
    Vec3 direction,
    float maxDistance,
    RayHit* hits,
    int32_t capacity);

bool raycastClosest(
    const World& world,
    Vec3 origin,
    Vec3 direction,
    float maxDistance,
    RayHit& hit);

int32_t overlapSphere(
    const World& world,
    Vec3 center,
    float radius,
    int32_t* bodies,
    int32_t capacity);

}
