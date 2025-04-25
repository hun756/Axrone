import { BoxMullerFactory } from './box_muller';

export type Scalar = number;
export type Vec2 = { x: Scalar; y: Scalar };
export type ReadonlyVec2 = Readonly<Vec2>;

export const EPSILON = 1e-10;
export const PI_2 = Math.PI * 2;
export const DEG_TO_RAD = Math.PI / 180;
export const RAD_TO_DEG = 180 / Math.PI;
export const SQRT2 = Math.SQRT2;
export const HALF_PI = Math.PI / 2;
export const INV_PI = 1 / Math.PI;

export const ZERO = Object.freeze<ReadonlyVec2>({ x: 0, y: 0 });
export const ONE = Object.freeze<ReadonlyVec2>({ x: 1, y: 1 });
export const UNIT_X = Object.freeze<ReadonlyVec2>({ x: 1, y: 0 });
export const UNIT_Y = Object.freeze<ReadonlyVec2>({ x: 0, y: 1 });
export const UP = UNIT_Y;
export const DOWN = Object.freeze<ReadonlyVec2>({ x: 0, y: -1 });
export const LEFT = Object.freeze<ReadonlyVec2>({ x: -1, y: 0 });
export const RIGHT = UNIT_X;

export const isVec2 = (v: unknown): v is ReadonlyVec2 =>
    v !== null &&
    typeof v === 'object' &&
    'x' in v &&
    'y' in v &&
    typeof (v as any).x === 'number' &&
    typeof (v as any).y === 'number';


export const create = (x = 0, y = 0): Vec2 => ({ x, y });
export const clone = <T extends ReadonlyVec2>(v: T): Vec2 => ({ x: v.x, y: v.y });

export const fromValues = (x: Scalar, y: Scalar): Vec2 => ({ x, y });

export const fromArray = (arr: ArrayLike<Scalar>, offset = 0): Vec2 => {
    if (offset < 0) {
        throw new RangeError('Offset cannot be negative');
    }
    if (arr.length < offset + 2) {
        throw new RangeError(`Array must have at least ${offset + 2} elements when using offset ${offset}`);
    }
    return { x: arr[offset], y: arr[offset + 1] };
}

export const fromAngle = (angleRad: Scalar, magnitude = 1): Vec2 => ({
    x: Math.cos(angleRad) * magnitude,
    y: Math.sin(angleRad) * magnitude,
});

export const fromDegrees = (degrees: Scalar, magnitude = 1): Vec2 =>
    fromAngle(degrees * DEG_TO_RAD, magnitude);

export const fromPolar = (radius: Scalar, angleRad: Scalar): Vec2 => fromAngle(angleRad, radius);

const standardNormalDist = BoxMullerFactory.createStandard({
    algorithm: 'polar',
    useCache: true,
    optimizeFor: 'speed',
});

const _boundedNormalRandom = (): Scalar => {
    const MAX_ATTEMPTS = 10;
    for (let i = 0; i < MAX_ATTEMPTS; i++) {
        const value = standardNormalDist.sample();
        if (value >= -1 && value <= 1) {
            return value;
        }
    }
    return Math.max(-1, Math.min(1, standardNormalDist.sample()));
};

const _normalRandom = (): Scalar => _boundedNormalRandom();

export const random = (scale = 1): Vec2 => {
    const u = 1 - Math.random();
    const v = Math.random();
    const r = scale * Math.sqrt(-2 * Math.log(u));
    const theta = PI_2 * v;
    return {
        x: r * Math.cos(theta),
        y: r * Math.sin(theta),
    };
};

export const randomFast = (scale = 1): Vec2 => {
    const angle = Math.random() * PI_2;
    return {
        x: Math.cos(angle) * scale,
        y: Math.sin(angle) * scale,
    };
};

