import { Vec3, IVec3Like } from '@axrone/numeric';
import { ParticleId } from './types';
import { IParticleSOA } from './interfaces';

export class ParticleSOA implements IParticleSOA {
    private _capacity: number;
    private _count: number;
    private _positions: Float32Array;
    private _velocities: Float32Array;
    private _accelerations: Float32Array;
    private _lifetimes: Float32Array;
    private _ages: Float32Array;
    private _sizes: Float32Array;
    private _colors: Float32Array;
    private _rotations: Float32Array;
    private _angularVelocities: Float32Array;
    private _customData1: Float32Array;
    private _customData2: Float32Array;
    private _ids: Uint32Array;
    private _freeList: number[];
    private _nextId: number = 0;

    constructor(capacity: number = 1000) {
        this._capacity = capacity;
        this._count = 0;
        this._freeList = [];

        // Allocate SOA arrays
        this._positions = new Float32Array(capacity * 3);
        this._velocities = new Float32Array(capacity * 3);
        this._accelerations = new Float32Array(capacity * 3);
        this._lifetimes = new Float32Array(capacity);
        this._ages = new Float32Array(capacity);
        this._sizes = new Float32Array(capacity * 3);
        this._colors = new Float32Array(capacity * 4);
        this._rotations = new Float32Array(capacity * 3);
        this._angularVelocities = new Float32Array(capacity * 3);
        this._customData1 = new Float32Array(capacity * 4);
        this._customData2 = new Float32Array(capacity * 4);
        this._ids = new Uint32Array(capacity);

        for (let i = capacity - 1; i >= 0; i--) {
            this._freeList.push(i);
        }
    }

    get capacity(): number {
        return this._capacity;
    }
    get count(): number {
        return this._count;
    }
    get positions(): Float32Array {
        return this._positions;
    }
    get velocities(): Float32Array {
        return this._velocities;
    }
    get accelerations(): Float32Array {
        return this._accelerations;
    }
    get lifetimes(): Float32Array {
        return this._lifetimes;
    }
    get ages(): Float32Array {
        return this._ages;
    }
    get sizes(): Float32Array {
        return this._sizes;
    }
    get colors(): Float32Array {
        return this._colors;
    }
    get rotations(): Float32Array {
        return this._rotations;
    }
    get angularVelocities(): Float32Array {
        return this._angularVelocities;
    }
    get customData1(): Float32Array {
        return this._customData1;
    }
    get customData2(): Float32Array {
        return this._customData2;
    }
    get ids(): Uint32Array {
        return this._ids;
    }

    addParticle(
        position: IVec3Like,
        velocity: IVec3Like,
        lifetime: number,
        size: number,
        color: number
    ): ParticleId | null {
        if (this._freeList.length === 0) {
            return null; // no free slots
        }

        const index = this._freeList.pop()!;
        const particleId = this._nextId++ as ParticleId;

        this._ids[index] = particleId;

        const posOffset = index * 3;
        this._positions[posOffset] = position.x;
        this._positions[posOffset + 1] = position.y;
        this._positions[posOffset + 2] = position.z;

        const velOffset = index * 3;
        this._velocities[velOffset] = velocity.x;
        this._velocities[velOffset + 1] = velocity.y;
        this._velocities[velOffset + 2] = velocity.z;

        this._accelerations[velOffset] = 0;
        this._accelerations[velOffset + 1] = 0;
        this._accelerations[velOffset + 2] = 0;

        this._lifetimes[index] = lifetime;
        this._ages[index] = 0;

        const sizeOffset = index * 3;
        this._sizes[sizeOffset] = size;
        this._sizes[sizeOffset + 1] = size;
        this._sizes[sizeOffset + 2] = size;

        const colorOffset = index * 4;
        const r = (color >>> 24) & 0xff;
        const g = (color >>> 16) & 0xff;
        const b = (color >>> 8) & 0xff;
        const a = color & 0xff;
        this._colors[colorOffset] = r / 255.0;
        this._colors[colorOffset + 1] = g / 255.0;
        this._colors[colorOffset + 2] = b / 255.0;
        this._colors[colorOffset + 3] = a / 255.0;

        const rotOffset = index * 3;
        this._rotations[rotOffset] = 0;
        this._rotations[rotOffset + 1] = 0;
        this._rotations[rotOffset + 2] = 0;
        this._angularVelocities[rotOffset] = 0;
        this._angularVelocities[rotOffset + 1] = 0;
        this._angularVelocities[rotOffset + 2] = 0;

        const customOffset = index * 4;
        for (let i = 0; i < 4; i++) {
            this._customData1[customOffset + i] = 0;
            this._customData2[customOffset + i] = 0;
        }

        this._count++;
        return particleId;
    }

