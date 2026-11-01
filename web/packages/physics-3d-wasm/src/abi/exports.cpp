#include "axrone/physics_wasm.h"

#include "core/world.h"
#include "platform/wasm.h"
#include "runtime/arena.h"

using axp::Vec3;
using axp::World;

namespace {

axp::Arena g_arena;
World g_world;

#if !AXP_WASM
alignas(64) unsigned char g_hostMemory[64 * 1024 * 1024];
#endif

uint64_t packView(uintptr_t pointer, size_t bytes) {
    return (static_cast<uint64_t>(pointer) << 32) | static_cast<uint64_t>(bytes);
}

}

#if AXP_WASM
extern "C" char __heap_base;
#endif

AXP_EXPORT(worldInit) int32_t worldInit(int32_t maxBodies, int32_t maxContacts, float gx, float gy, float gz) {
    g_world.initialized = false;

#if AXP_WASM
    const uintptr_t heapBase = reinterpret_cast<uintptr_t>(&__heap_base);
    const size_t memoryBytes = __builtin_wasm_memory_size(0) * 65536;
    if (memoryBytes <= heapBase) {
        return -1;
    }
    g_arena.init(reinterpret_cast<unsigned char*>(heapBase), memoryBytes - heapBase);
#else
    g_arena.init(g_hostMemory, sizeof(g_hostMemory));
#endif

    return g_world.init(g_arena, maxBodies, maxContacts, Vec3(gx, gy, gz)) ? 0 : -1;
}

AXP_EXPORT(worldSetGravity) int32_t worldSetGravity(float gx, float gy, float gz) {
    return g_world.setGravity(gx, gy, gz);
}

AXP_EXPORT(bodyCreate) int32_t bodyCreate(int32_t type, float px, float py, float pz, float qx, float qy, float qz, float qw) {
    return g_world.createBody(type, px, py, pz, qx, qy, qz, qw);
}

AXP_EXPORT(bodyDestroy) int32_t bodyDestroy(int32_t id) {
    return g_world.destroyBody(id);
}

AXP_EXPORT(bodySetSphere) int32_t bodySetSphere(int32_t id, float radius, float density) {
    return g_world.setSphere(id, radius, density);
}

AXP_EXPORT(bodySetBox) int32_t bodySetBox(int32_t id, float hx, float hy, float hz, float density) {
    return g_world.setBox(id, hx, hy, hz, density);
}

AXP_EXPORT(bodySetTransform) int32_t bodySetTransform(int32_t id, float px, float py, float pz, float qx, float qy, float qz, float qw) {
    return g_world.setTransform(id, px, py, pz, qx, qy, qz, qw);
}

AXP_EXPORT(bodySetVelocity) int32_t bodySetVelocity(int32_t id, float vx, float vy, float vz) {
    return g_world.setVelocity(id, vx, vy, vz);
}

AXP_EXPORT(bodySetAngularVelocity) int32_t bodySetAngularVelocity(int32_t id, float vx, float vy, float vz) {
    return g_world.setAngularVelocity(id, vx, vy, vz);
}

AXP_EXPORT(bodySetMaterial) int32_t bodySetMaterial(int32_t id, float friction, float restitution) {
    return g_world.setMaterial(id, friction, restitution);
}

AXP_EXPORT(step) int32_t step(float dt, int32_t velocityIterations, int32_t positionIterations) {
    return g_world.step(dt, velocityIterations, positionIterations);
}

AXP_EXPORT(bodyCount) int32_t bodyCount(void) {
    return g_world.bodyCount();
}

AXP_EXPORT(contactCount) int32_t contactCount(void) {
    return g_world.getContactCount();
}

AXP_EXPORT(transformsPtr) uintptr_t transformsPtr(void) {
    return g_world.initialized ? reinterpret_cast<uintptr_t>(g_world.transforms) : 0;
}

AXP_EXPORT(velocitiesPtr) uintptr_t velocitiesPtr(void) {
    return g_world.initialized ? reinterpret_cast<uintptr_t>(g_world.velocities) : 0;
}

AXP_EXPORT(angularVelocitiesPtr) uintptr_t angularVelocitiesPtr(void) {
    return g_world.initialized ? reinterpret_cast<uintptr_t>(g_world.angularVelocities) : 0;
}

AXP_EXPORT(contactsPtr) uintptr_t contactsPtr(void) {
    return g_world.initialized ? reinterpret_cast<uintptr_t>(g_world.packedContacts) : 0;
}

AXP_EXPORT(worldFingerprint) uint64_t worldFingerprint(void) {
    return g_world.initialized ? g_world.fingerprint() : 0;
}

AXP_EXPORT(worldMemoryBytes) uintptr_t worldMemoryBytes(void) {
#if AXP_WASM
    return static_cast<uintptr_t>(__builtin_wasm_memory_size(0) * 65536);
#else
    return static_cast<uintptr_t>(g_arena.capacity());
#endif
}

