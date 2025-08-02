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

export class AABB3D implements IAABB<IVec3Like> {
    public _min: Vec3;
    public _max: Vec3;
    public _center: Vec3;
    public _extents: Vec3;

    constructor(min?: IVec3Like, max?: IVec3Like) {
        this._min = min ? Vec3.from(min) : Vec3.ZERO.clone();
        this._max = max ? Vec3.from(max) : Vec3.ZERO.clone();
        this._center = Vec3.ZERO.clone();
        this._extents = Vec3.ZERO.clone();
        this.updateDerivedData();
    }

    get min(): IVec3Like {
        return this._min;
    }
    get max(): IVec3Like {
        return this._max;
    }
    get center(): IVec3Like {
        return this._center;
    }
    get extents(): IVec3Like {
        return this._extents;
    }

    get size(): IVec3Like {
        return Vec3.subtract(this._max, this._min);
    }

    get volume(): number {
        if (this.isEmpty) return 0;
        const size = this.size;
        return size.x * size.y * size.z;
    }

    get surfaceArea(): number {
        if (this.isEmpty) return 0;
        const size = this.size;
        return 2 * (size.x * size.y + size.y * size.z + size.z * size.x);
    }

    get isEmpty(): boolean {
        return this._min.x > this._max.x || this._min.y > this._max.y || this._min.z > this._max.z;
    }

    get dimensions(): number {
        return 3;
    }

    public updateDerivedData(): void {
        this._center = Vec3.add(this._min, this._max, this._center);
        this._center = Vec3.multiplyScalar(this._center, 0.5, this._center);

        this._extents = Vec3.subtract(this._max, this._min, this._extents);
        this._extents = Vec3.multiplyScalar(this._extents, 0.5, this._extents);
    }

    containsPoint(point: IVec3Like): boolean {
        return (
            point.x >= this._min.x &&
            point.x <= this._max.x &&
            point.y >= this._min.y &&
            point.y <= this._max.y &&
            point.z >= this._min.z &&
            point.z <= this._max.z
        );
    }

    containsAABB(other: IAABB<IVec3Like>): boolean {
        return (
            this._min.x <= other.min.x &&
            this._max.x >= other.max.x &&
            this._min.y <= other.min.y &&
            this._max.y >= other.max.y &&
            this._min.z <= other.min.z &&
            this._max.z >= other.max.z
        );
    }

    intersectsAABB(other: IAABB<IVec3Like>): boolean {
        return (
            this._min.x <= other.max.x &&
            this._max.x >= other.min.x &&
            this._min.y <= other.max.y &&
            this._max.y >= other.min.y &&
            this._min.z <= other.max.z &&
            this._max.z >= other.min.z
        );
    }

    getIntersection(other: IAABB<IVec3Like>, result?: IAABB<IVec3Like>): IAABB<IVec3Like> | null {
        if (!this.intersectsAABB(other)) return null;

        const out = result || new AABB3D();
        const outAABB = out as AABB3D;

        outAABB._min.x = Math.max(this._min.x, other.min.x);
        outAABB._min.y = Math.max(this._min.y, other.min.y);
        outAABB._min.z = Math.max(this._min.z, other.min.z);
        outAABB._max.x = Math.min(this._max.x, other.max.x);
        outAABB._max.y = Math.min(this._max.y, other.max.y);
        outAABB._max.z = Math.min(this._max.z, other.max.z);
        outAABB.updateDerivedData();

        return out;
    }

    getUnion(other: IAABB<IVec3Like>, result?: IAABB<IVec3Like>): IAABB<IVec3Like> {
        const out = result || new AABB3D();
        const outAABB = out as AABB3D;

        outAABB._min.x = Math.min(this._min.x, other.min.x);
        outAABB._min.y = Math.min(this._min.y, other.min.y);
        outAABB._min.z = Math.min(this._min.z, other.min.z);
        outAABB._max.x = Math.max(this._max.x, other.max.x);
        outAABB._max.y = Math.max(this._max.y, other.max.y);
        outAABB._max.z = Math.max(this._max.z, other.max.z);
        outAABB.updateDerivedData();

        return out;
    }

    expand(amount: number | IVec3Like): IAABB<IVec3Like> {
        const result = this.clone() as AABB3D;

        if (typeof amount === 'number') {
            result._min.x -= amount;
            result._min.y -= amount;
            result._min.z -= amount;
            result._max.x += amount;
            result._max.y += amount;
            result._max.z += amount;
        } else {
            result._min.x -= amount.x;
            result._min.y -= amount.y;
            result._min.z -= amount.z;
            result._max.x += amount.x;
            result._max.y += amount.y;
            result._max.z += amount.z;
        }

        result.updateDerivedData();
        return result;
    }

