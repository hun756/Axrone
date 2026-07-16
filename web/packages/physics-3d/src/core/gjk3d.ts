/**
 * 3D GJK (Gilbert-Johnson-Keerthi) collision detection plus EPA
 * (Expanding Polytope Algorithm) penetration resolution.
 *
 * Operates on convex shapes represented by a support-function
 * `Support3D(dir) -> farthest point in `dir`. This is exact for spheres,
 * oriented boxes, capsules and convex/vertex sets (convex hulls, meshes),
 * replacing the previous AABB fallback for those shapes.
 */

export interface IVec3 {
    x: number;
    y: number;
    z: number;
}

export type Support3D = (dir: IVec3) => IVec3;

export interface GJKContact3D {
    /** true when the two shapes overlap */
    hit: boolean;
    /** unit normal pointing from shape A to shape B (separation direction) */
    normal: IVec3;
    /** penetration depth (> 0 when hit) */
    depth: number;
    /** approximate world contact point */
    point: IVec3;
}

interface MVert {
    /** support point in Minkowski difference space: supportA(dir) - supportB(-dir) */
    v: IVec3;
    /** source support point on A */
    a: IVec3;
    /** source support point on B */
    b: IVec3;
}

const EPS = 1e-6;
const EPS_SQ = EPS * EPS;

function sub(a: IVec3, b: IVec3): IVec3 {
    return { x: a.x - b.x, y: a.y - b.y, z: a.z - b.z };
}
function add(a: IVec3, b: IVec3): IVec3 {
    return { x: a.x + b.x, y: a.y + b.y, z: a.z + b.z };
}
function scale(a: IVec3, s: number): IVec3 {
    return { x: a.x * s, y: a.y * s, z: a.z * s };
}
function negate(a: IVec3): IVec3 {
    return { x: -a.x, y: -a.y, z: -a.z };
}
function dot(a: IVec3, b: IVec3): number {
    return a.x * b.x + a.y * b.y + a.z * b.z;
}
function cross(a: IVec3, b: IVec3): IVec3 {
    return {
        x: a.y * b.z - a.z * b.y,
        y: a.z * b.x - a.x * b.z,
        z: a.x * b.y - a.y * b.x,
    };
}
function lengthSq(a: IVec3): number {
    return dot(a, a);
}
function length(a: IVec3): number {
    return Math.sqrt(lengthSq(a));
}
function normalize(a: IVec3): IVec3 {
    const len = length(a);
    if (len <= EPS) return { x: 0, y: 1, z: 0 };
    const inv = 1 / len;
    return { x: a.x * inv, y: a.y * inv, z: a.z * inv };
}
function sameDirection(a: IVec3, b: IVec3): boolean {
    return dot(a, b) > 0;
}

export class GJK3D {
    /**
     * Returns whether two convex shapes (given by support functions) overlap.
     * When overlapping, `result.hit` is true and `result.normal`/`result.depth`
     * describe the minimum translation to separate B from A.
     */
    static intersect(
        supportA: Support3D,
        supportB: Support3D,
        maxIterations = 32
    ): GJKContact3D {
        const support = (dir: IVec3): MVert => {
            const a = supportA(dir);
            const b = supportB(negate(dir));
            return { v: sub(a, b), a, b };
        };

        let dir: IVec3 = { x: 1, y: 0, z: 0 };
        let simplex: MVert[] = [support(dir)];
        let d = negate(simplex[0].v);

        let hit = false;
        for (let iter = 0; iter < maxIterations; iter++) {
            if (lengthSq(d) < EPS_SQ) {
                hit = true;
                break;
            }
            const a = support(d);
            if (dot(a.v, d) < 0) {
                // No progress toward the origin: a separating axis exists.
                break;
            }
            simplex.push(a);
            if (nextSimplex(simplex, d)) {
                hit = true;
                break;
            }
        }

        if (!hit) {
            return { hit: false, normal: { x: 0, y: 1, z: 0 }, depth: 0, point: { x: 0, y: 0, z: 0 } };
        }

        // EPA needs a full tetrahedron. GJK may terminate with a degenerate
        // (point/line/triangle) simplex when the origin lies on a boundary,
        // so expand it to a tetrahedron first.
        let guard = 0;
        while (simplex.length < 4 && guard < 16) {
            guard++;
            let dir: IVec3;
            if (simplex.length === 1) {
                dir = { x: 0, y: 1, z: 0 };
            } else if (simplex.length === 2) {
                const e = sub(simplex[1].v, simplex[0].v);
                dir = cross(e, { x: 1, y: 0, z: 0 });
                if (lengthSq(dir) < EPS_SQ) dir = cross(e, { x: 0, y: 1, z: 0 });
            } else {
                const ab = sub(simplex[1].v, simplex[0].v);
                const ac = sub(simplex[2].v, simplex[0].v);
                dir = cross(ab, ac);
            }
            if (lengthSq(dir) < EPS_SQ) dir = { x: 0, y: 1, z: 0 };
            if (dot(dir, simplex[0].v) > 0) dir = negate(dir);

            const a = support(dir);
            if (dot(a.v, dir) < 0) break; // separating axis found: not colliding
            simplex.push(a);
            if (nextSimplex(simplex, dir) && simplex.length === 4) break;
        }

        if (simplex.length === 4) {
            const epa = EPA(simplex, support);
            if (epa) {
                return { hit: true, normal: epa.normal, depth: epa.depth, point: epa.point };
            }
        }
        return { hit: true, normal: { x: 0, y: 1, z: 0 }, depth: 0, point: { x: 0, y: 0, z: 0 } };
    }
}