export const randomNormal = (): Vec2 => {
    const u = 1 - Math.random();
    const v = Math.random();
    return {
        x: Math.sqrt(-2 * Math.log(u)) * Math.cos(PI_2 * v),
        y: Math.sqrt(-2 * Math.log(u)) * Math.sin(PI_2 * v),
    };
};

export const randomBox = (minX: Scalar, maxX: Scalar, minY: Scalar, maxY: Scalar): Vec2 => ({
    x: minX + Math.random() * (maxX - minX),
    y: minY + Math.random() * (maxY - minY),
});

export const randomBoxNormal = (minX: Scalar, maxX: Scalar, minY: Scalar, maxY: Scalar): Vec2 => ({
    x: minX + (_normalRandom() + 1) * 0.5 * (maxX - minX),
    y: minY + (_normalRandom() + 1) * 0.5 * (maxY - minY),
});

export const set = <T extends Vec2>(v: T, x: Scalar, y: Scalar): T => {
    v.x = x;
    v.y = y;
    return v;
};

export const copy = <T extends Vec2>(out: T, v: ReadonlyVec2): T => {
    out.x = v.x;
    out.y = v.y;
    return out;
};

export const add = <T extends Vec2>(out: T, a: ReadonlyVec2, b: ReadonlyVec2): T => {
    out.x = a.x + b.x;
    out.y = a.y + b.y;
    return out;
};

export const addScalar = <T extends Vec2>(out: T, v: ReadonlyVec2, s: Scalar): T => {
    out.x = v.x + s;
    out.y = v.y + s;
    return out;
};

export const subtract = <T extends Vec2>(out: T, a: ReadonlyVec2, b: ReadonlyVec2): T => {
    out.x = a.x - b.x;
    out.y = a.y - b.y;
    return out;
};

export const subtractScalar = <T extends Vec2>(out: T, v: ReadonlyVec2, s: Scalar): T => {
    out.x = v.x - s;
    out.y = v.y - s;
    return out;
};

export const multiply = <T extends Vec2>(out: T, a: ReadonlyVec2, b: ReadonlyVec2): T => {
    out.x = a.x * b.x;
    out.y = a.y * b.y;
    return out;
};

export const multiplyScalar = <T extends Vec2>(out: T, v: ReadonlyVec2, s: Scalar): T => {
    out.x = v.x * s;
    out.y = v.y * s;
    return out;
};

export const divide = <T extends Vec2>(out: T, a: ReadonlyVec2, b: ReadonlyVec2): T => {
    const bx = b.x;
    const by = b.y;

    if (Math.abs(bx) < EPSILON || Math.abs(by) < EPSILON) {
        throw new Error('Division by zero or near-zero value');
    }

    out.x = a.x / bx;
    out.y = a.y / by;
    return out;
};

export const divideScalar = <T extends Vec2>(out: T, v: ReadonlyVec2, s: Scalar): T => {
    if (Math.abs(s) < EPSILON) {
        throw new Error('Division by zero or near-zero value');
    }

    const invS = 1 / s;
    out.x = v.x * invS;
    out.y = v.y * invS;
    return out;
};

export const negate = <T extends Vec2>(out: T, v: ReadonlyVec2): T => {
    out.x = -v.x;
    out.y = -v.y;
    return out;
};

export const inverse = <T extends Vec2>(out: T, v: ReadonlyVec2): T => {
    const vx = v.x;
    const vy = v.y;

    if (Math.abs(vx) < EPSILON || Math.abs(vy) < EPSILON) {
        throw new Error('Inversion of zero or near-zero value');
    }

    out.x = 1 / vx;
    out.y = 1 / vy;
    return out;
};

export const inverseSafe = <T extends Vec2>(out: T, v: ReadonlyVec2, defaultValue = 0): T => {
    const vx = v.x;
    const vy = v.y;

    out.x = Math.abs(vx) < EPSILON ? defaultValue : 1 / vx;
    out.y = Math.abs(vy) < EPSILON ? defaultValue : 1 / vy;
    return out;
};

