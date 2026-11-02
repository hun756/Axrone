#include "core/query.h"

#include "core/polytope.h"

namespace axrone::physics {

namespace {

constexpr float kRayDirEps = 1e-12f;
constexpr float kSlabEps = 1e-12f;

struct QueryRay {
    Vec3 origin;
    Vec3 dir;
    float maxDistance;
};

struct LocalHit {
    float t;
    Vec3 normal;
};

bool makeQueryRay(Vec3 origin, Vec3 direction, float maxDistance, QueryRay& ray) {
    if (maxDistance <= 0.0f) {
        return false;
    }
    const float lenSq = lengthSq(direction);
    if (lenSq < kRayDirEps) {
        return false;
    }
    ray.origin = origin;
    ray.dir = direction * (1.0f / sqrtf(lenSq));
    ray.maxDistance = maxDistance;
    return true;
}

bool worldQueryable(const World& world) {
    return world.initialized && world.bodies != nullptr && world.transforms != nullptr;
}

float axisValue(const Vec3& value, int32_t axis) {
    if (axis == 0) {
        return value.x;
    }
    if (axis == 1) {
        return value.y;
    }
    return value.z;
}

Vec3 axisUnit(int32_t axis) {
    if (axis == 0) {
        return Vec3(1.0f, 0.0f, 0.0f);
    }
    if (axis == 1) {
        return Vec3(0.0f, 1.0f, 0.0f);
    }
    return Vec3(0.0f, 0.0f, 1.0f);
}

bool raySphereShape(const QueryRay& ray, const Vec3& center, float radius, LocalHit& out) {
    const Vec3 oc = ray.origin - center;
    const float b = dot(oc, ray.dir);
    const float c = dot(oc, oc) - radius * radius;
    const float disc = b * b - c;
    if (disc < 0.0f) {
        return false;
    }
    const float s = sqrtf(disc);
    const float tEnter = -b - s;
    const float tExit = -b + s;
    if (tExit < 0.0f) {
        return false;
    }
    if (tEnter <= 0.0f) {
        out.t = 0.0f;
        out.normal = -ray.dir;
        return true;
    }
    if (tEnter > ray.maxDistance) {
        return false;
    }
    out.t = tEnter;
    out.normal = normalize((ray.origin + ray.dir * tEnter) - center);
    return true;
}

bool rayBoxShape(
    const QueryRay& ray,
    const Vec3& center,
    const Mat3& rotation,
    const Vec3& half,
    LocalHit& out) {
    const Vec3 offset = ray.origin - center;
    const Vec3 ro = rotation.transposeTransform(offset);
    const Vec3 rd = rotation.transposeTransform(ray.dir);
    float tMin = -kBig;
    float tMax = kBig;
    int32_t entryAxis = 0;
    float entrySign = 1.0f;
    for (int32_t axis = 0; axis < 3; ++axis) {
        const float o = axisValue(ro, axis);
        const float d = axisValue(rd, axis);
        const float e = axisValue(half, axis);
        if (fabsf(d) < kSlabEps) {
            if (fabsf(o) > e) {
                return false;
            }
            continue;
        }
        const float inv = 1.0f / d;
        float t0 = (-e - o) * inv;
        float t1 = (e - o) * inv;
        if (t0 > t1) {
            const float swap = t0;
            t0 = t1;
            t1 = swap;
        }
        if (t0 > tMin) {
            tMin = t0;
            entryAxis = axis;
            entrySign = d < 0.0f ? 1.0f : -1.0f;
        }
        if (t1 < tMax) {
            tMax = t1;
        }
        if (tMin > tMax) {
            return false;
        }
    }
    if (tMax < 0.0f) {
        return false;
    }
    Vec3 localNormal;
    if (tMin <= 0.0f) {
        out.t = 0.0f;
        int32_t dominant = 0;
        if (fabsf(rd.y) > fabsf(rd.x)) {
            dominant = 1;
        }
        if (fabsf(rd.z) > fabsf(axisValue(rd, dominant))) {
            dominant = 2;
        }
        localNormal = axisUnit(dominant) * (axisValue(rd, dominant) < 0.0f ? 1.0f : -1.0f);
    } else {
        if (tMin > ray.maxDistance) {
            return false;
        }
        out.t = tMin;
        localNormal = axisUnit(entryAxis) * entrySign;
    }
    out.normal = normalize(rotation.transform(localNormal));
    return true;
}

bool rayCapsuleShape(
    const QueryRay& ray,
    const Vec3& center,
    const Mat3& rotation,
    const Vec3& extents,
    LocalHit& out) {
    const float radius = extents.x;
    const float halfSegment = extents.y;
    const Vec3 axis(
        rotation.m[1] * halfSegment,
        rotation.m[4] * halfSegment,
        rotation.m[7] * halfSegment
    );
    if (lengthSq(axis) <= kRayDirEps) {
        return raySphereShape(ray, center, radius, out);
    }

    const Vec3 p0 = center - axis;
    const Vec3 p1 = center + axis;
    const Vec3 span = p1 - p0;
    const float spanLength = 2.0f * halfSegment;
    const float spanSq = lengthSq(span);
    const Vec3 oc = ray.origin - p0;
    const Vec3 u = span * (1.0f / sqrtf(spanSq));

    const float anchorParam = clampf(dot(oc, span) / spanSq, 0.0f, 1.0f);
    const Vec3 anchor = p0 + span * anchorParam;
    const Vec3 originRadial = ray.origin - anchor;
    if (lengthSq(originRadial) <= radius * radius) {
        out.t = 0.0f;
        out.normal = -ray.dir;
        return true;
    }

    bool found = false;
    LocalHit best;

    const float projO = dot(oc, u);
    const Vec3 oRad = oc - u * projO;
    const float projD = dot(ray.dir, u);
    const Vec3 dRad = ray.dir - u * projD;
    const float A = lengthSq(dRad);
    const float B = 2.0f * dot(oRad, dRad);
    const float C = lengthSq(oRad) - radius * radius;
    if (A > kSlabEps) {
        const float disc = B * B - 4.0f * A * C;
        if (disc >= 0.0f) {
            const float tEnter = (-B - sqrtf(disc)) * (1.0f / (2.0f * A));
            if (tEnter > 0.0f && tEnter <= ray.maxDistance) {
                const float along = projO + tEnter * projD;
                if (along >= 0.0f && along <= spanLength) {
                    best.t = tEnter;
                    best.normal = normalize(oRad + dRad * tEnter);
                    found = true;
                }
            }
        }
    }

    LocalHit candidate;
    if (raySphereShape(ray, p0, radius, candidate)) {
        if (!found || candidate.t < best.t) {
            best = candidate;
            found = true;
        }
    }
    if (raySphereShape(ray, p1, radius, candidate)) {
        if (!found || candidate.t < best.t) {
            best = candidate;
            found = true;
        }
    }

    if (!found) {
        return false;
    }
    out = best;
    return true;
}

bool rayHullShape(
    const QueryRay& ray,
    const Vec3& center,
    const Mat3& rotation,
    const Hull& hull,
    LocalHit& out) {
    const Vec3 offset = ray.origin - center;
    const Vec3 ro = rotation.transposeTransform(offset);
    const Vec3 rd = rotation.transposeTransform(ray.dir);

    float tMin = -kBig;
    float tMax = kBig;
    int32_t entryFace = 0;

    for (int32_t f = 0; f < hull.faceCount; ++f) {
        const Vec3& n = hull.faceNormals[f];
        const float dn = dot(n, rd);
        const float o = dot(n, ro);
        const float h = hull.faceOffsets[f];
        if (fabsf(dn) < kSlabEps) {
            if (o > h) {
                return false;
            }
            continue;
        }
        const float tSlab = (h - o) / dn;
        if (dn < 0.0f) {
            if (tSlab > tMin) {
                tMin = tSlab;
                entryFace = f;
            }
        } else if (tSlab < tMax) {
            tMax = tSlab;
        }
        if (tMin > tMax) {
            return false;
        }
    }

    if (tMax < 0.0f) {
        return false;
    }
    if (tMin <= 0.0f) {
        out.t = 0.0f;
        out.normal = -ray.dir;
        return true;
    }
    if (tMin > ray.maxDistance) {
        return false;
    }

    out.t = tMin;
    out.normal = normalize(rotation.transform(hull.faceNormals[entryFace]));
    return true;
}

bool rayCylinderShape(
    const QueryRay& ray,
    const Vec3& center,
    const Mat3& rotation,
    const Vec3& extents,
    LocalHit& out) {
    Mat3 identity;
    identity.setIdentity();
    Hull hull;
    buildPrismHull(Vec3(), identity, extents.x, extents.y, hull);
    return rayHullShape(ray, center, rotation, hull, out);
}

bool rayConeShape(
    const QueryRay& ray,
    const Vec3& center,
    const Mat3& rotation,
    const Vec3& extents,
    LocalHit& out) {
    Mat3 identity;
    identity.setIdentity();
    Hull hull;
    buildConeHull(Vec3(), identity, extents.x, extents.y, hull);
    return rayHullShape(ray, center, rotation, hull, out);
}

void hullLocalDistance(const Mat3& rotation, const Vec3& offset, int32_t kind, const Vec3& extents, float& distance) {
    Mat3 identity;
    identity.setIdentity();
    Hull hull;
    if (kind == kShapeCone) {
        buildConeHull(Vec3(), identity, extents.x, extents.y, hull);
    } else {
        buildPrismHull(Vec3(), identity, extents.x, extents.y, hull);
    }
    const Vec3 local = rotation.transposeTransform(offset);
    Vec3 closest;
    float signedDistance;
    Vec3 hint;
    hullClosestPoint(hull, local, closest, signedDistance, hint);
    distance = signedDistance;
}

bool testShape(
    const World& world,
    int32_t id,
    int32_t slot,
    const Body& body,
    const Vec3& position,
    const Shape& shape,
    const QueryRay& ray,
    RayHit& out) {
    const Vec3 center = shapeCenter(position, body.rotation, shape);
    LocalHit local;
    bool struck = false;
    if (shape.kind == kShapeSphere) {
        struck = raySphereShape(ray, center, shape.extents.x, local);
    } else if (shape.kind == kShapeBox) {
        Mat3 rotation;
        shapeWorldRotation(body.rotation, shape, rotation);
        struck = rayBoxShape(ray, center, rotation, shape.extents, local);
    } else if (shape.kind == kShapeCapsule) {
        Mat3 rotation;
        shapeWorldRotation(body.rotation, shape, rotation);
        struck = rayCapsuleShape(ray, center, rotation, shape.extents, local);
    } else if (shape.kind == kShapeCone) {
        Mat3 rotation;
        shapeWorldRotation(body.rotation, shape, rotation);
        struck = rayConeShape(ray, center, rotation, shape.extents, local);
    } else {
        Mat3 rotation;
        shapeWorldRotation(body.rotation, shape, rotation);
        struck = rayCylinderShape(ray, center, rotation, shape.extents, local);
    }
    if (!struck) {
        return false;
    }
    const Vec3 point = ray.origin + ray.dir * local.t;
    out.body = id;
    out.shape = world.shapeMakeId(id, slot);
    out.t = local.t;
    out.px = point.x;
    out.py = point.y;
    out.pz = point.z;
    out.nx = local.normal.x;
    out.ny = local.normal.y;
    out.nz = local.normal.z;
    out.reserved = 0.0f;
    return true;
}

bool hitBefore(const RayHit& left, const RayHit& right) {
    if (left.t != right.t) {
        return left.t < right.t;
    }
    if (left.body != right.body) {
        return left.body < right.body;
    }
    return left.shape < right.shape;
}

bool identicalShapeHit(const RayHit& left, const RayHit& right) {
    return left.body == right.body && left.shape == right.shape && left.t == right.t;
}

bool hasIdenticalShapeHit(const RayHit* hits, int32_t count, const RayHit& candidate) {
    for (int32_t index = 0; index < count; ++index) {
        if (identicalShapeHit(hits[index], candidate)) {
            return true;
        }
    }
    return false;
}

void insertSorted(RayHit* hits, int32_t capacity, int32_t& count, const RayHit& candidate) {
    int32_t slot = count;
    while (slot > 0 && hitBefore(candidate, hits[slot - 1])) {
        --slot;
    }
    if (count < capacity) {
        for (int32_t move = count; move > slot; --move) {
            hits[move] = hits[move - 1];
        }
        hits[slot] = candidate;
        ++count;
    } else if (slot < capacity) {
        for (int32_t move = capacity - 1; move > slot; --move) {
            hits[move] = hits[move - 1];
        }
        hits[slot] = candidate;
    }
}

}

int32_t raycastAll(
    const World& world,
    Vec3 origin,
    Vec3 direction,
    float maxDistance,
    RayHit* hits,
    int32_t capacity) {
    return raycastAll(world, origin, direction, maxDistance, 0, 0, hits, capacity);
}

int32_t raycastAll(
    const World& world,
    Vec3 origin,
    Vec3 direction,
    float maxDistance,
    int32_t categoryBits,
    int32_t maskBits,
    RayHit* hits,
    int32_t capacity) {
    if (capacity <= 0 || hits == nullptr || !worldQueryable(world)) {
        return 0;
    }
    QueryRay ray;
    if (!makeQueryRay(origin, direction, maxDistance, ray)) {
        return 0;
    }
    int32_t count = 0;
    for (int32_t id = 0; id < world.highWater; ++id) {
        const Body& body = world.bodies[id];
        if (!bodyQueryable(body)) {
            continue;
        }
        const Vec3 position = transformPosition(world.transforms[id]);
        const int32_t slots = shapeSlots(body);
        for (int32_t slot = 0; slot < slots; ++slot) {
            const Shape& shape = body.shapes[slot];
            if (!shapeValid(shape)) {
                continue;
            }
            if (!queryShapePassesFilter(shape, categoryBits, maskBits)) {
                continue;
            }
            RayHit candidate;
            if (!testShape(world, id, slot, body, position, shape, ray, candidate)) {
                continue;
            }
            if (hasIdenticalShapeHit(hits, count, candidate)) {
                continue;
            }
            insertSorted(hits, capacity, count, candidate);
        }
    }
    return count;
}

bool raycastClosest(
    const World& world,
    Vec3 origin,
    Vec3 direction,
    float maxDistance,
    RayHit& hit) {
    return raycastClosest(world, origin, direction, maxDistance, 0, 0, hit);
}

bool raycastClosest(
    const World& world,
    Vec3 origin,
    Vec3 direction,
    float maxDistance,
    int32_t categoryBits,
    int32_t maskBits,
    RayHit& hit) {
    if (!worldQueryable(world)) {
        return false;
    }
    QueryRay ray;
    if (!makeQueryRay(origin, direction, maxDistance, ray)) {
        return false;
    }
    bool found = false;
    for (int32_t id = 0; id < world.highWater; ++id) {
        const Body& body = world.bodies[id];
        if (!bodyQueryable(body)) {
            continue;
        }
        const Vec3 position = transformPosition(world.transforms[id]);
        const int32_t slots = shapeSlots(body);
        for (int32_t slot = 0; slot < slots; ++slot) {
            const Shape& shape = body.shapes[slot];
            if (!shapeValid(shape)) {
                continue;
            }
            if (!queryShapePassesFilter(shape, categoryBits, maskBits)) {
                continue;
            }
            RayHit candidate;
            if (!testShape(world, id, slot, body, position, shape, ray, candidate)) {
                continue;
            }
            if (!found || hitBefore(candidate, hit)) {
                hit = candidate;
                found = true;
            }
        }
    }
    return found;
}

int32_t overlapSphere(
    const World& world,
    Vec3 center,
    float radius,
    int32_t* bodies,
    int32_t capacity) {
    return overlapSphere(world, center, radius, 0, 0, bodies, capacity);
}

int32_t overlapSphere(
    const World& world,
    Vec3 center,
    float radius,
    int32_t categoryBits,
    int32_t maskBits,
    int32_t* bodies,
    int32_t capacity) {
    if (capacity <= 0 || bodies == nullptr || !worldQueryable(world) || radius < 0.0f) {
        return 0;
    }
    const float radiusSq = radius * radius;
    int32_t count = 0;
    for (int32_t id = 0; id < world.highWater; ++id) {
        const Body& body = world.bodies[id];
        if (!bodyQueryable(body)) {
            continue;
        }
        const Vec3 position = transformPosition(world.transforms[id]);
        const int32_t slots = shapeSlots(body);
        bool struck = false;
        for (int32_t slot = 0; slot < slots; ++slot) {
            const Shape& shape = body.shapes[slot];
            if (!shapeValid(shape)) {
                continue;
            }
            if (!queryShapePassesFilter(shape, categoryBits, maskBits)) {
                continue;
            }
            const Vec3 offset = center - shapeCenter(position, body.rotation, shape);
            if (shape.kind == kShapeSphere) {
                const float reach = radius + shape.extents.x;
                if (lengthSq(offset) <= reach * reach) {
                    struck = true;
                    break;
                }
                continue;
            }
            if (shape.kind == kShapeCapsule) {
                Mat3 rotation;
                shapeWorldRotation(body.rotation, shape, rotation);
                const Vec3 axis(
                    rotation.m[1] * shape.extents.y,
                    rotation.m[4] * shape.extents.y,
                    rotation.m[7] * shape.extents.y
                );
                const Vec3 span = axis * 2.0f;
                float distanceSq;
                if (lengthSq(span) <= kRayDirEps) {
                    distanceSq = lengthSq(offset);
                } else {
                    const float anchorParam = clampf(dot(offset + axis, span) / lengthSq(span), 0.0f, 1.0f);
                    const Vec3 anchor = (axis * -1.0f) + span * anchorParam;
                    distanceSq = lengthSq(offset - anchor);
                }
                const float reach = radius + shape.extents.x;
                if (distanceSq <= reach * reach) {
                    struck = true;
                    break;
                }
                continue;
            }
            Mat3 rotation;
            shapeWorldRotation(body.rotation, shape, rotation);
            const Vec3 local = rotation.transposeTransform(offset);
            Vec3 closest;
            if (shape.kind == kShapeCylinder || shape.kind == kShapeCone) {
                float distance = 0.0f;
                hullLocalDistance(rotation, offset, shape.kind, shape.extents, distance);
                if (distance <= radius) {
                    struck = true;
                    break;
                }
                continue;
            }
            closest = Vec3(
                clampf(local.x, -shape.extents.x, shape.extents.x),
                clampf(local.y, -shape.extents.y, shape.extents.y),
                clampf(local.z, -shape.extents.z, shape.extents.z)
            );
            if (lengthSq(local - closest) <= radiusSq) {
                struck = true;
                break;
            }
        }
        if (struck) {
            bodies[count] = id;
            ++count;
            if (count >= capacity) {
                break;
            }
        }
    }
    return count;
}

namespace {

bool boundsFinite(const AABB& bounds) {
    return isFinite(bounds.minX) && isFinite(bounds.minY) && isFinite(bounds.minZ) &&
           isFinite(bounds.maxX) && isFinite(bounds.maxY) && isFinite(bounds.maxZ);
}

}

int32_t overlapBoxAll(
    const World& world,
    const AABB& bounds,
    int32_t* bodies,
    int32_t capacity) {
    if (capacity <= 0 || bodies == nullptr || !worldQueryable(world)) {
        return 0;
    }
    if (!boundsFinite(bounds) || bounds.minX > bounds.maxX ||
        bounds.minY > bounds.maxY || bounds.minZ > bounds.maxZ) {
        return 0;
    }
    int32_t count = 0;
    for (int32_t id = 0; id < world.highWater; ++id) {
        const Body& body = world.bodies[id];
        if (!bodyQueryable(body)) {
            continue;
        }
        const AABB& aabb = body.aabb;
        if (bounds.minX > aabb.maxX || bounds.maxX < aabb.minX) {
            continue;
        }
        if (bounds.minY > aabb.maxY || bounds.maxY < aabb.minY) {
            continue;
        }
        if (bounds.minZ > aabb.maxZ || bounds.maxZ < aabb.minZ) {
            continue;
        }
        bodies[count] = id;
        ++count;
        if (count >= capacity) {
            break;
        }
    }
    return count;
}

int32_t queryPointAll(
    const World& world,
    Vec3 point,
    int32_t* bodies,
    int32_t capacity) {
    const AABB bounds = { point.x, point.y, point.z, point.x, point.y, point.z };
    return overlapBoxAll(world, bounds, bodies, capacity);
}

namespace {

float reciprocalDirectionLength(Vec3 direction) {
    const float lenSq = lengthSq(direction);
    if (lenSq < kRayDirEps) {
        return 0.0f;
    }
    return 1.0f / sqrtf(lenSq);
}

}

void applyFractionConvention(RayHit* hits, int32_t count, Vec3 direction) {
    const float inv = reciprocalDirectionLength(direction);
    if (inv == 0.0f) {
        return;
    }
    for (int32_t index = 0; index < count; ++index) {
        hits[index].t *= inv;
    }
}

void applyFractionConvention(RayHit& hit, Vec3 direction) {
    const float inv = reciprocalDirectionLength(direction);
    if (inv == 0.0f) {
        return;
    }
    hit.t *= inv;
}

namespace {

int32_t clampIterationCount(int32_t value) {
    if (value < 0) {
        return 0;
    }
    return value > kMaxIterations ? kMaxIterations : value;
}

}

void collectStepStatistics(
    const World& world,
    int32_t velocityIterations,
    int32_t positionIterations,
    StepStatistics& out) {
    out.broadphasePairsTested = 0.0f;
    out.shapePairsTested = 0.0f;
    out.contactsGenerated = 0.0f;
    out.sensorContacts = 0.0f;
    out.velocityIterations = static_cast<float>(clampIterationCount(velocityIterations));
    out.positionIterations = static_cast<float>(clampIterationCount(positionIterations));
    out.sleepingBodies = 0.0f;
    out.liveBodies = 0.0f;

    if (!worldQueryable(world)) {
        return;
    }

    int64_t broadphasePairs = 0;
    int64_t shapePairs = 0;
    for (int32_t a = 0; a < world.highWater; ++a) {
        const Body& bodyA = world.bodies[a];
        if (!bodyQueryable(bodyA)) {
            continue;
        }
        const AABB& boundsA = bodyA.aabb;
        const bool aDynamic = bodyA.state == kBodyDynamic;
        const bool aKinematic = bodyA.state == kBodyKinematic;
        const int32_t slotsA = shapeSlots(bodyA);

        for (int32_t b = a + 1; b < world.highWater; ++b) {
            const Body& bodyB = world.bodies[b];
            if (!bodyQueryable(bodyB)) {
                continue;
            }
            const AABB& boundsB = bodyB.aabb;
            if (boundsB.minX > boundsA.maxX || boundsA.minX > boundsB.maxX) {
                continue;
            }
            if (boundsB.minY > boundsA.maxY || boundsA.minY > boundsB.maxY) {
                continue;
            }
            if (boundsB.minZ > boundsA.maxZ || boundsA.minZ > boundsB.maxZ) {
                continue;
            }
            const bool bKinematic = bodyB.state == kBodyKinematic;
            if (!aDynamic && bodyB.state != kBodyDynamic && aKinematic == bKinematic) {
                continue;
            }

            ++broadphasePairs;

            const int32_t slotsB = shapeSlots(bodyB);
            for (int32_t slotA = 0; slotA < slotsA; ++slotA) {
                if (!shapeValid(bodyA.shapes[slotA])) {
                    continue;
                }
                for (int32_t slotB = 0; slotB < slotsB; ++slotB) {
                    if (!shapeValid(bodyB.shapes[slotB])) {
                        continue;
                    }
                    if (filtersCollide(bodyA.shapes[slotA], bodyB.shapes[slotB])) {
                        ++shapePairs;
                    }
                }
            }
        }
    }

    int32_t sleeping = 0;
    for (int32_t id = 0; id < world.highWater; ++id) {
        const Body& body = world.bodies[id];
        if (body.state == kBodyFree) {
            continue;
        }
        if (body.state == kBodyDynamic && body.awake == 0) {
            ++sleeping;
        }
    }

    int32_t sensors = 0;
    if (world.contacts != nullptr) {
        for (int32_t k = 0; k < world.contactCount; ++k) {
            if (world.contacts[k].sensor != 0) {
                ++sensors;
            }
        }
    }

    out.broadphasePairsTested = static_cast<float>(broadphasePairs);
    out.shapePairsTested = static_cast<float>(shapePairs);
    out.contactsGenerated = static_cast<float>(world.contactCount);
    out.sensorContacts = static_cast<float>(sensors);
    out.sleepingBodies = static_cast<float>(sleeping);
    out.liveBodies = static_cast<float>(world.liveCount);
}

}