    transform(matrix: IMat4Like): IAABB<IVec3Like> {
        const result = new AABB3D();
        result._min.x = Infinity;
        result._min.y = Infinity;
        result._min.z = Infinity;
        result._max.x = -Infinity;
        result._max.y = -Infinity;
        result._max.z = -Infinity;

        const corners: IVec3Like[] = [
            { x: this._min.x, y: this._min.y, z: this._min.z },
            { x: this._max.x, y: this._min.y, z: this._min.z },
            { x: this._min.x, y: this._max.y, z: this._min.z },
            { x: this._max.x, y: this._max.y, z: this._min.z },
            { x: this._min.x, y: this._min.y, z: this._max.z },
            { x: this._max.x, y: this._min.y, z: this._max.z },
            { x: this._min.x, y: this._max.y, z: this._max.z },
            { x: this._max.x, y: this._max.y, z: this._max.z },
        ];

        for (const corner of corners) {
            const transformed = Mat4.transformVec3(corner, matrix);

            result._min.x = Math.min(result._min.x, transformed.x);
            result._min.y = Math.min(result._min.y, transformed.y);
            result._min.z = Math.min(result._min.z, transformed.z);
            result._max.x = Math.max(result._max.x, transformed.x);
            result._max.y = Math.max(result._max.y, transformed.y);
            result._max.z = Math.max(result._max.z, transformed.z);
        }

        result.updateDerivedData();
        return result;
    }

    closestPoint(point: IVec3Like, out: IVec3Like): void {
        if ('x' in out && 'y' in out && 'z' in out) {
            out.x = Math.max(this._min.x, Math.min(this._max.x, point.x));
            out.y = Math.max(this._min.y, Math.min(this._max.y, point.y));
            out.z = Math.max(this._min.z, Math.min(this._max.z, point.z));
        }
    }

    distanceToPoint(point: IVec3Like): number {
        return Math.sqrt(this.squaredDistanceToPoint(point));
    }

    squaredDistanceToPoint(point: IVec3Like): number {
        let sqDist = 0;

        if (point.x < this._min.x) sqDist += (this._min.x - point.x) ** 2;
        if (point.x > this._max.x) sqDist += (point.x - this._max.x) ** 2;
        if (point.y < this._min.y) sqDist += (this._min.y - point.y) ** 2;
        if (point.y > this._max.y) sqDist += (point.y - this._max.y) ** 2;
        if (point.z < this._min.z) sqDist += (this._min.z - point.z) ** 2;
        if (point.z > this._max.z) sqDist += (point.z - this._max.z) ** 2;

        return sqDist;
    }

    clone(): IAABB<IVec3Like> {
        const result = new AABB3D();
        result._min = this._min.clone();
        result._max = this._max.clone();
        result.updateDerivedData();
        return result;
    }

    equals(other: IAABB<IVec3Like>, epsilon = EPSILON): boolean {
        return (
            Math.abs(this._min.x - other.min.x) <= epsilon &&
            Math.abs(this._min.y - other.min.y) <= epsilon &&
            Math.abs(this._min.z - other.min.z) <= epsilon &&
            Math.abs(this._max.x - other.max.x) <= epsilon &&
            Math.abs(this._max.y - other.max.y) <= epsilon &&
            Math.abs(this._max.z - other.max.z) <= epsilon
        );
    }

    copy(other: IAABB<IVec3Like>): void {
        this._min.x = other.min.x;
        this._min.y = other.min.y;
        this._min.z = other.min.z;
        this._max.x = other.max.x;
        this._max.y = other.max.y;
        this._max.z = other.max.z;
        this.updateDerivedData();
    }

    clear(): void {
        this._min.x = this._min.y = this._min.z = Infinity;
        this._max.x = this._max.y = this._max.z = -Infinity;
        this.updateDerivedData();
    }

    toString(): string {
        return `AABB3D(min: [${this._min.x}, ${this._min.y}, ${this._min.z}], max: [${this._max.x}, ${this._max.y}, ${this._max.z}])`;
    }
}

export function createAABB<T extends IVec2Like>(params: AABBCreateParams<T>): IAABB<T>;
export function createAABB<T extends IVec3Like>(params: AABBCreateParams<T>): IAABB<T>;
export function createAABB<T extends IVec2Like | IVec3Like>(params: AABBCreateParams<T>): IAABB<T> {
    if (params.min && params.max) {
        const isVec3 = 'z' in params.min;
        return isVec3
            ? (new AABB3D(params.min as IVec3Like, params.max as IVec3Like) as unknown as IAABB<T>)
            : (new AABB2D(params.min as IVec2Like, params.max as IVec2Like) as unknown as IAABB<T>);
    }

    if (params.center && params.extents) {
        const isVec3 = 'z' in params.center;
        const result = isVec3 ? new AABB3D() : new AABB2D();

        if (isVec3) {
            const result3D = result as AABB3D;
            const center3D = params.center as IVec3Like;
            const extents3D = params.extents as IVec3Like;

            result3D._min.x = center3D.x - extents3D.x;
            result3D._min.y = center3D.y - extents3D.y;
            result3D._min.z = center3D.z - extents3D.z;
            result3D._max.x = center3D.x + extents3D.x;
            result3D._max.y = center3D.y + extents3D.y;
            result3D._max.z = center3D.z + extents3D.z;
            result3D.updateDerivedData();
        } else {
            const result2D = result as AABB2D;
            const center2D = params.center as IVec2Like;
            const extents2D = params.extents as IVec2Like;

            result2D._min.x = center2D.x - extents2D.x;
            result2D._min.y = center2D.y - extents2D.y;
            result2D._max.x = center2D.x + extents2D.x;
            result2D._max.y = center2D.y + extents2D.y;
            result2D.updateDerivedData();
        }

        return result as unknown as IAABB<T>;
    }

    if (params.points && params.points.length > 0) {
        return createFromPoints(params.points);
    }

    throw new AABBError('Invalid parameters for creating AABB');
}

