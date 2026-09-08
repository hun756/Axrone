import { Vec3, IVec2Like, IVec3Like, EPSILON } from '@axrone/numeric';
import type { IAABB } from '@axrone/geometry';

export interface IRayIntersection {
    hit: boolean;
    distance: number;
    fraction: number;
}

export class RayPrimitiveIntersector2D {
    private static readonly PARALLEL_EPSILON = 1e-8;
    // Removed static mutable temp vectors — use local variables to avoid re-entrancy corruption

    public static intersectAABB(
        origin: Readonly<IVec2Like>,
        invDirection: Readonly<IVec2Like>,
        aabb: IAABB<IVec2Like>,
        maxDistance: number,
        out: { tMin: number; tMax: number }
    ): boolean {
        let tMin = 0;
        let tMax = maxDistance;

        const min = aabb.min;
        const max = aabb.max;

        {
            const t1 = (min.x - origin.x) * invDirection.x;
            const t2 = (max.x - origin.x) * invDirection.x;
            tMin = Math.max(tMin, Math.min(t1, t2));
            tMax = Math.min(tMax, Math.max(t1, t2));
        }

        {
            const t1 = (min.y - origin.y) * invDirection.y;
            const t2 = (max.y - origin.y) * invDirection.y;
            tMin = Math.max(tMin, Math.min(t1, t2));
            tMax = Math.min(tMax, Math.max(t1, t2));
        }

        const hit = tMax >= tMin && tMax >= 0;
        out.tMin = tMin;
        out.tMax = tMax;
        return hit;
    }

    public static intersectCircle(
        origin: Readonly<IVec2Like>,
        direction: Readonly<IVec2Like>,
        center: Readonly<IVec2Like>,
        radius: number,
        maxDistance: number
    ): IRayIntersection {
        const ocx = origin.x - center.x;
        const ocy = origin.y - center.y;

        const a = direction.x * direction.x + direction.y * direction.y;
        const b = 2.0 * (ocx * direction.x + ocy * direction.y);
        const c = ocx * ocx + ocy * ocy - radius * radius;
        const discriminant = b * b - 4 * a * c;

        if (discriminant < 0) return { hit: false, distance: 0, fraction: 0 };

        const sqrtDisc = Math.sqrt(discriminant);
        const t1 = (-b - sqrtDisc) / (2.0 * a);
        const t2 = (-b + sqrtDisc) / (2.0 * a);
        let t = t1 < 0 ? t2 : t1;

        if (t < 0 || t > maxDistance) return { hit: false, distance: 0, fraction: 0 };
        return { hit: true, distance: t, fraction: t / maxDistance };
    }

    public static intersectSegment(
        origin: Readonly<IVec2Like>,
        direction: Readonly<IVec2Like>,
        p0: Readonly<IVec2Like>,
        p1: Readonly<IVec2Like>,
        maxDistance: number
    ): IRayIntersection {
        const v1x = origin.x - p0.x;
        const v1y = origin.y - p0.y;
        const v2x = p1.x - p0.x;
        const v2y = p1.y - p0.y;

        const cross1 = direction.x * v2y - direction.y * v2x;
        if (Math.abs(cross1) < this.PARALLEL_EPSILON) return { hit: false, distance: 0, fraction: 0 };

        const t = (v2x * v1y - v2y * v1x) / cross1;
        const u = (direction.x * v1y - direction.y * v1x) / cross1;

        if (t >= 0 && t <= maxDistance && u >= 0 && u <= 1) {
            return { hit: true, distance: t, fraction: t / maxDistance };
        }
        return { hit: false, distance: 0, fraction: 0 };
    }

    public static intersectBox(
        origin: Readonly<IVec2Like>,
        direction: Readonly<IVec2Like>,
        center: Readonly<IVec2Like>,
        extents: Readonly<IVec2Like>,
        rotation: number,
        maxDistance: number
    ): IRayIntersection {
        const cos = Math.cos(-rotation);
        const sin = Math.sin(-rotation);

        const dx = origin.x - center.x;
        const dy = origin.y - center.y;
        const localOriginX = dx * cos - dy * sin;
        const localOriginY = dx * sin + dy * cos;

        const localDirX = direction.x * cos - direction.y * sin;
        const localDirY = direction.x * sin + direction.y * cos;

        const invDirX = Math.abs(localDirX) > EPSILON ? 1.0 / localDirX : Number.MAX_VALUE;
        const invDirY = Math.abs(localDirY) > EPSILON ? 1.0 / localDirY : Number.MAX_VALUE;

        let tMin = 0;
        let tMax = maxDistance;

        {
            const t1 = (-extents.x - localOriginX) * invDirX;
            const t2 = (extents.x - localOriginX) * invDirX;
            tMin = Math.max(tMin, Math.min(t1, t2));
            tMax = Math.min(tMax, Math.max(t1, t2));
        }

        {
            const t1 = (-extents.y - localOriginY) * invDirY;
            const t2 = (extents.y - localOriginY) * invDirY;
            tMin = Math.max(tMin, Math.min(t1, t2));
            tMax = Math.min(tMax, Math.max(t1, t2));
        }

        const hit = tMax >= tMin && tMax >= 0 && tMin <= maxDistance;
        const distance = tMin >= 0 ? tMin : tMax;

        return {
            hit,
            distance,
            fraction: hit ? distance / maxDistance : 0,
        };
    }

