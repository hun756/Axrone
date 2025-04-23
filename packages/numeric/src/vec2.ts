export type Scalar = number;
export type Vec2 = { x: Scalar; y: Scalar };
export type ReadonlyVec2 = Readonly<Vec2>;
export type Vec2Tuple = readonly [Scalar, Scalar];
export type Vec2Like = ReadonlyVec2 | Vec2Tuple;

export const EPSILON = 1e-6;
export const DEG_TO_RAD = Math.PI / 180;
export const SQRT2 = Math.sqrt(2);

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
