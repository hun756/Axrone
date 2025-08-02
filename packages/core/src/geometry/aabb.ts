import { IVec2Like, IVec3Like, Vec2, Vec3, Mat4, IMat4Like, EPSILON } from '@axrone/numeric';

export type Brand<K, T> = K & { readonly __brand: T };
export type Radians = Brand<number, 'Radians'>;
export type Degrees = Brand<number, 'Degrees'>;

export interface IAABB<T extends IVec2Like | IVec3Like> {
    readonly min: T;
    readonly max: T;
    readonly center: T;
    readonly extents: T;
    readonly size: T;
    readonly volume: number;
    readonly surfaceArea: number;
    readonly isEmpty: boolean;
    readonly dimensions: number;
    containsPoint(point: T): boolean;
    containsAABB(other: IAABB<T>): boolean;
    intersectsAABB(other: IAABB<T>): boolean;
    getIntersection(other: IAABB<T>, result?: IAABB<T>): IAABB<T> | null;
    getUnion(other: IAABB<T>, result?: IAABB<T>): IAABB<T>;
    expand(amount: number | T): IAABB<T>;
    transform(matrix: IMat4Like): IAABB<T>;
    closestPoint(point: T, out: T): void;
    distanceToPoint(point: T): number;
    squaredDistanceToPoint(point: T): number;
    clone(): IAABB<T>;
    equals(other: IAABB<T>, epsilon?: number): boolean;
    copy(other: IAABB<T>): void;
    clear(): void;
    toString(): string;
}

export type AABBCreateParams<T extends IVec2Like | IVec3Like> = {
    readonly min?: T;
    readonly max?: T;
    readonly center?: T;
    readonly extents?: T;
    readonly points?: readonly T[];
};

export class AABB2D implements IAABB<IVec2Like> {
    public _min: Vec2;
    public _max: Vec2;
    public _center: Vec2;
    public _extents: Vec2;

    constructor(min?: IVec2Like, max?: IVec2Like) {
        this._min = min ? Vec2.from(min) : Vec2.ZERO.clone();
        this._max = max ? Vec2.from(max) : Vec2.ZERO.clone();
        this._center = Vec2.ZERO.clone();
        this._extents = Vec2.ZERO.clone();
        this.updateDerivedData();
    }

    get min(): IVec2Like {
        return this._min;
    }
    get max(): IVec2Like {
        return this._max;
    }
    get center(): IVec2Like {
        return this._center;
    }
    get extents(): IVec2Like {
        return this._extents;
    }

    get size(): IVec2Like {
        return Vec2.subtract(this._max, this._min);
    }

    get volume(): number {
        if (this.isEmpty) return 0;
        const size = this.size;
        return size.x * size.y;
    }

    get surfaceArea(): number {
        if (this.isEmpty) return 0;
        const size = this.size;
        return 2 * (size.x + size.y);
    }

    get isEmpty(): boolean {
        return this._min.x > this._max.x || this._min.y > this._max.y;
    }

    get dimensions(): number {
        return 2;
    }

    public updateDerivedData(): void {
        this._center = Vec2.add(this._min, this._max, this._center);
        this._center = Vec2.multiplyScalar(this._center, 0.5, this._center);

        this._extents = Vec2.subtract(this._max, this._min, this._extents);
        this._extents = Vec2.multiplyScalar(this._extents, 0.5, this._extents);
    }

    containsPoint(point: IVec2Like): boolean {
        return (
            point.x >= this._min.x &&
            point.x <= this._max.x &&
            point.y >= this._min.y &&
            point.y <= this._max.y
        );
    }

    containsAABB(other: IAABB<IVec2Like>): boolean {
        return (
            this._min.x <= other.min.x &&
            this._max.x >= other.max.x &&
            this._min.y <= other.min.y &&
            this._max.y >= other.max.y
        );
    }

    intersectsAABB(other: IAABB<IVec2Like>): boolean {
        return (
            this._min.x <= other.max.x &&
            this._max.x >= other.min.x &&
            this._min.y <= other.max.y &&
            this._max.y >= other.min.y
        );
    }

