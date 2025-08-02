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
