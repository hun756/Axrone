#include "core/broadphase.h"

#include "core/narrowphase.h"
#include "platform/math.h"

namespace axrone::physics {

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

float selectExtent(const Vec3& half, int32_t axis) {
    if (axis == 0) {
        return half.x;
    }
    if (axis == 1) {
        return half.y;
    }
    return half.z;
}

}

void updateBounds(World& world) {
    for (int32_t i = 0; i < world.highWater; ++i) {
        Body& body = world.bodies[i];
        const int32_t slots = shapeSlots(body);
        if (body.state == kBodyFree || slots == 0) {
            body.aabb = { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
            continue;
        }

        const Vec3 position = transformPosition(world.transforms[i]);
        const Mat3& rotation = body.rotation;
        AABB bounds = { kBig, kBig, kBig, -kBig, -kBig, -kBig };
        bool covered = false;

        for (int32_t slot = 0; slot < slots; ++slot) {
            const Shape& shape = body.shapes[slot];
            if (!shapeValid(shape)) {
                continue;
            }

            const Vec3 center = shapeCenter(position, rotation, shape);
            float ex = 0.0f;
            float ey = 0.0f;
            float ez = 0.0f;

            if (shape.kind == kShapeSphere) {
                ex = shape.extents.x;
                ey = ex;
                ez = ex;
            } else if (shape.kind == kShapeCapsule) {
                Mat3 worldRotation;
                shapeWorldRotation(rotation, shape, worldRotation);
                const Vec3 halfAxis(worldRotation.m[1] * shape.extents.y,
                                     worldRotation.m[4] * shape.extents.y,
                                     worldRotation.m[7] * shape.extents.y);
                const float radius = shape.extents.x;
                ex = fabsf(halfAxis.x) + radius;
                ey = fabsf(halfAxis.y) + radius;
                ez = fabsf(halfAxis.z) + radius;
            } else {
                Vec3 half = shape.extents;
                if (shape.kind == kShapeCylinder) {
                    half.z = shape.extents.x;
                }
                Mat3 worldRotation;
                shapeWorldRotation(rotation, shape, worldRotation);
                ex = fabsf(worldRotation.m[0]) * selectExtent(half, 0) +
                     fabsf(worldRotation.m[1]) * selectExtent(half, 1) +
                     fabsf(worldRotation.m[2]) * selectExtent(half, 2);
                ey = fabsf(worldRotation.m[3]) * selectExtent(half, 0) +
                     fabsf(worldRotation.m[4]) * selectExtent(half, 1) +
                     fabsf(worldRotation.m[5]) * selectExtent(half, 2);
                ez = fabsf(worldRotation.m[6]) * selectExtent(half, 0) +
                     fabsf(worldRotation.m[7]) * selectExtent(half, 1) +
                     fabsf(worldRotation.m[8]) * selectExtent(half, 2);
            }

            if (center.x - ex < bounds.minX) {
                bounds.minX = center.x - ex;
            }
            if (center.y - ey < bounds.minY) {
                bounds.minY = center.y - ey;
            }
            if (center.z - ez < bounds.minZ) {
                bounds.minZ = center.z - ez;
            }
            if (center.x + ex > bounds.maxX) {
                bounds.maxX = center.x + ex;
            }
            if (center.y + ey > bounds.maxY) {
                bounds.maxY = center.y + ey;
            }
            if (center.z + ez > bounds.maxZ) {
                bounds.maxZ = center.z + ez;
            }
            covered = true;
        }

        body.aabb = covered ? bounds : AABB{ 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
    }
}

int32_t buildSweep(World& world) {
    int32_t count = 0;
    for (int32_t i = 0; i < world.highWater; ++i) {
        const Body& body = world.bodies[i];
        if (body.state != kBodyFree && shapeSlots(body) > 0) {
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
        const bool aKinematic = world.bodies[a].state == kBodyKinematic;

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
            const bool bKinematic = world.bodies[b].state == kBodyKinematic;
            if (!aDynamic && world.bodies[b].state != kBodyDynamic && aKinematic == bKinematic) {
                continue;
            }

            collidePair(world, a, b);
        }
    }
}

}
