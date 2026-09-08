import type { IVec3Like } from '@axrone/numeric';
import { makeCollisionPairKey } from '@axrone/physics-core';
import { SpatialHashGrid3D, SpatialOctree } from './raycast-spatial';

export interface IBroadphaseItem3D {
    readonly id: number;
}

export interface IAABB3DLike {
    readonly min: Readonly<IVec3Like>;
    readonly max: Readonly<IVec3Like>;
}

export interface IBroadphaseResult3D<T> {
    readonly itemA: T;
    readonly itemB: T;
}

export interface IBroadphase3D<T> {
    insert(item: T, min: Readonly<IVec3Like>, max: Readonly<IVec3Like>): void;
    update(item: T, min: Readonly<IVec3Like>, max: Readonly<IVec3Like>): void;
    remove(item: T): void;
    queryPairs(): IBroadphaseResult3D<T>[];
    queryAABB(min: Readonly<IVec3Like>, max: Readonly<IVec3Like>): T[];
    queryRay(origin: Readonly<IVec3Like>, direction: Readonly<IVec3Like>, maxDistance: number): T[];
    clear(): void;
    readonly itemCount: number;
}

function pairKey(a: number, b: number): number {
    return makeCollisionPairKey(a, b);
}

function dedupedPairs<T>(
    bounds: Map<T, [Readonly<IVec3Like>, Readonly<IVec3Like>]>,
    queryFn: (min: Readonly<IVec3Like>, max: Readonly<IVec3Like>) => T[],
    getIdFn: (item: T) => number
): IBroadphaseResult3D<T>[] {
    const results: IBroadphaseResult3D<T>[] = [];
    const seen = new Set<number>();

    for (const [item, [min, max]] of bounds) {
        const candidates = queryFn(min, max);
        const idA = getIdFn(item);
        for (const other of candidates) {
            if (other === item) continue;
            const idB = getIdFn(other);
            const key = pairKey(idA, idB);
            if (seen.has(key)) continue;
            seen.add(key);
            results.push({ itemA: item, itemB: other });
        }
    }
    return results;
}

export class SpatialHashBroadphase3D<T extends IBroadphaseItem3D> implements IBroadphase3D<T> {
    private readonly _grid: SpatialHashGrid3D<T>;
    private readonly _bounds = new Map<T, [Readonly<IVec3Like>, Readonly<IVec3Like>]>();
    private _nextId = 0;
    private readonly _itemId = new Map<T, number>();

    constructor(cellSize: number = 10) {
        this._grid = new SpatialHashGrid3D<T>(cellSize);
    }

    private _getId(item: T): number {
        let id = this._itemId.get(item);
        if (id === undefined) {
            id = this._nextId++;
            this._itemId.set(item, id);
        }
        return id;
    }

    insert(item: T, min: Readonly<IVec3Like>, max: Readonly<IVec3Like>): void {
        this._bounds.set(item, [min, max]);
        this._grid.insert(item, min, max);
        this._getId(item);
    }

    update(item: T, min: Readonly<IVec3Like>, max: Readonly<IVec3Like>): void {
        this._bounds.delete(item);
        this._grid.remove(item);
        this._bounds.set(item, [min, max]);
        this._grid.insert(item, min, max);
    }

    remove(item: T): void {
        this._grid.remove(item);
        this._bounds.delete(item);
        this._itemId.delete(item);
    }

    queryPairs(): IBroadphaseResult3D<T>[] {
        return dedupedPairs(this._bounds, (min, max) => this._grid.query(min, max), (item) => this._getId(item));
    }

    queryAABB(min: Readonly<IVec3Like>, max: Readonly<IVec3Like>): T[] {
        return this._grid.query(min, max);
    }

    queryRay(origin: Readonly<IVec3Like>, direction: Readonly<IVec3Like>, maxDistance: number): T[] {
        return this._grid.queryRay(origin, direction, maxDistance);
    }

    clear(): void {
        this._grid.clear();
        this._bounds.clear();
        this._itemId.clear();
    }

    get itemCount(): number {
        return this._grid.itemCount;
    }
}

export class OctreeBroadphase3D<T extends IBroadphaseItem3D> implements IBroadphase3D<T> {
    private readonly _octree: SpatialOctree<T>;
    private readonly _bounds = new Map<T, [Readonly<IVec3Like>, Readonly<IVec3Like>]>();
    private _nextId = 0;
    private readonly _itemId = new Map<T, number>();

    constructor(
        center: Readonly<IVec3Like>,
        halfSize: number,
        maxDepth: number = 8,
        maxItemsPerNode: number = 8,
        minNodeSize: number = 1.0
    ) {
        this._octree = new SpatialOctree<T>(center, halfSize, maxDepth, maxItemsPerNode, minNodeSize);
    }

    private _getId(item: T): number {
        let id = this._itemId.get(item);
        if (id === undefined) {
            id = this._nextId++;
            this._itemId.set(item, id);
        }
        return id;
    }

    insert(item: T, min: Readonly<IVec3Like>, max: Readonly<IVec3Like>): void {
        this._bounds.set(item, [min, max]);
        this._octree.insert(item, min, max);
        this._getId(item);
    }

    update(item: T, min: Readonly<IVec3Like>, max: Readonly<IVec3Like>): void {
        this._octree.remove(item);
        this._bounds.set(item, [min, max]);
        this._octree.insert(item, min, max);
    }

    remove(item: T): void {
        this._octree.remove(item);
        this._bounds.delete(item);
        this._itemId.delete(item);
    }

    queryPairs(): IBroadphaseResult3D<T>[] {
        return dedupedPairs(this._bounds, (min, max) => this._octree.query(min, max), (item) => this._getId(item));
    }

    queryAABB(min: Readonly<IVec3Like>, max: Readonly<IVec3Like>): T[] {
        return this._octree.query(min, max);
    }

    queryRay(origin: Readonly<IVec3Like>, direction: Readonly<IVec3Like>, maxDistance: number): T[] {
        return this._octree.queryRay(origin, direction, maxDistance);
    }

    clear(): void {
        this._octree.clear();
        this._bounds.clear();
        this._itemId.clear();
    }

    get itemCount(): number {
        return this._octree.itemCount;
    }
}
