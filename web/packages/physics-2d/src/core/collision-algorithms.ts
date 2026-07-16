import type { IVec2Like } from '@axrone/numeric';

interface Simplex {
    readonly points: IVec2Like[];
    count: number;
}

interface SupportPoint {
    readonly point: IVec2Like;
    readonly indexA: number;
    readonly indexB: number;
}

const _supportPoint: SupportPoint = { point: { x: 0, y: 0 }, indexA: 0, indexB: 0 };
const _supportPointB: SupportPoint = { point: { x: 0, y: 0 }, indexA: 0, indexB: 0 };

export class GJK2D {
    private static readonly MAX_ITERATIONS = 32;
    private static readonly EPSILON = 1e-10;

    static testIntersection(
        verticesA: readonly IVec2Like[],
        verticesB: readonly IVec2Like[],
        transformA: { position: IVec2Like; rotation: number },
        transformB: { position: IVec2Like; rotation: number }
    ): boolean {
        const simplex: Simplex = { points: [], count: 0 };
        const direction: IVec2Like = {
            x: transformB.position.x - transformA.position.x,
            y: transformB.position.y - transformA.position.y,
        };

        if (direction.x * direction.x + direction.y * direction.y < GJK2D.EPSILON) {
            direction.x = 1;
            direction.y = 0;
        }

        GJK2D._support(verticesA, verticesB, transformA, transformB, direction, _supportPoint);
        simplex.points.push({ x: _supportPoint.point.x, y: _supportPoint.point.y });
        simplex.count = 1;

        direction.x = -_supportPoint.point.x;
        direction.y = -_supportPoint.point.y;

        for (let iter = 0; iter < GJK2D.MAX_ITERATIONS; iter++) {
            GJK2D._support(verticesA, verticesB, transformA, transformB, direction, _supportPoint);

            if (_supportPoint.point.x * direction.x + _supportPoint.point.y * direction.y < 0) {
                return false;
            }

            simplex.points.push({ x: _supportPoint.point.x, y: _supportPoint.point.y });
            simplex.count++;

            if (GJK2D._processSimplex(simplex, direction)) {
                return true;
            }
        }

        return false;
    }

    static distance(
        verticesA: readonly IVec2Like[],
        verticesB: readonly IVec2Like[],
        transformA: { position: IVec2Like; rotation: number },
        transformB: { position: IVec2Like; rotation: number }
    ): { distance: number; closest: IVec2Like } {
        const direction: IVec2Like = {
            x: transformB.position.x - transformA.position.x,
            y: transformB.position.y - transformA.position.y,
        };
        if (direction.x * direction.x + direction.y * direction.y < GJK2D.EPSILON) {
            direction.x = 1;
            direction.y = 0;
        }

        let simplex: IVec2Like[] = [];
        GJK2D._support(verticesA, verticesB, transformA, transformB, direction, _supportPoint);
        simplex.push({ x: _supportPoint.point.x, y: _supportPoint.point.y });
        let closestX = _supportPoint.point.x;
        let closestY = _supportPoint.point.y;
        direction.x = -closestX;
        direction.y = -closestY;

        for (let iter = 0; iter < GJK2D.MAX_ITERATIONS; iter++) {
            if (direction.x * direction.x + direction.y * direction.y < GJK2D.EPSILON) {
                break;
            }

            GJK2D._support(verticesA, verticesB, transformA, transformB, direction, _supportPoint);
            simplex.push({ x: _supportPoint.point.x, y: _supportPoint.point.y });

            if (
                _supportPoint.point.x * direction.x +
                    _supportPoint.point.y * direction.y -
                    (closestX * direction.x + closestY * direction.y) <
                1e-10
            ) {
                break;
            }

            const reduced = GJK2D._closestPointOnSimplex(simplex);
            simplex = reduced.simplex;
            closestX = reduced.closestX;
            closestY = reduced.closestY;
            direction.x = -closestX;
            direction.y = -closestY;
        }

        return {
            distance: Math.sqrt(closestX * closestX + closestY * closestY),
            closest: { x: closestX, y: closestY },
        };
    }