    public static intersectCapsule(
        origin: Readonly<IVec2Like>,
        direction: Readonly<IVec2Like>,
        p0: Readonly<IVec2Like>,
        p1: Readonly<IVec2Like>,
        radius: number,
        maxDistance: number
    ): IRayIntersection {
        // Segment direction (normalized)
        const segX = p1.x - p0.x;
        const segY = p1.y - p0.y;
        const segLen = Math.sqrt(segX * segX + segY * segY);
        if (segLen < EPSILON) {
            // Degenerate capsule = circle
            return this.intersectCircle(origin, direction, p0, radius, maxDistance);
        }
        const segDirX = segX / segLen;
        const segDirY = segY / segLen;

        // Origin relative to p0
        const ocX = origin.x - p0.x;
        const ocY = origin.y - p0.y;

        // Infinite cylinder quadratic (ray vs offset curve around segment)
        const dirDotSeg = direction.x * segDirX + direction.y * segDirY;
        const ocDotSeg = ocX * segDirX + ocY * segDirY;
        const dirDotDir = direction.x * direction.x + direction.y * direction.y;
        const ocDotOc = ocX * ocX + ocY * ocY;

        const a = dirDotDir - dirDotSeg * dirDotSeg;
        const b = 2.0 * (ocX * direction.x + ocY * direction.y - ocDotSeg * dirDotSeg);
        const c = ocDotOc - ocDotSeg * ocDotSeg - radius * radius;

        const discriminant = b * b - 4 * a * c;
        if (discriminant < 0 || a < EPSILON) {
            // No cylinder hit or degenerate — fall back to cap circles
            const c0 = this.intersectCircle(origin, direction, p0, radius, maxDistance);
            const c1 = this.intersectCircle(origin, direction, p1, radius, maxDistance);
            if (c0.hit && (!c1.hit || c0.distance < c1.distance)) return c0;
            if (c1.hit) return c1;
            return { hit: false, distance: 0, fraction: 0 };
        }

        const sqrtDisc = Math.sqrt(discriminant);
        const t1 = (-b - sqrtDisc) / (2.0 * a);
        const t2 = (-b + sqrtDisc) / (2.0 * a);

        let closestT = -1;
        for (const t of [t1, t2]) {
            if (t >= 0 && t <= maxDistance) {
                const hitX = origin.x + direction.x * t;
                const hitY = origin.y + direction.y * t;
                const proj = (hitX - p0.x) * segDirX + (hitY - p0.y) * segDirY;
                if (proj >= 0 && proj <= segLen) {
                    if (closestT < 0 || t < closestT) closestT = t;
                }
            }
        }

        if (closestT >= 0) {
            return { hit: true, distance: closestT, fraction: closestT / maxDistance };
        }

        // Cylinder roots outside segment range — try cap circles
        const c0 = this.intersectCircle(origin, direction, p0, radius, maxDistance);
        const c1 = this.intersectCircle(origin, direction, p1, radius, maxDistance);
        if (c0.hit && (!c1.hit || c0.distance < c1.distance)) return c0;
        if (c1.hit) return c1;
        return { hit: false, distance: 0, fraction: 0 };
    }

    public static intersectPolygon(
        origin: Readonly<IVec2Like>,
        direction: Readonly<IVec2Like>,
        vertices: readonly IVec2Like[],
        maxDistance: number
    ): IRayIntersection {
        let closestHit: IRayIntersection = { hit: false, distance: Number.MAX_VALUE, fraction: 0 };

        for (let i = 0; i < vertices.length; i++) {
            const v0 = vertices[i];
            const v1 = vertices[(i + 1) % vertices.length];

            const hit = this.intersectSegment(origin, direction, v0, v1, maxDistance);
            if (hit.hit && hit.distance < closestHit.distance) {
                closestHit = hit;
            }
        }

        return closestHit.hit ? closestHit : { hit: false, distance: 0, fraction: 0 };
    }
}

