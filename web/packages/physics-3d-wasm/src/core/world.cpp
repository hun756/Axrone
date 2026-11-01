#include "core/world.h"

#include "core/broadphase.h"
#include "core/narrowphase.h"
#include "core/solver.h"
#include "platform/memory.h"
#include "runtime/arena.h"

namespace axrone::physics {

namespace {

template <typename T>
T* allocateArray(Arena& arena, int32_t count) {
    if (count <= 0) {
        return nullptr;
    }
    return static_cast<T*>(arena.alloc(sizeof(T) * static_cast<size_t>(count), 64));
}

uint32_t contactHash(int32_t a, int32_t b, int32_t feature) {
    return (static_cast<uint32_t>(a) * 73856093u) ^
           (static_cast<uint32_t>(b) * 19349663u) ^
           (static_cast<uint32_t>(feature) * 83492791u);
}

struct Hasher {
    uint64_t value;

    Hasher() : value(14695981039346656037ull) {}

    void byte(uint8_t input) {
        value ^= input;
        value *= 1099511628211ull;
    }

    void u32(uint32_t input) {
        byte(static_cast<uint8_t>(input));
        byte(static_cast<uint8_t>(input >> 8));
        byte(static_cast<uint8_t>(input >> 16));
        byte(static_cast<uint8_t>(input >> 24));
    }

    void i32(int32_t input) {
        u32(static_cast<uint32_t>(input));
    }

    void f32(float input) {
        uint32_t bits = 0;
        memcpy(&bits, &input, sizeof(bits));
        u32(bits);
    }

