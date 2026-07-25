import type { IVec2Like } from '@axrone/numeric';
import { Fnv1a32 } from '@axrone/hash';
import type {
    GradientSpread,
    ShapeApproximationOptions,
    ShapeBounds,
    ShapeFingerprint,
    ShapePointInput,
} from './types';
import { PaintValidationError, ShapeValidationError } from './errors';

export const EPSILON = 1e-9;
export const TAU = Math.PI * 2;
export const DEFAULT_CURVE_TOLERANCE = 0.25;
export const DEFAULT_MIN_CURVE_SEGMENTS = 16;
export const DEFAULT_MAX_CURVE_SEGMENTS = 128;
export const DEFAULT_GRADIENT_LOOKUP_SIZE = 256;
export const DEFAULT_REGISTRY_MAX_SHAPES = 2048;
export const DEFAULT_REGISTRY_MAX_COMPILED = 4096;

export const clamp = (value: number, min: number, max: number): number =>
    value < min ? min : value > max ? max : value;

export const clamp01 = (value: number): number => clamp(value, 0, 1);

export const isFiniteNumber = (value: unknown): value is number =>
    typeof value === 'number' && Number.isFinite(value);

export const normalizeNumberKey = (value: number): string =>
    Object.is(value, -0) ? '0' : Number.isInteger(value) ? `${value}` : `${value}`;

export const assertFiniteNumber = (value: unknown, name: string): number => {
    if (!isFiniteNumber(value)) {
        throw new ShapeValidationError(`${name} must be a finite number`);
    }
    return value;
};

export const assertPositiveNumber = (value: unknown, name: string): number => {
    const normalized = assertFiniteNumber(value, name);
    if (normalized <= 0) {
        throw new ShapeValidationError(`${name} must be greater than 0`);
    }
    return normalized;
};

export const assertNonNegativeNumber = (value: unknown, name: string): number => {
    const normalized = assertFiniteNumber(value, name);
    if (normalized < 0) {
        throw new ShapeValidationError(`${name} must be greater than or equal to 0`);
    }
    return normalized;
};

export const toPoint = (value: ShapePointInput, name: string): Readonly<IVec2Like> => {
    if (Array.isArray(value)) {
        if (value.length < 2) {
            throw new ShapeValidationError(`${name} must have at least two numeric values`);
        }

        return Object.freeze({
            x: assertFiniteNumber(value[0], `${name}[0]`),
            y: assertFiniteNumber(value[1], `${name}[1]`),
        });
    }

    if (value && typeof value === 'object' && 'x' in value && 'y' in value) {
        return Object.freeze({
            x: assertFiniteNumber(value.x, `${name}.x`),
            y: assertFiniteNumber(value.y, `${name}.y`),
        });
    }

    throw new ShapeValidationError(`${name} must be a point-like value`);
};

export const createBounds = (
    minX: number,
    minY: number,
    maxX: number,
    maxY: number
): ShapeBounds => {
    const safeMinX = Math.min(minX, maxX);
    const safeMaxX = Math.max(minX, maxX);
    const safeMinY = Math.min(minY, maxY);
    const safeMaxY = Math.max(minY, maxY);

    return Object.freeze({
        minX: safeMinX,
        minY: safeMinY,
        maxX: safeMaxX,
        maxY: safeMaxY,
        width: safeMaxX - safeMinX,
        height: safeMaxY - safeMinY,
        centerX: (safeMinX + safeMaxX) * 0.5,
        centerY: (safeMinY + safeMaxY) * 0.5,
    });
};

export const expandBounds = (bounds: ShapeBounds, amount: number): ShapeBounds =>
    createBounds(
        bounds.minX - amount,
        bounds.minY - amount,
        bounds.maxX + amount,
        bounds.maxY + amount
    );

export const pointInBounds = (bounds: ShapeBounds, point: Readonly<IVec2Like>): boolean =>
    point.x >= bounds.minX - EPSILON &&
    point.x <= bounds.maxX + EPSILON &&
    point.y >= bounds.minY - EPSILON &&
    point.y <= bounds.maxY + EPSILON;

export const distanceSquared = (
    ax: number,
    ay: number,
    bx: number,
    by: number
): number => {
    const dx = bx - ax;
    const dy = by - ay;
    return dx * dx + dy * dy;
};

export const distance = (ax: number, ay: number, bx: number, by: number): number =>
    Math.sqrt(distanceSquared(ax, ay, bx, by));

