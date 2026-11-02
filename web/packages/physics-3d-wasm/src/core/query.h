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

struct StepStatistics {
    float broadphasePairsTested;
    float shapePairsTested;
    float contactsGenerated;
    float sensorContacts;
    float velocityIterations;
    float positionIterations;
    float sleepingBodies;
    float liveBodies;
};

static_assert(sizeof(StepStatistics) == 32, "step statistics record must stay eight f32 fields");
static_assert(alignof(StepStatistics) == 4, "step statistics record must stay f32 aligned");

AXRONE_PHYSICS_INLINE bool queryFilterActive(int32_t categoryBits, int32_t maskBits) {
    return categoryBits != 0 || maskBits != 0;
}

AXRONE_PHYSICS_INLINE bool queryShapePassesFilter(const Shape& shape, int32_t categoryBits, int32_t maskBits) {
    if (!queryFilterActive(categoryBits, maskBits)) {
        return true;
    }
    return (shape.categoryBits & maskBits) != 0 && (categoryBits & shape.maskBits) != 0;
}

int32_t raycastAll(
    const World& world,
    Vec3 origin,
    Vec3 direction,
    float maxDistance,
    RayHit* hits,
    int32_t capacity);

int32_t raycastAll(
    const World& world,
    Vec3 origin,
    Vec3 direction,
    float maxDistance,
    int32_t categoryBits,
    int32_t maskBits,
    RayHit* hits,
    int32_t capacity);

bool raycastClosest(
    const World& world,
    Vec3 origin,
    Vec3 direction,
    float maxDistance,
    RayHit& hit);

bool raycastClosest(
    const World& world,
    Vec3 origin,
    Vec3 direction,
    float maxDistance,
    int32_t categoryBits,
    int32_t maskBits,
    RayHit& hit);

int32_t overlapSphere(
    const World& world,
    Vec3 center,
    float radius,
    int32_t* bodies,
    int32_t capacity);

int32_t overlapSphere(
    const World& world,
    Vec3 center,
    float radius,
    int32_t categoryBits,
    int32_t maskBits,
    int32_t* bodies,
    int32_t capacity);

int32_t overlapBoxAll(
    const World& world,
    const AABB& bounds,
    int32_t* bodies,
    int32_t capacity);

int32_t queryPointAll(
    const World& world,
    Vec3 point,
    int32_t* bodies,
    int32_t capacity);

void applyFractionConvention(RayHit* hits, int32_t count, Vec3 direction);

void applyFractionConvention(RayHit& hit, Vec3 direction);

void collectStepStatistics(
    const World& world,
    int32_t velocityIterations,
    int32_t positionIterations,
    StepStatistics& out);

}
