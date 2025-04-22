export type Scalar = number;

export type Vec2 = { x: Scalar; y: Scalar };
export type ReadonlyVec2 = Readonly<Vec2>;
export type Vec2Tuple = readonly [Scalar, Scalar];
export type Vec2Like = ReadonlyVec2 | Vec2Tuple;

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
