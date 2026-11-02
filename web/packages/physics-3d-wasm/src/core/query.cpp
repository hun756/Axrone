#include "core/query.h"

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
    } else {
        Mat3 rotation;
        shapeWorldRotation(body.rotation, shape, rotation);
        struck = rayBoxShape(ray, center, rotation, shape.extents, local);
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
            const Vec3 offset = center - shapeCenter(position, body.rotation, shape);
            if (shape.kind == kShapeSphere) {
                const float reach = radius + shape.extents.x;
                if (lengthSq(offset) <= reach * reach) {
                    struck = true;
                    break;
                }
                continue;
            }
            Mat3 rotation;
            shapeWorldRotation(body.rotation, shape, rotation);
            const Vec3 local = rotation.transposeTransform(offset);
            const Vec3 closest(
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

}