    getIntersection(other: IAABB<IVec2Like>, result?: IAABB<IVec2Like>): IAABB<IVec2Like> | null {
        if (!this.intersectsAABB(other)) return null;

        const out = result || new AABB2D();
        const outAABB = out as AABB2D;

        outAABB._min.x = Math.max(this._min.x, other.min.x);
        outAABB._min.y = Math.max(this._min.y, other.min.y);
        outAABB._max.x = Math.min(this._max.x, other.max.x);
        outAABB._max.y = Math.min(this._max.y, other.max.y);
        outAABB.updateDerivedData();

        return out;
    }

    getUnion(other: IAABB<IVec2Like>, result?: IAABB<IVec2Like>): IAABB<IVec2Like> {
        const out = result || new AABB2D();
        const outAABB = out as AABB2D;

        outAABB._min.x = Math.min(this._min.x, other.min.x);
        outAABB._min.y = Math.min(this._min.y, other.min.y);
        outAABB._max.x = Math.max(this._max.x, other.max.x);
        outAABB._max.y = Math.max(this._max.y, other.max.y);
        outAABB.updateDerivedData();

        return out;
    }

    expand(amount: number | IVec2Like): IAABB<IVec2Like> {
        const result = this.clone() as AABB2D;

        if (typeof amount === 'number') {
            result._min.x -= amount;
            result._min.y -= amount;
            result._max.x += amount;
            result._max.y += amount;
        } else {
            result._min.x -= amount.x;
            result._min.y -= amount.y;
            result._max.x += amount.x;
            result._max.y += amount.y;
        }

        result.updateDerivedData();
        return result;
    }

    transform(matrix: IMat4Like): IAABB<IVec2Like> {
        const result = new AABB2D();
        result._min.x = Infinity;
        result._min.y = Infinity;
        result._max.x = -Infinity;
        result._max.y = -Infinity;

        const corners: IVec2Like[] = [
            { x: this._min.x, y: this._min.y },
            { x: this._max.x, y: this._min.y },
            { x: this._min.x, y: this._max.y },
            { x: this._max.x, y: this._max.y },
        ];

        for (const corner of corners) {
            const point = Vec3.create(corner.x, corner.y, 0);
            const transformed = Mat4.transformVec3(point, matrix);

            result._min.x = Math.min(result._min.x, transformed.x);
            result._min.y = Math.min(result._min.y, transformed.y);
            result._max.x = Math.max(result._max.x, transformed.x);
            result._max.y = Math.max(result._max.y, transformed.y);
        }

        result.updateDerivedData();
        return result;
    }

    closestPoint(point: IVec2Like, out: IVec2Like): void {
        if ('x' in out && 'y' in out) {
            out.x = Math.max(this._min.x, Math.min(this._max.x, point.x));
            out.y = Math.max(this._min.y, Math.min(this._max.y, point.y));
        }
    }

    distanceToPoint(point: IVec2Like): number {
        return Math.sqrt(this.squaredDistanceToPoint(point));
    }

    squaredDistanceToPoint(point: IVec2Like): number {
        let sqDist = 0;

        if (point.x < this._min.x) sqDist += (this._min.x - point.x) ** 2;
        if (point.x > this._max.x) sqDist += (point.x - this._max.x) ** 2;
        if (point.y < this._min.y) sqDist += (this._min.y - point.y) ** 2;
        if (point.y > this._max.y) sqDist += (point.y - this._max.y) ** 2;

        return sqDist;
    }

    clone(): IAABB<IVec2Like> {
        const result = new AABB2D();
        result._min = this._min.clone();
        result._max = this._max.clone();
        result.updateDerivedData();
        return result;
    }

    equals(other: IAABB<IVec2Like>, epsilon = EPSILON): boolean {
        return (
            Math.abs(this._min.x - other.min.x) <= epsilon &&
            Math.abs(this._min.y - other.min.y) <= epsilon &&
            Math.abs(this._max.x - other.max.x) <= epsilon &&
            Math.abs(this._max.y - other.max.y) <= epsilon
        );
    }

    copy(other: IAABB<IVec2Like>): void {
        this._min.x = other.min.x;
        this._min.y = other.min.y;
        this._max.x = other.max.x;
        this._max.y = other.max.y;
        this.updateDerivedData();
    }

    clear(): void {
        this._min.x = this._min.y = Infinity;
        this._max.x = this._max.y = -Infinity;
        this.updateDerivedData();
    }

    toString(): string {
        return `AABB2D(min: [${this._min.x}, ${this._min.y}], max: [${this._max.x}, ${this._max.y}])`;
    }
}