    removeParticle(index: number): void {
        if (index < 0 || index >= this._capacity) {
            return;
        }

        this._freeList.push(index);
        this._count--;

        this._ids[index] = 0;
    }

    getParticlePosition(index: number): Vec3 {
        const offset = index * 3;
        return new Vec3(
            this._positions[offset],
            this._positions[offset + 1],
            this._positions[offset + 2]
        );
    }

    setParticlePosition(index: number, position: IVec3Like): void {
        const offset = index * 3;
        this._positions[offset] = position.x;
        this._positions[offset + 1] = position.y;
        this._positions[offset + 2] = position.z;
    }

    getParticleVelocity(index: number): Vec3 {
        const offset = index * 3;
        return new Vec3(
            this._velocities[offset],
            this._velocities[offset + 1],
            this._velocities[offset + 2]
        );
    }

    setParticleVelocity(index: number, velocity: IVec3Like): void {
        const offset = index * 3;
        this._velocities[offset] = velocity.x;
        this._velocities[offset + 1] = velocity.y;
        this._velocities[offset + 2] = velocity.z;
    }

    clear(): void {
        this._count = 0;
        this._nextId = 0;
        this._freeList.length = 0;

        for (let i = this._capacity - 1; i >= 0; i--) {
            this._freeList.push(i);
        }
    }

    resize(newCapacity: number): void {
        if (newCapacity === this._capacity) {
            return;
        }

        const oldCapacity = this._capacity;
        this._capacity = newCapacity;

        const newPositions = new Float32Array(newCapacity * 3);
        const newVelocities = new Float32Array(newCapacity * 3);
        const newAccelerations = new Float32Array(newCapacity * 3);
        const newLifetimes = new Float32Array(newCapacity);
        const newAges = new Float32Array(newCapacity);
        const newSizes = new Float32Array(newCapacity * 3);
        const newColors = new Float32Array(newCapacity * 4);
        const newRotations = new Float32Array(newCapacity * 3);
        const newAngularVelocities = new Float32Array(newCapacity * 3);
        const newCustomData1 = new Float32Array(newCapacity * 4);
        const newCustomData2 = new Float32Array(newCapacity * 4);
        const newIds = new Uint32Array(newCapacity);

        const copyCount = Math.min(oldCapacity, newCapacity);
        newPositions.set(this._positions.subarray(0, copyCount * 3));
        newVelocities.set(this._velocities.subarray(0, copyCount * 3));
        newAccelerations.set(this._accelerations.subarray(0, copyCount * 3));
        newLifetimes.set(this._lifetimes.subarray(0, copyCount));
        newAges.set(this._ages.subarray(0, copyCount));
        newSizes.set(this._sizes.subarray(0, copyCount * 3));
        newColors.set(this._colors.subarray(0, copyCount * 4));
        newRotations.set(this._rotations.subarray(0, copyCount * 3));
        newAngularVelocities.set(this._angularVelocities.subarray(0, copyCount * 3));
        newCustomData1.set(this._customData1.subarray(0, copyCount * 4));
        newCustomData2.set(this._customData2.subarray(0, copyCount * 4));
        newIds.set(this._ids.subarray(0, copyCount));

        this._positions = newPositions;
        this._velocities = newVelocities;
        this._accelerations = newAccelerations;
        this._lifetimes = newLifetimes;
        this._ages = newAges;
        this._sizes = newSizes;
        this._colors = newColors;
        this._rotations = newRotations;
        this._angularVelocities = newAngularVelocities;
        this._customData1 = newCustomData1;
        this._customData2 = newCustomData2;
        this._ids = newIds;

        this._freeList = this._freeList.filter((index) => index < newCapacity);
        if (newCapacity > oldCapacity) {
            for (let i = newCapacity - 1; i >= oldCapacity; i--) {
                this._freeList.push(i);
            }
        }
    }

    getActiveIndices(): number[] {
        const activeIndices: number[] = [];
        const usedIndices = new Set(this._freeList);

        for (let i = 0; i < this._capacity; i++) {
            if (!usedIndices.has(i)) {
                activeIndices.push(i);
            }
        }

        return activeIndices;
    }
}