export const lengthSq = (v: ReadonlyVec2): Scalar => {
    const x = v.x;
    const y = v.y;
    return x * x + y * y;
};

export const length = (v: ReadonlyVec2): Scalar => Math.sqrt(lengthSq(v));

export const fastLength = (v: ReadonlyVec2): Scalar => {
    // Fast approximation of vector length (~3.4% error max)
    const x = Math.abs(v.x);
    const y = Math.abs(v.y);
    const min = Math.min(x, y);
    const max = Math.max(x, y);
    return max + 0.3 * min;
};

export const normalize = <T extends Vec2>(out: T, v: ReadonlyVec2): T => {
    const x = v.x;
    const y = v.y;
    let lenSq = x * x + y * y;

    if (lenSq > EPSILON) {
        const invLen = 1 / Math.sqrt(lenSq);
        out.x = x * invLen;
        out.y = y * invLen;
    } else {
        out.x = 0;
        out.y = 0;
    }

    return out;
};

export const normalizeFast = <T extends Vec2>(out: T, v: ReadonlyVec2): T => {
    const x = v.x;
    const y = v.y;
    const lenSq = x * x + y * y;

    if (lenSq > EPSILON) {
        // Fast inverse square root approximation (Q_rsqrt)
        let i = 0;
        const buf = new ArrayBuffer(4);
        const view = new DataView(buf);
        view.setFloat32(0, lenSq);
        i = view.getInt32(0);
        i = 0x5f3759df - (i >> 1);
        view.setInt32(0, i);
        let invLen = view.getFloat32(0);

        invLen = invLen * (1.5 - lenSq * 0.5 * invLen * invLen);
        invLen = invLen * (1.5 - lenSq * 0.5 * invLen * invLen);

        out.x = x * invLen;
        out.y = y * invLen;
    } else {
        out.x = 0;
        out.y = 0;
    }

    return out;
};

export const perpendicular = <T extends Vec2>(out: T, v: ReadonlyVec2): T => {
    const x = v.x;
    const y = v.y;

    if (x === 0 && y === 0) {
        out.x = 0;
        out.y = 0;
        return out;
    }

    out.x = -y;
    out.y = x;
    return out;
};

export const perpendicularCCW = <T extends Vec2>(out: T, v: ReadonlyVec2): T => {
    const x = v.x;
    const y = v.y;

    if (x === 0 && y === 0) {
        out.x = 0;
        out.y = 0;
        return out;
    }

    out.x = y;
    out.y = -x;
    return out;
};

export const dot = (a: ReadonlyVec2, b: ReadonlyVec2): Scalar => a.x * b.x + a.y * b.y;

export const cross = (a: ReadonlyVec2, b: ReadonlyVec2): Scalar => a.x * b.y - a.y * b.x;

export const distanceSq = (a: ReadonlyVec2, b: ReadonlyVec2): Scalar => {
    const dx = a.x - b.x;
    const dy = a.y - b.y;
    return dx * dx + dy * dy;
};

export const distance = (a: ReadonlyVec2, b: ReadonlyVec2): Scalar => Math.sqrt(distanceSq(a, b));

export const fastDistance = (a: ReadonlyVec2, b: ReadonlyVec2): Scalar => {
    const dx = Math.abs(a.x - b.x);
    const dy = Math.abs(a.y - b.y);
    const min = Math.min(dx, dy);
    const max = Math.max(dx, dy);
    return max + 0.3 * min;
};

export const angle = (v: ReadonlyVec2): Scalar => Math.atan2(v.y, v.x);

export const fastAngle = (v: ReadonlyVec2): Scalar => {
    const x = v.x;
    const y = v.y;

    if (x === 0 && y === 0) return 0;
    if (x === 0) return y > 0 ? HALF_PI : -HALF_PI;

    const abs_y = Math.abs(y);
    const abs_x = Math.abs(x);
    const a = abs_x > abs_y ? abs_y / abs_x : abs_x / abs_y;
    const s = a * a;
    let r = ((-0.0464964749 * s + 0.15931422) * s - 0.327622764) * s * a + a;

    if (abs_y > abs_x) r = HALF_PI - r;
    if (x < 0) r = Math.PI - r;
    if (y < 0) r = -r;

    return r;
};

