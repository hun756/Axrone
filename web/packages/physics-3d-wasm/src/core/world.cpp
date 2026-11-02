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

void zeroMass(Body& body) {
    body.invMass = 0.0f;
    body.invInertia = Vec3();
}

bool accumulateMass(const Body& body, float& mass, float& inertiaX, float& inertiaY, float& inertiaZ) {
    mass = 0.0f;
    inertiaX = 0.0f;
    inertiaY = 0.0f;
    inertiaZ = 0.0f;

    const int32_t slots = shapeSlots(body);
    for (int32_t slot = 0; slot < slots; ++slot) {
        const Shape& shape = body.shapes[slot];
        if (!shapeValid(shape)) {
            continue;
        }

        float shapeMass = 0.0f;
        float shapeInertiaX = 0.0f;
        float shapeInertiaY = 0.0f;
        float shapeInertiaZ = 0.0f;

        if (shape.kind == kShapeSphere) {
            const float radius = shape.extents.x;
            shapeMass = shape.density * kSphereVolume * radius * radius * radius;
            const float spin = kSphereInertia * shapeMass * radius * radius;
            shapeInertiaX = spin;
            shapeInertiaY = spin;
            shapeInertiaZ = spin;
        } else {
            const float hx = shape.extents.x;
            const float hy = shape.extents.y;
            const float hz = shape.extents.z;
            shapeMass = shape.density * kBoxVolume * hx * hy * hz;
            const float third = 1.0f / 3.0f;
            shapeInertiaX = shapeMass * (hy * hy + hz * hz) * third;
            shapeInertiaY = shapeMass * (hx * hx + hz * hz) * third;
            shapeInertiaZ = shapeMass * (hx * hx + hy * hy) * third;
        }

        const float dx = shape.offset.x;
        const float dy = shape.offset.y;
        const float dz = shape.offset.z;

        mass += shapeMass;
        inertiaX += shapeInertiaX + shapeMass * (dy * dy + dz * dz);
        inertiaY += shapeInertiaY + shapeMass * (dx * dx + dz * dz);
        inertiaZ += shapeInertiaZ + shapeMass * (dx * dx + dy * dy);
    }

    return isFinite(mass) && isFinite(inertiaX) && isFinite(inertiaY) && isFinite(inertiaZ);
}

bool recomputeMass(Body& body) {
    float mass = 0.0f;
    float inertiaX = 0.0f;
    float inertiaY = 0.0f;
    float inertiaZ = 0.0f;
    const bool finite = accumulateMass(body, mass, inertiaX, inertiaY, inertiaZ);

    if (body.state != kBodyDynamic || !finite) {
        zeroMass(body);
        return finite;
    }

    const bool overridden = body.customMass > 0.0f;
    if (mass <= 0.0f && !overridden) {
        zeroMass(body);
        return true;
    }

    const float effectiveMass = overridden ? body.customMass : mass;
    float inverse[3];
    inverse[0] = body.customInertia.x > 0.0f ? 1.0f / body.customInertia.x : (inertiaX > 0.0f ? 1.0f / inertiaX : 0.0f);
    inverse[1] = body.customInertia.y > 0.0f ? 1.0f / body.customInertia.y : (inertiaY > 0.0f ? 1.0f / inertiaY : 0.0f);
    inverse[2] = body.customInertia.z > 0.0f ? 1.0f / body.customInertia.z : (inertiaZ > 0.0f ? 1.0f / inertiaZ : 0.0f);

    const float invMass = effectiveMass > 0.0f ? 1.0f / effectiveMass : 0.0f;
    if (!isFinite(invMass) || !isFinite(inverse[0]) || !isFinite(inverse[1]) || !isFinite(inverse[2])) {
        zeroMass(body);
        return false;
    }

    body.invMass = invMass;
    body.invInertia = Vec3(inverse[0], inverse[1], inverse[2]);
    return true;
}

void addAngularResponse(World& world, Body& body, int32_t id, const Vec3& angularImpulse) {
    const Vec3 inertia = body.invInertia;
    if (inertia.x == 0.0f && inertia.y == 0.0f && inertia.z == 0.0f) {
        return;
    }

    Vec3 local = body.rotation.transposeTransform(angularImpulse);
    local.x *= inertia.x;
    local.y *= inertia.y;
    local.z *= inertia.z;
    world.angularVelocities[id] += body.rotation.transform(local);
}

