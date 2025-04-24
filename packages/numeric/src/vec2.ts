import { BoxMullerFactory } from './box_muller';

export type Scalar = number;
export type Vec2 = { x: Scalar; y: Scalar };
export type ReadonlyVec2 = Readonly<Vec2>;
export type Vec2Tuple = readonly [Scalar, Scalar];
export type Vec2Like = ReadonlyVec2 | Vec2Tuple;

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

const _x = (v: Vec2Like): Scalar => (Array.isArray(v) ? v[0] : (v as ReadonlyVec2).x);
const _y = (v: Vec2Like): Scalar => (Array.isArray(v) ? v[1] : (v as ReadonlyVec2).y);

export const isVec2 = (v: unknown): v is ReadonlyVec2 =>
    v !== null &&
    typeof v === 'object' &&
    'x' in v &&
    'y' in v &&
    typeof (v as any).x === 'number' &&
    typeof (v as any).y === 'number';

export const isVec2Tuple = (v: unknown): v is Vec2Tuple =>
    Array.isArray(v) && v.length === 2 && typeof v[0] === 'number' && typeof v[1] === 'number';

export const isVec2Like = (v: unknown): v is Vec2Like => isVec2(v) || isVec2Tuple(v);

export const create = (x = 0, y = 0): Vec2 => ({ x, y });
export const clone = <T extends Vec2Like>(v: T): Vec2 => ({ x: _x(v), y: _y(v) });

export const fromValues = (x: Scalar, y: Scalar): Vec2 => ({ x, y });

export const fromArray = (arr: Vec2Tuple): Vec2 => ({ x: arr[0], y: arr[1] });

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

export const copy = <T extends Vec2>(out: T, v: Vec2Like): T => {
    out.x = _x(v);
    out.y = _y(v);
    return out;
};

export const add = <T extends Vec2>(out: T, a: Vec2Like, b: Vec2Like): T => {
    out.x = _x(a) + _x(b);
    out.y = _y(a) + _y(b);
    return out;
};

export const addScalar = <T extends Vec2>(out: T, v: Vec2Like, s: Scalar): T => {
    out.x = _x(v) + s;
    out.y = _y(v) + s;
    return out;
};

export const subtract = <T extends Vec2>(out: T, a: Vec2Like, b: Vec2Like): T => {
    out.x = _x(a) - _x(b);
    out.y = _y(a) - _y(b);
    return out;
};

export const subtractScalar = <T extends Vec2>(out: T, v: Vec2Like, s: Scalar): T => {
    out.x = _x(v) - s;
    out.y = _y(v) - s;
    return out;
};

export const multiply = <T extends Vec2>(out: T, a: Vec2Like, b: Vec2Like): T => {
    out.x = _x(a) * _x(b);
    out.y = _y(a) * _y(b);
    return out;
};

export const multiplyScalar = <T extends Vec2>(out: T, v: Vec2Like, s: Scalar): T => {
    out.x = _x(v) * s;
    out.y = _y(v) * s;
    return out;
};

export const divide = <T extends Vec2>(out: T, a: Vec2Like, b: Vec2Like): T => {
    const bx = _x(b);
    const by = _y(b);

    if (Math.abs(bx) < EPSILON || Math.abs(by) < EPSILON) {
        throw new Error('Division by zero or near-zero value');
    }

    out.x = _x(a) / bx;
    out.y = _y(a) / by;
    return out;
};

export const divideScalar = <T extends Vec2>(out: T, v: Vec2Like, s: Scalar): T => {
    if (Math.abs(s) < EPSILON) {
        throw new Error('Division by zero or near-zero value');
    }

    const invS = 1 / s;
    out.x = _x(v) * invS;
    out.y = _y(v) * invS;
    return out;
};

export const negate = <T extends Vec2>(out: T, v: Vec2Like): T => {
    out.x = -_x(v);
    out.y = -_y(v);
    return out;
};

export const inverse = <T extends Vec2>(out: T, v: Vec2Like): T => {
    const vx = _x(v);
    const vy = _y(v);

    if (Math.abs(vx) < EPSILON || Math.abs(vy) < EPSILON) {
        throw new Error('Inversion of zero or near-zero value');
    }

    out.x = 1 / vx;
    out.y = 1 / vy;
    return out;
};

export const inverseSafe = <T extends Vec2>(out: T, v: Vec2Like, defaultValue = 0): T => {
    const vx = _x(v);
    const vy = _y(v);

    out.x = Math.abs(vx) < EPSILON ? defaultValue : 1 / vx;
    out.y = Math.abs(vy) < EPSILON ? defaultValue : 1 / vy;
    return out;
};

