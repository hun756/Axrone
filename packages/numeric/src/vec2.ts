import { Equatable, ICloneable } from '@axrone/utility';
import { EPSILON, HALF_PI } from './common';
import { inverse, inverseSafe } from './vec2_legacy';

export interface IVec2Like {
    x: number;
    y: number;
}

export class Vec2 implements IVec2Like, ICloneable<Vec2>, Equatable {
    constructor(
        public x: number = 0,
        public y: number = 0
    ) {}

    static readonly ZERO: Readonly<Vec2> = Object.freeze(new Vec2(0, 0));
    static readonly ONE: Readonly<Vec2> = Object.freeze(new Vec2(1, 1));
    static readonly NEG_ONE: Readonly<Vec2> = Object.freeze(new Vec2(-1, -1));
    static readonly UNIT_X: Readonly<Vec2> = Object.freeze(new Vec2(1, 0));
    static readonly UNIT_Y: Readonly<Vec2> = Object.freeze(new Vec2(0, 1));
    static readonly UP: Readonly<Vec2> = Object.freeze(new Vec2(0, 1));
    static readonly DOWN: Readonly<Vec2> = Object.freeze(new Vec2(0, -1));
    static readonly LEFT: Readonly<Vec2> = Object.freeze(new Vec2(-1, 0));
    static readonly RIGHT: Readonly<Vec2> = Object.freeze(new Vec2(1, 0));

