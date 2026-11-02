#include <bit>
#include <stdint.h>
#include <stdio.h>

#include "core/query.h"

using axrone::physics::Body;
using axrone::physics::BodyState;
using axrone::physics::kBodyDynamic;
using axrone::physics::kBodyFree;
using axrone::physics::kBodyStatic;
using axrone::physics::kShapeBox;
using axrone::physics::kShapeNone;
using axrone::physics::kShapeSphere;
using axrone::physics::Mat3;
using axrone::physics::overlapSphere;
using axrone::physics::raycastAll;
using axrone::physics::raycastClosest;
using axrone::physics::RayHit;
using axrone::physics::ShapeKind;
using axrone::physics::Transform;
using axrone::physics::Vec3;
using axrone::physics::World;

namespace {

int gChecks = 0;
int gFailures = 0;

#define CHECK(cond)                                                              \
    do {                                                                         \
        ++gChecks;                                                               \
        if (!(cond)) {                                                           \
            ++gFailures;                                                         \
            printf("FAIL %s:%d: %s\n", __FILE__, __LINE__, #cond);              \
        }                                                                        \
    } while (0)

bool near(float left, float right) {
    float delta = left - right;
    if (delta < 0.0f) {
        delta = -delta;
    }
    return delta < 1e-4f;
}

struct Fixture {
    World world;
    Body bodies[16];
    Transform transforms[16];

    Fixture() : world(), bodies(), transforms() {
        world.initialized = true;
        world.bodies = bodies;
        world.transforms = transforms;
        world.highWater = 0;
    }

    int32_t addSphere(Vec3 position, float radius, int32_t state = kBodyStatic) {
        const int32_t id = world.highWater++;
        Body& body = bodies[id];
        body.state = state;
        body.shape = kShapeSphere;
        body.extents = Vec3(radius, 0.0f, 0.0f);
        body.rotation.setIdentity();
        Transform& transform = transforms[id];
        transform.px = position.x;
        transform.py = position.y;
        transform.pz = position.z;
        transform.qx = 0.0f;
        transform.qy = 0.0f;
        transform.qz = 0.0f;
        transform.qw = 1.0f;
        return id;
    }

    int32_t addBox(
        Vec3 position,
        Vec3 half,
        float qx = 0.0f,
        float qy = 0.0f,
        float qz = 0.0f,
        float qw = 1.0f,
        int32_t state = kBodyStatic) {
        const int32_t id = world.highWater++;
        Body& body = bodies[id];
        body.state = state;
        body.shape = kShapeBox;
        body.extents = half;
        body.rotation.fromQuaternion(qx, qy, qz, qw);
        Transform& transform = transforms[id];
        transform.px = position.x;
        transform.py = position.y;
        transform.pz = position.z;
        transform.qx = qx;
        transform.qy = qy;
        transform.qz = qz;
        transform.qw = qw;
        return id;
    }
};

uint32_t mix(uint32_t hash, uint32_t value) {
    hash ^= value;
    hash *= 16777619u;
    return hash;
}

uint32_t floatBits(float value) {
    return std::bit_cast<uint32_t>(value);
}

uint32_t checksum(const Fixture& fixture) {
    uint32_t hash = 2166136261u;
    hash = mix(hash, static_cast<uint32_t>(fixture.world.highWater));
    for (int32_t id = 0; id < fixture.world.highWater; ++id) {
        const Body& body = fixture.world.bodies[id];
        const Transform& transform = fixture.world.transforms[id];
        hash = mix(hash, static_cast<uint32_t>(body.state));
        hash = mix(hash, static_cast<uint32_t>(body.shape));
        hash = mix(hash, floatBits(body.invMass));
        hash = mix(hash, floatBits(body.invInertia.x));
        hash = mix(hash, floatBits(body.invInertia.y));
        hash = mix(hash, floatBits(body.invInertia.z));
        hash = mix(hash, floatBits(body.extents.x));
        hash = mix(hash, floatBits(body.extents.y));
        hash = mix(hash, floatBits(body.extents.z));
        hash = mix(hash, floatBits(body.friction));
        hash = mix(hash, floatBits(body.restitution));
        for (int32_t i = 0; i < 9; ++i) {
            hash = mix(hash, floatBits(body.rotation.m[i]));
        }
        hash = mix(hash, floatBits(body.aabb.minX));
        hash = mix(hash, floatBits(body.aabb.minY));
        hash = mix(hash, floatBits(body.aabb.minZ));
        hash = mix(hash, floatBits(body.aabb.maxX));
        hash = mix(hash, floatBits(body.aabb.maxY));
        hash = mix(hash, floatBits(body.aabb.maxZ));
        hash = mix(hash, floatBits(transform.px));
        hash = mix(hash, floatBits(transform.py));
        hash = mix(hash, floatBits(transform.pz));
        hash = mix(hash, floatBits(transform.qx));
        hash = mix(hash, floatBits(transform.qy));
        hash = mix(hash, floatBits(transform.qz));
        hash = mix(hash, floatBits(transform.qw));
    }
    return hash;
}

void testRayHitsSphereFromOutside() {
    Fixture fixture;
    fixture.addSphere(Vec3(0.0f, 0.0f, -5.0f), 1.0f);
    RayHit hits[4];
    const int32_t count = raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, hits, 4);
    CHECK(count == 1);
    if (count == 1) {
        CHECK(hits[0].body == 0);
        CHECK(near(hits[0].t, 4.0f));
        CHECK(near(hits[0].px, 0.0f));
        CHECK(near(hits[0].py, 0.0f));
        CHECK(near(hits[0].pz, -4.0f));
        CHECK(near(hits[0].nx, 0.0f));
        CHECK(near(hits[0].ny, 0.0f));
        CHECK(near(hits[0].nz, 1.0f));
    }
    RayHit closest;
    CHECK(raycastClosest(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, closest));
    CHECK(closest.body == 0);
    CHECK(near(closest.t, 4.0f));
}

void testRayMissesSphere() {
    Fixture fixture;
    fixture.addSphere(Vec3(4.0f, 0.0f, -5.0f), 1.0f);
    RayHit hits[4];
    CHECK(raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, hits, 4) == 0);
    RayHit closest;
    CHECK(!raycastClosest(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, closest));
}

