#include "core/narrowphase.h"

#include "platform/math.h"

namespace axrone::physics {

namespace {

Vec3 worldPosition(const World& world, int32_t id) {
    return transformPosition(world.transforms[id]);
}

float boxProjection(const Vec3 axes[3], const Vec3& half, const Vec3& normal) {
    return half.x * fabsf(dot(axes[0], normal)) +
           half.y * fabsf(dot(axes[1], normal)) +
           half.z * fabsf(dot(axes[2], normal));
}

void addContact(
    World& world,
    int32_t a,
    int32_t b,
    Vec3 normal,
    float pen,
    Vec3 point,
    Vec3 surfA,
    Vec3 surfB,
    int32_t normalMode,
    Vec3 localNormal,
    int32_t feature
) {
    if (world.contactCount >= world.maxContacts) {
        return;
    }

    if (a > b) {
        int32_t swap = a;
        a = b;
        b = swap;
        normal = -normal;
        Vec3 surfaceSwap = surfA;
        surfA = surfB;
        surfB = surfaceSwap;
        if (normalMode == 1) {
            normalMode = 2;
        } else if (normalMode == 2) {
            normalMode = 1;
        }
    }

    const int32_t index = world.contactCount++;
    Contact& contact = world.contacts[index];

    contact.a = a;
    contact.b = b;
    contact.normal = normal;
    contact.pen = pen;
    contact.normalMode = normalMode;
    contact.localNormal = localNormal;
    contact.t1 = Vec3();
    contact.t2 = Vec3();
    contact.ra = Vec3();
    contact.rb = Vec3();
    contact.massN = 0.0f;
    contact.massT1 = 0.0f;
    contact.massT2 = 0.0f;
    contact.impN = 0.0f;
    contact.impT1 = 0.0f;
    contact.impT2 = 0.0f;
    contact.velocityBias = 0.0f;

    const float frictionA = world.bodies[a].friction;
    const float frictionB = world.bodies[b].friction;
    contact.friction = sqrtf(frictionA * frictionB);
    contact.restitution = world.bodies[a].restitution > world.bodies[b].restitution
        ? world.bodies[a].restitution
        : world.bodies[b].restitution;

    const int32_t cached = world.cacheLookup(a, b, feature);
    if (cached >= 0) {
        const CacheEntry& entry = world.cachePrev[cached];
        contact.impN = entry.impN;
        contact.impT1 = entry.impT1;
        contact.impT2 = entry.impT2;
    }

    contact.cacheSlot = world.cacheStore(a, b, feature, contact.impN, contact.impT1, contact.impT2);

    const Vec3 posA = worldPosition(world, a);
    const Vec3 posB = worldPosition(world, b);
    contact.ra = point - posA;
    contact.rb = point - posB;
    contact.localA = world.bodies[a].rotation.transposeTransform(surfA - posA);
    contact.localB = world.bodies[b].rotation.transposeTransform(surfB - posB);
}

void collideSphereSphere(World& world, int32_t a, int32_t b) {
    const Vec3 posA = worldPosition(world, a);
    const Vec3 posB = worldPosition(world, b);
    const Vec3 delta = posB - posA;

    const float radiusA = world.bodies[a].extents.x;
    const float radiusB = world.bodies[b].extents.x;
    const float radiusSum = radiusA + radiusB;
    const float distanceSq = lengthSq(delta);
    if (distanceSq > radiusSum * radiusSum) {
        return;
    }

    float distance = 0.0f;
    Vec3 normal(1.0f, 0.0f, 0.0f);
    if (distanceSq > kEps) {
        distance = sqrtf(distanceSq);
        normal = delta * (1.0f / distance);
    }

    const float pen = radiusSum - distance;
    const Vec3 point = posA + normal * (radiusA - pen * 0.5f);
    const Vec3 surfA = posA + normal * radiusA;
    const Vec3 surfB = posB - normal * radiusB;

    addContact(world, a, b, normal, pen, point, surfA, surfB, 0, Vec3(), kKindSphereSphere * 16);
}

void collideSphereBox(World& world, int32_t sphere, int32_t box, bool sphereIsA) {
    const Vec3 spherePos = worldPosition(world, sphere);
    const Vec3 boxPos = worldPosition(world, box);
    const Vec3 delta = spherePos - boxPos;

    const Vec3 localSphere = world.bodies[box].rotation.transposeTransform(delta);
    const Vec3 half = world.bodies[box].extents;

    const Vec3 closest(
        localSphere.x > half.x ? half.x : (localSphere.x < -half.x ? -half.x : localSphere.x),
        localSphere.y > half.y ? half.y : (localSphere.y < -half.y ? -half.y : localSphere.y),
        localSphere.z > half.z ? half.z : (localSphere.z < -half.z ? -half.z : localSphere.z)
    );

    const Vec3 difference = localSphere - closest;
    const float distanceSq = lengthSq(difference);
    const float radius = world.bodies[sphere].extents.x;
    if (distanceSq > radius * radius) {
        return;
    }

    Vec3 localNormal(0.0f, 0.0f, 0.0f);
    float pen = 0.0f;
    Vec3 localPoint = closest;

    if (distanceSq > kEps) {
        const float distance = sqrtf(distanceSq);
        localNormal = difference * (1.0f / distance);
        pen = radius - distance;
    } else {
        const float faceX = half.x - fabsf(localSphere.x);
        const float faceY = half.y - fabsf(localSphere.y);
        const float faceZ = half.z - fabsf(localSphere.z);

        if (faceX <= faceY && faceX <= faceZ) {
            localNormal.x = localSphere.x >= 0.0f ? 1.0f : -1.0f;
            localPoint.x = localNormal.x * half.x;
            pen = radius + faceX;
        } else if (faceY <= faceZ) {
            localNormal.y = localSphere.y >= 0.0f ? 1.0f : -1.0f;
            localPoint.y = localNormal.y * half.y;
            pen = radius + faceY;
        } else {
            localNormal.z = localSphere.z >= 0.0f ? 1.0f : -1.0f;
            localPoint.z = localNormal.z * half.z;
            pen = radius + faceZ;
        }
    }

    const Vec3 worldNormal = world.bodies[box].rotation.transform(localNormal);
    const Vec3 point = boxPos + world.bodies[box].rotation.transform(localPoint);
    const Vec3 sphereSurface = spherePos - worldNormal * radius;

    if (sphereIsA) {
        addContact(world, sphere, box, -worldNormal, pen, point, sphereSurface, point, 0, Vec3(), kKindSphereBox * 16);
    } else {
        addContact(world, box, sphere, worldNormal, pen, point, point, sphereSurface, 0, Vec3(), kKindSphereBox * 16);
    }
}

void buildFace(const World& world, int32_t id, const Vec3 axes[3], int32_t axisIndex, float sign, Vec3 out[4]) {
    const Vec3 half = world.bodies[id].extents;
    const int32_t uIndex = (axisIndex + 1) % 3;
    const int32_t vIndex = (axisIndex + 2) % 3;

    const Vec3 u = axes[uIndex];
    const Vec3 v = axes[vIndex];
    const float hu = uIndex == 0 ? half.x : (uIndex == 1 ? half.y : half.z);
    const float hv = vIndex == 0 ? half.x : (vIndex == 1 ? half.y : half.z);
    const float ha = axisIndex == 0 ? half.x : (axisIndex == 1 ? half.y : half.z);

    const Vec3 center = worldPosition(world, id) + axes[axisIndex] * (sign * ha);

    out[0] = center + u * hu + v * hv;
    out[1] = center - u * hu + v * hv;
    out[2] = center - u * hu - v * hv;
    out[3] = center + u * hu - v * hv;
}

int32_t clipPlane(const Vec3 input[], int32_t count, Vec3 output[], const Vec3& reference, const Vec3& normal, float offset) {
    if (count == 0) {
        return 0;
    }

    int32_t outputCount = 0;
    Vec3 previous = input[count - 1];
    float previousDistance = dot(previous - reference, normal) - offset;

    for (int32_t i = 0; i < count; ++i) {
        const Vec3 current = input[i];
        const float currentDistance = dot(current - reference, normal) - offset;
        const bool previousInside = previousDistance <= 0.0f;
        const bool currentInside = currentDistance <= 0.0f;

        if (previousInside != currentInside) {
            const float t = previousDistance / (previousDistance - currentDistance);
            output[outputCount++] = previous + (current - previous) * t;
        }
        if (currentInside) {
            output[outputCount++] = current;
        }

        previous = current;
        previousDistance = currentDistance;
    }

    return outputCount;
}

void addFaceContacts(World& world, int32_t a, int32_t b, const Vec3& normal, bool referenceIsA, int32_t axisIndex) {
    const int32_t reference = referenceIsA ? a : b;
    const int32_t incident = referenceIsA ? b : a;
    const Vec3* referenceAxes = referenceIsA ? world.axesA : world.axesB;
    const Vec3* incidentAxes = referenceIsA ? world.axesB : world.axesA;

    const Vec3 referenceNormal = referenceIsA ? normal : -normal;
    const float sign = dot(referenceAxes[axisIndex], referenceNormal) >= 0.0f ? 1.0f : -1.0f;

    const Vec3 referenceHalf = world.bodies[reference].extents;
    const float referenceHalfExtent = axisIndex == 0 ? referenceHalf.x : (axisIndex == 1 ? referenceHalf.y : referenceHalf.z);

    const Vec3 referencePosition = worldPosition(world, reference);
    const Vec3 faceCenter = referencePosition + referenceAxes[axisIndex] * (sign * referenceHalfExtent);

    const int32_t uIndex = (axisIndex + 1) % 3;
    const int32_t vIndex = (axisIndex + 2) % 3;
    const Vec3 u = referenceAxes[uIndex];
    const Vec3 v = referenceAxes[vIndex];
    const float hu = uIndex == 0 ? referenceHalf.x : (uIndex == 1 ? referenceHalf.y : referenceHalf.z);
    const float hv = vIndex == 0 ? referenceHalf.x : (vIndex == 1 ? referenceHalf.y : referenceHalf.z);

    float bestDot = -1.0f;
    int32_t incidentAxis = 0;
    float incidentSign = 1.0f;

    for (int32_t k = 0; k < 3; ++k) {
        const float d = dot(incidentAxes[k], referenceNormal);
        const float absolute = fabsf(d);
        if (absolute > bestDot) {
            bestDot = absolute;
            incidentAxis = k;
            incidentSign = d >= 0.0f ? -1.0f : 1.0f;
        }
    }

    buildFace(world, incident, incidentAxes, incidentAxis, incidentSign, world.faceVertices);

    int32_t count = clipPlane(world.faceVertices, 4, world.clipA, referencePosition, u, hu);
    count = clipPlane(world.clipA, count, world.clipB, referencePosition, -u, hu);
    count = clipPlane(world.clipB, count, world.clipA, referencePosition, v, hv);
    count = clipPlane(world.clipA, count, world.clipB, referencePosition, -v, hv);

    const int32_t start = world.contactCount;
    int32_t added = 0;
    const Vec3 localNormal = world.bodies[reference].rotation.transposeTransform(referenceNormal);

    for (int32_t i = 0; i < count; ++i) {
        const Vec3 point = world.clipB[i];
        const float separation = dot(point - faceCenter, referenceNormal);
        if (separation > 0.0f) {
            continue;
        }

        bool duplicate = false;
        for (int32_t j = start; j < world.contactCount; ++j) {
            const Contact& existing = world.contacts[j];
            const Vec3 existingPoint = transformPosition(world.transforms[existing.a]) + existing.ra;
            if (lengthSq(point - existingPoint) < kCrossEps) {
                duplicate = true;
                break;
            }
        }
        if (duplicate) {
            continue;
        }

        const Vec3 referencePoint = point - referenceNormal * separation;
        if (referenceIsA) {
            addContact(world, a, b, normal, -separation, point, referencePoint, point, 1, localNormal, kKindBoxFace * 16 + added);
        } else {
            addContact(world, a, b, normal, -separation, point, point, referencePoint, 2, localNormal, kKindBoxFace * 16 + added);
        }

        if (++added >= 8) {
            break;
        }
    }
}

void addEdgeContact(World& world, int32_t a, int32_t b, const Vec3& normal, int32_t axisA, int32_t axisB, float pen) {
    const Vec3 halfA = world.bodies[a].extents;
    Vec3 centerA = worldPosition(world, a);
    for (int32_t k = 0; k < 3; ++k) {
        if (k == axisA) {
            continue;
        }
        const float d = dot(world.axesA[k], normal);
        const float sign = d >= 0.0f ? 1.0f : -1.0f;
        const float half = k == 0 ? halfA.x : (k == 1 ? halfA.y : halfA.z);
        centerA += world.axesA[k] * (sign * half);
    }
    const Vec3 directionA = world.axesA[axisA];
    const float halfExtentA = axisA == 0 ? halfA.x : (axisA == 1 ? halfA.y : halfA.z);

    const Vec3 halfB = world.bodies[b].extents;
    Vec3 centerB = worldPosition(world, b);
    for (int32_t k = 0; k < 3; ++k) {
        if (k == axisB) {
            continue;
        }
        const float d = dot(world.axesB[k], normal);
        const float sign = d <= 0.0f ? 1.0f : -1.0f;
        const float half = k == 0 ? halfB.x : (k == 1 ? halfB.y : halfB.z);
        centerB += world.axesB[k] * (sign * half);
    }
    const Vec3 directionB = world.axesB[axisB];
    const float halfExtentB = axisB == 0 ? halfB.x : (axisB == 1 ? halfB.y : halfB.z);

    const Vec3 offset = centerA - centerB;
    const float dotDirections = dot(directionA, directionB);
    const float dotA = dot(directionA, offset);
    const float dotB = dot(directionB, offset);
    const float denominator = 1.0f - dotDirections * dotDirections;

    float s = denominator > kEps ? (dotDirections * dotB - dotA) / denominator : 0.0f;
    s = clampf(s, -halfExtentA, halfExtentA);

    float t = dotDirections * s + dotB;
    t = clampf(t, -halfExtentB, halfExtentB);

    s = dotDirections * t - dotA;
    s = clampf(s, -halfExtentA, halfExtentA);

    const Vec3 pointA = centerA + directionA * s;
    const Vec3 pointB = centerB + directionB * t;

    addContact(world, a, b, normal, pen, (pointA + pointB) * 0.5f, pointA, pointB, 0, Vec3(), kKindBoxEdge * 16);
}

void collideBoxBox(World& world, int32_t a, int32_t b) {
    const Mat3& rotationA = world.bodies[a].rotation;
    world.axesA[0] = Vec3(rotationA.m[0], rotationA.m[3], rotationA.m[6]);
    world.axesA[1] = Vec3(rotationA.m[1], rotationA.m[4], rotationA.m[7]);
    world.axesA[2] = Vec3(rotationA.m[2], rotationA.m[5], rotationA.m[8]);

    const Mat3& rotationB = world.bodies[b].rotation;
    world.axesB[0] = Vec3(rotationB.m[0], rotationB.m[3], rotationB.m[6]);
    world.axesB[1] = Vec3(rotationB.m[1], rotationB.m[4], rotationB.m[7]);
    world.axesB[2] = Vec3(rotationB.m[2], rotationB.m[5], rotationB.m[8]);

    const Vec3 offset = worldPosition(world, b) - worldPosition(world, a);
    const Vec3 halfA = world.bodies[a].extents;
    const Vec3 halfB = world.bodies[b].extents;

    float bestSeparation = -kBig;
    int32_t bestKind = 0;
    int32_t bestIndexA = 0;
    int32_t bestIndexB = 0;
    Vec3 bestNormal(1.0f, 0.0f, 0.0f);

    for (int32_t i = 0; i < 3; ++i) {
        const Vec3 axis = world.axesA[i];
        const float d = dot(offset, axis);
        const float separation = fabsf(d) - boxProjection(world.axesA, halfA, axis) - boxProjection(world.axesB, halfB, axis);
        if (separation > 0.0f) {
            return;
        }
        if (separation > bestSeparation) {
            bestSeparation = separation;
            bestKind = 1;
            bestIndexA = i;
            bestNormal = axis * (d >= 0.0f ? 1.0f : -1.0f);
        }
    }

    for (int32_t i = 0; i < 3; ++i) {
        const Vec3 axis = world.axesB[i];
        const float d = dot(offset, axis);
        const float separation = fabsf(d) - boxProjection(world.axesA, halfA, axis) - boxProjection(world.axesB, halfB, axis);
        if (separation > 0.0f) {
            return;
        }
        if (separation > bestSeparation) {
            bestSeparation = separation;
            bestKind = 2;
            bestIndexA = i;
            bestNormal = axis * (d >= 0.0f ? 1.0f : -1.0f);
        }
    }

    for (int32_t i = 0; i < 3; ++i) {
        for (int32_t j = 0; j < 3; ++j) {
            const Vec3 axis = cross(world.axesA[i], world.axesB[j]);
            const float lengthSquared = lengthSq(axis);
            if (lengthSquared < kCrossEps) {
                continue;
            }

            const Vec3 normalized = axis * (1.0f / sqrtf(lengthSquared));
            const float d = dot(offset, normalized);
            const float separation = fabsf(d) - boxProjection(world.axesA, halfA, normalized) - boxProjection(world.axesB, halfB, normalized);
            if (separation > 0.0f) {
                return;
            }

            const float score = separation - kFaceEdgeBias;
            if (score > bestSeparation) {
                bestSeparation = separation;
                bestKind = 3;
                bestIndexA = i;
                bestIndexB = j;
                bestNormal = normalized * (d >= 0.0f ? 1.0f : -1.0f);
            }
        }
    }

    if (bestKind == 0) {
        return;
    }

    if (bestKind == 1) {
        addFaceContacts(world, a, b, bestNormal, true, bestIndexA);
    } else if (bestKind == 2) {
        addFaceContacts(world, a, b, bestNormal, false, bestIndexA);
    } else {
        addEdgeContact(world, a, b, bestNormal, bestIndexA, bestIndexB, -bestSeparation);
    }
}

}

void collidePair(World& world, int32_t a, int32_t b) {
    const int32_t shapeA = world.bodies[a].shape;
    const int32_t shapeB = world.bodies[b].shape;

    if (shapeA == kShapeSphere && shapeB == kShapeSphere) {
        collideSphereSphere(world, a, b);
    } else if (shapeA == kShapeSphere && shapeB == kShapeBox) {
        collideSphereBox(world, a, b, true);
    } else if (shapeA == kShapeBox && shapeB == kShapeSphere) {
        collideSphereBox(world, b, a, false);
    } else if (shapeA == kShapeBox && shapeB == kShapeBox) {
        collideBoxBox(world, a, b);
    }
}

}