export const distanceToSegmentSquared = (
    px: number,
    py: number,
    ax: number,
    ay: number,
    bx: number,
    by: number
): number => {
    const abx = bx - ax;
    const aby = by - ay;
    const abLengthSquared = abx * abx + aby * aby;

    if (abLengthSquared <= EPSILON) {
        return distanceSquared(px, py, ax, ay);
    }

    const t = clamp(((px - ax) * abx + (py - ay) * aby) / abLengthSquared, 0, 1);
    const cx = ax + abx * t;
    const cy = ay + aby * t;
    return distanceSquared(px, py, cx, cy);
};

export const polygonSignedArea = (points: ArrayLike<number>): number => {
    const count = Math.floor(points.length / 2);
    let area = 0;

    for (let index = 0; index < count; index++) {
        const current = index * 2;
        const next = ((index + 1) % count) * 2;
        area += points[current] * points[next + 1] - points[current + 1] * points[next];
    }

    return area * 0.5;
};

export const polygonAbsoluteArea = (points: ArrayLike<number>): number =>
    Math.abs(polygonSignedArea(points));

export const polygonPerimeter = (points: ArrayLike<number>): number => {
    const count = Math.floor(points.length / 2);
    if (count < 2) {
        return 0;
    }

    let perimeter = 0;
    for (let index = 0; index < count; index++) {
        const current = index * 2;
        const next = ((index + 1) % count) * 2;
        perimeter += Math.hypot(
            (points[next] as number) - (points[current] as number),
            (points[next + 1] as number) - (points[current + 1] as number)
        );
    }
    return perimeter;
};

export const polygonCentroid = (points: ArrayLike<number>): Readonly<IVec2Like> => {
    const count = Math.floor(points.length / 2);
    if (count === 0) {
        return Object.freeze({ x: 0, y: 0 });
    }

    let cx = 0;
    let cy = 0;
    let signedAreaTimesSix = 0;

    for (let index = 0; index < count; index++) {
        const current = index * 2;
        const next = ((index + 1) % count) * 2;
        const x0 = points[current] as number;
        const y0 = points[current + 1] as number;
        const x1 = points[next] as number;
        const y1 = points[next + 1] as number;
        const cross = x0 * y1 - x1 * y0;
        cx += (x0 + x1) * cross;
        cy += (y0 + y1) * cross;
        signedAreaTimesSix += cross;
    }

    if (Math.abs(signedAreaTimesSix) <= EPSILON) {
        let sx = 0;
        let sy = 0;
        for (let index = 0; index < count; index++) {
            sx += points[index * 2] as number;
            sy += points[index * 2 + 1] as number;
        }
        return Object.freeze({ x: sx / count, y: sy / count });
    }

    return Object.freeze({
        x: cx / (signedAreaTimesSix * 3),
        y: cy / (signedAreaTimesSix * 3),
    });
};

export const polygonBounds = (points: ArrayLike<number>): ShapeBounds => {
    const count = Math.floor(points.length / 2);
    if (count === 0) {
        return createBounds(0, 0, 0, 0);
    }

    let minX = Infinity;
    let minY = Infinity;
    let maxX = -Infinity;
    let maxY = -Infinity;

    for (let index = 0; index < count; index++) {
        const x = points[index * 2] as number;
        const y = points[index * 2 + 1] as number;
        if (x < minX) minX = x;
        if (y < minY) minY = y;
        if (x > maxX) maxX = x;
        if (y > maxY) maxY = y;
    }

    return createBounds(minX, minY, maxX, maxY);
};

export const pointInConvexPolygon = (
    points: ArrayLike<number>,
    point: Readonly<IVec2Like>
): boolean => {
    const count = Math.floor(points.length / 2);
    if (count < 3) {
        return false;
    }

    const winding = polygonSignedArea(points) >= 0 ? 1 : -1;

    for (let index = 0; index < count; index++) {
        const current = index * 2;
        const next = ((index + 1) % count) * 2;
        const edgeX = (points[next] as number) - (points[current] as number);
        const edgeY = (points[next + 1] as number) - (points[current + 1] as number);
        const pointX = point.x - (points[current] as number);
        const pointY = point.y - (points[current + 1] as number);
        const cross = edgeX * pointY - edgeY * pointX;

        if (cross * winding < -EPSILON) {
            return false;
        }
    }

    return true;
};

export const pointInPolygon = (
    points: ArrayLike<number>,
    point: Readonly<IVec2Like>
): boolean => {
    const count = Math.floor(points.length / 2);
    if (count < 3) {
        return false;
    }

    let inside = false;
    let j = count - 1;

    for (let i = 0; i < count; i++) {
        const xi = points[i * 2] as number;
        const yi = points[i * 2 + 1] as number;
        const xj = points[j * 2] as number;
        const yj = points[j * 2 + 1] as number;

        const intersects =
            yi > point.y !== yj > point.y &&
            point.x < ((xj - xi) * (point.y - yi)) / ((yj - yi) || EPSILON) + xi;
        if (intersects) {
            inside = !inside;
        }
        j = i;
    }

    return inside;
};

