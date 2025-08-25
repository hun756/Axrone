import { Vec3 } from '@axrone/numeric';
import { AABB3D } from '../geometry';
import { MemoryPool, PoolableObject } from '@axrone/utility';
import { ParticleId } from './types';
import { ISpatialCell, ISpatialGrid } from './interfaces';

export class SpatialCell implements ISpatialCell {
    bounds: AABB3D;
    particles: ParticleId[];
    neighborCells: ISpatialCell[];

    constructor() {
        this.bounds = new AABB3D(new Vec3(0, 0, 0), new Vec3(0, 0, 0));
        this.particles = [];
        this.neighborCells = [];
    }

    reset(): void {
        this.particles.length = 0;
        this.neighborCells.length = 0;
    }

    dispose(): void {
        this.reset();
    }
}

export class SpatialGrid implements ISpatialGrid {
    private readonly _cellSize: Vec3;
    private readonly _bounds: AABB3D;
    private readonly _cells: Map<bigint, ISpatialCell>;
    private readonly _cellPool: MemoryPool<SpatialCell>;
    private readonly _particleToCell: Map<
        ParticleId,
        { cell: ISpatialCell; index: number }
    >;

    constructor(bounds: AABB3D, cellSize: Vec3, poolCapacity: number = 100) {
        this._bounds = new AABB3D(Vec3.from(bounds.min), Vec3.from(bounds.max));
        this._cellSize = cellSize.clone();
        this._cells = new Map();
        this._cellPool = new MemoryPool({
            factory: () => new SpatialCell(),
            initialCapacity: poolCapacity,
            maxCapacity: Math.max(poolCapacity * 2, poolCapacity),
            preallocate: true,
        });
        this._particleToCell = new Map();
    }

    get cellSize(): Vec3 {
        return this._cellSize.clone();
    }
    get bounds(): AABB3D {
        return new AABB3D(Vec3.from(this._bounds.min), Vec3.from(this._bounds.max));
    }

    private getCellKey(x: number, y: number, z: number): bigint {
        const mask = BigInt(0x1fffff); // 21 bits
        const bias = 1 << 20; // 2^20
        const nx = (BigInt(x + bias) & mask);
        const ny = (BigInt(y + bias) & mask);
        const nz = (BigInt(z + bias) & mask);
        return nx | (ny << BigInt(21)) | (nz << BigInt(42));
    }

    private getCellCoordinates(position: Vec3): { x: number; y: number; z: number } {
        return {
            x: Math.floor((position.x - this._bounds.min.x) / this._cellSize.x),
            y: Math.floor((position.y - this._bounds.min.y) / this._cellSize.y),
            z: Math.floor((position.z - this._bounds.min.z) / this._cellSize.z),
        };
    }

    private getOrCreateCell(x: number, y: number, z: number): ISpatialCell {
        const key = this.getCellKey(x, y, z);
    let cell = this._cells.get(key as unknown as bigint);

        if (!cell) {
            cell = this._cellPool.acquire();
            const minX = this._bounds.min.x + x * this._cellSize.x;
            const minY = this._bounds.min.y + y * this._cellSize.y;
            const minZ = this._bounds.min.z + z * this._cellSize.z;
            const maxX = minX + this._cellSize.x;
            const maxY = minY + this._cellSize.y;
            const maxZ = minZ + this._cellSize.z;
            cell.bounds.copy(new AABB3D(new Vec3(minX, minY, minZ), new Vec3(maxX, maxY, maxZ)));

            this._cells.set(key as unknown as bigint, cell);
        }

        return cell;
    }

    insert(particleId: ParticleId, position: Vec3): void {
    this.remove(particleId);

        if (!this._bounds.containsPoint(position)) {
            return;
        }

        const coords = this.getCellCoordinates(position);
        const cell = this.getOrCreateCell(coords.x, coords.y, coords.z);

    const idx = cell.particles.length;
    cell.particles.push(particleId);
    this._particleToCell.set(particleId, { cell, index: idx });
    }

    remove(particleId: ParticleId): void {
        const entry = this._particleToCell.get(particleId);
        if (entry) {
            const { cell, index } = entry;
            const lastIdx = cell.particles.length - 1;
            const lastId = cell.particles[lastIdx];
            if (index !== lastIdx) {
                cell.particles[index] = lastId;
                this._particleToCell.set(lastId, { cell, index });
            }
            cell.particles.pop();
            this._particleToCell.delete(particleId);
        }
    }

    update(particleId: ParticleId, oldPosition: Vec3, newPosition: Vec3): void {
        const oldCoords = this.getCellCoordinates(oldPosition);
        const newCoords = this.getCellCoordinates(newPosition);

        if (
            oldCoords.x !== newCoords.x ||
            oldCoords.y !== newCoords.y ||
            oldCoords.z !== newCoords.z
        ) {
            this.insert(particleId, newPosition);
        }
    }

    query(bounds: AABB3D): ParticleId[] {
        const result: ParticleId[] = [];
    const visited = new Set<bigint>();

        const minCoords = this.getCellCoordinates(Vec3.from(bounds.min));
        const maxCoords = this.getCellCoordinates(Vec3.from(bounds.max));

        for (let x = minCoords.x; x <= maxCoords.x; x++) {
            for (let y = minCoords.y; y <= maxCoords.y; y++) {
                for (let z = minCoords.z; z <= maxCoords.z; z++) {
                    const key = this.getCellKey(x, y, z);
                    if (visited.has(key)) continue;
                    visited.add(key);

                    const cell = this._cells.get(key);
                    if (cell && cell.bounds.intersectsAABB(bounds)) {
                        result.push(...cell.particles);
                    }
                }
            }
        }

        return result;
    }

    queryRadius(center: Vec3, radius: number): ParticleId[] {
        const bounds = new AABB3D(
            center.subtract(new Vec3(radius, radius, radius)),
            center.add(new Vec3(radius, radius, radius))
        );
        return this.query(bounds);
    }

    getCellAt(position: Vec3): ISpatialCell | null {
        if (!this._bounds.containsPoint(position)) {
            return null;
        }

        const coords = this.getCellCoordinates(position);
        const key = this.getCellKey(coords.x, coords.y, coords.z);
        return this._cells.get(key) || null;
    }

    getNeighborCells(cell: ISpatialCell): ISpatialCell[] {
        if (cell.neighborCells.length > 0) {
            return cell.neighborCells;
        }

        const neighbors: ISpatialCell[] = [];
        const cellCenter = Vec3.from(cell.bounds.center);
        const coords = this.getCellCoordinates(cellCenter);

        for (let dx = -1; dx <= 1; dx++) {
            for (let dy = -1; dy <= 1; dy++) {
                for (let dz = -1; dz <= 1; dz++) {
                    if (dx === 0 && dy === 0 && dz === 0) continue;

                    const neighborKey = this.getCellKey(
                        coords.x + dx,
                        coords.y + dy,
                        coords.z + dz
                    );

                    const neighborCell = this._cells.get(neighborKey);
                    if (neighborCell) {
                        neighbors.push(neighborCell);
                    }
                }
            }
        }

        cell.neighborCells = neighbors;
        return neighbors;
    }

    clear(): void {
        for (const cell of this._cells.values()) {
            this._cellPool.release(cell as SpatialCell);
        }
        this._cells.clear();
        this._particleToCell.clear();
    }

    getCellCount(): number {
        return this._cells.size;
    }

    getParticleCount(): number {
        let count = 0;
        for (const cell of this._cells.values()) {
            count += cell.particles.length;
        }
        return count;
    }
}
