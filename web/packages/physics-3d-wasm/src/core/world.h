#pragma once

#include <stdint.h>

#include "core/body.h"
#include "core/contact.h"
#include "core/math.h"

namespace axrone::physics {

class Arena;

struct World {
    bool initialized;
    int32_t maxBodies;
    int32_t maxContacts;
    int32_t cacheCapacity;
    uint32_t cacheMask;
    int32_t highWater;
    int32_t liveCount;
    int32_t freeCount;
    int32_t contactCount;
    Vec3 gravity;

    Transform* transforms;
    Vec3* velocities;
    Vec3* angularVelocities;
    PackedContact* packedContacts;
    Body* bodies;
    int32_t* sweepOrder;
    int32_t* freeIds;
    Contact* contacts;
    CacheEntry* cachePrev;
    CacheEntry* cacheNext;
    int32_t* cachePrevUsed;
    int32_t* cacheNextUsed;
    int32_t cachePrevUsedCount;
    int32_t cacheNextUsedCount;

    Vec3 axesA[3];
    Vec3 axesB[3];
    Vec3 faceVertices[4];
    Vec3 clipA[8];
    Vec3 clipB[8];

    bool init(Arena& arena, int32_t maxBodies, int32_t maxContacts, Vec3 gravity);
    void reset();
    int32_t createBody(int32_t type, float px, float py, float pz, float qx, float qy, float qz, float qw);
    int32_t destroyBody(int32_t id);
    int32_t setSphere(int32_t id, float radius, float density);
    int32_t setBox(int32_t id, float hx, float hy, float hz, float density);
    int32_t setTransform(int32_t id, float px, float py, float pz, float qx, float qy, float qz, float qw);
    int32_t setVelocity(int32_t id, float vx, float vy, float vz);
    int32_t setAngularVelocity(int32_t id, float vx, float vy, float vz);
    int32_t setMaterial(int32_t id, float friction, float restitution);
    int32_t setGravity(float gx, float gy, float gz);
    int32_t step(float dt, int32_t velocityIterations, int32_t positionIterations);

    int32_t bodyCount() const;
    int32_t getContactCount() const;
    uint64_t fingerprint() const;

    bool isActive(int32_t id) const;
    int32_t cacheLookup(int32_t a, int32_t b, int32_t feature) const;
    int32_t cacheStore(int32_t a, int32_t b, int32_t feature, float impN, float impT1, float impT2);
};

}
