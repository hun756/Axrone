#pragma once

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

enum {
    AXRONE_PHYSICS_BODY_STATIC = 0,
    AXRONE_PHYSICS_BODY_DYNAMIC = 1,
    AXRONE_PHYSICS_BODY_KINEMATIC = 2
};

enum {
    AXRONE_PHYSICS_SHAPE_NONE = 0,
    AXRONE_PHYSICS_SHAPE_SPHERE = 1,
    AXRONE_PHYSICS_SHAPE_BOX = 2
};

int32_t worldInit(int32_t maxBodies, int32_t maxContacts, float gx, float gy, float gz);
int32_t world_init(int32_t maxBodies, int32_t maxContacts, float gx, float gy, float gz);
int32_t lib_init(int32_t maxBodies, int32_t maxContacts, float gx, float gy, float gz);
void world_reset(void);
void worldReset(void);
void lib_reset(void);
int32_t worldSetGravity(float gx, float gy, float gz);
int32_t world_set_gravity(float gx, float gy, float gz);
int32_t bodyCreate(int32_t type, float px, float py, float pz, float qx, float qy, float qz, float qw);
int32_t body_create(int32_t type, float px, float py, float pz, float qx, float qy, float qz, float qw);
int32_t bodyDestroy(int32_t id);
int32_t body_destroy(int32_t id);
int32_t bodySetSphere(int32_t id, float radius, float density);
int32_t body_set_sphere(int32_t id, float radius, float density);
int32_t bodySetBox(int32_t id, float hx, float hy, float hz, float density);
int32_t body_set_box(int32_t id, float hx, float hy, float hz, float density);
int32_t bodySetTransform(int32_t id, float px, float py, float pz, float qx, float qy, float qz, float qw);
int32_t body_set_transform(int32_t id, float px, float py, float pz, float qx, float qy, float qz, float qw);
int32_t bodySetVelocity(int32_t id, float vx, float vy, float vz);
int32_t body_set_velocity(int32_t id, float vx, float vy, float vz);
int32_t bodySetAngularVelocity(int32_t id, float vx, float vy, float vz);
int32_t body_set_angular_velocity(int32_t id, float vx, float vy, float vz);
int32_t bodySetMaterial(int32_t id, float friction, float restitution);
int32_t body_set_material(int32_t id, float friction, float restitution);
int32_t step(float dt, int32_t velocityIterations, int32_t positionIterations);
int32_t bodyCount(void);
int32_t body_count(void);
int32_t contactCount(void);
int32_t contact_count(void);
uintptr_t transformsPtr(void);
uintptr_t transforms_ptr(void);
uintptr_t velocitiesPtr(void);
uintptr_t velocities_ptr(void);
uintptr_t angularVelocitiesPtr(void);
uintptr_t angular_velocities_ptr(void);
uintptr_t contactsPtr(void);
uintptr_t contacts_ptr(void);
uint64_t worldFingerprint(void);
uint64_t world_fingerprint(void);
uintptr_t worldMemoryBytes(void);
uintptr_t world_memory_bytes(void);
uint64_t get_transforms_view(void);
uint64_t get_velocities_view(void);
uint64_t get_angular_velocities_view(void);
uint64_t get_contacts_view(void);
uint64_t getTransformsView(void);
uint64_t getVelocitiesView(void);
uint64_t getAngularVelocitiesView(void);
uint64_t getContactsView(void);

#ifdef __cplusplus
}
#endif