int32_t shapeSignature(const Body& body) {
    const int32_t slots = shapeSlots(body);
    if (slots == 1) {
        return body.shapes[0].kind;
    }
    if (slots == 0) {
        return kShapeNone;
    }
    return kMaxShapesPerBody + slots;
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
    if (!initialized || (type != kBodyTypeStatic && type != kBodyTypeDynamic && type != kBodyTypeKinematic)) {
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
    body.state = type == kBodyTypeStatic ? kBodyStatic : (type == kBodyTypeDynamic ? kBodyDynamic : kBodyKinematic);
    body.shapeCount = 0;
    body.invMass = 0.0f;
    body.gravityScale = 1.0f;
    body.linearDamping = 0.0f;
    body.angularDamping = 0.0f;
    body.customMass = 0.0f;
    body.flags = 0;
    body.invInertia = Vec3();
    body.rotation.setIdentity();
    body.rotation.fromQuaternion(transform.qx, transform.qy, transform.qz, transform.qw);
    body.aabb = { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
    body.friction = kDefaultFriction;
    body.restitution = 0.0f;
    body.customInertia = Vec3();
    body.forceAccum = Vec3();
    body.torqueAccum = Vec3();

    ++liveCount;
    return id;
}

int32_t World::destroyBody(int32_t id) {
    if (!isActive(id)) {
        return -1;
    }

    Body& body = bodies[id];
    body.state = kBodyFree;
    body.shapeCount = 0;
    body.invMass = 0.0f;
    body.invInertia = Vec3();
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

    body.shapeCount = 1;
    body.shapes[0].setSphere(radius, density, Vec3());
    recomputeMass(body);
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

    body.shapeCount = 1;
    body.shapes[0].setBox(hx, hy, hz, density, Vec3(), 0.0f, 0.0f, 0.0f, 1.0f);
    recomputeMass(body);
    return 0;
}

int32_t World::addSphere(int32_t id, float radius, float density, float px, float py, float pz) {
    if (!isActive(id) || !isFinite(radius) || !isFinite(density) ||
        !isFinite(px) || !isFinite(py) || !isFinite(pz) ||
        radius <= 0.0f || density < 0.0f) {
        return -1;
    }

    Body& body = bodies[id];
    if (body.shapeCount >= kMaxShapesPerBody) {
        return -1;
    }

    const int32_t slot = body.shapeCount;
    body.shapes[slot].setSphere(radius, density, Vec3(px, py, pz));
    body.shapeCount = slot + 1;

    if (!recomputeMass(body)) {
        body.shapeCount = slot;
        recomputeMass(body);
        return -1;
    }
    return slot;
}

int32_t World::addBox(
    int32_t id,
    float hx,
    float hy,
    float hz,
    float density,
    float px,
    float py,
    float pz,
    float qx,
    float qy,
    float qz,
    float qw
) {
    if (!isActive(id) || !isFinite(hx) || !isFinite(hy) || !isFinite(hz) || !isFinite(density) ||
        !isFinite(px) || !isFinite(py) || !isFinite(pz) ||
        !isFinite(qx) || !isFinite(qy) || !isFinite(qz) || !isFinite(qw) ||
        hx <= 0.0f || hy <= 0.0f || hz <= 0.0f || density < 0.0f) {
        return -1;
    }

    Body& body = bodies[id];
    if (body.shapeCount >= kMaxShapesPerBody) {
        return -1;
    }

    Transform local = { 0.0f, 0.0f, 0.0f, qx, qy, qz, qw };
    normalizeQuaternion(local);

    const int32_t slot = body.shapeCount;
    body.shapes[slot].setBox(hx, hy, hz, density, Vec3(px, py, pz), local.qx, local.qy, local.qz, local.qw);
    body.shapeCount = slot + 1;

    if (!recomputeMass(body)) {
        body.shapeCount = slot;
        recomputeMass(body);
        return -1;
    }
    return slot;
}

int32_t World::clearShapes(int32_t id) {
    if (!isActive(id)) {
        return -1;
    }

    Body& body = bodies[id];
    body.shapeCount = 0;
    body.aabb = { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
    recomputeMass(body);
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

int32_t World::setDamping(int32_t id, float linear, float angular) {
    if (!isActive(id) || !isFinite(linear) || !isFinite(angular) || linear < 0.0f || angular < 0.0f) {
        return -1;
    }
    bodies[id].linearDamping = linear;
    bodies[id].angularDamping = angular;
    return 0;
}

int32_t World::setGravityScale(int32_t id, float scale) {
    if (!isActive(id) || !isFinite(scale)) {
        return -1;
    }
    bodies[id].gravityScale = scale;
    return 0;
}

int32_t World::setFlags(int32_t id, int32_t setMask, int32_t clearMask) {
    if (!isActive(id) || setMask < 0 || clearMask < 0) {
        return -1;
    }
    Body& body = bodies[id];
    body.flags = (body.flags | setMask) & ~clearMask;
    return 0;
}

int32_t World::applyForce(int32_t id, float fx, float fy, float fz, float px, float py, float pz) {
    if (!isActive(id)) {
        return -1;
    }
    Body& body = bodies[id];
    if (body.state != kBodyDynamic) {
        return -1;
    }
    if (!isFinite(fx) || !isFinite(fy) || !isFinite(fz) ||
        !isFinite(px) || !isFinite(py) || !isFinite(pz)) {
        return -1;
    }
    const Vec3 force(fx, fy, fz);
    body.forceAccum += force;
    body.torqueAccum += cross(Vec3(px, py, pz), force);
    return 0;
}

int32_t World::applyImpulse(int32_t id, float ix, float iy, float iz, float px, float py, float pz) {
    if (!isActive(id)) {
        return -1;
    }
    Body& body = bodies[id];
    if (body.state != kBodyDynamic) {
        return -1;
    }
    if (!isFinite(ix) || !isFinite(iy) || !isFinite(iz) ||
        !isFinite(px) || !isFinite(py) || !isFinite(pz)) {
        return -1;
    }
    const Vec3 impulse(ix, iy, iz);
    velocities[id] += impulse * body.invMass;
    addAngularResponse(*this, body, id, cross(Vec3(px, py, pz), impulse));
    return 0;
}

int32_t World::applyTorque(int32_t id, float tx, float ty, float tz) {
    if (!isActive(id)) {
        return -1;
    }
    Body& body = bodies[id];
    if (body.state != kBodyDynamic) {
        return -1;
    }
    if (!isFinite(tx) || !isFinite(ty) || !isFinite(tz)) {
        return -1;
    }
    body.torqueAccum += Vec3(tx, ty, tz);
    return 0;
}

int32_t World::applyAngularImpulse(int32_t id, float ix, float iy, float iz) {
    if (!isActive(id)) {
        return -1;
    }
    Body& body = bodies[id];
    if (body.state != kBodyDynamic) {
        return -1;
    }
    if (!isFinite(ix) || !isFinite(iy) || !isFinite(iz)) {
        return -1;
    }
    addAngularResponse(*this, body, id, Vec3(ix, iy, iz));
    return 0;
}

int32_t World::setMassData(int32_t id, float mass, float ix, float iy, float iz) {
    if (!isActive(id) || !isFinite(mass) || !isFinite(ix) || !isFinite(iy) || !isFinite(iz)) {
        return -1;
    }

    Body& body = bodies[id];
    if (body.state != kBodyDynamic) {
        return 0;
    }

    if (mass <= 0.0f) {
        body.customMass = 0.0f;
        body.customInertia = Vec3();
        recomputeMass(body);
        return 0;
    }

    if (!isFinite(1.0f / mass)) {
        return -1;
    }

    const Vec3 requested(ix > 0.0f ? ix : 0.0f, iy > 0.0f ? iy : 0.0f, iz > 0.0f ? iz : 0.0f);
    if ((requested.x > 0.0f && !isFinite(1.0f / requested.x)) ||
        (requested.y > 0.0f && !isFinite(1.0f / requested.y)) ||
        (requested.z > 0.0f && !isFinite(1.0f / requested.z))) {
        return -1;
    }

    const float previousMass = body.customMass;
    const Vec3 previousInertia = body.customInertia;
    body.customMass = mass;
    body.customInertia = requested;
    if (!recomputeMass(body)) {
        body.customMass = previousMass;
        body.customInertia = previousInertia;
        recomputeMass(body);
        return -1;
    }
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
        Body& body = bodies[i];
        const int32_t state = body.state;
        if (state != kBodyDynamic && state != kBodyKinematic) {
            continue;
        }

        Vec3& velocity = velocities[i];
        Vec3& angularVelocity = angularVelocities[i];

        if (state == kBodyDynamic) {
            if (body.forceAccum.x != 0.0f || body.forceAccum.y != 0.0f || body.forceAccum.z != 0.0f) {
                velocity += (body.forceAccum * dt) * body.invMass;
            }
            if (body.torqueAccum.x != 0.0f || body.torqueAccum.y != 0.0f || body.torqueAccum.z != 0.0f) {
                addAngularResponse(*this, body, i, body.torqueAccum * dt);
            }
            body.forceAccum = Vec3();
            body.torqueAccum = Vec3();
            velocity += gravityStep * body.gravityScale;
        }

        float linearFactor = 1.0f - body.linearDamping * dt;
        if (linearFactor < 0.0f) {
            linearFactor = 0.0f;
        }
        velocity *= linearFactor;

        float angularFactor = 1.0f - body.angularDamping * dt;
        if (angularFactor < 0.0f) {
            angularFactor = 0.0f;
        }
        angularVelocity *= angularFactor;

        const int32_t flags = body.flags;
        if ((flags & kBodyFreezePositionX) != 0) {
            velocity.x = 0.0f;
        }
        if ((flags & kBodyFreezePositionY) != 0) {
            velocity.y = 0.0f;
        }
        if ((flags & kBodyFreezePositionZ) != 0) {
            velocity.z = 0.0f;
        }
        if ((flags & kBodyFreezeRotationX) != 0) {
            angularVelocity.x = 0.0f;
        }
        if ((flags & kBodyFreezeRotationY) != 0) {
            angularVelocity.y = 0.0f;
        }
        if ((flags & kBodyFreezeRotationZ) != 0) {
            angularVelocity.z = 0.0f;
        }

        Transform& transform = transforms[i];
        if ((flags & kBodyFreezePositionX) == 0) {
            transform.px += velocity.x * dt;
        }
        if ((flags & kBodyFreezePositionY) == 0) {
            transform.py += velocity.y * dt;
        }
        if ((flags & kBodyFreezePositionZ) == 0) {
            transform.pz += velocity.z * dt;
        }
        integrateRotation(transform, angularVelocity, dt);
        body.rotation.fromQuaternion(transform.qx, transform.qy, transform.qz, transform.qw);
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
        hasher.i32(shapeSignature(body));
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