export const isConvexPolygon = (points: ArrayLike<number>): boolean => {
    const count = Math.floor(points.length / 2);
    if (count < 3) {
        return false;
    }

    let sign = 0;
    for (let index = 0; index < count; index++) {
        const a = index * 2;
        const b = ((index + 1) % count) * 2;
        const c = ((index + 2) % count) * 2;
        const ax = points[a] as number;
        const ay = points[a + 1] as number;
        const bx = points[b] as number;
        const by = points[b + 1] as number;
        const cx = points[c] as number;
        const cy = points[c + 1] as number;
        const cross = (bx - ax) * (cy - by) - (by - ay) * (cx - bx);
        if (Math.abs(cross) <= EPSILON) {
            continue;
        }
        const currentSign = cross > 0 ? 1 : -1;
        if (sign === 0) {
            sign = currentSign;
        } else if (sign !== currentSign) {
            return false;
        }
    }

    return sign !== 0;
};

export const isSimplePolygon = (points: ArrayLike<number>): boolean => {
    const count = Math.floor(points.length / 2);
    if (count < 3) {
        return false;
    }

    for (let i = 0; i < count; i++) {
        const a1 = i * 2;
        const a2 = ((i + 1) % count) * 2;
        for (let j = i + 1; j < count; j++) {
            if (i === j) continue;
            const b1 = j * 2;
            const b2 = ((j + 1) % count) * 2;
            if (i === 0 && j === count - 1) continue;
            if (i === j + 1) continue;
            if (
                segmentsIntersect(
                    points[a1] as number,
                    points[a1 + 1] as number,
                    points[a2] as number,
                    points[a2 + 1] as number,
                    points[b1] as number,
                    points[b1 + 1] as number,
                    points[b2] as number,
                    points[b2 + 1] as number
                )
            ) {
                return false;
            }
        }
    }
    return true;
};

const segmentsIntersect = (
    ax: number,
    ay: number,
    bx: number,
    by: number,
    cx: number,
    cy: number,
    dx: number,
    dy: number
): boolean => {
    const r1 = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
    const r2 = (bx - ax) * (dy - ay) - (by - ay) * (dx - ax);
    const r3 = (dx - cx) * (ay - cy) - (dy - cy) * (ax - cx);
    const r4 = (dx - cx) * (by - cy) - (dy - cy) * (bx - cx);

    if (((r1 > 0 && r2 < 0) || (r1 < 0 && r2 > 0)) && ((r3 > 0 && r4 < 0) || (r3 < 0 && r4 > 0))) {
        return true;
    }

    if (Math.abs(r1) <= EPSILON && onSegment(ax, ay, cx, cy, bx, by)) return true;
    if (Math.abs(r2) <= EPSILON && onSegment(ax, ay, dx, dy, bx, by)) return true;
    if (Math.abs(r3) <= EPSILON && onSegment(cx, cy, ax, ay, dx, dy)) return true;
    if (Math.abs(r4) <= EPSILON && onSegment(cx, cy, bx, by, dx, dy)) return true;

    return false;
};

const onSegment = (
    px: number,
    py: number,
    qx: number,
    qy: number,
    rx: number,
    ry: number
): boolean =>
    Math.min(px, rx) - EPSILON <= qx &&
    qx <= Math.max(px, rx) + EPSILON &&
    Math.min(py, ry) - EPSILON <= qy &&
    qy <= Math.max(py, ry) + EPSILON;

export const pointInPolygonWithHoles = (
    outer: ArrayLike<number>,
    holes: ReadonlyArray<ArrayLike<number>>,
    point: Readonly<IVec2Like>
): boolean => {
    if (!pointInPolygon(outer, point)) {
        return false;
    }
    for (let i = 0; i < holes.length; i++) {
        if (pointInPolygon(holes[i] as ArrayLike<number>, point)) {
            return false;
        }
    }
    return true;
};

export const pointToPolygonEdgeDistance = (
    points: ArrayLike<number>,
    point: Readonly<IVec2Like>
): number => {
    const count = Math.floor(points.length / 2);
    if (count < 2) {
        return Infinity;
    }

    let minDistanceSquared = Infinity;
    for (let i = 0; i < count; i++) {
        const a = i * 2;
        const b = ((i + 1) % count) * 2;
        const d = distanceToSegmentSquared(
            point.x,
            point.y,
            points[a] as number,
            points[a + 1] as number,
            points[b] as number,
            points[b + 1] as number
        );
        if (d < minDistanceSquared) {
            minDistanceSquared = d;
        }
    }
    return Math.sqrt(minDistanceSquared);
};