void testRayBehindSphereMisses() {
    Fixture fixture;
    fixture.addSphere(Vec3(0.0f, 0.0f, 5.0f), 1.0f);
    RayHit hits[4];
    CHECK(raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, hits, 4) == 0);
}

void testRayInsideSphere() {
    Fixture fixture;
    fixture.addSphere(Vec3(0.0f, 0.0f, -5.0f), 2.0f);
    RayHit hits[4];
    const int32_t count = raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, -5.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, hits, 4);
    CHECK(count == 1);
    if (count == 1) {
        CHECK(near(hits[0].t, 0.0f));
        CHECK(near(hits[0].px, 0.0f));
        CHECK(near(hits[0].py, 0.0f));
        CHECK(near(hits[0].pz, -5.0f));
        CHECK(near(hits[0].nx, 0.0f));
        CHECK(near(hits[0].ny, 0.0f));
        CHECK(near(hits[0].nz, 1.0f));
    }
}

void testRayHitsAxisAlignedBox() {
    Fixture fixture;
    fixture.addBox(Vec3(0.0f, 0.0f, -5.0f), Vec3(1.0f, 1.0f, 1.0f));
    RayHit hits[4];
    const int32_t count = raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, hits, 4);
    CHECK(count == 1);
    if (count == 1) {
        CHECK(near(hits[0].t, 4.0f));
        CHECK(near(hits[0].pz, -4.0f));
        CHECK(near(hits[0].nx, 0.0f));
        CHECK(near(hits[0].ny, 0.0f));
        CHECK(near(hits[0].nz, 1.0f));
    }
}

void testRayHitsRotatedBox() {
    Fixture fixture;
    const float yawHalf = 0.3826834323650898f;
    const float yawCos = 0.9238795325112867f;
    fixture.addBox(Vec3(0.0f, 0.0f, -5.0f), Vec3(1.0f, 1.0f, 0.5f), 0.0f, yawHalf, 0.0f, yawCos);
    RayHit hits[4];
    const int32_t count = raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, hits, 4);
    CHECK(count == 1);
    if (count == 1) {
        CHECK(near(hits[0].t, 4.2928932f));
        CHECK(near(hits[0].px, 0.0f));
        CHECK(near(hits[0].pz, -4.2928932f));
        CHECK(near(hits[0].nx, 0.7071068f));
        CHECK(near(hits[0].ny, 0.0f));
        CHECK(near(hits[0].nz, 0.7071068f));
    }
}

