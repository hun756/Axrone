#include "core/broadphase.h"

#include "core/narrowphase.h"
#include "platform/math.h"

namespace axp {

namespace {

bool sweepLess(const World& world, int32_t left, int32_t right) {
    const float leftMin = world.bodies[left].aabb.minX;
    const float rightMin = world.bodies[right].aabb.minX;
    if (leftMin < rightMin) {
        return true;
    }
    if (rightMin < leftMin) {
        return false;
    }
    return left < right;
}

}

void updateBounds(World& world) {
    for (int32_t i = 0; i < world.highWater; ++i) {
        Body& body = world.bodies[i];
        if (body.state == kBodyFree || body.shape == kShapeNone) {
            continue;
        }

        const Vec3 position = transformPosition(world.transforms[i]);
        AABB& bounds = body.aabb;

        if (body.shape == kShapeSphere) {
            const float radius = body.extents.x;
            bounds.minX = position.x - radius;
            bounds.minY = position.y - radius;
            bounds.minZ = position.z - radius;
            bounds.maxX = position.x + radius;
            bounds.maxY = position.y + radius;
            bounds.maxZ = position.z + radius;
            continue;
        }

        const Mat3& rotation = body.rotation;
        const Vec3 half = body.extents;
        const float ex = fabsf(rotation.m[0]) * half.x + fabsf(rotation.m[1]) * half.y + fabsf(rotation.m[2]) * half.z;
        const float ey = fabsf(rotation.m[3]) * half.x + fabsf(rotation.m[4]) * half.y + fabsf(rotation.m[5]) * half.z;
        const float ez = fabsf(rotation.m[6]) * half.x + fabsf(rotation.m[7]) * half.y + fabsf(rotation.m[8]) * half.z;
        bounds.minX = position.x - ex;
        bounds.minY = position.y - ey;
        bounds.minZ = position.z - ez;
        bounds.maxX = position.x + ex;
        bounds.maxY = position.y + ey;
        bounds.maxZ = position.z + ez;
    }
}

int32_t buildSweep(World& world) {
    int32_t count = 0;
    for (int32_t i = 0; i < world.highWater; ++i) {
        const Body& body = world.bodies[i];
        if (body.state != kBodyFree && body.shape != kShapeNone) {
            world.sweepOrder[count++] = i;
        }
    }

    int32_t gap = count >> 1;
    while (gap > 0) {
        for (int32_t i = gap; i < count; ++i) {
            const int32_t item = world.sweepOrder[i];
            int32_t j = i;
            while (j >= gap && sweepLess(world, item, world.sweepOrder[j - gap])) {
                world.sweepOrder[j] = world.sweepOrder[j - gap];
                j -= gap;
            }
            world.sweepOrder[j] = item;
        }
        gap >>= 1;
    }
    return count;
}

void broadphase(World& world) {
    const int32_t count = buildSweep(world);

    for (int32_t i = 0; i < count; ++i) {
        const int32_t a = world.sweepOrder[i];
        const AABB& boundsA = world.bodies[a].aabb;
        const bool aDynamic = world.bodies[a].state == kBodyDynamic;

        for (int32_t j = i + 1; j < count; ++j) {
            const int32_t b = world.sweepOrder[j];
            const AABB& boundsB = world.bodies[b].aabb;

            if (boundsB.minX > boundsA.maxX) {
                break;
            }
            if (boundsB.minY > boundsA.maxY || boundsB.maxY < boundsA.minY) {
                continue;
            }
            if (boundsB.minZ > boundsA.maxZ || boundsB.maxZ < boundsA.minZ) {
                continue;
            }
            if (!aDynamic && world.bodies[b].state != kBodyDynamic) {
                continue;
            }

            collidePair(world, a, b);
        }
    }
}

}