function nextSimplex(simplex: MVert[], dir: IVec3): boolean {
    switch (simplex.length) {
        case 2:
            return lineCase(simplex, dir);
        case 3:
            return triangleCase(simplex, dir);
        case 4:
            return tetrahedronCase(simplex, dir);
        default:
            return false;
    }
}

function lineCase(simplex: MVert[], dir: IVec3): boolean {
    const [a, b] = simplex;
    const ab = sub(b.v, a.v);
    const ao = negate(a.v);
    if (sameDirection(ab, ao)) {
        // Origin is beyond AB on the line: keep A and B, search perpendicular.
        const d = cross(cross(ab, ao), ab);
        dir.x = d.x;
        dir.y = d.y;
        dir.z = d.z;
    } else {
        simplex.length = 0;
        simplex.push(a);
        dir.x = ao.x;
        dir.y = ao.y;
        dir.z = ao.z;
    }
    return false;
}

function triangleCase(simplex: MVert[], dir: IVec3): boolean {
    const [a, b, c] = simplex;
    const ab = sub(b.v, a.v);
    const ac = sub(c.v, a.v);
    const ao = negate(a.v);
    const abc = cross(ab, ac);

    if (sameDirection(cross(abc, ac), ao)) {
        if (sameDirection(ac, ao)) {
            simplex.length = 0;
            simplex.push(a, c);
            const d = cross(cross(ac, ao), ac);
            dir.x = d.x;
            dir.y = d.y;
            dir.z = d.z;
        } else {
            // Fall through to the AB region via the line case.
            simplex.length = 0;
            simplex.push(a, b);
            return lineCase(simplex, dir);
        }
    } else {
        if (sameDirection(cross(ab, abc), ao)) {
            simplex.length = 0;
            simplex.push(a, b);
            const d = cross(cross(ab, ao), ab);
            dir.x = d.x;
            dir.y = d.y;
            dir.z = d.z;
        } else {
            if (sameDirection(abc, ao)) {
                dir.x = abc.x;
                dir.y = abc.y;
                dir.z = abc.z;
            } else {
                const d = negate(abc);
                dir.x = d.x;
                dir.y = d.y;
                dir.z = d.z;
            }
            // Keep the full triangle.
        }
    }
    return false;
}

function tetrahedronCase(simplex: MVert[], dir: IVec3): boolean {
    const [a, b, c, d] = simplex;
    const ab = sub(b.v, a.v);
    const ac = sub(c.v, a.v);
    const ad = sub(d.v, a.v);
    const ao = negate(a.v);

    const abc = cross(ab, ac);
    const acd = cross(ac, ad);
    const adb = cross(ad, ab);

    if (sameDirection(abc, ao)) {
        simplex.length = 0;
        simplex.push(a, b, c);
        return triangleCase(simplex, dir);
    }
    if (sameDirection(acd, ao)) {
        simplex.length = 0;
        simplex.push(a, c, d);
        return triangleCase(simplex, dir);
    }
    if (sameDirection(adb, ao)) {
        simplex.length = 0;
        simplex.push(a, d, b);
        return triangleCase(simplex, dir);
    }
    // Origin is inside the tetrahedron: definite penetration.
    return true;
}