    private static _closestPointOnSimplex(simplex: IVec2Like[]): {
        simplex: IVec2Like[];
        closestX: number;
        closestY: number;
    } {
        if (simplex.length === 1) {
            return { simplex, closestX: simplex[0].x, closestY: simplex[0].y };
        }

        if (simplex.length === 2) {
            const a = simplex[0];
            const b = simplex[1];
            const abx = b.x - a.x;
            const aby = b.y - a.y;
            const denom = abx * abx + aby * aby;
            let t = denom > GJK2D.EPSILON ? -(a.x * abx + a.y * aby) / denom : 0;
            if (t <= 0) return { simplex: [a], closestX: a.x, closestY: a.y };
            if (t >= 1) return { simplex: [b], closestX: b.x, closestY: b.y };
            return {
                simplex: [a, b],
                closestX: a.x + t * abx,
                closestY: a.y + t * aby,
            };
        }

        const a = simplex[0];
        const b = simplex[1];
        const c = simplex[2];

        const abx = b.x - a.x;
        const aby = b.y - a.y;
        const acx = c.x - a.x;
        const acy = c.y - a.y;
        const apx = -a.x;
        const apy = -a.y;
        const d1 = abx * apx + aby * apy;
        const d2 = acx * apx + acy * apy;
        if (d1 <= 0 && d2 <= 0) return { simplex: [a], closestX: a.x, closestY: a.y };

        const bpx = -b.x;
        const bpy = -b.y;
        const d3 = abx * bpx + aby * bpy;
        const d4 = acx * bpx + acy * bpy;
        if (d3 >= 0 && d4 <= d3) return { simplex: [b], closestX: b.x, closestY: b.y };

        const vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) {
            const t = d1 / (d1 - d3);
            return { simplex: [a, b], closestX: a.x + t * abx, closestY: a.y + t * aby };
        }

        const cpx = -c.x;
        const cpy = -c.y;
        const d5 = abx * cpx + aby * cpy;
        const d6 = acx * cpx + acy * cpy;
        if (d6 >= 0 && d5 <= d6) return { simplex: [c], closestX: c.x, closestY: c.y };