export const angleDeg = (v: ReadonlyVec2): Scalar => angle(v) * RAD_TO_DEG;

export const angleBetween = (a: ReadonlyVec2, b: ReadonlyVec2): Scalar => {
    const lenSqA = lengthSq(a);
    const lenSqB = lengthSq(b);

    if (lenSqA < EPSILON || lenSqB < EPSILON) return 0;

    const dotProduct = dot(a, b);
    const lenProduct = Math.sqrt(lenSqA * lenSqB);

    return Math.acos(Math.min(Math.max(dotProduct / lenProduct, -1), 1));
};

export const fastAngleBetween = (a: ReadonlyVec2, b: ReadonlyVec2): Scalar => {
    // Faster approximation using cross product
    const lenSqA = lengthSq(a);
    const lenSqB = lengthSq(b);

    if (lenSqA < EPSILON || lenSqB < EPSILON) return 0;

    const crossProduct = cross(a, b);
    const lenProduct = Math.sqrt(lenSqA * lenSqB);

    return Math.asin(Math.min(Math.max(crossProduct / lenProduct, -1), 1));
};

export const angleBetweenSigned = (a: ReadonlyVec2, b: ReadonlyVec2): Scalar =>
    Math.atan2(cross(a, b), dot(a, b));

export const rotate = <T extends Vec2>(out: T, v: ReadonlyVec2, angle: Scalar): T => {
    const x = v.x;
    const y = v.y;
    const c = Math.cos(angle);
    const s = Math.sin(angle);

    out.x = x * c - y * s;
    out.y = x * s + y * c;
    return out;
};

export const fastRotate = <T extends Vec2>(out: T, v: ReadonlyVec2, angle: Scalar): T => {
    const x = v.x;
    const y = v.y;

    // lookup tables or approximations for very common angles
    if (angle === Math.PI) {
        out.x = -x;
        out.y = -y;
        return out;
    }

    if (angle === HALF_PI) {
        out.x = -y;
        out.y = x;
        return out;
    }

    if (angle === -HALF_PI) {
        out.x = y;
        out.y = -x;
        return out;
    }

    // small angles, sin(θ) ≈ θ and cos(θ) ≈ 1 - θ²/2
    if (Math.abs(angle) < 0.1) {
        const θ2_2 = (angle * angle) / 2;
        const s = angle;
        const c = 1 - θ2_2;

        out.x = x * c - y * s;
        out.y = x * s + y * c;
        return out;
    }

    const c = Math.cos(angle);
    const s = Math.sin(angle);

    out.x = x * c - y * s;
    out.y = x * s + y * c;
    return out;
};

export const rotateAround = <T extends Vec2>(
    out: T,
    v: ReadonlyVec2,
    origin: ReadonlyVec2,
    angle: Scalar
): T => {
    const x = v.x - origin.x;
    const y = v.y - origin.y;
    const c = Math.cos(angle);
    const s = Math.sin(angle);

    out.x = x * c - y * s + origin.x;
    out.y = x * s + y * c + origin.y;
    return out;
};

export const lerp = <T extends Vec2>(out: T, a: ReadonlyVec2, b: ReadonlyVec2, t: Scalar): T => {
    const ax = a.x;
    const ay = a.y;
    const t1 = t < 0 ? 0 : t > 1 ? 1 : t;

    out.x = ax + t1 * (b.x - ax);
    out.y = ay + t1 * (b.y - ay);
    return out;
};