    void vec3(const Vec3& input) {
        f32(input.x);
        f32(input.y);
        f32(input.z);
    }
};

}

bool World::init(Arena& arena, int32_t bodyCapacity, int32_t contactCapacity, Vec3 initialGravity) {
    initialized = false;
    if (bodyCapacity <= 0 || bodyCapacity > kMaxBodies ||
        contactCapacity <= 0 || contactCapacity > kMaxContacts ||
        !isFinite(initialGravity.x) || !isFinite(initialGravity.y) || !isFinite(initialGravity.z)) {
        return false;
    }

    maxBodies = bodyCapacity;
    maxContacts = contactCapacity;
    gravity = initialGravity;
    highWater = 0;
    liveCount = 0;
    freeCount = 0;
    contactCount = 0;

    cacheCapacity = 64;
    while (cacheCapacity < maxContacts * 2) {
        cacheCapacity <<= 1;
    }
    cacheMask = static_cast<uint32_t>(cacheCapacity - 1);

    transforms = allocateArray<Transform>(arena, maxBodies);
    velocities = allocateArray<Vec3>(arena, maxBodies);
    angularVelocities = allocateArray<Vec3>(arena, maxBodies);
    packedContacts = allocateArray<PackedContact>(arena, maxContacts);
    bodies = allocateArray<Body>(arena, maxBodies);
    sweepOrder = allocateArray<int32_t>(arena, maxBodies);
    freeIds = allocateArray<int32_t>(arena, maxBodies);
    contacts = allocateArray<Contact>(arena, maxContacts);
    cachePrev = allocateArray<CacheEntry>(arena, cacheCapacity);
    cacheNext = allocateArray<CacheEntry>(arena, cacheCapacity);
    cachePrevUsed = allocateArray<int32_t>(arena, maxContacts);
    cacheNextUsed = allocateArray<int32_t>(arena, maxContacts);

    if (transforms == nullptr || velocities == nullptr || angularVelocities == nullptr ||
        packedContacts == nullptr || bodies == nullptr || sweepOrder == nullptr ||
        freeIds == nullptr || contacts == nullptr || cachePrev == nullptr || cacheNext == nullptr ||
        cachePrevUsed == nullptr || cacheNextUsed == nullptr) {
        return false;
    }

    cachePrevUsedCount = 0;
    cacheNextUsedCount = 0;
    memset(cachePrev, 0, sizeof(CacheEntry) * static_cast<size_t>(cacheCapacity));
    memset(cacheNext, 0, sizeof(CacheEntry) * static_cast<size_t>(cacheCapacity));

    initialized = true;
    return true;
}

void World::reset() {
    if (!initialized) {
        return;
    }
    highWater = 0;
    liveCount = 0;
    freeCount = 0;
    contactCount = 0;
    cachePrevUsedCount = 0;
    cacheNextUsedCount = 0;
    memset(cachePrev, 0, sizeof(CacheEntry) * static_cast<size_t>(cacheCapacity));
    memset(cacheNext, 0, sizeof(CacheEntry) * static_cast<size_t>(cacheCapacity));
}

bool World::isActive(int32_t id) const {
    return initialized && id >= 0 && id < highWater && bodies[id].state != kBodyFree;
}

int32_t World::createBody(int32_t type, float px, float py, float pz, float qx, float qy, float qz, float qw) {
    if (!initialized || (type != kBodyTypeStatic && type != kBodyTypeDynamic)) {
        return -1;
    }
    if (!isFinite(px) || !isFinite(py) || !isFinite(pz) ||
        !isFinite(qx) || !isFinite(qy) || !isFinite(qz) || !isFinite(qw)) {
        return -1;
    }

    int32_t id = -1;
    if (freeCount > 0) {
        id = freeIds[--freeCount];
    } else if (highWater < maxBodies) {
        id = highWater++;
    } else {
        return -1;
    }

    Transform transform = { px, py, pz, qx, qy, qz, qw };
    normalizeQuaternion(transform);
    transforms[id] = transform;
    velocities[id] = Vec3();
    angularVelocities[id] = Vec3();

    Body& body = bodies[id];
    body.state = type == kBodyTypeStatic ? kBodyStatic : kBodyDynamic;
    body.shape = kShapeNone;
    body.invMass = 0.0f;
    body.invInertia = Vec3();
    body.extents = Vec3();
    body.rotation.setIdentity();
    body.rotation.fromQuaternion(transform.qx, transform.qy, transform.qz, transform.qw);
    body.aabb = { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
    body.friction = kDefaultFriction;
    body.restitution = 0.0f;

    ++liveCount;
    return id;
}

int32_t World::destroyBody(int32_t id) {
    if (!isActive(id)) {
        return -1;
    }

    Body& body = bodies[id];
    body.state = kBodyFree;
    body.shape = kShapeNone;
    body.invMass = 0.0f;
    body.invInertia = Vec3();
    body.extents = Vec3();
    body.aabb = { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
    velocities[id] = Vec3();
    angularVelocities[id] = Vec3();

    freeIds[freeCount++] = id;
    --liveCount;
    contactCount = 0;
    return 0;
}

int32_t World::setSphere(int32_t id, float radius, float density) {
    if (!isActive(id) || !isFinite(radius) || !isFinite(density) || radius <= 0.0f || density < 0.0f) {
        return -1;
    }

    Body& body = bodies[id];
    const bool dynamic = body.state == kBodyDynamic;
    float mass = 0.0f;
    float inertia = 0.0f;
    float invMass = 0.0f;
    float invInertia = 0.0f;

    if (dynamic) {
        if (density <= 0.0f) {
            return -1;
        }
        mass = density * kSphereVolume * radius * radius * radius;
        inertia = kSphereInertia * mass * radius * radius;
        if (!isFinite(mass) || !isFinite(inertia) || mass <= 0.0f || inertia <= 0.0f) {
            return -1;
        }
        invMass = 1.0f / mass;
        invInertia = 1.0f / inertia;
        if (!isFinite(invMass) || !isFinite(invInertia)) {
            return -1;
        }
    }

    body.shape = kShapeSphere;
    body.extents = Vec3(radius, 0.0f, 0.0f);
    body.invMass = invMass;
    body.invInertia = Vec3(invInertia, invInertia, invInertia);
    return 0;
}

int32_t World::setBox(int32_t id, float hx, float hy, float hz, float density) {
    if (!isActive(id) || !isFinite(hx) || !isFinite(hy) || !isFinite(hz) ||
        !isFinite(density) || hx <= 0.0f || hy <= 0.0f || hz <= 0.0f || density < 0.0f) {
        return -1;
    }

    Body& body = bodies[id];
    const bool dynamic = body.state == kBodyDynamic;
    float mass = 0.0f;
    float inertiaX = 0.0f;
    float inertiaY = 0.0f;
    float inertiaZ = 0.0f;
    float invMass = 0.0f;
    float invInertiaX = 0.0f;
    float invInertiaY = 0.0f;
    float invInertiaZ = 0.0f;

    if (dynamic) {
        if (density <= 0.0f) {
            return -1;
        }
        mass = density * kBoxVolume * hx * hy * hz;
        const float third = 1.0f / 3.0f;
        inertiaX = mass * (hy * hy + hz * hz) * third;
        inertiaY = mass * (hx * hx + hz * hz) * third;
        inertiaZ = mass * (hx * hx + hy * hy) * third;
        if (!isFinite(mass) || !isFinite(inertiaX) || !isFinite(inertiaY) || !isFinite(inertiaZ) ||
            mass <= 0.0f || inertiaX <= 0.0f || inertiaY <= 0.0f || inertiaZ <= 0.0f) {
            return -1;
        }
        invMass = 1.0f / mass;
        invInertiaX = 1.0f / inertiaX;
        invInertiaY = 1.0f / inertiaY;
        invInertiaZ = 1.0f / inertiaZ;
        if (!isFinite(invMass) || !isFinite(invInertiaX) || !isFinite(invInertiaY) || !isFinite(invInertiaZ)) {
            return -1;
        }
    }

    body.shape = kShapeBox;
    body.extents = Vec3(hx, hy, hz);
    body.invMass = invMass;
    body.invInertia = Vec3(invInertiaX, invInertiaY, invInertiaZ);
    return 0;
}

int32_t World::setTransform(int32_t id, float px, float py, float pz, float qx, float qy, float qz, float qw) {
    if (!isActive(id) || !isFinite(px) || !isFinite(py) || !isFinite(pz) ||
        !isFinite(qx) || !isFinite(qy) || !isFinite(qz) || !isFinite(qw)) {
        return -1;
    }

    Transform transform = { px, py, pz, qx, qy, qz, qw };
    normalizeQuaternion(transform);
    transforms[id] = transform;
    bodies[id].rotation.fromQuaternion(transform.qx, transform.qy, transform.qz, transform.qw);
    return 0;
}

int32_t World::setVelocity(int32_t id, float vx, float vy, float vz) {
    if (!isActive(id) || !isFinite(vx) || !isFinite(vy) || !isFinite(vz)) {
        return -1;
    }
    velocities[id] = Vec3(vx, vy, vz);
    return 0;
}

int32_t World::setAngularVelocity(int32_t id, float vx, float vy, float vz) {
    if (!isActive(id) || !isFinite(vx) || !isFinite(vy) || !isFinite(vz)) {
        return -1;
    }
    angularVelocities[id] = Vec3(vx, vy, vz);
    return 0;
}

int32_t World::setMaterial(int32_t id, float friction, float restitution) {
    if (!isActive(id) || !isFinite(friction) || !isFinite(restitution) || friction < 0.0f || restitution < 0.0f) {
        return -1;
    }
    bodies[id].friction = friction;
    bodies[id].restitution = clampf(restitution, 0.0f, 1.0f);
    return 0;
}

int32_t World::setGravity(float gx, float gy, float gz) {
    if (!initialized || !isFinite(gx) || !isFinite(gy) || !isFinite(gz)) {
        return -1;
    }
    gravity = Vec3(gx, gy, gz);
    return 0;
}

int32_t World::step(float dt, int32_t velocityIterations, int32_t positionIterations) {
    if (!initialized) {
        return -1;
    }
    if (!isFinite(dt) || dt <= 0.0f) {
        return -1;
    }
    if (dt > kMaxDt) {
        dt = kMaxDt;
    }
    velocityIterations = velocityIterations < 0 ? 0 : (velocityIterations > kMaxIterations ? kMaxIterations : velocityIterations);
    positionIterations = positionIterations < 0 ? 0 : (positionIterations > kMaxIterations ? kMaxIterations : positionIterations);

    const Vec3 gravityStep = gravity * dt;

    for (int32_t i = 0; i < highWater; ++i) {
        if (bodies[i].state != kBodyDynamic) {
            continue;
        }

        velocities[i] += gravityStep;
        transforms[i].px += velocities[i].x * dt;
        transforms[i].py += velocities[i].y * dt;
        transforms[i].pz += velocities[i].z * dt;
        integrateRotation(transforms[i], angularVelocities[i], dt);
        bodies[i].rotation.fromQuaternion(transforms[i].qx, transforms[i].qy, transforms[i].qz, transforms[i].qw);
    }

    updateBounds(*this);
    contactCount = 0;
    for (int32_t i = 0; i < cacheNextUsedCount; ++i) {
        cacheNext[cacheNextUsed[i]].keyA = 0;
    }
    cacheNextUsedCount = 0;
    broadphase(*this);
    prepareContacts(*this);
    solveVelocities(*this, velocityIterations);
    solvePositions(*this, positionIterations);

    for (int32_t k = 0; k < contactCount; ++k) {
        const Contact& contact = contacts[k];
        if (contact.cacheSlot < 0) {
            continue;
        }
        CacheEntry& entry = cacheNext[contact.cacheSlot];
        entry.impN = contact.impN;
        entry.impT1 = contact.impT1;
        entry.impT2 = contact.impT2;
    }

    CacheEntry* previousTable = cachePrev;
    cachePrev = cacheNext;
    cacheNext = previousTable;

    int32_t* previousUsed = cachePrevUsed;
    cachePrevUsed = cacheNextUsed;
    cacheNextUsed = previousUsed;

    const int32_t previousUsedCount = cachePrevUsedCount;
    cachePrevUsedCount = cacheNextUsedCount;
    cacheNextUsedCount = previousUsedCount;

    writePackedContacts(*this);
    return contactCount;
}

int32_t World::bodyCount() const {
    return liveCount;
}

int32_t World::getContactCount() const {
    return contactCount;
}

int32_t World::cacheLookup(int32_t a, int32_t b, int32_t feature) const {
    uint32_t index = contactHash(a, b, feature) & cacheMask;
    for (int32_t probe = 0; probe < cacheCapacity; ++probe) {
        const CacheEntry& entry = cachePrev[index];
        if (entry.keyA == 0) {
            return -1;
        }
        if (entry.keyA == a + 1 && entry.keyB == b && entry.feature == feature) {
            return static_cast<int32_t>(index);
        }
        index = (index + 1) & cacheMask;
    }
    return -1;
}

int32_t World::cacheStore(int32_t a, int32_t b, int32_t feature, float impN, float impT1, float impT2) {
    if (cacheNextUsedCount >= maxContacts) {
        return -1;
    }
    uint32_t index = contactHash(a, b, feature) & cacheMask;
    for (int32_t probe = 0; probe < cacheCapacity; ++probe) {
        CacheEntry& entry = cacheNext[index];
        if (entry.keyA == 0) {
            entry.keyA = a + 1;
            entry.keyB = b;
            entry.feature = feature;
            entry.impN = impN;
            entry.impT1 = impT1;
            entry.impT2 = impT2;
            cacheNextUsed[cacheNextUsedCount++] = static_cast<int32_t>(index);
            return static_cast<int32_t>(index);
        }
        index = (index + 1) & cacheMask;
    }
    return -1;
}

uint64_t World::fingerprint() const {
    Hasher hasher;
    hasher.i32(highWater);
    hasher.i32(contactCount);

    for (int32_t i = 0; i < highWater; ++i) {
        const Body& body = bodies[i];
        hasher.i32(body.state);
        hasher.i32(body.shape);
        if (body.state == kBodyFree) {
            continue;
        }
        const Transform& transform = transforms[i];
        hasher.f32(transform.px);
        hasher.f32(transform.py);
        hasher.f32(transform.pz);
        hasher.f32(transform.qx);
        hasher.f32(transform.qy);
        hasher.f32(transform.qz);
        hasher.f32(transform.qw);
        hasher.vec3(velocities[i]);
        hasher.vec3(angularVelocities[i]);
    }

    for (int32_t i = 0; i < contactCount; ++i) {
        const PackedContact& contact = packedContacts[i];
        hasher.i32(contact.a);
        hasher.i32(contact.b);
        hasher.f32(contact.nx);
        hasher.f32(contact.ny);
        hasher.f32(contact.nz);
        hasher.f32(contact.pen);
    }

    return hasher.value;
}

}