AXP_EXPORT(world_init) int32_t world_init(int32_t maxBodies, int32_t maxContacts, float gx, float gy, float gz) {
    return worldInit(maxBodies, maxContacts, gx, gy, gz);
}

AXP_EXPORT(lib_init) int32_t lib_init(int32_t maxBodies, int32_t maxContacts, float gx, float gy, float gz) {
    return worldInit(maxBodies, maxContacts, gx, gy, gz);
}

AXP_EXPORT(world_reset) void world_reset(void) {
    g_world.reset();
}

AXP_EXPORT(worldReset) void worldReset(void) {
    world_reset();
}

AXP_EXPORT(lib_reset) void lib_reset(void) {
    world_reset();
}

AXP_EXPORT(world_set_gravity) int32_t world_set_gravity(float gx, float gy, float gz) {
    return worldSetGravity(gx, gy, gz);
}

AXP_EXPORT(body_create) int32_t body_create(int32_t type, float px, float py, float pz, float qx, float qy, float qz, float qw) {
    return bodyCreate(type, px, py, pz, qx, qy, qz, qw);
}

AXP_EXPORT(body_destroy) int32_t body_destroy(int32_t id) {
    return bodyDestroy(id);
}

AXP_EXPORT(body_set_sphere) int32_t body_set_sphere(int32_t id, float radius, float density) {
    return bodySetSphere(id, radius, density);
}

AXP_EXPORT(body_set_box) int32_t body_set_box(int32_t id, float hx, float hy, float hz, float density) {
    return bodySetBox(id, hx, hy, hz, density);
}

AXP_EXPORT(body_set_transform) int32_t body_set_transform(int32_t id, float px, float py, float pz, float qx, float qy, float qz, float qw) {
    return bodySetTransform(id, px, py, pz, qx, qy, qz, qw);
}

AXP_EXPORT(body_set_velocity) int32_t body_set_velocity(int32_t id, float vx, float vy, float vz) {
    return bodySetVelocity(id, vx, vy, vz);
}

AXP_EXPORT(body_set_angular_velocity) int32_t body_set_angular_velocity(int32_t id, float vx, float vy, float vz) {
    return bodySetAngularVelocity(id, vx, vy, vz);
}

AXP_EXPORT(body_set_material) int32_t body_set_material(int32_t id, float friction, float restitution) {
    return bodySetMaterial(id, friction, restitution);
}

AXP_EXPORT(body_count) int32_t body_count(void) {
    return bodyCount();
}

AXP_EXPORT(contact_count) int32_t contact_count(void) {
    return contactCount();
}

AXP_EXPORT(transforms_ptr) uintptr_t transforms_ptr(void) {
    return transformsPtr();
}

AXP_EXPORT(velocities_ptr) uintptr_t velocities_ptr(void) {
    return velocitiesPtr();
}

AXP_EXPORT(angular_velocities_ptr) uintptr_t angular_velocities_ptr(void) {
    return angularVelocitiesPtr();
}

AXP_EXPORT(contacts_ptr) uintptr_t contacts_ptr(void) {
    return contactsPtr();
}

AXP_EXPORT(world_fingerprint) uint64_t world_fingerprint(void) {
    return worldFingerprint();
}

AXP_EXPORT(world_memory_bytes) uintptr_t world_memory_bytes(void) {
    return worldMemoryBytes();
}

AXP_EXPORT(get_transforms_view) uint64_t get_transforms_view(void) {
    return g_world.initialized
        ? packView(reinterpret_cast<uintptr_t>(g_world.transforms), sizeof(axp::Transform) * static_cast<size_t>(g_world.highWater))
        : 0;
}

AXP_EXPORT(get_velocities_view) uint64_t get_velocities_view(void) {
    return g_world.initialized
        ? packView(reinterpret_cast<uintptr_t>(g_world.velocities), sizeof(axp::Vec3) * static_cast<size_t>(g_world.highWater))
        : 0;
}

AXP_EXPORT(get_angular_velocities_view) uint64_t get_angular_velocities_view(void) {
    return g_world.initialized
        ? packView(reinterpret_cast<uintptr_t>(g_world.angularVelocities), sizeof(axp::Vec3) * static_cast<size_t>(g_world.highWater))
        : 0;
}

AXP_EXPORT(get_contacts_view) uint64_t get_contacts_view(void) {
    return g_world.initialized
        ? packView(reinterpret_cast<uintptr_t>(g_world.packedContacts), sizeof(axp::PackedContact) * static_cast<size_t>(g_world.contactCount))
        : 0;
}

AXP_EXPORT(getTransformsView) uint64_t getTransformsView(void) {
    return get_transforms_view();
}

AXP_EXPORT(getVelocitiesView) uint64_t getVelocitiesView(void) {
    return get_velocities_view();
}

AXP_EXPORT(getAngularVelocitiesView) uint64_t getAngularVelocitiesView(void) {
    return get_angular_velocities_view();
}

AXP_EXPORT(getContactsView) uint64_t getContactsView(void) {
    return get_contacts_view();
}