export const removeCollinearVertices = (points: ArrayLike<number>): Float32Array => {
    const count = Math.floor(points.length / 2);
    if (count < 3) {
        return new Float32Array(points);
    }

    const result: number[] = [];
    for (let i = 0; i < count; i++) {
        const a = i * 2;
        const b = ((i + 1) % count) * 2;
        const c = ((i + 2) % count) * 2;
        const ax = points[a] as number;
        const ay = points[a + 1] as number;
        const bx = points[b] as number;
        const by = points[b + 1] as number;
        const cx = points[c] as number;
        const cy = points[c + 1] as number;
        const cross = (bx - ax) * (cy - by) - (by - ay) * (cx - bx);
        if (Math.abs(cross) > EPSILON) {
            result.push(bx, by);
        }
    }

    return result.length === points.length
        ? new Float32Array(points)
        : new Float32Array(result);
};

export const deduplicateVertices = (points: ArrayLike<number>, epsilon: number = EPSILON): Float32Array => {
    const count = Math.floor(points.length / 2);
    if (count < 2) {
        return new Float32Array(points);
    }

    const epsilonSq = epsilon * epsilon;
    const result: number[] = [points[0] as number, points[1] as number];
    for (let i = 1; i < count; i++) {
        const x = points[i * 2] as number;
        const y = points[i * 2 + 1] as number;
        const lastX = result[result.length - 2] as number;
        const lastY = result[result.length - 1] as number;
        if ((x - lastX) * (x - lastX) + (y - lastY) * (y - lastY) > epsilonSq) {
            result.push(x, y);
        }
    }

    const firstX = result[0] as number;
    const firstY = result[1] as number;
    const lastX = result[result.length - 2] as number;
    const lastY = result[result.length - 1] as number;
    if (
        (firstX - lastX) * (firstX - lastX) + (firstY - lastY) * (firstY - lastY) <=
        epsilonSq
    ) {
        result.length -= 2;
    }

    return new Float32Array(result);
};

export const isPointInTriangle = (
    px: number,
    py: number,
    ax: number,
    ay: number,
    bx: number,
    by: number,
    cx: number,
    cy: number
): boolean => {
    const d1 = sign(px, py, ax, ay, bx, by);
    const d2 = sign(px, py, bx, by, cx, cy);
    const d3 = sign(px, py, cx, cy, ax, ay);
    const hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
    const hasPos = d1 > 0 || d2 > 0 || d3 > 0;
    return !(hasNeg && hasPos);
};

const sign = (
    px: number,
    py: number,
    ax: number,
    ay: number,
    bx: number,
    by: number
): number => (px - bx) * (ay - by) - (ax - bx) * (py - by);

export const triangulateEarClipping = (
    points: ArrayLike<number>
): Uint16Array | Uint32Array => {
    const initialCount = Math.floor(points.length / 2);
    if (initialCount < 3) {
        return new Uint16Array(0);
    }

    const working = new Float32Array(points.length);
    working.set(points);
    let vertexCount = initialCount;
    const indices: number[] = [];
    const useUint32 = initialCount > 65535;
    const maxIterations = vertexCount * vertexCount + 1;
    let iterations = 0;

    while (vertexCount > 3 && iterations++ < maxIterations) {
        let earFound = false;

        for (let i = 0; i < vertexCount; i++) {
            const a = i;
            const b = (i + 1) % vertexCount;
            const c = (i + 2) % vertexCount;

            const ax = working[a * 2] as number;
            const ay = working[a * 2 + 1] as number;
            const bx = working[b * 2] as number;
            const by = working[b * 2 + 1] as number;
            const cx = working[c * 2] as number;
            const cy = working[c * 2 + 1] as number;

            const cross = (bx - ax) * (cy - by) - (by - ay) * (cx - bx);
            if (cross <= EPSILON) {
                continue;
            }

            let isEar = true;
            for (let j = 0; j < vertexCount; j++) {
                if (j === a || j === b || j === c) continue;
                const px = working[j * 2] as number;
                const py = working[j * 2 + 1] as number;
                if (isPointInTriangle(px, py, ax, ay, bx, by, cx, cy)) {
                    isEar = false;
                    break;
                }
            }

            if (!isEar) continue;

            indices.push(a, b, c);
            working[b * 2] = working[c * 2] as number;
            working[b * 2 + 1] = working[c * 2 + 1] as number;
            for (let k = c; k < vertexCount - 1; k++) {
                working[k * 2] = working[(k + 1) * 2] as number;
                working[k * 2 + 1] = working[(k + 1) * 2 + 1] as number;
            }
            vertexCount--;
            earFound = true;
            break;
        }

        if (!earFound) {
            break;
        }
    }

    if (vertexCount === 3) {
        indices.push(0, 1, 2);
    }

    return useUint32 ? new Uint32Array(indices) : new Uint16Array(indices);
};