export const lerpUnclamped = <T extends Vec2>(out: T, a: ReadonlyVec2, b: ReadonlyVec2, t: Scalar): T => {
    const ax = a.x;
    const ay = a.y;

    out.x = ax + t * (b.x - ax);
    out.y = ay + t * (b.y - ay);
    return out;
};

export const slerp = <T extends Vec2>(out: T, a: ReadonlyVec2, b: ReadonlyVec2, t: Scalar): T => {
    const t1 = t < 0 ? 0 : t > 1 ? 1 : t;
    const angleA = angle(a);
    const angleB = angle(b);
    let angleDiff = angleB - angleA;

    if (angleDiff > Math.PI) {
        angleDiff -= PI_2;
    } else if (angleDiff < -Math.PI) {
        angleDiff += PI_2;
    }

    const resultAngle = angleA + t1 * angleDiff;
    const lenA = length(a);
    const lenB = length(b);
    const resultLen = lenA + t1 * (lenB - lenA);

    out.x = Math.cos(resultAngle) * resultLen;
    out.y = Math.sin(resultAngle) * resultLen;
    return out;
};

export const smoothStep = <T extends Vec2>(out: T, a: ReadonlyVec2, b: ReadonlyVec2, t: Scalar): T => {
    const t1 = t < 0 ? 0 : t > 1 ? 1 : t;
    // 3t² - 2t³
    const t2 = t1 * t1 * (3 - 2 * t1);

    const ax = a.x;
    const ay = a.y;

    out.x = ax + t2 * (b.x - ax);
    out.y = ay + t2 * (b.y - ay);
    return out;
};

export const smootherStep = <T extends Vec2>(out: T, a: ReadonlyVec2, b: ReadonlyVec2, t: Scalar): T => {
    const t1 = t < 0 ? 0 : t > 1 ? 1 : t;
    // 6t⁵ - 15t⁴ + 10t³
    const t2 = t1 * t1 * t1 * (t1 * (t1 * 6 - 15) + 10);

    const ax = a.x;
    const ay = a.y;

    out.x = ax + t2 * (b.x - ax);
    out.y = ay + t2 * (b.y - ay);
    return out;
};

export const cubicBezier = <T extends Vec2>(
    out: T,
    a: ReadonlyVec2,
    c1: ReadonlyVec2,
    c2: ReadonlyVec2,
    b: ReadonlyVec2,
    t: Scalar
): T => {
    const t1 = t < 0 ? 0 : t > 1 ? 1 : t;
    const oneMinusT = 1 - t1;
    const oneMinusT2 = oneMinusT * oneMinusT;
    const t2 = t1 * t1;

    const oneMinusT3 = oneMinusT2 * oneMinusT;
    const t3 = t2 * t1;
    const oneMinusT2_3t = oneMinusT2 * 3 * t1;
    const oneMinusT_3t2 = oneMinusT * 3 * t2;

    out.x = oneMinusT3 * a.x + oneMinusT2_3t * c1.x + oneMinusT_3t2 * c2.x + t3 * b.x;
    out.y = oneMinusT3 * a.y + oneMinusT2_3t * c1.y + oneMinusT_3t2 * c2.y + t3 * b.y;
    return out;
};

export const hermite = <T extends Vec2>(
    out: T,
    p0: ReadonlyVec2,
    m0: ReadonlyVec2,
    p1: ReadonlyVec2,
    m1: ReadonlyVec2,
    t: Scalar
): T => {
    const t1 = t < 0 ? 0 : t > 1 ? 1 : t;
    const t2 = t1 * t1;
    const t3 = t2 * t1;

    const h00 = 2 * t3 - 3 * t2 + 1;
    const h10 = t3 - 2 * t2 + t1;
    const h01 = -2 * t3 + 3 * t2;
    const h11 = t3 - t2;

    out.x = h00 * p0.x + h10 * m0.x + h01 * p1.x + h11 * m1.x;
    out.y = h00 * p0.y + h10 * m0.y + h01 * p1.y + h11 * m1.y;
    return out;
};

