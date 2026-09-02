import { Comparer, CompareResult, EqualityComparer, Equatable, ICloneable } from '@axrone/utility';
import { EPSILON, HALF_PI, PI_2, ensureFinite } from './common';
import { clamp01, clampNegOneOne } from './clamp';
import { Fnv1a32, IHasher, HashValue, IHashable } from '@axrone/hash';
import {
    sampleStandardNormal,
    sampleNormalInRange,
    sampleUniform,
    sampleUniformRange,
} from './box-muller';

export interface IVec2Like {
    x: number;
    y: number;
}

export class Vec2 implements IVec2Like, ICloneable<Vec2>, Equatable {
    private static _hasher = new Fnv1a32();

    constructor(
        public x: number = 0,
        public y: number = 0
    ) {
        ensureFinite(x, 'Vec2.x');
        ensureFinite(y, 'Vec2.y');
    }

    static readonly ZERO: Readonly<Vec2> = Object.freeze(new Vec2(0, 0));
    static readonly ONE: Readonly<Vec2> = Object.freeze(new Vec2(1, 1));
    static readonly NEG_ONE: Readonly<Vec2> = Object.freeze(new Vec2(-1, -1));
    static readonly UNIT_X: Readonly<Vec2> = Object.freeze(new Vec2(1, 0));
    static readonly UNIT_Y: Readonly<Vec2> = Object.freeze(new Vec2(0, 1));
    static readonly UP: Readonly<Vec2> = Object.freeze(new Vec2(0, 1));
    static readonly DOWN: Readonly<Vec2> = Object.freeze(new Vec2(0, -1));
    static readonly LEFT: Readonly<Vec2> = Object.freeze(new Vec2(-1, 0));
    static readonly RIGHT: Readonly<Vec2> = Object.freeze(new Vec2(1, 0));

    static from<T extends IVec2Like>(v: Readonly<T>): Vec2 {
        return new Vec2(v.x, v.y);
    }

    static fromArray(arr: ArrayLike<number>, offset: number = 0): Vec2 {
        if (offset < 0) {
            throw new RangeError('Offset cannot be negative');
        }

        if (arr.length < offset + 2) {
            throw new RangeError(
                `Array must have at least ${offset + 2} elements when using offset ${offset}`
            );
        }

        return new Vec2(Number(arr[offset]), Number(arr[offset + 1]));
    }

    static create(x: number = 0, y: number = 0): Vec2 {
        return new Vec2(x, y);
    }

    static copy<T extends IVec2Like, V extends IVec2Like>(source: Readonly<T>, out?: V): V {
        if (out) {
            out.x = source.x;
            out.y = source.y;
            return out;
        }
        return { x: source.x, y: source.y } as V;
    }

    clone(): Vec2 {
        return new Vec2(this.x, this.y);
    }

    equals(other: unknown): boolean {
        if (!(other instanceof Vec2)) return false;

        return Math.abs(this.x - other.x) < EPSILON && Math.abs(this.y - other.y) < EPSILON;
    }

    getHashCode(): number {
        return Vec2._hasher.reset().updateF32(this.x).updateF32(this.y).digest();
    }

    hashInto<H extends HashValue = any>(hasher: IHasher<H>): void {
        hasher.updateF32(this.x).updateF32(this.y);
    }