void testRayInsideBox() {
    Fixture fixture;
    fixture.addBox(Vec3(0.0f, 0.0f, -5.0f), Vec3(1.0f, 1.0f, 1.0f));
    RayHit hits[4];
    const int32_t count = raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, -5.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, hits, 4);
    CHECK(count == 1);
    if (count == 1) {
        CHECK(near(hits[0].t, 0.0f));
        CHECK(near(hits[0].pz, -5.0f));
        CHECK(near(hits[0].nx, 0.0f));
        CHECK(near(hits[0].ny, 0.0f));
        CHECK(near(hits[0].nz, 1.0f));
    }
}

void testRayMissesBox() {
    Fixture fixture;
    fixture.addBox(Vec3(3.0f, 0.0f, -5.0f), Vec3(1.0f, 1.0f, 1.0f));
    RayHit hits[4];
    CHECK(raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, hits, 4) == 0);
    RayHit closest;
    CHECK(!raycastClosest(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, closest));
}

void testMaxDistanceCutoff() {
    Fixture fixture;
    fixture.addSphere(Vec3(0.0f, 0.0f, -10.0f), 1.0f);
    fixture.addBox(Vec3(0.0f, 0.0f, -10.0f), Vec3(1.0f, 1.0f, 1.0f));
    RayHit hits[4];
    CHECK(raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 5.0f, hits, 4) == 0);
    CHECK(raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 9.0f, hits, 4) == 2);
    RayHit closest;
    CHECK(!raycastClosest(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 5.0f, closest));
    CHECK(!raycastClosest(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 0.0f, closest));
    CHECK(raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), -1.0f, hits, 4) == 0);
}

void testUnnormalizedDirection() {
    Fixture fixture;
    fixture.addSphere(Vec3(0.0f, 0.0f, -5.0f), 1.0f);
    RayHit hits[4];
    const int32_t count = raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -200.0f), 100.0f, hits, 4);
    CHECK(count == 1);
    if (count == 1) {
        CHECK(near(hits[0].t, 4.0f));
        CHECK(near(hits[0].pz, -4.0f));
    }
    RayHit closest;
    CHECK(raycastClosest(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -0.5f), 100.0f, closest));
    CHECK(near(closest.t, 4.0f));
}

void testZeroDirection() {
    Fixture fixture;
    fixture.addSphere(Vec3(0.0f, 0.0f, -5.0f), 1.0f);
    RayHit hits[4];
    CHECK(raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, 0.0f), 100.0f, hits, 4) == 0);
    RayHit closest;
    CHECK(!raycastClosest(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(1e-7f, 0.0f, 0.0f), 100.0f, closest));
}

void testCapacityTruncationKeepsClosest() {
    Fixture fixture;
    fixture.addSphere(Vec3(0.0f, 0.0f, -3.0f), 1.0f);
    fixture.addSphere(Vec3(0.0f, 0.0f, -7.0f), 1.0f);
    fixture.addSphere(Vec3(0.0f, 0.0f, -11.0f), 1.0f);
    RayHit hits[2];
    const int32_t count = raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, hits, 2);
    CHECK(count == 2);
    if (count == 2) {
        CHECK(hits[0].body == 0);
        CHECK(hits[1].body == 1);
        CHECK(near(hits[0].t, 2.0f));
        CHECK(near(hits[1].t, 6.0f));
    }
    RayHit single[1];
    const int32_t one = raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, single, 1);
    CHECK(one == 1);
    if (one == 1) {
        CHECK(single[0].body == 0);
    }
}

void testHitOrderingByT() {
    Fixture fixture;
    fixture.addSphere(Vec3(0.0f, 0.0f, -9.0f), 1.0f);
    fixture.addSphere(Vec3(0.0f, 0.0f, -3.0f), 1.0f);
    fixture.addSphere(Vec3(0.0f, 0.0f, -6.0f), 1.0f);
    RayHit hits[8];
    const int32_t count = raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, hits, 8);
    CHECK(count == 3);
    if (count == 3) {
        CHECK(hits[0].body == 1);
        CHECK(hits[1].body == 2);
        CHECK(hits[2].body == 0);
        CHECK(hits[0].t <= hits[1].t);
        CHECK(hits[1].t <= hits[2].t);
    }
}

void testTieBreakByBodyId() {
    Fixture fixture;
    fixture.addSphere(Vec3(2.0f, 0.0f, -5.0f), 3.0f);
    fixture.addSphere(Vec3(-2.0f, 0.0f, -5.0f), 3.0f);
    RayHit hits[4];
    const int32_t count = raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, hits, 4);
    CHECK(count == 2);
    if (count == 2) {
        CHECK(hits[0].t == hits[1].t);
        CHECK(hits[0].body == 0);
        CHECK(hits[1].body == 1);
    }
}