export function createFromPoints<T extends IVec2Like>(points: readonly T[]): IAABB<T>;
export function createFromPoints<T extends IVec3Like>(points: readonly T[]): IAABB<T>;
export function createFromPoints<T extends IVec2Like | IVec3Like>(points: readonly T[]): IAABB<T> {
    if (!points.length) throw new AABBError('Cannot create AABB from empty points array');

    const firstPoint = points[0];
    const isVec3 = 'z' in firstPoint;
    const result = isVec3 ? new AABB3D() : new AABB2D();

    if (isVec3) {
        const result3D = result as AABB3D;
        const firstPoint3D = firstPoint as IVec3Like;

        result3D._min.x = result3D._max.x = firstPoint3D.x;
        result3D._min.y = result3D._max.y = firstPoint3D.y;
        result3D._min.z = result3D._max.z = firstPoint3D.z;

        for (let i = 1; i < points.length; i++) {
            const point = points[i] as IVec3Like;
            result3D._min.x = Math.min(result3D._min.x, point.x);
            result3D._min.y = Math.min(result3D._min.y, point.y);
            result3D._min.z = Math.min(result3D._min.z, point.z);
            result3D._max.x = Math.max(result3D._max.x, point.x);
            result3D._max.y = Math.max(result3D._max.y, point.y);
            result3D._max.z = Math.max(result3D._max.z, point.z);
        }
        result3D.updateDerivedData();
    } else {
        const result2D = result as AABB2D;
        const firstPoint2D = firstPoint as IVec2Like;

        result2D._min.x = result2D._max.x = firstPoint2D.x;
        result2D._min.y = result2D._max.y = firstPoint2D.y;

        for (let i = 1; i < points.length; i++) {
            const point = points[i] as IVec2Like;
            result2D._min.x = Math.min(result2D._min.x, point.x);
            result2D._min.y = Math.min(result2D._min.y, point.y);
            result2D._max.x = Math.max(result2D._max.x, point.x);
            result2D._max.y = Math.max(result2D._max.y, point.y);
        }
        result2D.updateDerivedData();
    }

    return result as unknown as IAABB<T>;
}

export function createFromCenterAndSize<T extends IVec2Like>(center: T, size: T): IAABB<T>;
export function createFromCenterAndSize<T extends IVec3Like>(center: T, size: T): IAABB<T>;
export function createFromCenterAndSize<T extends IVec2Like | IVec3Like>(
    center: T,
    size: T
): IAABB<T> {
    const isVec3 = 'z' in center;
    const result = isVec3 ? new AABB3D() : new AABB2D();

    if (isVec3) {
        const result3D = result as AABB3D;
        const center3D = center as IVec3Like;
        const size3D = size as IVec3Like;

        const halfSizeX = size3D.x * 0.5;
        const halfSizeY = size3D.y * 0.5;
        const halfSizeZ = size3D.z * 0.5;

        result3D._min.x = center3D.x - halfSizeX;
        result3D._min.y = center3D.y - halfSizeY;
        result3D._min.z = center3D.z - halfSizeZ;
        result3D._max.x = center3D.x + halfSizeX;
        result3D._max.y = center3D.y + halfSizeY;
        result3D._max.z = center3D.z + halfSizeZ;
        result3D.updateDerivedData();
    } else {
        const result2D = result as AABB2D;
        const center2D = center as IVec2Like;
        const size2D = size as IVec2Like;

        const halfSizeX = size2D.x * 0.5;
        const halfSizeY = size2D.y * 0.5;

        result2D._min.x = center2D.x - halfSizeX;
        result2D._min.y = center2D.y - halfSizeY;
        result2D._max.x = center2D.x + halfSizeX;
        result2D._max.y = center2D.y + halfSizeY;
        result2D.updateDerivedData();
    }

    return result as unknown as IAABB<T>;
}

export function createEmpty<T extends IVec2Like | IVec3Like>(dimensions: 2 | 3): IAABB<T> {
    if (dimensions === 2) {
        const result = new AABB2D();
        result._min.x = result._min.y = Infinity;
        result._max.x = result._max.y = -Infinity;
        result.updateDerivedData();
        return result as unknown as IAABB<T>;
    } else {
        const result = new AABB3D();
        result._min.x = result._min.y = result._min.z = Infinity;
        result._max.x = result._max.y = result._max.z = -Infinity;
        result.updateDerivedData();
        return result as unknown as IAABB<T>;
    }
}

export class AABBError extends Error {
    constructor(message: string) {
        super(message);
        this.name = 'AABBError';
    }
}