    static add<T extends IVec2Like, U extends IVec2Like, V extends IVec2Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        if (out) {
            out.x = a.x + b.x;
            out.y = a.y + b.y;
            return out;
        } else {
            return { x: a.x + b.x, y: a.y + b.y } as V;
        }
    }

    static addScalar<T extends IVec2Like, V extends IVec2Like>(
        a: Readonly<T>,
        b: number,
        out?: V
    ): V {
        if (out) {
            out.x = a.x + b;
            out.y = a.y + b;
            return out;
        } else {
            return { x: a.x + b, y: a.y + b } as V;
        }
    }

    static subtract<T extends IVec2Like, U extends IVec2Like, V extends IVec2Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        if (out) {
            out.x = a.x - b.x;
            out.y = a.y - b.y;
            return out;
        } else {
            return { x: a.x - b.x, y: a.y - b.y } as V;
        }
    }

    static subtractScalar<T extends IVec2Like, V extends IVec2Like>(
        a: Readonly<T>,
        b: number,
        out?: V
    ): V {
        if (out) {
            out.x = a.x - b;
            out.y = a.y - b;
            return out;
        } else {
            return { x: a.x - b, y: a.y - b } as V;
        }
    }

    static multiply<T extends IVec2Like, U extends IVec2Like, V extends IVec2Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        if (out) {
            out.x = a.x * b.x;
            out.y = a.y * b.y;
            return out;
        } else {
            return { x: a.x * b.x, y: a.y * b.y } as V;
        }
    }

    static multiplyScalar<T extends IVec2Like, V extends IVec2Like>(
        a: Readonly<T>,
        b: number,
        out?: V
    ): V {
        if (out) {
            out.x = a.x * b;
            out.y = a.y * b;
            return out;
        } else {
            return { x: a.x * b, y: a.y * b } as V;
        }
    }

    static divide<T extends IVec2Like, U extends IVec2Like, V extends IVec2Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        if (Math.abs(b.x) < EPSILON || Math.abs(b.y) < EPSILON) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        if (out) {
            out.x = a.x / b.x;
            out.y = a.y / b.y;
            return out;
        } else {
            return { x: a.x / b.x, y: a.y / b.y } as V;
        }
    }

    static divideSafe<T extends IVec2Like, U extends IVec2Like, V extends IVec2Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V,
        defaultValue: number = 0
    ): V {
        if (out) {
            out.x = Math.abs(b.x) < EPSILON ? defaultValue : a.x / b.x;
            out.y = Math.abs(b.y) < EPSILON ? defaultValue : a.y / b.y;
            return out;
        } else {
            return {
                x: Math.abs(b.x) < EPSILON ? defaultValue : a.x / b.x,
                y: Math.abs(b.y) < EPSILON ? defaultValue : a.y / b.y,
            } as V;
        }
    }

    static divideScalar<T extends IVec2Like, V extends IVec2Like>(
        a: Readonly<T>,
        b: number,
        out?: V
    ): V {
        if (Math.abs(b) < EPSILON) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        if (out) {
            out.x = a.x / b;
            out.y = a.y / b;
            return out;
        } else {
            return { x: a.x / b, y: a.y / b } as V;
        }
    }

    static negate<T extends IVec2Like, V extends IVec2Like>(a: Readonly<T>, out?: V): V {
        if (out) {
            out.x = a.x === 0 ? 0 : -a.x;
            out.y = a.y === 0 ? 0 : -a.y;
            return out;
        } else {
            return {
                x: a.x === 0 ? 0 : -a.x,
                y: a.y === 0 ? 0 : -a.y,
            } as V;
        }
    }

    static inverse<T extends IVec2Like, V extends IVec2Like>(a: Readonly<T>, out?: V): V {
        if (out) {
            out.x = 1 / a.x;
            out.y = 1 / a.y;
            return out;
        } else {
            return { x: 1 / a.x, y: 1 / a.y } as V;
        }
    }

    static inverseSafe<T extends IVec2Like, V extends IVec2Like>(
        v: Readonly<T>,
        out?: V,
        defaultValue = 0
    ): V {
        const vx = v.x;
        const vy = v.y;

        if (out) {
            out.x = Math.abs(vx) < EPSILON ? defaultValue : 1 / vx;
            out.y = Math.abs(vy) < EPSILON ? defaultValue : 1 / vy;
            return out;
        } else {
            return {
                x: Math.abs(vx) < EPSILON ? defaultValue : 1 / vx,
                y: Math.abs(vy) < EPSILON ? defaultValue : 1 / vy,
            } as V;
        }
    }

    static perpendicular<T extends IVec2Like, V extends IVec2Like>(v: Readonly<T>, out?: V): V {
        if (out) {
            out.x = -v.y;
            out.y = v.x;
            return out;
        } else {
            return { x: -v.y, y: v.x } as V;
        }
    }

    static perpendicularCCW<T extends IVec2Like, V extends IVec2Like>(v: Readonly<T>, out?: V): V {
        if (out) {
            out.x = v.y;
            out.y = -v.x;
            return out;
        } else {
            return { x: v.y, y: -v.x } as V;
        }
    }

    static dot<T extends IVec2Like, U extends IVec2Like>(a: Readonly<T>, b: Readonly<U>): number {
        return a.x * b.x + a.y * b.y;
    }

    static cross<T extends IVec2Like, U extends IVec2Like>(a: Readonly<T>, b: Readonly<U>): number {
        return a.x * b.y - a.y * b.x;
    }

    static len<T extends IVec2Like>(v: Readonly<T>): number {
        return Math.sqrt(v.x * v.x + v.y * v.y);
    }

    static lengthSquared<T extends IVec2Like>(v: Readonly<T>): number {
        return v.x * v.x + v.y * v.y;
    }

    static normalize<T extends IVec2Like, V extends IVec2Like>(v: Readonly<T>, out?: V): V {
        const length = Math.sqrt(v.x * v.x + v.y * v.y);
        if (length < EPSILON) {
            throw new Error('Cannot normalize a zero-length vector');
        }

        if (out) {
            out.x = v.x / length;
            out.y = v.y / length;
            return out;
        } else {
            return { x: v.x / length, y: v.y / length } as V;
        }
    }

    static normalizeSafe<T extends IVec2Like, V extends IVec2Like>(
        v: Readonly<T>,
        out?: V
    ): V {
        const length = Math.sqrt(v.x * v.x + v.y * v.y);
        if (length < EPSILON) {
            if (out) {
                out.x = 0;
                out.y = 0;
                return out;
            } else {
                return { x: 0, y: 0 } as V;
            }
        }

        if (out) {
            out.x = v.x / length;
            out.y = v.y / length;
            return out;
        } else {
            return { x: v.x / length, y: v.y / length } as V;
        }
    }

    static distanceSquared<T extends IVec2Like, U extends IVec2Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        const dx = a.x - b.x;
        const dy = a.y - b.y;
        return dx * dx + dy * dy;
    }

    static distance<T extends IVec2Like, U extends IVec2Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        const dx = a.x - b.x;
        const dy = a.y - b.y;
        return Math.sqrt(dx * dx + dy * dy);
    }

    static manhattanDistance<T extends IVec2Like, U extends IVec2Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        return Math.abs(a.x - b.x) + Math.abs(a.y - b.y);
    }

    static chebyshevDistance<T extends IVec2Like, U extends IVec2Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        return Math.max(Math.abs(a.x - b.x), Math.abs(a.y - b.y));
    }

    static angleBetween<T extends IVec2Like, U extends IVec2Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        const dotProduct = Vec2.dot(a, b);
        const lengthA = Vec2.len(a);
        const lengthB = Vec2.len(b);

        if (lengthA < EPSILON || lengthB < EPSILON) {
            throw new Error('Cannot calculate angle with zero-length vector');
        }

        const cosTheta = dotProduct / (lengthA * lengthB);
        return Math.acos(clampNegOneOne(cosTheta));
    }

    static angle2Deg<T extends IVec2Like, U extends IVec2Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        const angle = Vec2.angleBetween(a, b);
        return (angle * 180) / Math.PI;
    }

    static rotate<T extends IVec2Like, V extends IVec2Like>(v: Readonly<T>, angle: number, out?: V): V {
        const cos = Math.cos(angle);
        const sin = Math.sin(angle);

        if (out) {
            out.x = v.x * cos - v.y * sin;
            out.y = v.x * sin + v.y * cos;
            return out;
        } else {
            return { x: v.x * cos - v.y * sin, y: v.x * sin + v.y * cos } as V;
        }
    }

    static fastRotate<T extends IVec2Like, V extends IVec2Like>(v: Readonly<T>, angle: number, out?: V): V {
        const x = v.x;
        const y = v.y;

        if (!out) {
            out = { x: 0, y: 0 } as V;
        }

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
    }

    static rotateAround<T extends IVec2Like, U extends IVec2Like, V extends IVec2Like>(
        v: Readonly<T>,
        angle: number,
        pivot: Readonly<U>,
        out?: V
    ): V {
        const cos = Math.cos(angle);
        const sin = Math.sin(angle);

        if (out) {
            out.x = (v.x - pivot.x) * cos - (v.y - pivot.y) * sin + pivot.x;
            out.y = (v.x - pivot.x) * sin + (v.y - pivot.y) * cos + pivot.y;
            return out;
        } else {
            return {
                x: (v.x - pivot.x) * cos - (v.y - pivot.y) * sin + pivot.x,
                y: (v.x - pivot.x) * sin + (v.y - pivot.y) * cos + pivot.y,
            } as V;
        }
    }

    static lerp<T extends IVec2Like, U extends IVec2Like, V extends IVec2Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        const t1 = clamp01(t);
        if (out) {
            out.x = a.x + (b.x - a.x) * t1;
            out.y = a.y + (b.y - a.y) * t1;
            return out;
        } else {
            return { x: a.x + (b.x - a.x) * t1, y: a.y + (b.y - a.y) * t1 } as V;
        }
    }

    static lerpUnClamped<T extends IVec2Like, U extends IVec2Like, V extends IVec2Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        if (out) {
            out.x = a.x + (b.x - a.x) * t;
            out.y = a.y + (b.y - a.y) * t;
            return out;
        } else {
            return { x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t } as V;
        }
    }

    static slerp<T extends IVec2Like, U extends IVec2Like, V extends IVec2Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        const t1 = clamp01(t);

        const lenASq = a.x * a.x + a.y * a.y;
        const lenBSq = b.x * b.x + b.y * b.y;
        if (lenASq < EPSILON * EPSILON || lenBSq < EPSILON * EPSILON) {
            return Vec2.lerpUnClamped(a, b, t1, out);
        }

        const rA = Math.sqrt(lenASq);
        const rB = Math.sqrt(lenBSq);

        const angleA = Math.atan2(a.y, a.x);
        const angleB = Math.atan2(b.y, b.x);

        let diff = angleB - angleA;
        if (diff > Math.PI) {
            diff -= Math.PI * 2;
        } else if (diff < -Math.PI) {
            diff += Math.PI * 2;
        }

        const angle = angleA + diff * t1;
        const r = rA + (rB - rA) * t1;

        const x = r * Math.cos(angle);
        const y = r * Math.sin(angle);

        if (out) {
            out.x = x;
            out.y = y;
            return out;
        } else {
            return { x, y } as V;
        }
    }

    static smoothStep<T extends IVec2Like, U extends IVec2Like, V extends IVec2Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        const t1 = clamp01(t);
        const t2 = t1 * t1 * (3 - 2 * t1); // Smooth step function: 3t² - 2t³
        if (out) {
            out.x = a.x + (b.x - a.x) * t2;
            out.y = a.y + (b.y - a.y) * t2;
            return out;
        } else {
            return { x: a.x + (b.x - a.x) * t2, y: a.y + (b.y - a.y) * t2 } as V;
        }
    }

    static smootherStep<T extends IVec2Like, U extends IVec2Like, V extends IVec2Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        const t1 = clamp01(t);
        // Smoother step: 6t⁵ - 15t⁴ + 10t³
        const t2 = t1 * t1 * t1 * (10 - 15 * t1 + 6 * t1 * t1);
        if (out) {
            out.x = a.x + (b.x - a.x) * t2;
            out.y = a.y + (b.y - a.y) * t2;
            return out;
        } else {
            return { x: a.x + (b.x - a.x) * t2, y: a.y + (b.y - a.y) * t2 } as V;
        }
    }

    static cubicBezier<
        T extends IVec2Like,
        U extends IVec2Like,
        V extends IVec2Like,
        W extends IVec2Like,
        O extends IVec2Like,
    >(a: Readonly<T>, c1: Readonly<U>, c2: Readonly<V>, b: Readonly<W>, t: number, out?: O): O {
        const t1 = clamp01(t);
        const oneMinusT = 1 - t1;
        const oneMinusT2 = oneMinusT * oneMinusT;
        const t2 = t1 * t1;

        // Optimized cubic Bézier computation using expanded form
        const oneMinusT3 = oneMinusT2 * oneMinusT;
        const t3 = t2 * t1;
        const oneMinusT2_3t = oneMinusT2 * 3 * t1;
        const oneMinusT_3t2 = oneMinusT * 3 * t2;

        if (out) {
            out.x = oneMinusT3 * a.x + oneMinusT2_3t * c1.x + oneMinusT_3t2 * c2.x + t3 * b.x;
            out.y = oneMinusT3 * a.y + oneMinusT2_3t * c1.y + oneMinusT_3t2 * c2.y + t3 * b.y;
            return out;
        } else {
            return {
                x: oneMinusT3 * a.x + oneMinusT2_3t * c1.x + oneMinusT_3t2 * c2.x + t3 * b.x,
                y: oneMinusT3 * a.y + oneMinusT2_3t * c1.y + oneMinusT_3t2 * c2.y + t3 * b.y,
            } as O;
        }
    }

    static hermite<
        T extends IVec2Like,
        U extends IVec2Like,
        V extends IVec2Like,
        W extends IVec2Like,
        O extends IVec2Like,
    >(p0: Readonly<T>, m0: Readonly<U>, p1: Readonly<V>, m1: Readonly<W>, t: number, out?: O): O {
        const t1 = clamp01(t);
        const t2 = t1 * t1;
        const t3 = t2 * t1;

        const h00 = 2 * t3 - 3 * t2 + 1;
        const h10 = t3 - 2 * t2 + t1;
        const h01 = -2 * t3 + 3 * t2;
        const h11 = t3 - t2;

        if (out) {
            out.x = h00 * p0.x + h10 * m0.x + h01 * p1.x + h11 * m1.x;
            out.y = h00 * p0.y + h10 * m0.y + h01 * p1.y + h11 * m1.y;
            return out;
        } else {
            return {
                x: h00 * p0.x + h10 * m0.x + h01 * p1.x + h11 * m1.x,
                y: h00 * p0.y + h10 * m0.y + h01 * p1.y + h11 * m1.y,
            } as O;
        }
    }

    static catmullRom<
        T extends IVec2Like,
        U extends IVec2Like,
        V extends IVec2Like,
        W extends IVec2Like,
        O extends IVec2Like,
    >(
        p0: Readonly<T>,
        p1: Readonly<U>,
        p2: Readonly<V>,
        p3: Readonly<W>,
        t: number,
        tension: number = 0.5,
        out?: O
    ): O {
        const t1 = clamp01(t);

        if (!out) {
            out = { x: 0, y: 0 } as O;
        }

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

        const h00 = 2 * t3 - 3 * t2 + 1;
        const h10 = t3 - 2 * t2 + t1;
        const h01 = -2 * t3 + 3 * t2;
        const h11 = t3 - t2;

        const alpha = (1 - tension) / 2;

        const m0x = alpha * (p2.x - p0.x);
        const m0y = alpha * (p2.y - p0.y);
        const m1x = alpha * (p3.x - p1.x);
        const m1y = alpha * (p3.y - p1.y);

        out.x = h00 * p1.x + h10 * m0x + h01 * p2.x + h11 * m1x;
        out.y = h00 * p1.y + h10 * m0y + h01 * p2.y + h11 * m1y;

        return out;
    }

    static random<T extends IVec2Like, V extends IVec2Like>(scale: number = 1, out?: V): V {
        const x = sampleStandardNormal() * scale;
        const y = sampleStandardNormal() * scale;

        if (out) {
            out.x = x;
            out.y = y;
            return out;
        } else {
            return { x, y } as V;
        }
    }

    static fastRandom<T extends IVec2Like, V extends IVec2Like>(scale: number = 1, out?: V): V {
        const angle = sampleUniform() * PI_2;

        if (out) {
            out.x = Math.cos(angle) * scale;
            out.y = Math.sin(angle) * scale;
            return out;
        } else {
            return {
                x: Math.cos(angle) * scale,
                y: Math.sin(angle) * scale,
            } as V;
        }
    }

    static randomBox<T extends IVec2Like, V extends IVec2Like>(
        minX: number,
        maxX: number,
        minY: number,
        maxY: number,
        out?: V
    ): V {
        const x = sampleUniformRange(minX, maxX);
        const y = sampleUniformRange(minY, maxY);

        if (out) {
            out.x = x;
            out.y = y;
            return out;
        } else {
            return { x, y } as V;
        }
    }

    static randomBoxNormal<T extends IVec2Like, V extends IVec2Like>(
        minX: number,
        maxX: number,
        minY: number,
        maxY: number,
        out?: V
    ): V {
        const centerX = (minX + maxX) * 0.5;
        const centerY = (minY + maxY) * 0.5;
        const rangeX = maxX - minX;
        const rangeY = maxY - minY;

        const x = sampleNormalInRange(centerX, rangeX);
        const y = sampleNormalInRange(centerY, rangeY);

        if (out) {
            out.x = x;
            out.y = y;
            return out;
        } else {
            return { x, y } as V;
        }
    }

    add<T extends IVec2Like>(other: Readonly<T>): Vec2 {
        this.x += other.x;
        this.y += other.y;
        return this;
    }

    addScalar(num: number): Vec2 {
        this.x += num;
        this.y += num;
        return this;
    }

    subtract<T extends IVec2Like>(other: Readonly<T>): Vec2 {
        this.x -= other.x;
        this.y -= other.y;
        return this;
    }

    subtractScalar(num: number): Vec2 {
        this.x -= num;
        this.y -= num;
        return this;
    }

    multiply<T extends IVec2Like>(other: Readonly<T>): Vec2 {
        this.x *= other.x;
        this.y *= other.y;
        return this;
    }

    multiplyScalar(num: number): Vec2 {
        this.x *= num;
        this.y *= num;
        return this;
    }

    divide<T extends IVec2Like>(other: Readonly<T>): Vec2 {
        if (Math.abs(other.x) < EPSILON || Math.abs(other.y) < EPSILON) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        this.x /= other.x;
        this.y /= other.y;
        return this;
    }

    divideScalar(num: number): Vec2 {
        if (Math.abs(num) < EPSILON) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        this.x /= num;
        this.y /= num;
        return this;
    }

    dot<T extends IVec2Like>(other: Readonly<T>): number {
        return this.x * other.x + this.y * other.y;
    }

    cross<T extends IVec2Like>(other: Readonly<T>): number {
        return this.x * other.y - this.y * other.x;
    }

    lengthSquared(): number {
        return this.x * this.x + this.y * this.y;
    }

    length(): number {
        return Math.sqrt(this.lengthSquared());
    }

    inverse(): Vec2 {
        if (Math.abs(this.x) < EPSILON || Math.abs(this.y) < EPSILON) {
            throw new Error('Inversion of zero or near-zero value');
        }

        this.x = 1 / this.x;
        this.y = 1 / this.y;
        return this;
    }

    inverseSafe(defaultValue: number = 0): Vec2 {
        const vx = this.x;
        const vy = this.y;

        this.x = Math.abs(vx) < EPSILON ? defaultValue : 1 / vx;
        this.y = Math.abs(vy) < EPSILON ? defaultValue : 1 / vy;
        return this;
    }

    normalize(): Vec2 {
        const length = this.length();
        if (length < EPSILON) {
            throw new Error('Cannot normalize a zero-length vector');
        }

        this.x /= length;
        this.y /= length;
        return this;
    }

    distanceSquared<T extends IVec2Like>(other: Readonly<T>): number {
        const dx = this.x - other.x;
        const dy = this.y - other.y;
        return dx * dx + dy * dy;
    }

    distance<T extends IVec2Like>(other: Readonly<T>): number {
        const dx = this.x - other.x;
        const dy = this.y - other.y;
        return Math.sqrt(dx * dx + dy * dy);
    }

    manhattanDistance<T extends IVec2Like>(other: Readonly<T>): number {
        return Math.abs(this.x - other.x) + Math.abs(this.y - other.y);
    }

    chebyshevDistance<T extends IVec2Like>(other: Readonly<T>): number {
        return Math.max(Math.abs(this.x - other.x), Math.abs(this.y - other.y));
    }

    angleBetween<T extends IVec2Like>(other: Readonly<T>): number {
        const dotProduct = this.dot(other);
        const lengthA = this.length();
        const lengthB = Vec2.len(other);

        if (lengthA < EPSILON || lengthB < EPSILON) {
            throw new Error('Cannot calculate angle with zero-length vector');
        }

        const cosTheta = dotProduct / (lengthA * lengthB);
        return Math.acos(Math.max(-1, Math.min(1, cosTheta)));
    }

    angle(): number {
        const angle = Math.atan2(this.y, this.x);
        return angle < 0 ? angle + Math.PI * 2 : angle;
    }

    angle2Deg<T extends IVec2Like>(other: Readonly<T>): number {
        const angle = this.angleBetween(other);
        return (angle * 180) / Math.PI;
    }

    rotate(angle: number): Vec2 {
        const cos = Math.cos(angle);
        const sin = Math.sin(angle);

        const x = this.x * cos - this.y * sin;
        const y = this.x * sin + this.y * cos;

        this.x = x;
        this.y = y;
        return this;
    }

    fastRotate(angle: number): Vec2 {
        Vec2.fastRotate(this, angle, this);
        return this;
    }

    rotateAround<T extends IVec2Like>(pivot: Readonly<T>, angle: number): Vec2 {
        const cos = Math.cos(angle);
        const sin = Math.sin(angle);

        const x = (this.x - pivot.x) * cos - (this.y - pivot.y) * sin + pivot.x;
        const y = (this.x - pivot.x) * sin + (this.y - pivot.y) * cos + pivot.y;

        this.x = x;
        this.y = y;
        return this;
    }
}