export class RayPrimitiveIntersector3D {
    private static readonly PARALLEL_EPSILON = 1e-8;
    // Removed static mutable temp vectors — use local variables to avoid re-entrancy corruption

    public static intersectAABB(
        origin: Readonly<IVec3Like>,
        invDirection: Readonly<IVec3Like>,
        aabb: IAABB<IVec3Like>,
        maxDistance: number,
        out: { tMin: number; tMax: number }
    ): boolean {
        let tMin = 0;
        let tMax = maxDistance;

        const min = aabb.min;
        const max = aabb.max;

        {
            const t1 = (min.x - origin.x) * invDirection.x;
            const t2 = (max.x - origin.x) * invDirection.x;
            tMin = Math.max(tMin, Math.min(t1, t2));
            tMax = Math.min(tMax, Math.max(t1, t2));
        }

        {
            const t1 = (min.y - origin.y) * invDirection.y;
            const t2 = (max.y - origin.y) * invDirection.y;
            tMin = Math.max(tMin, Math.min(t1, t2));
            tMax = Math.min(tMax, Math.max(t1, t2));
        }

        {
            const t1 = (min.z - origin.z) * invDirection.z;
            const t2 = (max.z - origin.z) * invDirection.z;
            tMin = Math.max(tMin, Math.min(t1, t2));
            tMax = Math.min(tMax, Math.max(t1, t2));
        }

        const hit = tMax >= tMin && tMax >= 0;
        out.tMin = tMin;
        out.tMax = tMax;
        return hit;
    }

    public static intersectSphere(
        origin: Readonly<IVec3Like>,
        direction: Readonly<IVec3Like>,
        center: Readonly<IVec3Like>,
        radius: number,
        maxDistance: number
    ): IRayIntersection {
        const ocx = origin.x - center.x;
        const ocy = origin.y - center.y;
        const ocz = origin.z - center.z;

        const a = direction.x * direction.x + direction.y * direction.y + direction.z * direction.z;
        const b = 2.0 * (ocx * direction.x + ocy * direction.y + ocz * direction.z);
        const c = ocx * ocx + ocy * ocy + ocz * ocz - radius * radius;
        const discriminant = b * b - 4 * a * c;

        if (discriminant < 0) return { hit: false, distance: 0, fraction: 0 };

        const sqrtDisc = Math.sqrt(discriminant);
        const t1 = (-b - sqrtDisc) / (2.0 * a);
        const t2 = (-b + sqrtDisc) / (2.0 * a);
        let t = t1 < 0 ? t2 : t1;

        if (t < 0 || t > maxDistance) return { hit: false, distance: 0, fraction: 0 };
        return { hit: true, distance: t, fraction: t / maxDistance };
    }

    public static intersectPlane(
        origin: Readonly<IVec3Like>,
        direction: Readonly<IVec3Like>,
        planeNormal: Readonly<IVec3Like>,
        planeDistance: number,
        maxDistance: number
    ): IRayIntersection {
        const denom = direction.x * planeNormal.x + direction.y * planeNormal.y + direction.z * planeNormal.z;
        if (Math.abs(denom) < this.PARALLEL_EPSILON) return { hit: false, distance: 0, fraction: 0 };

        const t = -(origin.x * planeNormal.x + origin.y * planeNormal.y + origin.z * planeNormal.z + planeDistance) / denom;
        if (t < 0 || t > maxDistance) return { hit: false, distance: 0, fraction: 0 };
        return { hit: true, distance: t, fraction: t / maxDistance };
    }

