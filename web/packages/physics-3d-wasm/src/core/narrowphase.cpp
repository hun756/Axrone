#include "core/narrowphase.h"

#include "platform/math.h"

namespace axrone::physics {

namespace {

struct ShapeWorld {
    int32_t kind;
    Vec3 center;
    Vec3 half;
    Mat3 rotation;
};

Vec3 worldPosition(const World& world, int32_t id) {
    return transformPosition(world.transforms[id]);
}

void loadShapeWorld(const World& world, int32_t id, int32_t slot, ShapeWorld& out) {
    const Body& body = world.bodies[id];
    const Shape& shape = body.shapes[slot];
    const Vec3 position = worldPosition(world, id);

    out.kind = shape.kind;
    out.half = shape.extents;
    out.center = shapeCenter(position, body.rotation, shape);
    if (shape.kind == kShapeSphere) {
        out.rotation = body.rotation;
    } else {
        shapeWorldRotation(body.rotation, shape, out.rotation);
    }
}

float shapeRadius(const ShapeWorld& shape) {
    return shape.half.x;
}

int32_t pairFeature(int32_t shapeIndexA, int32_t shapeIndexB, int32_t baseFeature) {
    return ((shapeIndexA * kMaxShapesPerBody + shapeIndexB) << 12) | baseFeature;
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
    int32_t shapeIndexA,
    int32_t shapeIndexB,
    int32_t baseFeature
) {
    if (world.contactCount >= world.maxContacts) {
        return;
    }

    if (a > b) {
        int32_t swap = a;
        a = b;
        b = swap;
        swap = shapeIndexA;
        shapeIndexA = shapeIndexB;
        shapeIndexB = swap;
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

    const int32_t feature = pairFeature(shapeIndexA, shapeIndexB, baseFeature);

    const int32_t index = world.contactCount++;
    Contact& contact = world.contacts[index];

    contact.a = a;
    contact.b = b;
    contact.shapeA = shapeIndexA;
    contact.shapeB = shapeIndexB;
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

    const Shape& shapeA = world.bodies[a].shapes[shapeIndexA];
    const Shape& shapeB = world.bodies[b].shapes[shapeIndexB];
    const float frictionA = shapeEffectiveFriction(shapeA, world.bodies[a].friction);
    const float frictionB = shapeEffectiveFriction(shapeB, world.bodies[b].friction);
    contact.friction = sqrtf(frictionA * frictionB);
    const float restitutionA = shapeEffectiveRestitution(shapeA, world.bodies[a].restitution);
    const float restitutionB = shapeEffectiveRestitution(shapeB, world.bodies[b].restitution);
    contact.restitution = restitutionA > restitutionB ? restitutionA : restitutionB;
    contact.sensor = (shapeA.sensor != 0 || shapeB.sensor != 0) ? 1 : 0;

    if (contact.sensor != 0) {
        contact.cacheSlot = -1;
    } else {
        const int32_t cached = world.cacheLookup(a, b, feature);
        if (cached >= 0) {
            const CacheEntry& entry = world.cachePrev[cached];
            contact.impN = entry.impN;
            contact.impT1 = entry.impT1;
            contact.impT2 = entry.impT2;
        }
        contact.cacheSlot = world.cacheStore(a, b, feature, contact.impN, contact.impT1, contact.impT2);
    }

    const Vec3 posA = worldPosition(world, a);
    const Vec3 posB = worldPosition(world, b);
    contact.ra = point - posA;
    contact.rb = point - posB;
    contact.localA = world.bodies[a].rotation.transposeTransform(surfA - posA);
    contact.localB = world.bodies[b].rotation.transposeTransform(surfB - posB);
}

void collideSphereVersusSphere(
    World& world,
    int32_t a,
    int32_t indexA,
    const Vec3& centerA,
    float radiusA,
    int32_t b,
    int32_t indexB,
    const Vec3& centerB,
    float radiusB,
    int32_t baseFeature
) {
    const Vec3 delta = centerB - centerA;

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
    const Vec3 point = centerA + normal * (radiusA - pen * 0.5f);
    const Vec3 surfA = centerA + normal * radiusA;
    const Vec3 surfB = centerB - normal * radiusB;

    addContact(world, a, b, normal, pen, point, surfA, surfB, 0, Vec3(), indexA, indexB, baseFeature);
}

void collideSphereSphere(World& world, int32_t a, int32_t indexA, const ShapeWorld& sphereA, int32_t b, int32_t indexB, const ShapeWorld& sphereB) {
    collideSphereVersusSphere(
        world,
        a,
        indexA,
        sphereA.center,
        shapeRadius(sphereA),
        b,
        indexB,
        sphereB.center,
        shapeRadius(sphereB),
        kKindSphereSphere * 16);
}

void capsuleSegment(const ShapeWorld& capsule, Vec3& p0, Vec3& p1) {
    const float halfSegment = capsule.half.y;
    const Vec3 axis(
        capsule.rotation.m[1] * halfSegment,
        capsule.rotation.m[4] * halfSegment,
        capsule.rotation.m[7] * halfSegment
    );
    p0 = capsule.center - axis;
    p1 = capsule.center + axis;
}

Vec3 closestPointOnSegment(const Vec3& point, const Vec3& p0, const Vec3& p1) {
    const Vec3 direction = p1 - p0;
    const float span = lengthSq(direction);
    if (span <= kEps) {
        return p0;
    }
    const float s = clampf(dot(point - p0, direction) / span, 0.0f, 1.0f);
    return p0 + direction * s;
}

void closestPointsBetweenSegments(
    const Vec3& a0,
    const Vec3& a1,
    const Vec3& b0,
    const Vec3& b1,
    Vec3& outA,
    Vec3& outB
) {
    const Vec3 d1 = a1 - a0;
    const Vec3 d2 = b1 - b0;
    const Vec3 w = a0 - b0;
    const float aa = dot(d1, d1);
    const float bb = dot(d1, d2);
    const float cc = dot(d2, d2);
    const float dd = dot(d1, w);
    const float ee = dot(d2, w);

    float s = 0.0f;
    float t = 0.0f;

    if (aa <= kEps && cc <= kEps) {
        s = 0.0f;
        t = 0.0f;
    } else if (aa <= kEps) {
        t = clampf(ee / cc, 0.0f, 1.0f);
    } else if (cc <= kEps) {
        s = clampf(-dd / aa, 0.0f, 1.0f);
    } else {
        const float denominator = aa * cc - bb * bb;
        s = denominator > kEps ? clampf((bb * ee - cc * dd) / denominator, 0.0f, 1.0f) : 0.0f;
        t = clampf((ee + bb * s) / cc, 0.0f, 1.0f);
        s = clampf((t * bb - dd) / aa, 0.0f, 1.0f);
    }

    outA = a0 + d1 * s;
    outB = b0 + d2 * t;
}

void collideSphereCapsule(
    World& world,
    int32_t sphere,
    int32_t sphereIndex,
    const ShapeWorld& sphereShape,
    int32_t capsule,
    int32_t capsuleIndex,
    const ShapeWorld& capsuleShape
) {
    Vec3 p0;
    Vec3 p1;
    capsuleSegment(capsuleShape, p0, p1);
    const Vec3 anchor = closestPointOnSegment(sphereShape.center, p0, p1);

    collideSphereVersusSphere(
        world,
        sphere,
        sphereIndex,
        sphereShape.center,
        shapeRadius(sphereShape),
        capsule,
        capsuleIndex,
        anchor,
        shapeRadius(capsuleShape),
        kKindCapsuleSphere * 16);
}

void collideCapsuleCapsule(
    World& world,
    int32_t a,
    int32_t indexA,
    const ShapeWorld& capsuleA,
    int32_t b,
    int32_t indexB,
    const ShapeWorld& capsuleB
) {
    Vec3 a0;
    Vec3 a1;
    Vec3 b0;
    Vec3 b1;
    capsuleSegment(capsuleA, a0, a1);
    capsuleSegment(capsuleB, b0, b1);

    Vec3 pointA;
    Vec3 pointB;
    closestPointsBetweenSegments(a0, a1, b0, b1, pointA, pointB);

    collideSphereVersusSphere(
        world,
        a,
        indexA,
        pointA,
        shapeRadius(capsuleA),
        b,
        indexB,
        pointB,
        shapeRadius(capsuleB),
        kKindCapsuleCapsule * 16);
}

void collideSphereBox(
    World& world,
    int32_t sphere,
    int32_t sphereIndex,
    const ShapeWorld& sphereShape,
    int32_t box,
    int32_t boxIndex,
    const ShapeWorld& boxShape,
    bool sphereIsA,
    int32_t baseFeature
) {
    const Vec3 delta = sphereShape.center - boxShape.center;

    const Vec3 localSphere = boxShape.rotation.transposeTransform(delta);
    const Vec3 half = boxShape.half;

    const Vec3 closest(
        localSphere.x > half.x ? half.x : (localSphere.x < -half.x ? -half.x : localSphere.x),
        localSphere.y > half.y ? half.y : (localSphere.y < -half.y ? -half.y : localSphere.y),
        localSphere.z > half.z ? half.z : (localSphere.z < -half.z ? -half.z : localSphere.z)
    );

    const Vec3 difference = localSphere - closest;
    const float distanceSq = lengthSq(difference);
    const float radius = shapeRadius(sphereShape);
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

    const Vec3 worldNormal = boxShape.rotation.transform(localNormal);
    const Vec3 point = boxShape.center + boxShape.rotation.transform(localPoint);
    const Vec3 sphereSurface = sphereShape.center - worldNormal * radius;

    if (sphereIsA) {
        addContact(world, sphere, box, -worldNormal, pen, point, sphereSurface, point, 0, Vec3(), sphereIndex, boxIndex, baseFeature);
    } else {
        addContact(world, box, sphere, worldNormal, pen, point, point, sphereSurface, 0, Vec3(), boxIndex, sphereIndex, baseFeature);
    }
}

Vec3 clampToBox(const Vec3& value, const Vec3& half) {
    return Vec3(
        value.x > half.x ? half.x : (value.x < -half.x ? -half.x : value.x),
        value.y > half.y ? half.y : (value.y < -half.y ? -half.y : value.y),
        value.z > half.z ? half.z : (value.z < -half.z ? -half.z : value.z)
    );
}

float segmentBoxParametricDistanceSq(const Vec3& l0, const Vec3& dl, const Vec3& half, float t, Vec3& outLocal, Vec3& outClamped) {
    outLocal = l0 + dl * t;
    outClamped = clampToBox(outLocal, half);
    const Vec3 difference = outLocal - outClamped;
    return lengthSq(difference);
}

float closestSegmentBoxParameter(const Vec3& l0, const Vec3& l1, const Vec3& half) {
    const Vec3 dl = l1 - l0;
    float lo = 0.0f;
    float hi = 1.0f;
    float bestT = 0.0f;
    int32_t bestIndex = 0;

    for (int32_t pass = 0; pass < 2; ++pass) {
        float bestDistanceSq = kBig;
        const float step = (hi - lo) / static_cast<float>(kCapsuleBoxSamples);
        for (int32_t i = 0; i <= kCapsuleBoxSamples; ++i) {
            const float t = lo + step * static_cast<float>(i);
            Vec3 local;
            Vec3 clamped;
            const float distanceSq = segmentBoxParametricDistanceSq(l0, dl, half, t, local, clamped);
            if (distanceSq < bestDistanceSq) {
                bestDistanceSq = distanceSq;
                bestT = t;
                bestIndex = i;
            }
        }
        if (pass == 1) {
            break;
        }
        const float windowStart = lo + step * static_cast<float>(bestIndex - 1 > 0 ? bestIndex - 1 : 0);
        const float windowEnd = lo + step * static_cast<float>(bestIndex + 1 < kCapsuleBoxSamples ? bestIndex + 1 : kCapsuleBoxSamples);
        lo = windowStart;
        hi = windowEnd;
    }

    return bestT;
}

void collideCapsuleBox(
    World& world,
    int32_t capsule,
    int32_t capsuleIndex,
    const ShapeWorld& capsuleShape,
    int32_t box,
    int32_t boxIndex,
    const ShapeWorld& boxShape,
    bool capsuleIsA
) {
    Vec3 p0;
    Vec3 p1;
    capsuleSegment(capsuleShape, p0, p1);

    const Vec3 delta0 = p0 - boxShape.center;
    const Vec3 delta1 = p1 - boxShape.center;
    const Vec3 l0 = boxShape.rotation.transposeTransform(delta0);
    const Vec3 l1 = boxShape.rotation.transposeTransform(delta1);

    const float t = closestSegmentBoxParameter(l0, l1, boxShape.half);
    const Vec3 localAnchor = l0 + (l1 - l0) * t;
    const Vec3 worldAnchor = boxShape.center + boxShape.rotation.transform(localAnchor);

    ShapeWorld anchorSphere;
    anchorSphere.kind = kShapeSphere;
    anchorSphere.center = worldAnchor;
    anchorSphere.half = Vec3(capsuleShape.half.x, 0.0f, 0.0f);
    anchorSphere.rotation = boxShape.rotation;

    collideSphereBox(
        world,
        capsule,
        capsuleIndex,
        anchorSphere,
        box,
        boxIndex,
        boxShape,
        capsuleIsA,
        kKindCapsuleBox * 16);
}

void buildFace(const Vec3& center, const Vec3 axes[3], const Vec3& half, int32_t axisIndex, float sign, Vec3 out[4]) {
    const int32_t uIndex = (axisIndex + 1) % 3;
    const int32_t vIndex = (axisIndex + 2) % 3;

    const Vec3 u = axes[uIndex];
    const Vec3 v = axes[vIndex];
    const float hu = uIndex == 0 ? half.x : (uIndex == 1 ? half.y : half.z);
    const float hv = vIndex == 0 ? half.x : (vIndex == 1 ? half.y : half.z);
    const float ha = axisIndex == 0 ? half.x : (axisIndex == 1 ? half.y : half.z);

    const Vec3 faceCenter = center + axes[axisIndex] * (sign * ha);

    out[0] = faceCenter + u * hu + v * hv;
    out[1] = faceCenter - u * hu + v * hv;
    out[2] = faceCenter - u * hu - v * hv;
    out[3] = faceCenter + u * hu - v * hv;
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

void addFaceContacts(
    World& world,
    int32_t a,
    int32_t indexA,
    const ShapeWorld& shapeA,
    int32_t b,
    int32_t indexB,
    const ShapeWorld& shapeB,
    const Vec3& normal,
    bool referenceIsA,
    int32_t axisIndex
) {
    const Vec3* referenceAxes = referenceIsA ? world.axesA : world.axesB;
    const Vec3* incidentAxes = referenceIsA ? world.axesB : world.axesA;
    const ShapeWorld& referenceShape = referenceIsA ? shapeA : shapeB;
    const ShapeWorld& incidentShape = referenceIsA ? shapeB : shapeA;
    const int32_t reference = referenceIsA ? a : b;

    const Vec3 referenceNormal = referenceIsA ? normal : -normal;
    const float sign = dot(referenceAxes[axisIndex], referenceNormal) >= 0.0f ? 1.0f : -1.0f;

    const Vec3& referenceHalf = referenceShape.half;
    const float referenceHalfExtent = axisIndex == 0 ? referenceHalf.x : (axisIndex == 1 ? referenceHalf.y : referenceHalf.z);

    const Vec3 referencePosition = referenceShape.center;
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

    buildFace(incidentShape.center, incidentAxes, incidentShape.half, incidentAxis, incidentSign, world.faceVertices);

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
            addContact(world, a, b, normal, -separation, point, referencePoint, point, 1, localNormal, indexA, indexB, kKindBoxFace * 16 + added);
        } else {
            addContact(world, a, b, normal, -separation, point, point, referencePoint, 2, localNormal, indexA, indexB, kKindBoxFace * 16 + added);
        }

        if (++added >= 8) {
            break;
        }
    }
}

void addEdgeContact(
    World& world,
    int32_t a,
    int32_t indexA,
    const ShapeWorld& shapeA,
    int32_t b,
    int32_t indexB,
    const ShapeWorld& shapeB,
    const Vec3& normal,
    int32_t axisA,
    int32_t axisB,
    float pen
) {
    const Vec3& halfA = shapeA.half;
    Vec3 centerA = shapeA.center;
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

    const Vec3& halfB = shapeB.half;
    Vec3 centerB = shapeB.center;
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

    addContact(world, a, b, normal, pen, (pointA + pointB) * 0.5f, pointA, pointB, 0, Vec3(), indexA, indexB, kKindBoxEdge * 16);
}

void collideBoxBox(World& world, int32_t a, int32_t indexA, const ShapeWorld& shapeA, int32_t b, int32_t indexB, const ShapeWorld& shapeB) {
    const Mat3& rotationA = shapeA.rotation;
    world.axesA[0] = Vec3(rotationA.m[0], rotationA.m[3], rotationA.m[6]);
    world.axesA[1] = Vec3(rotationA.m[1], rotationA.m[4], rotationA.m[7]);
    world.axesA[2] = Vec3(rotationA.m[2], rotationA.m[5], rotationA.m[8]);

    const Mat3& rotationB = shapeB.rotation;
    world.axesB[0] = Vec3(rotationB.m[0], rotationB.m[3], rotationB.m[6]);
    world.axesB[1] = Vec3(rotationB.m[1], rotationB.m[4], rotationB.m[7]);
    world.axesB[2] = Vec3(rotationB.m[2], rotationB.m[5], rotationB.m[8]);

    const Vec3 offset = shapeB.center - shapeA.center;
    const Vec3& halfA = shapeA.half;
    const Vec3& halfB = shapeB.half;

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
        addFaceContacts(world, a, indexA, shapeA, b, indexB, shapeB, bestNormal, true, bestIndexA);
    } else if (bestKind == 2) {
        addFaceContacts(world, a, indexA, shapeA, b, indexB, shapeB, bestNormal, false, bestIndexA);
    } else {
        addEdgeContact(world, a, indexA, shapeA, b, indexB, shapeB, bestNormal, bestIndexA, bestIndexB, -bestSeparation);
    }
}

void cylinderProxy(const ShapeWorld& shape, ShapeWorld& out) {
    if (shape.kind != kShapeCylinder) {
        out = shape;
        return;
    }
    out.kind = kShapeBox;
    out.center = shape.center;
    out.half = Vec3(shape.half.x, shape.half.y, shape.half.x);
    out.rotation = shape.rotation;
}

void collideShapePair(World& world, int32_t a, int32_t indexA, const ShapeWorld& shapeA, int32_t b, int32_t indexB, const ShapeWorld& shapeB) {
    if (shapeA.kind == kShapeSphere) {
        if (shapeB.kind == kShapeSphere) {
            collideSphereSphere(world, a, indexA, shapeA, b, indexB, shapeB);
        } else if (shapeB.kind == kShapeBox) {
            collideSphereBox(world, a, indexA, shapeA, b, indexB, shapeB, true, kKindSphereBox * 16);
        } else if (shapeB.kind == kShapeCapsule) {
            collideSphereCapsule(world, a, indexA, shapeA, b, indexB, shapeB);
        }
    } else if (shapeA.kind == kShapeBox) {
        if (shapeB.kind == kShapeSphere) {
            collideSphereBox(world, b, indexB, shapeB, a, indexA, shapeA, false, kKindSphereBox * 16);
        } else if (shapeB.kind == kShapeBox) {
            collideBoxBox(world, a, indexA, shapeA, b, indexB, shapeB);
        } else if (shapeB.kind == kShapeCapsule) {
            collideCapsuleBox(world, b, indexB, shapeB, a, indexA, shapeA, false);
        }
    } else if (shapeA.kind == kShapeCapsule) {
        if (shapeB.kind == kShapeSphere) {
            collideSphereCapsule(world, b, indexB, shapeB, a, indexA, shapeA);
        } else if (shapeB.kind == kShapeBox) {
            collideCapsuleBox(world, a, indexA, shapeA, b, indexB, shapeB, true);
        } else if (shapeB.kind == kShapeCapsule) {
            collideCapsuleCapsule(world, a, indexA, shapeA, b, indexB, shapeB);
        }
    }
}

}

void collidePair(World& world, int32_t a, int32_t b) {
    const Body& bodyA = world.bodies[a];
    const Body& bodyB = world.bodies[b];
    const int32_t countA = shapeSlots(bodyA);
    const int32_t countB = shapeSlots(bodyB);

    ShapeWorld stateA;
    ShapeWorld stateB;
    ShapeWorld proxyA;
    ShapeWorld proxyB;

    for (int32_t indexA = 0; indexA < countA; ++indexA) {
        if (!shapeValid(bodyA.shapes[indexA])) {
            continue;
        }
        loadShapeWorld(world, a, indexA, stateA);
        cylinderProxy(stateA, proxyA);

        for (int32_t indexB = 0; indexB < countB; ++indexB) {
            if (!shapeValid(bodyB.shapes[indexB])) {
                continue;
            }
            if (!filtersCollide(bodyA.shapes[indexA], bodyB.shapes[indexB])) {
                continue;
            }
            loadShapeWorld(world, b, indexB, stateB);
            cylinderProxy(stateB, proxyB);
            collideShapePair(world, a, indexA, proxyA, b, indexB, proxyB);
        }
    }
}

}