export const catmullRom = <T extends Vec2>(
    out: T,
    p0: ReadonlyVec2,
    p1: ReadonlyVec2,
    p2: ReadonlyVec2,
    p3: ReadonlyVec2,
    t: Scalar,
    tension = 0.5
): T => {
    const t1 = t < 0 ? 0 : t > 1 ? 1 : t;
    
    if (t1 === 0) {
        out.x = p1.x;
        out.y = p1.y;
        return out;
    } 
    
    if (t1 === 1) {
        out.x = p2.x;
        out.y = p2.y;
        return out;
    }
    
    const t2 = t1 * t1;
    const t3 = t2 * t1;
    
    const h00 = 2*t3 - 3*t2 + 1;
    const h10 = t3 - 2*t2 + t1;
    const h01 = -2*t3 + 3*t2;
    const h11 = t3 - t2;
    
    const alpha = (1 - tension) / 2;
    
    const m0x = alpha * (p2.x - p0.x);
    const m0y = alpha * (p2.y - p0.y);
    const m1x = alpha * (p3.x - p1.x);
    const m1y = alpha * (p3.y - p1.y);
    
    out.x = h00 * p1.x + h10 * m0x + h01 * p2.x + h11 * m1x;
    out.y = h00 * p1.y + h10 * m0y + h01 * p2.y + h11 * m1y;
    
    return out;
};

export const reflect = <T extends Vec2>(out: T, v: ReadonlyVec2, normal: ReadonlyVec2): T => {
    const vx = v.x;
    const vy = v.y;
    const nx = normal.x;
    const ny = normal.y;
    const dot2 = 2 * (vx * nx + vy * ny);

    out.x = vx - dot2 * nx;
    out.y = vy - dot2 * ny;
    return out;
};

export const project = <T extends Vec2>(out: T, v: ReadonlyVec2, onto: ReadonlyVec2): T => {
    const vx = v.x;
    const vy = v.y;
    const ontoX = onto.x;
    const ontoY = onto.y;

    const ontoLenSq = ontoX * ontoX + ontoY * ontoY;

    if (ontoLenSq < EPSILON) {
        out.x = 0;
        out.y = 0;
        return out;
    }

    const dotProduct = vx * ontoX + vy * ontoY;
    const scale = dotProduct / ontoLenSq;

    out.x = ontoX * scale;
    out.y = ontoY * scale;
    return out;
};

export const projectN = <T extends Vec2>(out: T, v: ReadonlyVec2, normalizedOnto: ReadonlyVec2): T => {
    const vx = v.x;
    const vy = v.y;
    const ontoX = normalizedOnto.x;
    const ontoY = normalizedOnto.y;

    const dotProduct = vx * ontoX + vy * ontoY;

    out.x = ontoX * dotProduct;
    out.y = ontoY * dotProduct;
    return out;
};

export const reject = <T extends Vec2>(out: T, v: ReadonlyVec2, from: ReadonlyVec2): T => {
    const vx = v.x;
    const vy = v.y;
    const fromX = from.x;
    const fromY = from.y;

    const fromLenSq = fromX * fromX + fromY * fromY;

    if (fromLenSq < EPSILON) {
        out.x = vx;
        out.y = vy;
        return out;
    }

    const dotProduct = vx * fromX + vy * fromY;
    const scale = dotProduct / fromLenSq;

    out.x = vx - fromX * scale;
    out.y = vy - fromY * scale;
    return out;
};

export const rejectN = <T extends Vec2>(out: T, v: ReadonlyVec2, normalizedFrom: ReadonlyVec2): T => {
    const vx = v.x;
    const vy = v.y;
    const fromX = normalizedFrom.x;
    const fromY = normalizedFrom.y;

    const dotProduct = vx * fromX + vy * fromY;

    out.x = vx - fromX * dotProduct;
    out.y = vy - fromY * dotProduct;
    return out;
};