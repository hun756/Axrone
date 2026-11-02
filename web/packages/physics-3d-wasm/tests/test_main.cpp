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
    testArenaExhaustion();
    testStressDeterminism();

    if (g_failures == 0) {
        std::printf("PASS\n");
        return 0;
    }

    std::printf("FAILED %d\n", g_failures);
    return 1;
}