void testRaycastClosestPicksMinimum() {
    Fixture fixture;
    fixture.addSphere(Vec3(0.0f, 0.0f, -9.0f), 1.0f);
    fixture.addBox(Vec3(0.0f, 0.0f, -4.0f), Vec3(0.5f, 0.5f, 0.5f));
    fixture.addSphere(Vec3(0.0f, 0.0f, -7.0f), 1.0f);
    RayHit closest;
    CHECK(raycastClosest(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, closest));
    CHECK(closest.body == 1);
    CHECK(near(closest.t, 3.5f));
}

void testInvalidArguments() {
    Fixture fixture;
    fixture.addSphere(Vec3(0.0f, 0.0f, -5.0f), 1.0f);
    RayHit hits[4];
    CHECK(raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, nullptr, 4) == 0);
    CHECK(raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, hits, 0) == 0);
    CHECK(raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, hits, -3) == 0);
    int32_t bodies[4];
    CHECK(overlapSphere(fixture.world, Vec3(0.0f, 0.0f, -5.0f), 1.0f, nullptr, 4) == 0);
    CHECK(overlapSphere(fixture.world, Vec3(0.0f, 0.0f, -5.0f), 1.0f, bodies, 0) == 0);
    Fixture uninitialized;
    uninitialized.world.initialized = false;
    CHECK(raycastAll(
        uninitialized.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, hits, 4) == 0);
    RayHit closest;
    CHECK(!raycastClosest(
        uninitialized.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, closest));
}

void testSkipsUnusableBodies() {
    Fixture fixture;
    const int32_t freeId = fixture.addSphere(Vec3(0.0f, 0.0f, -5.0f), 1.0f);
    fixture.bodies[freeId].state = kBodyFree;
    const int32_t noneId = fixture.addSphere(Vec3(0.0f, 0.0f, -5.0f), 1.0f);
    fixture.bodies[noneId].shape = kShapeNone;
    const int32_t flatId = fixture.addSphere(Vec3(0.0f, 0.0f, -5.0f), 0.0f);
    fixture.bodies[flatId].extents = Vec3(0.0f, 0.0f, 0.0f);
    const int32_t degenerateId = fixture.addBox(Vec3(0.0f, 0.0f, -5.0f), Vec3(1.0f, 0.0f, 1.0f));
    CHECK(flatId == 2);
    CHECK(degenerateId == 3);
    RayHit hits[4];
    CHECK(raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, hits, 4) == 0);
    int32_t bodies[4];
    CHECK(overlapSphere(fixture.world, Vec3(0.0f, 0.0f, -5.0f), 0.5f, bodies, 4) == 0);
    fixture.bodies[freeId].state = kBodyDynamic;
    CHECK(raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.0f, 0.0f, -1.0f), 100.0f, hits, 4) == 1);
}

void testOverlapSphereWithSphere() {
    Fixture fixture;
    fixture.addSphere(Vec3(0.0f, 0.0f, 0.0f), 1.0f);
    fixture.addSphere(Vec3(5.0f, 0.0f, 0.0f), 1.0f);
    fixture.addSphere(Vec3(2.4f, 0.0f, 0.0f), 1.0f);
    int32_t bodies[4];
    const int32_t count = overlapSphere(fixture.world, Vec3(1.4f, 0.0f, 0.0f), 0.5f, bodies, 4);
    CHECK(count == 2);
    if (count == 2) {
        CHECK(bodies[0] == 0);
        CHECK(bodies[1] == 2);
    }
}

void testOverlapSphereWithBox() {
    Fixture fixture;
    fixture.addBox(Vec3(0.0f, 0.0f, 0.0f), Vec3(1.0f, 1.0f, 1.0f));
    int32_t bodies[4];
    CHECK(overlapSphere(fixture.world, Vec3(2.5f, 0.0f, 0.0f), 1.5f, bodies, 4) == 1);
    CHECK(overlapSphere(fixture.world, Vec3(2.5f, 0.0f, 0.0f), 1.4f, bodies, 4) == 0);
    CHECK(overlapSphere(fixture.world, Vec3(1.9f, 1.9f, 1.9f), 1.6f, bodies, 4) == 1);
    CHECK(overlapSphere(fixture.world, Vec3(0.0f, 0.0f, 0.0f), 0.1f, bodies, 4) == 1);
    const float yawHalf = 0.3826834323650898f;
    const float yawCos = 0.9238795325112867f;
    Fixture rotated;
    rotated.addBox(Vec3(0.0f, 0.0f, 0.0f), Vec3(1.0f, 1.0f, 1.0f), 0.0f, yawHalf, 0.0f, yawCos);
    const float edge = 1.4142136f;
    CHECK(overlapSphere(rotated.world, Vec3(edge, 0.0f, 0.0f), 0.2f, bodies, 4) == 1);
    CHECK(overlapSphere(rotated.world, Vec3(edge + 0.3f, 0.0f, 0.0f), 0.2f, bodies, 4) == 0);
}