    public static intersectTriangle(
        origin: Readonly<IVec3Like>,
        direction: Readonly<IVec3Like>,
        v0: Readonly<IVec3Like>,
        v1: Readonly<IVec3Like>,
        v2: Readonly<IVec3Like>,
        maxDistance: number,
        cullBackface: boolean,
        outBarycentric?: { u: number; v: number }
    ): IRayIntersection {
        // Möller–Trumbore — all local variables, no shared mutable state
        const e1x = v1.x - v0.x, e1y = v1.y - v0.y, e1z = v1.z - v0.z;
        const e2x = v2.x - v0.x, e2y = v2.y - v0.y, e2z = v2.z - v0.z;
        const hx = direction.y * e2z - direction.z * e2y;
        const hy = direction.z * e2x - direction.x * e2z;
        const hz = direction.x * e2y - direction.y * e2x;

        const a = e1x * hx + e1y * hy + e1z * hz;

        if (cullBackface && a < this.PARALLEL_EPSILON) return { hit: false, distance: 0, fraction: 0 };
        if (Math.abs(a) < this.PARALLEL_EPSILON) return { hit: false, distance: 0, fraction: 0 };

        const f = 1.0 / a;
        const sx = origin.x - v0.x, sy = origin.y - v0.y, sz = origin.z - v0.z;
        const u = f * (sx * hx + sy * hy + sz * hz);
        if (u < 0.0 || u > 1.0) return { hit: false, distance: 0, fraction: 0 };

        const qx = sy * e1z - sz * e1y;
        const qy = sz * e1x - sx * e1z;
        const qz = sx * e1y - sy * e1x;
        const v = f * (direction.x * qx + direction.y * qy + direction.z * qz);
        if (v < 0.0 || u + v > 1.0) return { hit: false, distance: 0, fraction: 0 };

        const t = f * (e2x * qx + e2y * qy + e2z * qz);
        if (t < EPSILON || t > maxDistance) return { hit: false, distance: 0, fraction: 0 };

        if (outBarycentric) { outBarycentric.u = u; outBarycentric.v = v; }
        return { hit: true, distance: t, fraction: t / maxDistance };
    }

    public static intersectBox(
        origin: Readonly<IVec3Like>,
        direction: Readonly<IVec3Like>,
        center: Readonly<IVec3Like>,
        extents: Readonly<IVec3Like>,
        maxDistance: number
    ): IRayIntersection {
        const lox = origin.x - center.x;
        const loy = origin.y - center.y;
        const loz = origin.z - center.z;

        const invDirX = Math.abs(direction.x) > EPSILON ? 1.0 / direction.x : Number.MAX_VALUE;
        const invDirY = Math.abs(direction.y) > EPSILON ? 1.0 / direction.y : Number.MAX_VALUE;
        const invDirZ = Math.abs(direction.z) > EPSILON ? 1.0 / direction.z : Number.MAX_VALUE;

        let tMin = 0;
        let tMax = maxDistance;

        {
            const t1 = (-extents.x - lox) * invDirX;
            const t2 = (extents.x - lox) * invDirX;
            tMin = Math.max(tMin, Math.min(t1, t2));
            tMax = Math.min(tMax, Math.max(t1, t2));
        }

        {
            const t1 = (-extents.y - loy) * invDirY;
            const t2 = (extents.y - loy) * invDirY;
            tMin = Math.max(tMin, Math.min(t1, t2));
            tMax = Math.min(tMax, Math.max(t1, t2));
        }

        {
            const t1 = (-extents.z - loz) * invDirZ;
            const t2 = (extents.z - loz) * invDirZ;
            tMin = Math.max(tMin, Math.min(t1, t2));
            tMax = Math.min(tMax, Math.max(t1, t2));
        }

        const hit = tMax >= tMin && tMax >= 0 && tMin <= maxDistance;
        const distance = tMin >= 0 ? tMin : tMax;

        return {
            hit,
            distance,
            fraction: hit ? distance / maxDistance : 0,
        };
    }

    public static intersectCylinder(
        origin: Readonly<IVec3Like>,
        direction: Readonly<IVec3Like>,
        baseCenter: Readonly<IVec3Like>,
        axis: Readonly<IVec3Like>,
        radius: number,
        height: number,
        maxDistance: number
    ): IRayIntersection {
        const ocx = origin.x - baseCenter.x;
        const ocy = origin.y - baseCenter.y;
        const ocz = origin.z - baseCenter.z;
        const dirDotAxis = direction.x * axis.x + direction.y * axis.y + direction.z * axis.z;
        const ocDotAxis = ocx * axis.x + ocy * axis.y + ocz * axis.z;

        const dirDotDir = direction.x * direction.x + direction.y * direction.y + direction.z * direction.z;
        const ocDotOc = ocx * ocx + ocy * ocy + ocz * ocz;

        const a = dirDotDir - dirDotAxis * dirDotAxis;
        const b = 2.0 * (ocx * direction.x + ocy * direction.y + ocz * direction.z - ocDotAxis * dirDotAxis);
        const c = ocDotOc - ocDotAxis * ocDotAxis - radius * radius;

        const discriminant = b * b - 4 * a * c;
        if (discriminant < 0) {
            return { hit: false, distance: 0, fraction: 0 };
        }

        const sqrtDisc = Math.sqrt(discriminant);
        const t1 = (-b - sqrtDisc) / (2.0 * a);
        const t2 = (-b + sqrtDisc) / (2.0 * a);

        for (const t of [t1, t2]) {
            if (t >= 0 && t <= maxDistance) {
                const hitX = origin.x + direction.x * t;
                const hitY = origin.y + direction.y * t;
                const hitZ = origin.z + direction.z * t;
                const hitVecX = hitX - baseCenter.x;
                const hitVecY = hitY - baseCenter.y;
                const hitVecZ = hitZ - baseCenter.z;
                const projection = hitVecX * axis.x + hitVecY * axis.y + hitVecZ * axis.z;

                if (projection >= 0 && projection <= height) {
                    return { hit: true, distance: t, fraction: t / maxDistance };
                }
            }
        }

        return { hit: false, distance: 0, fraction: 0 };
    }