        const vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) {
            const t = d2 / (d2 - d6);
            return { simplex: [a, c], closestX: a.x + t * acx, closestY: a.y + t * acy };
        }

        const va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) {
            const t = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            const bcx = c.x - b.x;
            const bcy = c.y - b.y;
            return { simplex: [b, c], closestX: b.x + t * bcx, closestY: b.y + t * bcy };
        }

        const denom = va + vb + vc;
        const inv = denom > GJK2D.EPSILON ? 1 / denom : 0;
        return {
            simplex: [a, b, c],
            closestX: va * inv * a.x + vb * inv * b.x + vc * inv * c.x,
            closestY: va * inv * a.y + vb * inv * b.y + vc * inv * c.y,
        };
    }

    static _support(
        verticesA: readonly IVec2Like[],
        verticesB: readonly IVec2Like[],
        transformA: { position: IVec2Like; rotation: number },
        transformB: { position: IVec2Like; rotation: number },
        direction: IVec2Like,
        out: SupportPoint
    ): void {
        const supportA = GJK2D._getFarthestPointInDirection(verticesA, transformA, direction);
        const supportB = GJK2D._getFarthestPointInDirection(verticesB, transformB, {
            x: -direction.x,
            y: -direction.y,
        });

        out.point.x = supportA.x - supportB.x;
        out.point.y = supportA.y - supportB.y;
    }

    private static _getFarthestPointInDirection(
        vertices: readonly IVec2Like[],
        transform: { position: IVec2Like; rotation: number },
        direction: IVec2Like
    ): IVec2Like {
        let maxDot = -Infinity;
        let maxX = 0;
        let maxY = 0;

        const cos = Math.cos(transform.rotation);
        const sin = Math.sin(transform.rotation);

        for (let i = 0; i < vertices.length; i++) {
            const v = vertices[i];
            const x = cos * v.x - sin * v.y + transform.position.x;
            const y = sin * v.x + cos * v.y + transform.position.y;
            const d = x * direction.x + y * direction.y;

            if (d > maxDot) {
                maxDot = d;
                maxX = x;
                maxY = y;
            }
        }

        return { x: maxX, y: maxY };
    }

    private static _processSimplex(simplex: Simplex, direction: IVec2Like): boolean {
        if (simplex.count === 2) {
            return GJK2D._line(simplex, direction);
        } else if (simplex.count === 3) {
            return GJK2D._triangle(simplex, direction);
        }
        return false;
    }

    private static _line(simplex: Simplex, direction: IVec2Like): boolean {
        const A = simplex.points[1];
        const B = simplex.points[0];

        const ABx = B.x - A.x;
        const ABy = B.y - A.y;
        const AOx = -A.x;
        const AOy = -A.y;

        if (ABx * AOx + ABy * AOy > 0) {
            direction.x = -ABy;
            direction.y = ABx;
            const d = direction.x * AOx + direction.y * AOy;
            if (d < 0) {
                direction.x = -direction.x;
                direction.y = -direction.y;
            }
        } else {
            simplex.points[0] = A;
            simplex.count = 1;
            direction.x = AOx;
            direction.y = AOy;
        }

        return false;
    }

    private static _triangle(simplex: Simplex, direction: IVec2Like): boolean {
        const A = simplex.points[2];
        const B = simplex.points[1];
        const C = simplex.points[0];

        const ABx = B.x - A.x;
        const ABy = B.y - A.y;
        const ACx = C.x - A.x;
        const ACy = C.y - A.y;
        const AOx = -A.x;
        const AOy = -A.y;

        const ABperpX = -ABy;
        const ABperpY = ABx;
        const ACperpX = ACy;
        const ACperpY = -ACx;

        if (ABperpX * AOx + ABperpY * AOy > 0) {
            simplex.points[0] = B;
            simplex.points[1] = A;
            simplex.count = 2;
            direction.x = ABperpX;
            direction.y = ABperpY;
            return false;
        }

        if (ACperpX * AOx + ACperpY * AOy > 0) {
            simplex.points[0] = C;
            simplex.points[1] = A;
            simplex.count = 2;
            direction.x = ACperpX;
            direction.y = ACperpY;
            return false;
        }

        return true;
    }
}

export class SAT2D {
    private static readonly EPSILON = 1e-10;

    static testPolygonPolygon(
        verticesA: readonly IVec2Like[],
        verticesB: readonly IVec2Like[],
        transformA: { position: IVec2Like; rotation: number },
        transformB: { position: IVec2Like; rotation: number }
    ): { colliding: boolean; penetration: number; normal: IVec2Like } {
        const result = SAT2D._findMinSeparation(verticesA, verticesB, transformA, transformB);
        if (!result.colliding) {
            return { colliding: false, penetration: 0, normal: { x: 0, y: 0 } };
        }

        const result2 = SAT2D._findMinSeparation(verticesB, verticesA, transformB, transformA);
        if (!result2.colliding) {
            return { colliding: false, penetration: 0, normal: { x: 0, y: 0 } };
        }

        if (result.penetration < result2.penetration) {
            return { colliding: true, penetration: result.penetration, normal: result.normal };
        }

        return {
            colliding: true,
            penetration: result2.penetration,
            normal: { x: -result2.normal.x, y: -result2.normal.y },
        };
    }