void testOverlapDisjointAndOrdering() {
    Fixture fixture;
    fixture.addSphere(Vec3(-8.0f, 0.0f, 0.0f), 1.0f);
    fixture.addBox(Vec3(0.0f, 0.0f, 0.0f), Vec3(1.0f, 1.0f, 1.0f));
    fixture.addSphere(Vec3(8.0f, 0.0f, 0.0f), 1.0f);
    fixture.addSphere(Vec3(3.0f, 0.0f, 0.0f), 2.0f);
    int32_t bodies[8];
    const int32_t count = overlapSphere(fixture.world, Vec3(2.0f, 0.0f, 0.0f), 1.0f, bodies, 8);
    CHECK(count == 2);
    if (count == 2) {
        CHECK(bodies[0] == 1);
        CHECK(bodies[1] == 3);
    }
    const int32_t all = overlapSphere(fixture.world, Vec3(0.0f, 0.0f, 0.0f), 20.0f, bodies, 8);
    CHECK(all == 4);
    if (all == 4) {
        CHECK(bodies[0] == 0);
        CHECK(bodies[1] == 1);
        CHECK(bodies[2] == 2);
        CHECK(bodies[3] == 3);
    }
    const int32_t truncated = overlapSphere(fixture.world, Vec3(0.0f, 0.0f, 0.0f), 20.0f, bodies, 2);
    CHECK(truncated == 2);
    if (truncated == 2) {
        CHECK(bodies[0] == 0);
        CHECK(bodies[1] == 1);
    }
    CHECK(overlapSphere(fixture.world, Vec3(100.0f, 100.0f, 100.0f), 1.0f, bodies, 8) == 0);
}

void testNoMutationOfWorld() {
    Fixture fixture;
    fixture.addSphere(Vec3(0.0f, 1.0f, -3.0f), 1.5f);
    const float yawHalf = 0.3826834323650898f;
    const float yawCos = 0.9238795325112867f;
    fixture.addBox(Vec3(-2.0f, 0.0f, -6.0f), Vec3(1.0f, 2.0f, 0.5f), 0.0f, yawHalf, 0.0f, yawCos);
    const uint32_t before = checksum(fixture);
    RayHit hits[8];
    RayHit closest;
    int32_t bodies[8];
    for (int32_t i = 0; i < 8; ++i) {
        hits[i].body = -777;
        hits[i].t = -777.0f;
        bodies[i] = -777;
    }
    const int32_t all = raycastAll(
        fixture.world, Vec3(0.0f, 0.0f, 0.0f), Vec3(0.5f, -0.25f, -3.0f), 50.0f, hits, 8);
    CHECK(all >= 0);
    raycastClosest(
        fixture.world, Vec3(1.0f, 2.0f, 3.0f), Vec3(-0.75f, 0.25f, -4.5f), 50.0f, closest);
    overlapSphere(fixture.world, Vec3(0.0f, 0.0f, -4.0f), 3.0f, bodies, 8);
    const uint32_t after = checksum(fixture);
    CHECK(before == after);
}

}

int main() {
    testRayHitsSphereFromOutside();
    testRayMissesSphere();
    testRayBehindSphereMisses();
    testRayInsideSphere();
    testRayHitsAxisAlignedBox();
    testRayHitsRotatedBox();
    testRayInsideBox();
    testRayMissesBox();
    testMaxDistanceCutoff();
    testUnnormalizedDirection();
    testZeroDirection();
    testCapacityTruncationKeepsClosest();
    testHitOrderingByT();
    testTieBreakByBodyId();
    testRaycastClosestPicksMinimum();
    testInvalidArguments();
    testSkipsUnusableBodies();
    testOverlapSphereWithSphere();
    testOverlapSphereWithBox();
    testOverlapDisjointAndOrdering();
    testNoMutationOfWorld();

    printf("query_test: %d checks, %d failures\n", gChecks, gFailures);
    return gFailures == 0 ? 0 : 1;
}