    static from<T extends IVec2Like>(v: T): Vec2 {
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

    clone(): Vec2 {
        return new Vec2(this.x, this.y);
    }

    equals(other: unknown): boolean {
        if (!(other instanceof Vec2)) return false;

        return Math.abs(this.x - other.x) < EPSILON && Math.abs(this.y - other.y) < EPSILON;
    }

    getHashCode(): number {
        let h1 = 2166136261;
        h1 = Math.imul(h1 ^ Math.floor(this.x * 1000), 16777619);
        h1 = Math.imul(h1 ^ Math.floor(this.y * 1000), 16777619);
        return h1 >>> 0;
    }

    static add<T extends IVec2Like>(a: T, b: T, out?: T): T {
        if (out) {
            out.x = a.x + b.x;
            out.y = a.y + b.y;
            return out;
        } else {
            return { x: a.x + b.x, y: a.y + b.y } as T;
        }
    }

    static addScalar<T extends IVec2Like>(a: T, b: number, out?: T): T {
        if (out) {
            out.x = a.x + b;
            out.y = a.y + b;
            return out;
        } else {
            return { x: a.x + b, y: a.y + b } as T;
        }
    }

    static subtract<T extends IVec2Like>(a: T, b: T, out?: T): T {
        if (out) {
            out.x = a.x - b.x;
            out.y = a.y - b.y;
            return out;
        } else {
            return { x: a.x - b.x, y: a.y - b.y } as T;
        }
    }

    static subtractScalar<T extends IVec2Like>(a: T, b: number, out?: T): T {
        if (out) {
            out.x = a.x - b;
            out.y = a.y - b;
            return out;
        } else {
            return { x: a.x - b, y: a.y - b } as T;
        }
    }

    static multiply<T extends IVec2Like>(a: T, b: T, out?: T): T {
        if (out) {
            out.x = a.x * b.x;
            out.y = a.y * b.y;
            return out;
        } else {
            return { x: a.x * b.x, y: a.y * b.y } as T;
        }
    }

    static multiplyScalar<T extends IVec2Like>(a: T, b: number, out?: T): T {
        if (out) {
            out.x = a.x * b;
            out.y = a.y * b;
            return out;
        } else {
            return { x: a.x * b, y: a.y * b } as T;
        }
    }

    static divide<T extends IVec2Like>(a: T, b: T, out?: T): T {
        if (Math.abs(b.x) < EPSILON || Math.abs(b.y) < EPSILON) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        if (out) {
            out.x = a.x / b.x;
            out.y = a.y / b.y;
            return out;
        } else {
            return { x: a.x / b.x, y: a.y / b.y } as T;
        }
    }

    static divideScalar<T extends IVec2Like>(a: T, b: number, out?: T): T {
        if (Math.abs(b) < EPSILON) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        if (out) {
            out.x = a.x / b;
            out.y = a.y / b;
            return out;
        } else {
            return { x: a.x / b, y: a.y / b } as T;
        }
    }

    static negate<T extends IVec2Like>(a: T, out?: T): T {
        if (out) {
            out.x = -a.x;
            out.y = -a.y;
            return out;
        } else {
            return { x: -a.x, y: -a.y } as T;
        }
    }

    static inverse<T extends IVec2Like>(a: T, out?: T): T {
        if (out) {
            out.x = 1 / a.x;
            out.y = 1 / a.y;
            return out;
        } else {
            return { x: 1 / a.x, y: 1 / a.y } as T;
        }
    }

    static inverseSafe<T extends IVec2Like>(v: T, out?: T, defaultValue = 0): T {
        const vx = v.x;
        const vy = v.y;

        if (Math.abs(vx) < EPSILON || Math.abs(vy) < EPSILON) {
            throw new Error('Inversion of zero or near-zero value');
        }

        if (out) {
            out.x = Math.abs(vx) < EPSILON ? defaultValue : 1 / vx;
            out.y = Math.abs(vy) < EPSILON ? defaultValue : 1 / vy;
            return out;
        } else {
            return {
                x: Math.abs(vx) < EPSILON ? defaultValue : 1 / vx,
                y: Math.abs(vy) < EPSILON ? defaultValue : 1 / vy,
            } as T;
        }
    }

    static prependicular<T extends IVec2Like>(v: T, out?: T): T {
        if (out) {
            out.x = -v.y;
            out.y = v.x;
            return out;
        } else {
            return { x: -v.y, y: v.x } as T;
        }
    }

    static prependicularCCW<T extends IVec2Like>(v: T, out?: T): T {
        if (out) {
            out.x = v.y;
            out.y = -v.x;
            return out;
        } else {
            return { x: v.y, y: -v.x } as T;
        }
    }

    static dot<T extends IVec2Like>(a: T, b: T): number {
        return a.x * b.x + a.y * b.y;
    }

    static cross<T extends IVec2Like>(a: T, b: T): number {
        return a.x * b.y - a.y * b.x;
    }

    static len<T extends IVec2Like>(v: T): number {
        return Math.sqrt(v.x * v.x + v.y * v.y);
    }

    static lengthSquared<T extends IVec2Like>(v: T): number {
        return v.x * v.x + v.y * v.y;
    }

    static fastLength<T extends IVec2Like>(v: T): number {
        // Fast approximation of vector length (~3.4% error max)
        const min = Math.min(v.x, v.y);
        const max = Math.max(v.x, v.y);
        return max + 0.3 * min;
    }

    static normalize<T extends IVec2Like>(v: T, out?: T): T {
        const length = Math.sqrt(v.x * v.x + v.y * v.y);
        if (length < EPSILON) {
            throw new Error('Cannot normalize a zero-length vector');
        }

        if (out) {
            out.x = v.x / length;
            out.y = v.y / length;
            return out;
        } else {
            return { x: v.x / length, y: v.y / length } as T;
        }
    }

    static normalizeFast<T extends IVec2Like>(v: T, out?: T): T {
        const vx = v.x;
        const vy = v.y;
        const lenSq = vx * vx + vy * vy;
        if (lenSq < EPSILON) {
            throw new Error('Cannot normalize a zero-length vector');
        }

        let i = 0;
        const buf = new ArrayBuffer(4);
        const view = new DataView(buf);
        view.setFloat32(0, lenSq);
        i = view.getInt32(0);
        i = 0x5f3759df - (i >> 1);
        view.setInt32(0, i);
        let invLen = view.getFloat32(0);
        invLen = invLen * (1.5 - lenSq * 0.5 * invLen * invLen);

        if (out) {
            out.x = vx * invLen;
            out.y = vy * invLen;
            return out;
        } else {
            return { x: vx * invLen, y: vy * invLen } as T;
        }
    }

    static distanceSquared<T extends IVec2Like>(a: T, b: T): number {
        const dx = a.x - b.x;
        const dy = a.y - b.y;
        return dx * dx + dy * dy;
    }

    static distance<T extends IVec2Like>(a: T, b: T): number {
        const dx = a.x - b.x;
        const dy = a.y - b.y;
        return Math.sqrt(dx * dx + dy * dy);
    }

    static distanceFast<T extends IVec2Like>(a: T, b: T): number {
        // Fast approximation of vector distance (~3.4% error max)
        const dx = a.x - b.x;
        const dy = a.y - b.y;
        const min = Math.min(dx, dy);
        const max = Math.max(dx, dy);
        return max + 0.3 * min;
    }

    static manhattanDistance<T extends IVec2Like>(a: T, b: T): number {
        return Math.abs(a.x - b.x) + Math.abs(a.y - b.y);
    }

    static chebyshevDistance<T extends IVec2Like>(a: T, b: T): number {
        return Math.max(Math.abs(a.x - b.x), Math.abs(a.y - b.y));
    }

    static angle<T extends IVec2Like>(a: T, b: T): number {
        const dotProduct = Vec2.dot(a, b);
        const lengthA = Vec2.len(a);
        const lengthB = Vec2.len(b);

        if (lengthA < EPSILON || lengthB < EPSILON) {
            throw new Error('Cannot calculate angle with zero-length vector');
        }

        const cosTheta = dotProduct / (lengthA * lengthB);
        return Math.acos(Math.max(-1, Math.min(1, cosTheta)));
    }

    static fastAngle<T extends IVec2Like>(a: T, b: T): number {
        const x = b.x - a.x;
        const y = b.y - a.y;

        if (x === 0) return y > 0 ? HALF_PI : -HALF_PI;

        const abs_y = Math.abs(y);
        const abs_x = Math.abs(x);
        const a_val = abs_x > abs_y ? abs_y / abs_x : abs_x / abs_y;
        const s = a_val * a_val;
        let r = ((-0.0464964749 * s + 0.15931422) * s - 0.327622764) * s * a_val + a_val;

        if (abs_y > abs_x) r = HALF_PI - r;
        if (x < 0) r = Math.PI - r;
        if (y < 0) r = -r;

        return r;
    }

    static angle2Deg<T extends IVec2Like>(a: T, b: T): number {
        const angle = Vec2.angle(a, b);
        return (angle * 180) / Math.PI;
    }

    static rotate<T extends IVec2Like>(v: T, angle: number, out?: T): T {
        const cos = Math.cos(angle);
        const sin = Math.sin(angle);

        if (out) {
            out.x = v.x * cos - v.y * sin;
            out.y = v.x * sin + v.y * cos;
            return out;
        } else {
            return { x: v.x * cos - v.y * sin, y: v.x * sin + v.y * cos } as T;
        }
    }

        static fastRotate<T extends IVec2Like>(v: T, angle: number, out?: T): T {
        const x = v.x;
        const y = v.y;
        
        if (!out) {
            out = { x: 0, y: 0 } as T;
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
            const θ2_2 = angle * angle / 2;
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

    static rotateAround<T extends IVec2Like>(v: T, angle: number, pivot: T, out?: T): T {
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
            } as T;
        }
    }

    add<T extends IVec2Like>(other: T): Vec2 {
        this.x += other.x;
        this.y += other.y;
        return this;
    }

    addScalar(num: number): Vec2 {
        this.x += num;
        this.y += num;
        return this;
    }

    subtract<T extends IVec2Like>(other: T): Vec2 {
        this.x -= other.x;
        this.y -= other.y;
        return this;
    }

    subtractScalar(num: number): Vec2 {
        this.x -= num;
        this.y -= num;
        return this;
    }

    multiply<T extends IVec2Like>(other: T): Vec2 {
        this.x *= other.x;
        this.y *= other.y;
        return this;
    }

    multiplyScalar(num: number): Vec2 {
        this.x *= num;
        this.y *= num;
        return this;
    }

    divide<T extends IVec2Like>(other: T): Vec2 {
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

    dot<T extends IVec2Like>(other: T): number {
        return this.x * other.x + this.y * other.y;
    }

    cross<T extends IVec2Like>(other: T): number {
        return this.x * other.y - this.y * other.x;
    }

    lengthSquared(): number {
        return this.x * this.x + this.y * this.y;
    }

    length(): number {
        return Math.sqrt(this.lengthSquared());
    }

    fastLength(): number {
        // Fast approximation of vector length (~3.4% error max)
        const min = Math.min(this.x, this.y);
        const max = Math.max(this.x, this.y);
        return max + 0.3 * min;
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

    normalizeFast(): Vec2 {
        const lenSq = this.x * this.x + this.y * this.y;
        if (lenSq < EPSILON) {
            throw new Error('Cannot normalize a zero-length vector');
        }

        let i = 0;
        const buf = new ArrayBuffer(4);
        const view = new DataView(buf);
        view.setFloat32(0, lenSq);
        i = view.getInt32(0);
        i = 0x5f3759df - (i >> 1);
        view.setInt32(0, i);
        let invLen = view.getFloat32(0);
        invLen = invLen * (1.5 - lenSq * 0.5 * invLen * invLen);

        this.x *= invLen;
        this.y *= invLen;
        return this;
    }

    distanceSquared<T extends IVec2Like>(other: T): number {
        const dx = this.x - other.x;
        const dy = this.y - other.y;
        return dx * dx + dy * dy;
    }

    distance<T extends IVec2Like>(other: T): number {
        const dx = this.x - other.x;
        const dy = this.y - other.y;
        return Math.sqrt(dx * dx + dy * dy);
    }

    distanceFast<T extends IVec2Like>(other: T): number {
        // Fast approximation of vector distance (~3.4% error max)
        const dx = this.x - other.x;
        const dy = this.y - other.y;
        const min = Math.min(dx, dy);
        const max = Math.max(dx, dy);
        return max + 0.3 * min;
    }

    manhattanDistance<T extends IVec2Like>(other: T): number {
        return Math.abs(this.x - other.x) + Math.abs(this.y - other.y);
    }

    chebyshevDistance<T extends IVec2Like>(other: T): number {
        return Math.max(Math.abs(this.x - other.x), Math.abs(this.y - other.y));
    }

    angle<T extends IVec2Like>(other: T): number {
        const dotProduct = this.dot(other);
        const lengthA = this.length();
        const lengthB = Vec2.len(other);

        if (lengthA < EPSILON || lengthB < EPSILON) {
            throw new Error('Cannot calculate angle with zero-length vector');
        }

        const cosTheta = dotProduct / (lengthA * lengthB);
        return Math.acos(Math.max(-1, Math.min(1, cosTheta)));
    }

    fastAngle<T extends IVec2Like>(other: T): number {
        const x = other.x - this.x;
        const y = other.y - this.y;

        if (x === 0) return y > 0 ? HALF_PI : -HALF_PI;

        const abs_y = Math.abs(y);
        const abs_x = Math.abs(x);
        const a_val = abs_x > abs_y ? abs_y / abs_x : abs_x / abs_y;
        const s = a_val * a_val;
        let r = ((-0.0464964749 * s + 0.15931422) * s - 0.327622764) * s * a_val + a_val;

        if (abs_y > abs_x) r = HALF_PI - r;
        if (x < 0) r = Math.PI - r;
        if (y < 0) r = -r;

        return r;
    }

    angle2Deg<T extends IVec2Like>(other: T): number {
        const angle = this.angle(other);
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

    rotateAround<T extends IVec2Like>(pivot: T, angle: number): Vec2 {
        const cos = Math.cos(angle);
        const sin = Math.sin(angle);

        const x = (this.x - pivot.x) * cos - (this.y - pivot.y) * sin + pivot.x;
        const y = (this.x - pivot.x) * sin + (this.y - pivot.y) * cos + pivot.y;

        this.x = x;
        this.y = y;
        return this;
    }
}