export enum Vec2ComparisonMode {
    LEXICOGRAPHIC,
    MAGNITUDE,
    ANGLE,
    MANHATTAN,
}

export class Vec2Comparer implements Comparer<Vec2> {
    private readonly mode: Vec2ComparisonMode;

    constructor(mode: Vec2ComparisonMode = Vec2ComparisonMode.LEXICOGRAPHIC) {
        this.mode = mode;
    }

    compare(a: Readonly<Vec2>, b: Readonly<Vec2>): CompareResult {
        switch (this.mode) {
            case Vec2ComparisonMode.LEXICOGRAPHIC:
                if (Math.abs(a.x - b.x) < EPSILON) {
                    if (Math.abs(a.y - b.y) < EPSILON) return 0;
                    return a.y < b.y ? -1 : 1;
                }
                return a.x < b.x ? -1 : 1;

            case Vec2ComparisonMode.MAGNITUDE: {
                const lenA = a.lengthSquared();
                const lenB = b.lengthSquared();
                if (Math.abs(lenA - lenB) < EPSILON) return 0;
                return lenA < lenB ? -1 : 1;
            }

            case Vec2ComparisonMode.ANGLE: {
                const angleA = a.angle();
                const angleB = b.angle();
                if (Math.abs(angleA - angleB) < EPSILON) return 0;
                return angleA < angleB ? -1 : 1;
            }

            case Vec2ComparisonMode.MANHATTAN: {
                const distA = Math.abs(a.x) + Math.abs(a.y);
                const distB = Math.abs(b.x) + Math.abs(b.y);
                if (Math.abs(distA - distB) < EPSILON) return 0;
                return distA < distB ? -1 : 1;
            }

            default:
                throw new Error(`Unsupported Vec2 comparison mode: ${this.mode}`);
        }
    }
}

export class Vec2EqualityComparer implements EqualityComparer<Vec2> {
    private readonly epsilon: number;

    constructor(epsilon: number = EPSILON) {
        this.epsilon = epsilon;
    }

    equals(a: Readonly<Vec2>, b: Readonly<Vec2>): boolean {
        if (a === b) return true;
        if (!a || !b) return false;

        return Math.abs(a.x - b.x) < this.epsilon && Math.abs(a.y - b.y) < this.epsilon;
    }

    hash(obj: Readonly<Vec2>): number {
        if (!obj) return 0;
        return new Fnv1a32().updateF32(obj.x).updateF32(obj.y).digest();
    }
}