    private static _findMinSeparation(
        verticesA: readonly IVec2Like[],
        verticesB: readonly IVec2Like[],
        transformA: { position: IVec2Like; rotation: number },
        transformB: { position: IVec2Like; rotation: number }
    ): { colliding: boolean; penetration: number; normal: IVec2Like } {
        let minPenetration = -Infinity;
        let bestNormalX = 0;
        let bestNormalY = 0;

        const cosA = Math.cos(transformA.rotation);
        const sinA = Math.sin(transformA.rotation);

        for (let i = 0; i < verticesA.length; i++) {
            const j = (i + 1) % verticesA.length;

            const edgeX = verticesA[j].x - verticesA[i].x;
            const edgeY = verticesA[j].y - verticesA[i].y;

            let nx = -edgeY;
            let ny = edgeX;

            const len = Math.sqrt(nx * nx + ny * ny);
            if (len > SAT2D.EPSILON) {
                nx /= len;
                ny /= len;
            }

            const normalX = cosA * nx - sinA * ny;
            const normalY = sinA * nx + cosA * ny;

            const { min: minA, max: maxA } = SAT2D._projectPolygon(verticesA, transformA, normalX, normalY);
            const { min: minB, max: maxB } = SAT2D._projectPolygon(verticesB, transformB, normalX, normalY);

            if (maxA < minB || maxB < minA) {
                return { colliding: false, penetration: 0, normal: { x: 0, y: 0 } };
            }

            const penetration = Math.min(maxA - minB, maxB - minA);
            if (penetration > minPenetration) {
                minPenetration = penetration;
                bestNormalX = normalX;
                bestNormalY = normalY;
            }
        }

        return {
            colliding: true,
            penetration: minPenetration,
            normal: { x: bestNormalX, y: bestNormalY },
        };
    }

    private static _projectPolygon(
        vertices: readonly IVec2Like[],
        transform: { position: IVec2Like; rotation: number },
        axisX: number,
        axisY: number
    ): { min: number; max: number } {
        let min = Infinity;
        let max = -Infinity;

        const cos = Math.cos(transform.rotation);
        const sin = Math.sin(transform.rotation);

        for (let i = 0; i < vertices.length; i++) {
            const v = vertices[i];
            const x = cos * v.x - sin * v.y + transform.position.x;
            const y = sin * v.x + cos * v.y + transform.position.y;
            const projection = x * axisX + y * axisY;

            if (projection < min) min = projection;
            if (projection > max) max = projection;
        }

        return { min, max };
    }
}

export class EPA2D {
    private static readonly MAX_ITERATIONS = 32;
    private static readonly EPSILON = 1e-10;

    static findPenetrationDepth(
        verticesA: readonly IVec2Like[],
        verticesB: readonly IVec2Like[],
        transformA: { position: IVec2Like; rotation: number },
        transformB: { position: IVec2Like; rotation: number },
        simplex: IVec2Like[]
    ): { depth: number; normal: IVec2Like } {
        const polytope = simplex.slice();

        for (let iter = 0; iter < EPA2D.MAX_ITERATIONS; iter++) {
            let minDistance = Infinity;
            let minIndex = 0;
            let minNormalX = 0;
            let minNormalY = 0;

            for (let i = 0; i < polytope.length; i++) {
                const j = (i + 1) % polytope.length;

                const edgeX = polytope[j].x - polytope[i].x;
                const edgeY = polytope[j].y - polytope[i].y;

                let nx = -edgeY;
                let ny = edgeX;

                const len = Math.sqrt(nx * nx + ny * ny);
                if (len > EPA2D.EPSILON) {
                    nx /= len;
                    ny /= len;
                }

                const distance = nx * polytope[i].x + ny * polytope[i].y;

                if (distance < minDistance) {
                    minDistance = distance;
                    minIndex = i;
                    minNormalX = nx;
                    minNormalY = ny;
                }
            }

            const direction: IVec2Like = { x: minNormalX, y: minNormalY };
            GJK2D._support(verticesA, verticesB, transformA, transformB, direction, _supportPointB);

            const distance = minNormalX * _supportPointB.point.x + minNormalY * _supportPointB.point.y;

            if (distance - minDistance < EPA2D.EPSILON) {
                return {
                    depth: minDistance,
                    normal: { x: minNormalX, y: minNormalY },
                };
            }

            polytope.splice(minIndex + 1, 0, { x: _supportPointB.point.x, y: _supportPointB.point.y });
        }

        return { depth: 0, normal: { x: 0, y: 0 } };
    }
}