interface EPAResult {
    normal: IVec3;
    depth: number;
    point: IVec3;
}

function EPA(simplex: MVert[], support: (dir: IVec3) => MVert, maxIterations = 64): EPAResult | null {
    const vertices: IVec3[] = simplex.map((m) => m.v);
    let faces: number[][] = [
        [0, 1, 2],
        [0, 2, 3],
        [0, 3, 1],
        [1, 3, 2],
    ];

    let bestNormal: IVec3 = { x: 0, y: 1, z: 0 };
    let bestDepth = 0;

    for (let iter = 0; iter < maxIterations; iter++) {
        let minDist = Infinity;
        let minNormal: IVec3 = { x: 0, y: 1, z: 0 };
        let minFace = -1;

        for (const face of faces) {
            const [ia, ib, ic] = face;
            const pa = vertices[ia];
            const pb = vertices[ib];
            const pc = vertices[ic];
            let n = cross(sub(pb, pa), sub(pc, pa));
            const nLen = length(n);
            if (nLen <= EPS) continue;
            n = scale(n, 1 / nLen);
            if (dot(n, pa) < 0) {
                n = negate(n);
            }
            const dist = dot(n, pa);
            if (dist < minDist) {
                minDist = dist;
                minNormal = n;
                minFace = face[0] * 1000000 + face[1] * 1000 + face[2];
            }
        }

        if (minFace < 0) break;

        bestNormal = minNormal;
        bestDepth = minDist;

        const sup = support(minNormal);
        const newDist = dot(minNormal, sup.v);
        if (minDist > 1e-4 && newDist - minDist < EPS) {
            return { normal: minNormal, depth: minDist, point: scale(minNormal, minDist) };
        }

        const newIndex = vertices.length;
        vertices.push(sup.v);

        const remaining: number[][] = [];
        const edges: Array<[number, number]> = [];
        for (const face of faces) {
            const [ia, ib, ic] = face;
            const pa = vertices[ia];
            const normal = (() => {
                let n = cross(sub(vertices[ib], pa), sub(vertices[ic], pa));
                const nLen = length(n);
                if (nLen <= EPS) return { x: 0, y: 0, z: 0 };
                n = scale(n, 1 / nLen);
                return dot(n, pa) < 0 ? negate(n) : n;
            })();
            if (dot(normal, sub(sup.v, pa)) > 0) {
                edges.push([ia, ib], [ib, ic], [ic, ia]);
            } else {
                remaining.push(face);
            }
        }

        const boundary: Array<[number, number]> = [];
        for (let i = 0; i < edges.length; i++) {
            const [e0, e1] = edges[i];
            let shared = 0;
            for (let j = 0; j < edges.length; j++) {
                if (i === j) continue;
                const [f0, f1] = edges[j];
                if ((f0 === e0 && f1 === e1) || (f0 === e1 && f1 === e0)) {
                    shared++;
                    break;
                }
            }
            if (shared === 0) boundary.push([e0, e1]);
        }

        for (const [e0, e1] of boundary) {
            remaining.push([e0, e1, newIndex]);
        }
        faces = remaining;
    }

    return bestDepth > 0 ? { normal: bestNormal, depth: bestDepth, point: scale(bestNormal, bestDepth) } : null;
}

/** Builds a convex support function from a world-space vertex set. */
export function supportFromVertices(worldVertices: ReadonlyArray<IVec3>): Support3D {
    return (dir: IVec3): IVec3 => {
        let best = worldVertices[0];
        let bestDot = dot(best, dir);
        for (let i = 1; i < worldVertices.length; i++) {
            const d = dot(worldVertices[i], dir);
            if (d > bestDot) {
                bestDot = d;
                best = worldVertices[i];
            }
        }
        return { x: best.x, y: best.y, z: best.z };
    };
}
