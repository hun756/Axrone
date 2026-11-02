#include <cstdio>

#include "core/world.h"
#include "runtime/arena.h"
#include "scenario.h"

using namespace axrone::physics;

namespace {

alignas(64) unsigned char g_memoryA[64 * 1024 * 1024];
alignas(64) unsigned char g_memoryB[64 * 1024 * 1024];

int g_failures = 0;

#define CHECK(condition) \
    do { \
        if (!(condition)) { \
            std::printf("FAIL %s:%d %s\n", __FILE__, __LINE__, #condition); \
            ++g_failures; \
        } \
    } while (0)

bool near(float value, float expected, float tolerance) {
    const float difference = value - expected;
    return difference <= tolerance && difference >= -tolerance;
}

bool finiteVector(const Vec3& value) {
    return isFinite(value.x) && isFinite(value.y) && isFinite(value.z);
}

bool initWorld(World& world, Arena& arena, unsigned char* memory, int32_t bodies, int32_t contacts) {
    arena.init(memory, 64 * 1024 * 1024);
    return world.init(arena, bodies, contacts, Vec3(0.0f, -9.81f, 0.0f));
}

void testInitValidation() {
    Arena arena;
    World world;
    arena.init(g_memoryA, sizeof(g_memoryA));

    CHECK(!world.init(arena, 0, 16, Vec3(0.0f, -9.81f, 0.0f)));
    CHECK(!world.init(arena, 16, 0, Vec3(0.0f, -9.81f, 0.0f)));
    CHECK(!world.init(arena, -1, 16, Vec3(0.0f, -9.81f, 0.0f)));

    arena.reset();
    CHECK(world.init(arena, 16, 256, Vec3(0.0f, -9.81f, 0.0f)));
    CHECK(world.bodyCount() == 0);
    CHECK(world.getContactCount() == 0);
}

void testSphereRest() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 64, 2048));

    const int32_t ground = world.createBody(kBodyTypeStatic, 0.0f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(ground >= 0);
    CHECK(world.setBox(ground, 10.0f, 0.5f, 10.0f, 0.0f) == 0);

    const int32_t sphere = world.createBody(kBodyTypeDynamic, 0.0f, 2.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(sphere >= 0);
    CHECK(world.setSphere(sphere, 0.5f, 1.0f) == 0);
    CHECK(world.setMaterial(sphere, 0.6f, 0.0f) == 0);

    for (int32_t i = 0; i < 180; ++i) {
        world.step(1.0f / 60.0f, 8, 3);
    }

    const Transform& transform = world.transforms[sphere];
    CHECK(isFinite(transform.px) && isFinite(transform.py) && isFinite(transform.pz));
    CHECK(near(transform.py, 0.5f, 0.08f));
    CHECK(fabsf(world.velocities[sphere].y) < 0.25f);
    CHECK(world.getContactCount() >= 1);
}

void testBoxStack() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 64, 2048));

    const int32_t ground = world.createBody(kBodyTypeStatic, 0.0f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(ground >= 0);
    CHECK(world.setBox(ground, 10.0f, 0.5f, 10.0f, 0.0f) == 0);

    int32_t boxes[3];
    for (int32_t i = 0; i < 3; ++i) {
        boxes[i] = world.createBody(kBodyTypeDynamic, 0.0f, 0.5f + static_cast<float>(i), 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
        CHECK(boxes[i] >= 0);
        CHECK(world.setBox(boxes[i], 0.5f, 0.5f, 0.5f, 1.0f) == 0);
        CHECK(world.setMaterial(boxes[i], 0.7f, 0.0f) == 0);
    }

    for (int32_t i = 0; i < 240; ++i) {
        world.step(1.0f / 60.0f, 8, 3);
    }

    for (int32_t i = 0; i < 3; ++i) {
        const Transform& transform = world.transforms[boxes[i]];
        CHECK(isFinite(transform.px) && isFinite(transform.py) && isFinite(transform.pz));
        CHECK(transform.py > 0.2f && transform.py < 5.0f);
    }
    CHECK(world.getContactCount() >= 1);
}

void testDeterminism() {
    Arena arenaA;
    Arena arenaB;
    World worldA;
    World worldB;
    CHECK(initWorld(worldA, arenaA, g_memoryA, 512, 16384));
    CHECK(initWorld(worldB, arenaB, g_memoryB, 512, 16384));

    addStandardBodies(worldA);
    addStandardBodies(worldB);
    runStandardScene(worldA);
    runStandardScene(worldB);

    CHECK(worldA.getContactCount() == worldB.getContactCount());
    CHECK(worldA.fingerprint() == worldB.fingerprint());

    const uint64_t first = worldA.fingerprint();
    worldA.step(kStandardDt, 8, 3);
    CHECK(worldA.fingerprint() != first);
}

void testDestroyAndReuse() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 8, 64));

    const int32_t first = world.createBody(kBodyTypeDynamic, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    const int32_t second = world.createBody(kBodyTypeDynamic, 1.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    const int32_t third = world.createBody(kBodyTypeDynamic, 2.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(first >= 0 && second >= 0 && third >= 0);
    CHECK(world.setBox(first, 0.5f, 0.5f, 0.5f, 1.0f) == 0);
    CHECK(world.setBox(second, 0.5f, 0.5f, 0.5f, 1.0f) == 0);
    CHECK(world.setBox(third, 0.5f, 0.5f, 0.5f, 1.0f) == 0);

    CHECK(world.destroyBody(second) == 0);
    CHECK(world.bodyCount() == 2);

    const int32_t reused = world.createBody(kBodyTypeDynamic, 3.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(reused == second);
    CHECK(world.bodyCount() == 3);
    CHECK(world.setBox(reused, 0.5f, 0.5f, 0.5f, 1.0f) == 0);
    CHECK(world.step(1.0f / 60.0f, 4, 2) >= 0);
}

void testContactManifolds() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t boxA = world.createBody(kBodyTypeStatic, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    const int32_t boxB = world.createBody(kBodyTypeDynamic, 0.6f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(world.setBox(boxA, 0.5f, 0.5f, 0.5f, 0.0f) == 0);
    CHECK(world.setBox(boxB, 0.5f, 0.5f, 0.5f, 1.0f) == 0);
    CHECK(world.step(1.0f / 60.0f, 4, 2) >= 0);
    CHECK(world.getContactCount() >= 1);

    const int32_t sphere = world.createBody(kBodyTypeDynamic, 0.4f, 0.4f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(world.setSphere(sphere, 0.5f, 1.0f) == 0);
    CHECK(world.step(1.0f / 60.0f, 4, 2) >= 0);
    CHECK(world.getContactCount() >= 1);

    for (int32_t i = 0; i < world.getContactCount(); ++i) {
        const PackedContact& contact = world.packedContacts[i];
        CHECK(contact.a >= 0 && contact.a < world.highWater);
        CHECK(contact.b >= 0 && contact.b < world.highWater);
        CHECK(isFinite(contact.nx) && isFinite(contact.ny) && isFinite(contact.nz));
        CHECK(isFinite(contact.pen));
    }
}

void testInvalidInputs() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 2, 32));

    CHECK(world.createBody(99, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f) == -1);
    CHECK(world.createBody(kBodyTypeDynamic, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f) >= 0);

    const int32_t id = world.createBody(kBodyTypeDynamic, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(id >= 0);
    CHECK(world.createBody(kBodyTypeDynamic, 0.0f, 2.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f) == -1);

    CHECK(world.setSphere(id, -1.0f, 1.0f) == -1);
    CHECK(world.setSphere(id, 1.0f, 0.0f) == -1);
    CHECK(world.setBox(id, 0.0f, 1.0f, 1.0f, 1.0f) == -1);
    CHECK(world.setBox(id, 1.0f, 1.0f, 1.0f, -1.0f) == -1);
    CHECK(world.setTransform(id, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f) == 0);
    CHECK(world.setVelocity(id, 0.0f, 0.0f, 0.0f) == 0);
    CHECK(world.setAngularVelocity(id, 0.0f, 0.0f, 0.0f) == 0);
    CHECK(world.setMaterial(id, -0.1f, 0.0f) == -1);
    CHECK(world.setMaterial(id, 0.5f, 2.0f) == 0);
    CHECK(world.step(0.0f, 4, 2) == -1);
    CHECK(world.step(-1.0f, 4, 2) == -1);
    CHECK(world.step(1.0f / 60.0f, -5, -5) >= 0);
    CHECK(world.destroyBody(-1) == -1);
    CHECK(world.destroyBody(999) == -1);
}

void testRestitution() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t ground = world.createBody(kBodyTypeStatic, 0.0f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(ground >= 0);
    CHECK(world.setBox(ground, 10.0f, 0.5f, 10.0f, 0.0f) == 0);

    const int32_t sphere = world.createBody(kBodyTypeDynamic, 0.0f, 3.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(sphere >= 0);
    CHECK(world.setSphere(sphere, 0.5f, 1.0f) == 0);
    CHECK(world.setMaterial(ground, 0.6f, 1.0f) == 0);
    CHECK(world.setMaterial(sphere, 0.6f, 1.0f) == 0);

    bool touched = false;
    float highestAfterContact = 0.0f;
    for (int32_t i = 0; i < 240; ++i) {
        world.step(1.0f / 60.0f, 8, 3);
        if (world.getContactCount() > 0) {
            touched = true;
        }
        if (touched && world.transforms[sphere].py > highestAfterContact) {
            highestAfterContact = world.transforms[sphere].py;
        }
    }

    CHECK(touched);
    CHECK(highestAfterContact > 0.8f);
    CHECK(isFinite(world.transforms[sphere].py));
}

void testFriction() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t ground = world.createBody(kBodyTypeStatic, 0.0f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(ground >= 0);
    CHECK(world.setBox(ground, 10.0f, 0.5f, 10.0f, 0.0f) == 0);

    const int32_t box = world.createBody(kBodyTypeDynamic, 0.0f, 0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(box >= 0);
    CHECK(world.setBox(box, 0.5f, 0.5f, 0.5f, 1.0f) == 0);
    CHECK(world.setMaterial(box, 0.8f, 0.0f) == 0);
    CHECK(world.setVelocity(box, 5.0f, 0.0f, 0.0f) == 0);

    for (int32_t i = 0; i < 180; ++i) {
        world.step(1.0f / 60.0f, 8, 3);
    }

    CHECK(fabsf(world.velocities[box].x) < 4.0f);
    CHECK(isFinite(world.transforms[box].px));
}

void testRotatedBoxContact() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t boxA = world.createBody(kBodyTypeStatic, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    const float half = 0.70710678f;
    const int32_t boxB = world.createBody(kBodyTypeDynamic, 0.7f, 0.0f, 0.0f, 0.0f, 0.0f, half, half);
    CHECK(world.setBox(boxA, 0.5f, 0.5f, 0.5f, 0.0f) == 0);
    CHECK(world.setBox(boxB, 0.5f, 0.5f, 0.5f, 1.0f) == 0);
    CHECK(world.step(1.0f / 60.0f, 4, 2) >= 0);
    CHECK(world.getContactCount() >= 1);

    for (int32_t i = 0; i < world.getContactCount(); ++i) {
        CHECK(isFinite(world.packedContacts[i].pen));
        CHECK(isFinite(world.packedContacts[i].nx));
    }
}

void testKinematicBodyCreation() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t platform = world.createBody(kBodyTypeKinematic, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(platform >= 0);
    CHECK(world.bodies[platform].state == kBodyKinematic);
    CHECK(world.setBox(platform, 2.0f, 0.1f, 2.0f, 1.0f) == 0);
    CHECK(world.bodies[platform].invMass == 0.0f);
    CHECK(world.bodies[platform].invInertia.x == 0.0f);
    CHECK(world.bodies[platform].invInertia.y == 0.0f);
    CHECK(world.bodies[platform].invInertia.z == 0.0f);
    CHECK(world.setSphere(platform, 0.5f, 1.0f) == 0);
    CHECK(world.bodies[platform].invMass == 0.0f);
    CHECK(world.bodies[platform].invInertia.x == 0.0f);

    CHECK(world.createBody(3, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f) == -1);
    CHECK(world.destroyBody(platform) == 0);
}

void testKinematicIgnoresGravity() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t body = world.createBody(kBodyTypeKinematic, 0.0f, 3.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(body >= 0);
    CHECK(world.setBox(body, 0.5f, 0.5f, 0.5f, 1.0f) == 0);

    for (int32_t i = 0; i < 60; ++i) {
        world.step(1.0f / 60.0f, 8, 3);
    }

    CHECK(world.transforms[body].px == 0.0f);
    CHECK(world.transforms[body].py == 3.0f);
    CHECK(world.transforms[body].pz == 0.0f);
    CHECK(world.velocities[body].x == 0.0f);
    CHECK(world.velocities[body].y == 0.0f);

    CHECK(world.setVelocity(body, 1.0f, 0.0f, 0.0f) == 0);
    for (int32_t i = 0; i < 60; ++i) {
        world.step(1.0f / 60.0f, 8, 3);
    }

    CHECK(near(world.transforms[body].px, 1.0f, 0.01f));
    CHECK(near(world.transforms[body].py, 3.0f, 0.01f));
    CHECK(world.velocities[body].x == 1.0f);
    CHECK(world.velocities[body].y == 0.0f);

    CHECK(world.setAngularVelocity(body, 0.0f, 2.0f, 0.0f) == 0);
    const float qyBefore = world.transforms[body].qy;
    world.step(1.0f / 60.0f, 8, 3);
    CHECK(fabsf(world.transforms[body].qy - qyBefore) > 1e-4f);
    CHECK(near(world.transforms[body].px, 1.0f, 0.02f));
    CHECK(world.angularVelocities[body].y == 2.0f);
}

void testKinematicCollidesWithStatic() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t ground = world.createBody(kBodyTypeStatic, 0.0f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(ground >= 0);
    CHECK(world.setBox(ground, 10.0f, 0.5f, 10.0f, 0.0f) == 0);

    const int32_t drifter = world.createBody(kBodyTypeKinematic, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(drifter >= 0);
    CHECK(world.setBox(drifter, 0.5f, 0.5f, 0.5f, 1.0f) == 0);

    CHECK(world.step(1.0f / 60.0f, 8, 3) >= 0);
    CHECK(world.getContactCount() >= 1);
    CHECK(world.transforms[ground].py == -0.5f);
    CHECK(world.transforms[drifter].py == 0.0f);

    const int32_t other = world.createBody(kBodyTypeKinematic, 3.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(other >= 0);
    CHECK(world.setBox(other, 0.5f, 0.5f, 0.5f, 1.0f) == 0);
    CHECK(world.step(1.0f / 60.0f, 8, 3) >= 0);
    CHECK(world.transforms[other].px == 3.0f);
}

void testKinematicPlatformCarriesBox() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t platform = world.createBody(kBodyTypeKinematic, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(platform >= 0);
    CHECK(world.setBox(platform, 2.0f, 0.1f, 2.0f, 1.0f) == 0);
    CHECK(world.setMaterial(platform, 0.9f, 0.0f) == 0);
    CHECK(world.setVelocity(platform, 0.0f, 1.0f, 0.0f) == 0);

    const int32_t box = world.createBody(kBodyTypeDynamic, 0.0f, 0.6f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(box >= 0);
    CHECK(world.setBox(box, 0.5f, 0.5f, 0.5f, 1.0f) == 0);
    CHECK(world.setMaterial(box, 0.9f, 0.0f) == 0);

    for (int32_t i = 0; i < 120; ++i) {
        world.step(1.0f / 60.0f, 8, 3);
    }

    CHECK(near(world.transforms[platform].py, 2.0f, 0.01f));
    CHECK(world.transforms[box].py > 2.2f);
    CHECK(world.transforms[box].py < 2.9f);
    CHECK(world.velocities[box].y > 0.4f);
    CHECK(world.velocities[box].y < 1.6f);
    CHECK(world.getContactCount() >= 1);
}

void testDynamicCannotPushKinematic() {
    Arena arena;
    World world;
    arena.init(g_memoryA, 64 * 1024 * 1024);
    CHECK(world.init(arena, 16, 256, Vec3(0.0f, 0.0f, 0.0f)));

    const int32_t wall = world.createBody(kBodyTypeKinematic, 2.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(wall >= 0);
    CHECK(world.setBox(wall, 0.2f, 1.0f, 1.0f, 1.0f) == 0);
    CHECK(world.setMaterial(wall, 0.5f, 0.0f) == 0);

    const int32_t ram = world.createBody(kBodyTypeDynamic, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(ram >= 0);
    CHECK(world.setBox(ram, 0.5f, 0.5f, 0.5f, 1.0f) == 0);
    CHECK(world.setMaterial(ram, 0.0f, 0.0f) == 0);
    CHECK(world.setVelocity(ram, 5.0f, 0.0f, 0.0f) == 0);

    for (int32_t i = 0; i < 120; ++i) {
        world.step(1.0f / 60.0f, 8, 3);
    }

    CHECK(world.transforms[wall].px == 2.0f);
    CHECK(world.transforms[wall].py == 0.0f);
    CHECK(world.velocities[wall].x == 0.0f);
    CHECK(world.transforms[ram].px < 1.5f);
    CHECK(isFinite(world.transforms[ram].px));
}

void testMultiShapeMassAccumulation() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t single = world.createBody(kBodyTypeDynamic, 0.0f, 10.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(single >= 0);
    CHECK(world.setBox(single, 0.5f, 0.5f, 0.5f, 1.0f) == 0);
    CHECK(world.bodies[single].shapeCount == 1);
    CHECK(near(world.bodies[single].invMass, 1.0f, 1e-5f));
    CHECK(near(world.bodies[single].invInertia.x, 6.0f, 1e-4f));
    CHECK(near(world.bodies[single].invInertia.y, 6.0f, 1e-4f));

    const int32_t compound = world.createBody(kBodyTypeDynamic, 0.0f, 10.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(compound >= 0);
    CHECK(world.setBox(compound, 0.5f, 0.5f, 0.5f, 1.0f) == 0);
    CHECK(world.addBox(compound, 0.5f, 0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f) == 1);
    CHECK(world.bodies[compound].shapeCount == 2);
    CHECK(near(world.bodies[compound].invMass, 0.5f, 1e-5f));
    CHECK(near(world.bodies[compound].invInertia.x, 3.0f, 1e-4f));
    CHECK(near(world.bodies[compound].invInertia.y, 3.0f, 1e-4f));
    CHECK(near(world.bodies[compound].invInertia.z, 3.0f, 1e-4f));

    CHECK(world.addSphere(compound, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f) == 2);
    CHECK(world.bodies[compound].shapeCount == 3);
    CHECK(world.bodies[compound].invMass < 0.5f);
    CHECK(world.bodies[compound].invMass > 0.0f);
}

void testParallelAxisInertiaGrows() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t centered = world.createBody(kBodyTypeDynamic, 0.0f, 10.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(world.setBox(centered, 0.5f, 0.5f, 0.5f, 1.0f) == 0);
    CHECK(world.addBox(centered, 0.5f, 0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f) == 1);

    const int32_t offset = world.createBody(kBodyTypeDynamic, 0.0f, 10.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(world.setBox(offset, 0.5f, 0.5f, 0.5f, 1.0f) == 0);
    CHECK(world.addBox(offset, 0.5f, 0.5f, 0.5f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f) == 1);

    CHECK(world.bodies[offset].invMass == world.bodies[centered].invMass);
    CHECK(world.bodies[offset].invInertia.x < world.bodies[centered].invInertia.x);
    CHECK(world.bodies[offset].invInertia.z < world.bodies[centered].invInertia.z);
    CHECK(near(world.bodies[offset].invInertia.y, 3.0f, 1e-4f));
    CHECK(near(world.bodies[offset].invInertia.x, 0.75f, 1e-4f));
    CHECK(near(world.bodies[offset].invInertia.z, 0.75f, 1e-4f));
}

void testOffsetShapeRestsOnGround() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t ground = world.createBody(kBodyTypeStatic, 0.0f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(ground >= 0);
    CHECK(world.setBox(ground, 10.0f, 0.5f, 10.0f, 0.0f) == 0);

    const int32_t body = world.createBody(kBodyTypeDynamic, 0.0f, 3.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(body >= 0);
    CHECK(world.addBox(body, 0.5f, 0.5f, 0.5f, 1.0f, 0.0f, -1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f) == 0);
    CHECK(world.setMaterial(body, 0.7f, 0.0f) == 0);

    for (int32_t i = 0; i < 240; ++i) {
        world.step(1.0f / 60.0f, 8, 3);
    }

    const Transform& transform = world.transforms[body];
    CHECK(isFinite(transform.px) && isFinite(transform.py) && isFinite(transform.pz));
    CHECK(near(transform.py, 1.5f, 0.08f));
    CHECK(fabsf(world.velocities[body].y) < 0.25f);
    CHECK(world.getContactCount() >= 1);
}

void testClearShapesResetsProperties() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t ground = world.createBody(kBodyTypeStatic, 0.0f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(ground >= 0);
    CHECK(world.setBox(ground, 10.0f, 0.5f, 10.0f, 0.0f) == 0);

    const int32_t body = world.createBody(kBodyTypeDynamic, 0.0f, 0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(body >= 0);
    CHECK(world.setSphere(body, 0.5f, 1.0f) == 0);
    CHECK(world.bodies[body].invMass > 0.0f);
    CHECK(world.step(1.0f / 60.0f, 4, 2) >= 0);
    CHECK(world.getContactCount() >= 1);

    CHECK(world.clearShapes(body) == 0);
    CHECK(world.bodies[body].shapeCount == 0);
    CHECK(world.bodies[body].invMass == 0.0f);
    CHECK(world.bodies[body].invInertia.x == 0.0f);
    CHECK(world.bodies[body].invInertia.y == 0.0f);
    CHECK(world.bodies[body].invInertia.z == 0.0f);
    CHECK(world.step(1.0f / 60.0f, 4, 2) >= 0);
    CHECK(world.getContactCount() == 0);

    CHECK(world.addBox(body, 0.5f, 0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f) == 0);
    CHECK(world.bodies[body].shapeCount == 1);
    CHECK(near(world.bodies[body].invMass, 1.0f, 1e-5f));
    CHECK(world.clearShapes(-1) == -1);
    CHECK(world.clearShapes(9999) == -1);
}

void testShapeCapacityRejection() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t body = world.createBody(kBodyTypeDynamic, 0.0f, 10.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(body >= 0);

    for (int32_t slot = 0; slot < kMaxShapesPerBody; ++slot) {
        const float offset = static_cast<float>(slot) * 0.1f;
        CHECK(world.addSphere(body, 0.5f, 1.0f, offset, 0.0f, 0.0f) == slot);
    }
    CHECK(world.bodies[body].shapeCount == kMaxShapesPerBody);

    const float invMassBefore = world.bodies[body].invMass;
    const Vec3 inertiaBefore = world.bodies[body].invInertia;
    CHECK(world.addSphere(body, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f) == -1);
    CHECK(world.addBox(body, 0.5f, 0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f) == -1);
    CHECK(world.bodies[body].shapeCount == kMaxShapesPerBody);
    CHECK(world.bodies[body].invMass == invMassBefore);
    CHECK(world.bodies[body].invInertia.x == inertiaBefore.x);
    CHECK(world.bodies[body].invInertia.y == inertiaBefore.y);
    CHECK(world.bodies[body].invInertia.z == inertiaBefore.z);
    CHECK(world.step(1.0f / 60.0f, 4, 2) >= 0);

    const int32_t probe = world.createBody(kBodyTypeDynamic, 0.0f, 10.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(probe >= 0);
    CHECK(world.setSphere(probe, 0.5f, 1.0f) == 0);
    CHECK(world.addSphere(probe, -1.0f, 1.0f, 0.0f, 0.0f, 0.0f) == -1);
    CHECK(world.addSphere(probe, 1.0f, -1.0f, 0.0f, 0.0f, 0.0f) == -1);
    CHECK(world.addSphere(probe, 1.0f, 1.0f, kFloatMax, 0.0f, 0.0f) == -1);
    CHECK(world.addBox(probe, 0.0f, 1.0f, 1.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f) == -1);
    CHECK(world.addBox(probe, 1.0f, 1.0f, 1.0f, 1.0f, 0.0f, kFloatMax, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f) == -1);
    CHECK(world.addSphere(probe, 1.0f, 1.0f, 0.0f, 0.0f, 0.0f) == 1);
    CHECK(world.bodies[probe].shapeCount == 2);
}

void testStaticAndKinematicKeepZeroMass() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t staticBody = world.createBody(kBodyTypeStatic, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    const int32_t kinematicBody = world.createBody(kBodyTypeKinematic, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(staticBody >= 0);
    CHECK(kinematicBody >= 0);

    const int32_t bodies[2] = { staticBody, kinematicBody };
    for (int32_t i = 0; i < 2; ++i) {
        const int32_t id = bodies[i];
        CHECK(world.setSphere(id, 0.5f, 5.0f) == 0);
        CHECK(world.bodies[id].invMass == 0.0f);
        CHECK(world.setBox(id, 0.5f, 0.5f, 0.5f, 5.0f) == 0);
        CHECK(world.bodies[id].invMass == 0.0f);
        CHECK(world.bodies[id].invInertia.x == 0.0f);
        CHECK(world.addSphere(id, 0.5f, 5.0f, 0.0f, 1.0f, 0.0f) == 1);
        CHECK(world.bodies[id].invMass == 0.0f);
        CHECK(world.addBox(id, 0.5f, 0.5f, 0.5f, 5.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.70710678f, 0.0f, 0.70710678f) == 2);
        CHECK(world.bodies[id].shapeCount == 3);
        CHECK(world.bodies[id].invMass == 0.0f);
        CHECK(world.bodies[id].invInertia.y == 0.0f);
        CHECK(world.bodies[id].invInertia.z == 0.0f);
    }
}

void testOffsetShapeAabbUnion() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t body = world.createBody(kBodyTypeStatic, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(body >= 0);
    CHECK(world.setBox(body, 1.0f, 1.0f, 1.0f, 0.0f) == 0);
    CHECK(world.step(1.0f / 60.0f, 1, 1) >= 0);
    CHECK(near(world.bodies[body].aabb.minX, -1.0f, 1e-5f));
    CHECK(near(world.bodies[body].aabb.maxX, 1.0f, 1e-5f));

    CHECK(world.addBox(body, 0.5f, 0.5f, 0.5f, 0.0f, 4.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f) == 1);
    CHECK(world.step(1.0f / 60.0f, 1, 1) >= 0);
    CHECK(near(world.bodies[body].aabb.maxX, 4.5f, 1e-5f));
    CHECK(near(world.bodies[body].aabb.minX, -1.0f, 1e-5f));
    CHECK(near(world.bodies[body].aabb.maxY, 1.0f, 1e-5f));

    CHECK(world.addSphere(body, 2.0f, 0.0f, -6.0f, 0.0f, 0.0f) == 2);
    CHECK(world.step(1.0f / 60.0f, 1, 1) >= 0);
    CHECK(near(world.bodies[body].aabb.minX, -8.0f, 1e-5f));
    CHECK(near(world.bodies[body].aabb.maxX, 4.5f, 1e-5f));
    CHECK(near(world.bodies[body].aabb.minY, -2.0f, 1e-5f));
}

void testMultiShapeDeterminism() {
    Arena arenaA;
    Arena arenaB;
    World worldA;
    World worldB;
    CHECK(initWorld(worldA, arenaA, g_memoryA, 64, 4096));
    CHECK(initWorld(worldB, arenaB, g_memoryB, 64, 4096));

    World* const worlds[2] = { &worldA, &worldB };
    for (int32_t w = 0; w < 2; ++w) {
        World& world = *worlds[w];
        const int32_t ground = world.createBody(kBodyTypeStatic, 0.0f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
        CHECK(ground >= 0);
        CHECK(world.setBox(ground, 20.0f, 0.5f, 20.0f, 0.0f) == 0);
        CHECK(world.setMaterial(ground, 0.8f, 0.0f) == 0);

        for (int32_t i = 0; i < 6; ++i) {
            const float x = static_cast<float>(i) * 2.5f - 6.0f;
            const int32_t body = world.createBody(
                kBodyTypeDynamic, x, 2.0f + static_cast<float>(i), 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
            CHECK(body >= 0);
            CHECK(world.addBox(body, 0.5f, 0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f) == 0);
            CHECK(world.addSphere(body, 0.4f, 1.0f, 1.1f, 0.0f, 0.0f) == 1);
            CHECK(world.addBox(body, 0.25f, 0.75f, 0.25f, 2.0f, -1.0f, 0.5f, 0.0f, 0.0f, 0.3826834f, 0.0f, 0.9238795f) == 2);
            CHECK(world.setMaterial(body, 0.7f, 0.0f) == 0);
        }
    }

    for (int32_t i = 0; i < 180; ++i) {
        worldA.step(1.0f / 60.0f, 8, 3);
        worldB.step(1.0f / 60.0f, 8, 3);
    }

    CHECK(worldA.getContactCount() == worldB.getContactCount());
    CHECK(worldA.fingerprint() == worldB.fingerprint());
    CHECK(worldA.getContactCount() >= 1);

    for (int32_t i = 0; i < worldA.highWater; ++i) {
        if (worldA.bodies[i].state == kBodyFree) {
            continue;
        }
        const Transform& transform = worldA.transforms[i];
        CHECK(isFinite(transform.px) && isFinite(transform.py) && isFinite(transform.pz));
        CHECK(isFinite(transform.qx) && isFinite(transform.qy) && isFinite(transform.qz) && isFinite(transform.qw));
        CHECK(finiteVector(worldA.velocities[i]));
        CHECK(finiteVector(worldA.angularVelocities[i]));
        CHECK(isFinite(worldA.bodies[i].invMass));
        CHECK(finiteVector(worldA.bodies[i].invInertia));
    }
}

void testArenaExhaustion() {
    alignas(64) unsigned char small[8192];
    Arena arena;
    arena.init(small, sizeof(small));
    World world;
    CHECK(!world.init(arena, 512, 16384, Vec3(0.0f, -9.81f, 0.0f)));
    CHECK(world.bodyCount() == 0);
}

void testStressDeterminism() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 512, 16384));
    addStandardBodies(world);
    runStandardScene(world);

    for (int32_t i = 0; i < world.highWater; ++i) {
        if (world.bodies[i].state == kBodyFree) {
            continue;
        }
        const Transform& transform = world.transforms[i];
        CHECK(isFinite(transform.px) && isFinite(transform.py) && isFinite(transform.pz));
        CHECK(isFinite(transform.qx) && isFinite(transform.qy) && isFinite(transform.qz) && isFinite(transform.qw));
        CHECK(finiteVector(world.velocities[i]));
        CHECK(finiteVector(world.angularVelocities[i]));
    }

    const uint64_t first = world.fingerprint();
    const int32_t contacts = world.getContactCount();
    world.step(kStandardDt, 8, 3);
    CHECK(world.getContactCount() > 0);
    CHECK(contacts > 0);
    CHECK(world.fingerprint() != first);
}

bool initGravitylessWorld(World& world, Arena& arena, unsigned char* memory, int32_t bodies, int32_t contacts) {
    arena.init(memory, 64 * 1024 * 1024);
    return world.init(arena, bodies, contacts, Vec3(0.0f, 0.0f, 0.0f));
}

int32_t addFreeBox(World& world, float x, float y, float z) {
    const int32_t id = world.createBody(kBodyTypeDynamic, x, y, z, 0.0f, 0.0f, 0.0f, 1.0f);
    if (id < 0) {
        return id;
    }
    if (world.setBox(id, 0.5f, 0.5f, 0.5f, 1.0f) != 0) {
        return -1;
    }
    return id;
}

void testDampingDecay() {
    Arena arena;
    World world;
    CHECK(initGravitylessWorld(world, arena, g_memoryA, 16, 256));

    const int32_t body = addFreeBox(world, 0.0f, 10.0f, 0.0f);
    CHECK(body >= 0);
    CHECK(world.setDamping(body, 0.1f, 0.2f) == 0);
    CHECK(world.setVelocity(body, 1.0f, 0.0f, 0.0f) == 0);
    CHECK(world.setAngularVelocity(body, 0.0f, 1.0f, 0.0f) == 0);
    CHECK(world.bodies[body].linearDamping == 0.1f);
    CHECK(world.bodies[body].angularDamping == 0.2f);

    const float dt = 1.0f / 60.0f;
    for (int32_t i = 0; i < 10; ++i) {
        world.step(dt, 4, 2);
    }

    const double factor = 1.0 - 0.1 * static_cast<double>(dt);
    const double expectedLinear = factor * factor * factor * factor * factor *
                                  factor * factor * factor * factor * factor;
    const double expectedAngular = (1.0 - 0.2 * static_cast<double>(dt)) *
                                   (1.0 - 0.2 * static_cast<double>(dt)) *
                                   (1.0 - 0.2 * static_cast<double>(dt)) *
                                   (1.0 - 0.2 * static_cast<double>(dt)) *
                                   (1.0 - 0.2 * static_cast<double>(dt)) *
                                   (1.0 - 0.2 * static_cast<double>(dt)) *
                                   (1.0 - 0.2 * static_cast<double>(dt)) *
                                   (1.0 - 0.2 * static_cast<double>(dt)) *
                                   (1.0 - 0.2 * static_cast<double>(dt)) *
                                   (1.0 - 0.2 * static_cast<double>(dt));

    const float relative = 1e-4f;
    CHECK(fabsf(world.velocities[body].x - static_cast<float>(expectedLinear)) <=
          relative * static_cast<float>(expectedLinear));
    CHECK(fabsf(world.angularVelocities[body].y - static_cast<float>(expectedAngular)) <=
          relative * static_cast<float>(expectedAngular));

    CHECK(world.setDamping(body, -0.1f, 0.0f) == -1);
    CHECK(world.setDamping(body, 0.0f, kFloatMax * 10.0f) == -1);
    CHECK(world.setDamping(-1, 0.0f, 0.0f) == -1);

    const int32_t blocker = addFreeBox(world, 5.0f, 10.0f, 0.0f);
    CHECK(blocker >= 0);
    CHECK(world.setDamping(blocker, 10000.0f, 0.0f) == 0);
    CHECK(world.setVelocity(blocker, 3.0f, 0.0f, 0.0f) == 0);
    world.step(dt, 4, 2);
    CHECK(world.velocities[blocker].x == 0.0f);
}

void testGravityScale() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t floater = addFreeBox(world, -4.0f, 100.0f, 0.0f);
    const int32_t normal = addFreeBox(world, 0.0f, 100.0f, 0.0f);
    const int32_t doublePull = addFreeBox(world, 4.0f, 100.0f, 0.0f);
    CHECK(floater >= 0 && normal >= 0 && doublePull >= 0);

    CHECK(world.setGravityScale(floater, 0.0f) == 0);
    CHECK(world.setGravityScale(doublePull, 2.0f) == 0);
    CHECK(world.setGravityScale(normal, kFloatMax * 10.0f) == -1);

    const float dt = 1.0f / 60.0f;
    for (int32_t i = 0; i < 60; ++i) {
        world.step(dt, 4, 2);
    }

    CHECK(world.transforms[floater].py == 100.0f);
    CHECK(world.velocities[floater].y == 0.0f);

    const float normalDrop = 100.0f - world.transforms[normal].py;
    const float doubleDrop = 100.0f - world.transforms[doublePull].py;
    CHECK(normalDrop > 0.1f);
    CHECK(fabsf(doubleDrop - 2.0f * normalDrop) < 0.02f);
    CHECK(fabsf(world.velocities[doublePull].y - 2.0f * world.velocities[normal].y) < 0.02f);

    CHECK(world.setGravityScale(-1, 1.0f) == -1);
}

void testFrozenPositionAxes() {
    Arena arena;
    World world;
    CHECK(initGravitylessWorld(world, arena, g_memoryA, 16, 256));

    const int32_t body = addFreeBox(world, 0.0f, 10.0f, 0.0f);
    CHECK(body >= 0);
    CHECK(world.setFlags(body, kBodyFreezePositionY | kBodyFreezePositionZ, 0) == 0);
    CHECK(world.bodies[body].flags == (kBodyFreezePositionY | kBodyFreezePositionZ));
    CHECK(world.setVelocity(body, 1.0f, 1.0f, 1.0f) == 0);

    const float dt = 1.0f / 60.0f;
    for (int32_t i = 0; i < 10; ++i) {
        world.step(dt, 4, 2);
    }

    CHECK(near(world.transforms[body].px, 10.0f * dt, 1e-4f));
    CHECK(world.transforms[body].py == 10.0f);
    CHECK(world.transforms[body].pz == 0.0f);
    CHECK(world.velocities[body].x == 1.0f);
    CHECK(world.velocities[body].y == 0.0f);
    CHECK(world.velocities[body].z == 0.0f);

    CHECK(world.setFlags(body, 0, kBodyFreezePositionY) == 0);
    CHECK(world.bodies[body].flags == kBodyFreezePositionZ);
    CHECK(world.setFlags(body, 64 | 128, 0) == 0);
    CHECK(world.bodies[body].flags == (64 | 128 | kBodyFreezePositionZ));
    CHECK(world.setFlags(body, -1, 0) == -1);
    CHECK(world.setFlags(body, 0, -1) == -1);
    CHECK(world.setFlags(999, 1, 0) == -1);
}

void testFrozenRotationAxes() {
    Arena arena;
    World world;
    CHECK(initGravitylessWorld(world, arena, g_memoryA, 16, 256));

    const int32_t body = addFreeBox(world, 0.0f, 10.0f, 0.0f);
    CHECK(body >= 0);
    CHECK(world.setFlags(body, kBodyFreezeRotationX | kBodyFreezeRotationZ, 0) == 0);
    CHECK(world.setAngularVelocity(body, 1.0f, 2.0f, 1.0f) == 0);

    for (int32_t i = 0; i < 10; ++i) {
        world.step(1.0f / 60.0f, 4, 2);
    }

    CHECK(world.angularVelocities[body].x == 0.0f);
    CHECK(world.angularVelocities[body].y == 2.0f);
    CHECK(world.angularVelocities[body].z == 0.0f);

    const Transform& transform = world.transforms[body];
    CHECK(transform.qx == 0.0f);
    CHECK(transform.qz == 0.0f);
    CHECK(fabsf(transform.qy) > 1e-4f);
    CHECK(near(transform.qy * transform.qy + transform.qw * transform.qw, 1.0f, 1e-4f));

    CHECK(world.setFlags(body, 0, kBodyFreezeRotationX | kBodyFreezeRotationY | kBodyFreezeRotationZ) == 0);
    CHECK(world.bodies[body].flags == 0);
}

void testForceAppliedPerStep() {
    Arena arena;
    World world;
    CHECK(initGravitylessWorld(world, arena, g_memoryA, 16, 256));

    const int32_t body = addFreeBox(world, 0.0f, 10.0f, 0.0f);
    CHECK(body >= 0);
    CHECK(world.bodies[body].invMass == 1.0f);
    CHECK(world.applyForce(body, 60.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f) == 0);
    CHECK(world.bodies[body].forceAccum.x == 60.0f);
    CHECK(world.applyForce(body, 0.0f, kFloatMax * 10.0f, 0.0f, 0.0f, 0.0f, 0.0f) == -1);
    CHECK(world.applyForce(body, kFloatMax * 10.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f) == -1);

    const float dt = 1.0f / 60.0f;
    world.step(dt, 4, 2);
    CHECK(near(world.velocities[body].x, 60.0f * dt * 1.0f, 1e-5f));
    CHECK(world.bodies[body].forceAccum.x == 0.0f);
    CHECK(world.bodies[body].forceAccum.y == 0.0f);
    CHECK(world.bodies[body].torqueAccum.y == 0.0f);

    world.step(dt, 4, 2);
    CHECK(near(world.velocities[body].x, 1.0f, 1e-6f));

    const int32_t staticBody = world.createBody(kBodyTypeStatic, 8.0f, 10.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    const int32_t kinematicBody = world.createBody(kBodyTypeKinematic, 9.0f, 10.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(staticBody >= 0);
    CHECK(kinematicBody >= 0);
    CHECK(world.setBox(staticBody, 0.5f, 0.5f, 0.5f, 0.0f) == 0);
    CHECK(world.setBox(kinematicBody, 0.5f, 0.5f, 0.5f, 1.0f) == 0);
    CHECK(world.applyForce(staticBody, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f) == -1);
    CHECK(world.applyForce(kinematicBody, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f) == -1);
    CHECK(world.applyTorque(kinematicBody, 0.0f, 1.0f, 0.0f) == -1);
    CHECK(world.applyImpulse(kinematicBody, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f) == -1);
    CHECK(world.applyAngularImpulse(staticBody, 0.0f, 1.0f, 0.0f) == -1);
    CHECK(world.applyForce(999, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f) == -1);

    CHECK(world.applyTorque(body, 0.0f, 0.0f, 6.0f) == 0);
    CHECK(world.bodies[body].torqueAccum.z == 6.0f);
    world.step(dt, 4, 2);
    CHECK(near(world.angularVelocities[body].z, 6.0f * dt * world.bodies[body].invInertia.z, 1e-5f));
    CHECK(world.bodies[body].torqueAccum.z == 0.0f);
}

void testImpulseAtOffset() {
    Arena arena;
    World world;
    CHECK(initGravitylessWorld(world, arena, g_memoryA, 16, 256));

    const int32_t body = addFreeBox(world, 0.0f, 10.0f, 0.0f);
    CHECK(body >= 0);
    CHECK(world.applyImpulse(body, 0.0f, 0.0f, 10.0f, 1.0f, 0.0f, 0.0f) == 0);
    CHECK(world.velocities[body].z == 10.0f);
    CHECK(near(world.angularVelocities[body].y, -10.0f * world.bodies[body].invInertia.y, 1e-3f));

    CHECK(world.applyAngularImpulse(body, 0.0f, 10.0f, 0.0f) == 0);
    CHECK(near(world.angularVelocities[body].y, 0.0f, 1e-3f));
    CHECK(world.applyImpulse(body, kFloatMax * 10.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f) == -1);
    CHECK(world.applyAngularImpulse(body, 0.0f, kFloatMax * 10.0f, 0.0f) == -1);
}

void testMassOverride() {
    Arena arena;
    World world;
    CHECK(initGravitylessWorld(world, arena, g_memoryA, 16, 256));

    const int32_t body = addFreeBox(world, 0.0f, 10.0f, 0.0f);
    const int32_t reference = addFreeBox(world, 3.0f, 10.0f, 0.0f);
    CHECK(body >= 0 && reference >= 0);
    CHECK(world.bodies[body].invMass == 1.0f);

    CHECK(world.setMassData(body, 2.0f, 0.0f, 0.0f, 0.0f) == 0);
    CHECK(world.bodies[body].customMass == 2.0f);
    CHECK(world.bodies[body].invMass == 0.5f);

    CHECK(world.addSphere(body, 0.5f, 1.0f, 1.0f, 0.0f, 0.0f) == 1);
    CHECK(world.bodies[body].invMass == 0.5f);

    CHECK(world.setMassData(body, 4.0f, 1.0f, 0.0f, 2.0f) == 0);
    CHECK(world.bodies[body].invMass == 0.25f);
    CHECK(world.bodies[body].invInertia.x == 1.0f);
    CHECK(world.bodies[body].invInertia.z == 0.5f);
    CHECK(world.bodies[body].invInertia.y > 0.0f);

    CHECK(world.addBox(body, 0.25f, 0.25f, 0.25f, 1.0f, -1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f) == 2);
    CHECK(world.bodies[body].invMass == 0.25f);
    CHECK(world.bodies[body].invInertia.x == 1.0f);
    CHECK(world.bodies[body].invInertia.z == 0.5f);

    CHECK(world.setMassData(body, kFloatMax * 10.0f, 0.0f, 0.0f, 0.0f) == -1);
    CHECK(world.setMassData(body, 1.0f, kFloatMax * 10.0f, 0.0f, 0.0f) == -1);
    CHECK(world.bodies[body].invMass == 0.25f);
    CHECK(world.setMassData(body, 0.0f, 0.0f, 0.0f, 0.0f) == 0);
    CHECK(world.bodies[body].customMass == 0.0f);
    CHECK(world.bodies[body].invMass > 0.0f);

    CHECK(world.addSphere(reference, 0.5f, 1.0f, 1.0f, 0.0f, 0.0f) == 1);
    CHECK(world.addBox(reference, 0.25f, 0.25f, 0.25f, 1.0f, -1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f) == 2);
    CHECK(world.bodies[body].invMass == world.bodies[reference].invMass);
    CHECK(world.bodies[body].invInertia.x == world.bodies[reference].invInertia.x);
    CHECK(world.bodies[body].invInertia.y == world.bodies[reference].invInertia.y);
    CHECK(world.bodies[body].invInertia.z == world.bodies[reference].invInertia.z);

    const int32_t staticBody = world.createBody(kBodyTypeStatic, 6.0f, 10.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    const int32_t kinematicBody = world.createBody(kBodyTypeKinematic, 7.0f, 10.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(world.setBox(staticBody, 0.5f, 0.5f, 0.5f, 0.0f) == 0);
    CHECK(world.setBox(kinematicBody, 0.5f, 0.5f, 0.5f, 1.0f) == 0);
    CHECK(world.setMassData(staticBody, 5.0f, 0.0f, 0.0f, 0.0f) == 0);
    CHECK(world.setMassData(kinematicBody, 5.0f, 0.0f, 0.0f, 0.0f) == 0);
    CHECK(world.bodies[staticBody].invMass == 0.0f);
    CHECK(world.bodies[kinematicBody].invMass == 0.0f);
    CHECK(world.bodies[staticBody].customMass == 0.0f);
    CHECK(world.bodies[kinematicBody].customMass == 0.0f);
    CHECK(world.setMassData(999, 1.0f, 0.0f, 0.0f, 0.0f) == -1);
    CHECK(world.setMassData(body, kFloatMax, kFloatMax, 0.0f, 0.0f) == 0);
    CHECK(world.bodies[body].invMass > 0.0f);
}

void testKinematicDampingKeepsMotion() {
    Arena arena;
    World world;
    CHECK(initWorld(world, arena, g_memoryA, 16, 256));

    const int32_t body = world.createBody(kBodyTypeKinematic, 0.0f, 10.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
    CHECK(body >= 0);
    CHECK(world.setBox(body, 0.5f, 0.5f, 0.5f, 1.0f) == 0);
    CHECK(world.setDamping(body, 0.5f, 0.0f) == 0);
    CHECK(world.setVelocity(body, 1.0f, 0.0f, 0.0f) == 0);
    CHECK(world.setGravityScale(body, 3.0f) == 0);

    const float dt = 1.0f / 60.0f;
    world.step(dt, 4, 2);
    CHECK(near(world.velocities[body].x, 1.0f - 0.5f * dt, 1e-6f));
    CHECK(world.transforms[body].py == 10.0f);
    CHECK(world.velocities[body].y == 0.0f);
}

void testDynamicsDefaultsAndReset() {
    Arena arena;
    World world;
    CHECK(initGravitylessWorld(world, arena, g_memoryA, 16, 256));

    const int32_t body = addFreeBox(world, 0.0f, 10.0f, 0.0f);
    CHECK(body >= 0);
    CHECK(world.bodies[body].linearDamping == 0.0f);
    CHECK(world.bodies[body].angularDamping == 0.0f);
    CHECK(world.bodies[body].gravityScale == 1.0f);
    CHECK(world.bodies[body].flags == 0);
    CHECK(world.bodies[body].customMass == 0.0f);

    CHECK(world.setMassData(body, 3.0f, 2.0f, 2.0f, 2.0f) == 0);
    CHECK(world.applyForce(body, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f) == 0);
    CHECK(world.destroyBody(body) == 0);

    const int32_t reused = addFreeBox(world, 0.0f, 10.0f, 0.0f);
    CHECK(reused == body);
    CHECK(world.bodies[reused].customMass == 0.0f);
    CHECK(world.bodies[reused].gravityScale == 1.0f);
    CHECK(world.bodies[reused].flags == 0);
    CHECK(world.bodies[reused].forceAccum.x == 0.0f);
    CHECK(world.bodies[reused].torqueAccum.x == 0.0f);
    CHECK(world.bodies[reused].invMass == 1.0f);
}

}

int main() {
    testInitValidation();
    testSphereRest();
    testBoxStack();
    testDeterminism();
    testDestroyAndReuse();
    testContactManifolds();
    testInvalidInputs();
    testRestitution();
    testFriction();
    testRotatedBoxContact();
    testKinematicBodyCreation();
    testKinematicIgnoresGravity();
    testKinematicCollidesWithStatic();
    testKinematicPlatformCarriesBox();
    testDynamicCannotPushKinematic();
    testMultiShapeMassAccumulation();
    testParallelAxisInertiaGrows();
    testOffsetShapeRestsOnGround();
    testClearShapesResetsProperties();
    testShapeCapacityRejection();
    testStaticAndKinematicKeepZeroMass();
    testOffsetShapeAabbUnion();
    testMultiShapeDeterminism();
    testArenaExhaustion();
    testStressDeterminism();
    testDampingDecay();
    testGravityScale();
    testFrozenPositionAxes();
    testFrozenRotationAxes();
    testForceAppliedPerStep();
    testImpulseAtOffset();
    testMassOverride();
    testKinematicDampingKeepsMotion();
    testDynamicsDefaultsAndReset();

    if (g_failures == 0) {
        std::printf("PASS\n");
        return 0;
    }

    std::printf("FAILED %d\n", g_failures);
    return 1;
}
