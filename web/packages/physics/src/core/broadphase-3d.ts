import type { IVec3Like } from '@axrone/numeric';
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

function pairKey(a: number, b: number): string {
    return a < b ? `${a}:${b}` : `${b}:${a}`;
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
        const results: IBroadphaseResult3D<T>[] = [];
        const seen = new Set<string>();

        for (const [item, [min, max]] of this._bounds) {
            const candidates = this._grid.query(min, max);
            const idA = this._getId(item);
            for (const other of candidates) {
                if (other === item) continue;
                const idB = this._getId(other);
                const key = pairKey(idA, idB);
                if (seen.has(key)) continue;
                seen.add(key);
                results.push({ itemA: item, itemB: other });
            }
        }
        return results;
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
        this._bounds.delete(item);
        this._octree.clear();
        this._bounds.set(item, [min, max]);
        for (const [existing, [existingMin, existingMax]] of this._bounds) {
            this._octree.insert(existing, existingMin, existingMax);
        }
    }

    remove(item: T): void {
        this._bounds.delete(item);
        this._itemId.delete(item);
        this._octree.clear();
        for (const [existing, [existingMin, existingMax]] of this._bounds) {
            this._octree.insert(existing, existingMin, existingMax);
        }
    }

    queryPairs(): IBroadphaseResult3D<T>[] {
        const results: IBroadphaseResult3D<T>[] = [];
        const seen = new Set<string>();

        for (const [item, [min, max]] of this._bounds) {
            const candidates = this._octree.query(min, max);
            const idA = this._getId(item);
            for (const other of candidates) {
                if (other === item) continue;
                const idB = this._getId(other);
                const key = pairKey(idA, idB);
                if (seen.has(key)) continue;
                seen.add(key);
                results.push({ itemA: item, itemB: other });
            }
        }
        return results;
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