    public static intersectCapsule(
        origin: Readonly<IVec3Like>,
        direction: Readonly<IVec3Like>,
        p0: Readonly<IVec3Like>,
        p1: Readonly<IVec3Like>,
        radius: number,
        maxDistance: number
    ): IRayIntersection {
        const segX = p1.x - p0.x;
        const segY = p1.y - p0.y;
        const segZ = p1.z - p0.z;
        const segmentLength = Math.sqrt(segX * segX + segY * segY + segZ * segZ);
        if (segmentLength < EPSILON) {
            return this.intersectSphere(origin, direction, p0, radius, maxDistance);
        }
        const segDirX = segX / segmentLength;
        const segDirY = segY / segmentLength;
        const segDirZ = segZ / segmentLength;

        const ocX = origin.x - p0.x;
        const ocY = origin.y - p0.y;
        const ocZ = origin.z - p0.z;
        const dirDotSeg = direction.x * segDirX + direction.y * segDirY + direction.z * segDirZ;
        const ocDotSeg = ocX * segDirX + ocY * segDirY + ocZ * segDirZ;

        const dirDotDir = direction.x * direction.x + direction.y * direction.y + direction.z * direction.z;
        const ocDotOc = ocX * ocX + ocY * ocY + ocZ * ocZ;

        const a = dirDotDir - dirDotSeg * dirDotSeg;
        const b = 2.0 * (ocX * direction.x + ocY * direction.y + ocZ * direction.z - ocDotSeg * dirDotSeg);
        const c = ocDotOc - ocDotSeg * ocDotSeg - radius * radius;

        const discriminant = b * b - 4 * a * c;
        if (discriminant < 0) {
            const sphere0 = this.intersectSphere(origin, direction, p0, radius, maxDistance);
            const sphere1 = this.intersectSphere(origin, direction, p1, radius, maxDistance);

            if (sphere0.hit && (!sphere1.hit || sphere0.distance < sphere1.distance)) {
                return sphere0;
            }
            if (sphere1.hit) {
                return sphere1;
            }

            return { hit: false, distance: 0, fraction: 0 };
        }

        const sqrtDisc = Math.sqrt(discriminant);
        const t1 = (-b - sqrtDisc) / (2.0 * a);
        const t2 = (-b + sqrtDisc) / (2.0 * a);

        let closestT = -1;

        for (const t of [t1, t2]) {
            if (t >= 0 && t <= maxDistance) {
                const hitX = origin.x + direction.x * t;
                const hitY = origin.y + direction.y * t;
                const hitZ = origin.z + direction.z * t;
                const hitVecX = hitX - p0.x;
                const hitVecY = hitY - p0.y;
                const hitVecZ = hitZ - p0.z;
                const projection = hitVecX * segDirX + hitVecY * segDirY + hitVecZ * segDirZ;

                if (projection >= 0 && projection <= segmentLength) {
                    if (closestT < 0 || t < closestT) {
                        closestT = t;
                    }
                }
            }
        }

        if (closestT >= 0) {
            return { hit: true, distance: closestT, fraction: closestT / maxDistance };
        }

        const sphere0 = this.intersectSphere(origin, direction, p0, radius, maxDistance);
        const sphere1 = this.intersectSphere(origin, direction, p1, radius, maxDistance);

        if (sphere0.hit && (!sphere1.hit || sphere0.distance < sphere1.distance)) {
            return sphere0;
        }
        if (sphere1.hit) {
            return sphere1;
        }

        return { hit: false, distance: 0, fraction: 0 };
    }
}