export const pointInRing = (
    outer: ArrayLike<number>,
    holes: ReadonlyArray<ArrayLike<number>>,
    point: Readonly<IVec2Like>
): boolean => pointInPolygonWithHoles(outer, holes, point);

export const normalizeContourOrientation = (
    contour: Float32Array,
    ccw: boolean = true
): Float32Array => {
    const area = polygonSignedArea(contour);
    const isCcw = area >= 0;
    if (isCcw === ccw) {
        return contour;
    }

    const reversed = new Float32Array(contour.length);
    const count = contour.length / 2;

    for (let index = 0; index < count; index++) {
        const source = ((count - index) % count) * 2;
        const target = index * 2;
        reversed[target] = contour[source] as number;
        reversed[target + 1] = contour[source + 1] as number;
    }

    return reversed;
};

export const pointsToFloat32 = (points: ReadonlyArray<Readonly<IVec2Like>>): Float32Array => {
    const buffer = new Float32Array(points.length * 2);
    for (let i = 0; i < points.length; i++) {
        buffer[i * 2] = (points[i] as Readonly<IVec2Like>).x;
        buffer[i * 2 + 1] = (points[i] as Readonly<IVec2Like>).y;
    }
    return buffer;
};

export const float32ToPoints = (contour: Float32Array): Readonly<IVec2Like>[] => {
    const count = contour.length / 2;
    const points: Readonly<IVec2Like>[] = new Array(count);
    for (let i = 0; i < count; i++) {
        points[i] = Object.freeze({
            x: contour[i * 2] as number,
            y: contour[i * 2 + 1] as number,
        });
    }
    return points;
};

export const toIndexArray = (
    indices: readonly number[],
    vertexCount: number
): Uint16Array | Uint32Array =>
    vertexCount <= 65535 ? new Uint16Array(indices) : new Uint32Array(indices);

export const approximateCurveSegments = (
    radiusX: number,
    radiusY: number,
    options: ShapeApproximationOptions = {}
): number => {
    const tolerance = Math.max(options.curveTolerance ?? DEFAULT_CURVE_TOLERANCE, EPSILON);
    const minSegments = Math.max(3, Math.floor(options.minCurveSegments ?? DEFAULT_MIN_CURVE_SEGMENTS));
    const maxSegments = Math.max(
        minSegments,
        Math.floor(options.maxCurveSegments ?? DEFAULT_MAX_CURVE_SEGMENTS)
    );
    const radius = Math.max(Math.abs(radiusX), Math.abs(radiusY));

    if (radius <= EPSILON) {
        return minSegments;
    }

    const ratio = clamp(1 - tolerance / radius, -1, 1);
    const theta = Math.max(EPSILON, 2 * Math.acos(ratio));
    const segments = Math.ceil(TAU / theta);
    return clamp(segments, minSegments, maxSegments);
};

export const applyGradientSpread = (value: number, spread: GradientSpread): number => {
    if (!Number.isFinite(value)) {
        throw new PaintValidationError('Gradient sample value must be finite');
    }

    switch (spread) {
        case 'pad':
            return clamp01(value);
        case 'repeat': {
            const normalized = value % 1;
            return normalized < 0 ? normalized + 1 : normalized;
        }
        case 'reflect': {
            const wrapped = Math.abs(value % 2);
            return wrapped > 1 ? 2 - wrapped : wrapped;
        }
        default:
            return clamp01(value);
    }
};

export const hashString = (value: string): string => {
    const h = new Fnv1a32();
    h.updateString(value);
    return h.digestHex();
};

export const formatPointKey = (point: Readonly<IVec2Like>): string =>
    `${normalizeNumberKey(point.x)},${normalizeNumberKey(point.y)}`;

export const formatBoundsKey = (bounds: ShapeBounds): string =>
    `${normalizeNumberKey(bounds.minX)},${normalizeNumberKey(bounds.minY)},${normalizeNumberKey(bounds.maxX)},${normalizeNumberKey(bounds.maxY)}`;

export const withFingerprintPrefix = <K extends string>(
    prefix: K,
    value: string
): `${K}:${string}` => `${prefix}:${value}`;