export const lengthSq = (v: Vec2Like): Scalar => {
    const x = _x(v);
    const y = _y(v);
    return x * x + y * y;
};

export const length = (v: Vec2Like): Scalar => Math.sqrt(lengthSq(v));

export const fastLength = (v: Vec2Like): Scalar => {
    // Fast approximation of vector length (~3.4% error max)
    const x = Math.abs(_x(v));
    const y = Math.abs(_y(v));
    const min = Math.min(x, y);
    const max = Math.max(x, y);
    return max + 0.3 * min;
};

export const normalize = <T extends Vec2>(out: T, v: Vec2Like): T => {
    const x = _x(v);
    const y = _y(v);
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

export const normalizeFast = <T extends Vec2>(out: T, v: Vec2Like): T => {
    const x = _x(v);
    const y = _y(v);
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

export const perpendicular = <T extends Vec2>(out: T, v: Vec2Like): T => {
    const x = _x(v);
    const y = _y(v);
    
    if (x === 0 && y === 0) {
        out.x = 0;
        out.y = 0;
        return out;
    }
    
    out.x = -y;
    out.y = x;
    return out;
};

export const perpendicularCCW = <T extends Vec2>(out: T, v: Vec2Like): T => {
    const x = _x(v);
    const y = _y(v);
    
    if (x === 0 && y === 0) {
        out.x = 0;
        out.y = 0;
        return out;
    }
    
    out.x = y;
    out.y = -x;
    return out;
};

export const dot = (a: Vec2Like, b: Vec2Like): Scalar => _x(a) * _x(b) + _y(a) * _y(b);

export const cross = (a: Vec2Like, b: Vec2Like): Scalar => _x(a) * _y(b) - _y(a) * _x(b);

export const distanceSq = (a: Vec2Like, b: Vec2Like): Scalar => {
    const dx = _x(a) - _x(b);
    const dy = _y(a) - _y(b);
    return dx * dx + dy * dy;
};

export const distance = (a: Vec2Like, b: Vec2Like): Scalar => Math.sqrt(distanceSq(a, b));

export const fastDistance = (a: Vec2Like, b: Vec2Like): Scalar => {
    const dx = Math.abs(_x(a) - _x(b));
    const dy = Math.abs(_y(a) - _y(b));
    const min = Math.min(dx, dy);
    const max = Math.max(dx, dy);
    return max + 0.3 * min;
};

export const angle = (v: Vec2Like): Scalar => Math.atan2(_y(v), _x(v));

export const fastAngle = (v: Vec2Like): Scalar => {
    const x = _x(v);
    const y = _y(v);

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

export const angleDeg = (v: Vec2Like): Scalar => angle(v) * RAD_TO_DEG;

export const angleBetween = (a: Vec2Like, b: Vec2Like): Scalar => {
    const lenSqA = lengthSq(a);
    const lenSqB = lengthSq(b);

    if (lenSqA < EPSILON || lenSqB < EPSILON) return 0;

    const dotProduct = dot(a, b);
    const lenProduct = Math.sqrt(lenSqA * lenSqB);

    return Math.acos(Math.min(Math.max(dotProduct / lenProduct, -1), 1));
};

export const fastAngleBetween = (a: Vec2Like, b: Vec2Like): Scalar => {
    // Faster approximation using cross product
    const lenSqA = lengthSq(a);
    const lenSqB = lengthSq(b);

    if (lenSqA < EPSILON || lenSqB < EPSILON) return 0;

    const crossProduct = cross(a, b);
    const lenProduct = Math.sqrt(lenSqA * lenSqB);

    return Math.asin(Math.min(Math.max(crossProduct / lenProduct, -1), 1));
};

export const angleBetweenSigned = (a: Vec2Like, b: Vec2Like): Scalar =>
    Math.atan2(cross(a, b), dot(a, b));

export const rotate = <T extends Vec2>(out: T, v: Vec2Like, angle: Scalar): T => {
    const x = _x(v);
    const y = _y(v);
    const c = Math.cos(angle);
    const s = Math.sin(angle);

    out.x = x * c - y * s;
    out.y = x * s + y * c;
    return out;
};

export const fastRotate = <T extends Vec2>(out: T, v: Vec2Like, angle: Scalar): T => {
    const x = _x(v);
    const y = _y(v);

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
    v: Vec2Like,
    origin: Vec2Like,
    angle: Scalar
): T => {
    const x = _x(v) - _x(origin);
    const y = _y(v) - _y(origin);
    const c = Math.cos(angle);
    const s = Math.sin(angle);

    out.x = x * c - y * s + _x(origin);
    out.y = x * s + y * c + _y(origin);
    return out;
};
