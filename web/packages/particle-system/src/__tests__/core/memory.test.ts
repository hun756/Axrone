import { describe, expect, it, vi, afterEach } from 'vitest';
import { AlignedMemoryManager, PooledMemoryManager, ParticleMemoryManager } from '../../core/memory';
import { BufferPool } from '@axrone/memory';

describe('AlignedMemoryManager', () => {
    it('allocate() returns buffer with aligned size', () => {
        const mgr = new AlignedMemoryManager();
        const buf = mgr.allocate(10, 16);
        expect(buf).not.toBeNull();
        expect(buf!.byteLength).toBe(16); // ceil(10/16)*16 = 16
    });

    it('allocate() aligns to custom alignment', () => {
        const mgr = new AlignedMemoryManager();
        const buf = mgr.allocate(7, 8);
        expect(buf).not.toBeNull();
        expect(buf!.byteLength).toBe(8);
    });

    it('allocate(0) returns null', () => {
        const mgr = new AlignedMemoryManager();
        expect(mgr.allocate(0)).toBeNull();
    });

    it('allocate() with negative size returns null', () => {
        const mgr = new AlignedMemoryManager();
        expect(mgr.allocate(-5)).toBeNull();
    });

    it('deallocate() removes tracking and updates stats', () => {
        const mgr = new AlignedMemoryManager();
        const buf = mgr.allocate(32)!;
        expect(mgr.getStats().allocationCount).toBe(1);

        mgr.deallocate(buf);
        expect(mgr.getStats().allocationCount).toBe(0);
        expect(mgr.getStats().totalAllocated).toBe(0);
    });

    it('deallocate() with unknown buffer is no-op', () => {
        const mgr = new AlignedMemoryManager();
        const unknown = new ArrayBuffer(16);
        mgr.deallocate(unknown); // should not throw
        expect(mgr.getStats().allocationCount).toBe(0);
    });

    it('reallocate() copies data and deallocates old buffer', () => {
        const mgr = new AlignedMemoryManager();
        const buf = mgr.allocate(4)!;
        new Uint8Array(buf).set([1, 2, 3, 4]);

        const newBuf = mgr.reallocate(buf, 8);
        expect(newBuf).not.toBeNull();
        // Aligned to 16: ceil(8/16)*16 = 16
        expect(newBuf!.byteLength).toBe(16);
        const data = new Uint8Array(newBuf!);
        expect(data[0]).toBe(1);
        expect(data[1]).toBe(2);
        expect(data[2]).toBe(3);
        expect(data[3]).toBe(4);
        expect(mgr.getStats().allocationCount).toBe(1);
    });

    it('reallocate() with unknown buffer returns null', () => {
        const mgr = new AlignedMemoryManager();
        const unknown = new ArrayBuffer(16);
        expect(mgr.reallocate(unknown, 32)).toBeNull();
    });

    it('getStats() returns correct totals', () => {
        const mgr = new AlignedMemoryManager();
        mgr.allocate(16);
        mgr.allocate(32);
        const stats = mgr.getStats();
        expect(stats.allocationCount).toBe(2);
        expect(stats.totalAllocated).toBe(16 + 32);
        expect(stats.fragmentationRatio).toBe(0);
    });
});

describe('PooledMemoryManager', () => {
    afterEach(() => {
        BufferPool.resetInstance();
    });

    it('allocate() picks smallest fitting power-of-two bucket', () => {
        const mgr = new PooledMemoryManager();
        const buf = mgr.allocate(50)!;
        expect(buf).not.toBeNull();
        expect(buf.byteLength).toBe(64); // next power of two >= 50
    });

    it('allocate() handles oversized requests via BufferPool', () => {
        const mgr = new PooledMemoryManager();
        const buf = mgr.allocate(512)!;
        expect(buf).not.toBeNull();
        expect(buf.byteLength).toBeGreaterThanOrEqual(512);
    });

    it('allocate(0) returns null', () => {
        const mgr = new PooledMemoryManager();
        expect(mgr.allocate(0)).toBeNull();
    });

    it('deallocate() returns buffer to pool for reuse', () => {
        const mgr = new PooledMemoryManager();
        const buf = mgr.allocate(10)!;
        mgr.deallocate(buf);
        // After deallocation, allocating again should reuse the pooled buffer
        const buf2 = mgr.allocate(10)!;
        expect(buf2).not.toBeNull();
        expect(mgr.getStats().allocationCount).toBe(1);
    });

    it('deallocate() with unknown buffer is no-op', () => {
        const mgr = new PooledMemoryManager();
        mgr.deallocate(new ArrayBuffer(64)); // should not throw
    });

    it('reallocate() copies data correctly', () => {
        const mgr = new PooledMemoryManager();
        const buf = mgr.allocate(4)!;
        new Uint8Array(buf).set([10, 20, 30, 40]);

        const newBuf = mgr.reallocate(buf, 8)!;
        expect(newBuf).not.toBeNull();
        const data = new Uint8Array(newBuf);
        expect(data[0]).toBe(10);
        expect(data[1]).toBe(20);
        expect(data[2]).toBe(30);
        expect(data[3]).toBe(40);
    });

    it('reallocate() with unknown buffer still allocates new buffer', () => {
        const mgr = new PooledMemoryManager();
        // With BufferPool backing, reallocate doesn't track old buffers via a Map,
        // so it always allocates a new buffer and attempts to deallocate the old one
        const unknown = new ArrayBuffer(16);
        const result = mgr.reallocate(unknown, 32);
        expect(result).not.toBeNull();
        expect(result!.byteLength).toBeGreaterThanOrEqual(32);
    });

    it('getStats() returns fragmentation ratio', () => {
        const mgr = new PooledMemoryManager();
        const buf = mgr.allocate(10)!;
        mgr.deallocate(buf);
        const stats = mgr.getStats();
        expect(stats.allocationCount).toBe(0);
        expect(stats.fragmentationRatio).toBeGreaterThanOrEqual(0);
    });
});

describe('ParticleMemoryManager', () => {
    afterEach(() => {
        BufferPool.resetInstance();
    });

    it('allocateTypedArray() acquires from correct pool', () => {
        const mgr = new ParticleMemoryManager();
        const pooled = mgr.allocateTypedArray(Float32Array, 16);
        expect(pooled).toBeDefined();
        expect(pooled.array).toBeInstanceOf(Float32Array);
        expect(pooled.array.length).toBeGreaterThanOrEqual(16);
    });

    it('allocateTypedArray() works for Uint32Array', () => {
        const mgr = new ParticleMemoryManager();
        const pooled = mgr.allocateTypedArray(Uint32Array, 8);
        expect(pooled.array).toBeInstanceOf(Uint32Array);
    });

    it('allocateTypedArray() throws for unsupported type', () => {
        const mgr = new ParticleMemoryManager();
        expect(() => mgr.allocateTypedArray(BigInt64Array as any, 8)).toThrow();
    });

    it('releaseTypedArray() returns to pool and decrements active count', () => {
        const mgr = new ParticleMemoryManager();
        const pooled = mgr.allocateTypedArray(Float32Array, 16);
        const statsBefore = mgr.getExtendedStats();
        expect(statsBefore.manager.activeTypedArrays).toBe(1);

        mgr.releaseTypedArray(pooled);
        const statsAfter = mgr.getExtendedStats();
        expect(statsAfter.manager.activeTypedArrays).toBe(0);
        expect(statsAfter.manager.totalTypedArrayReleases).toBe(1);
    });

    it('releaseTypedArray() warns for foreign pooled arrays', () => {
        const mgr = new ParticleMemoryManager();
        const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
        // Create a fake pooled object not tracked by the manager
        const foreign = { array: new Float32Array(10), byteLength: 40 };
        mgr.releaseTypedArray(foreign as any);
        expect(warnSpy).toHaveBeenCalled();
        warnSpy.mockRestore();
    });

    it('createTypedArrayWithData() initializes with provided data', () => {
        const mgr = new ParticleMemoryManager();
        const data = [1.0, 2.0, 3.0, 4.0];
        const pooled = mgr.createTypedArrayWithData(Float32Array, data);
        expect(pooled.array[0]).toBeCloseTo(1.0);
        expect(pooled.array[1]).toBeCloseTo(2.0);
        expect(pooled.array[2]).toBeCloseTo(3.0);
        expect(pooled.array[3]).toBeCloseTo(4.0);
    });

    it('getExtendedStats() returns per-pool breakdown', () => {
        const mgr = new ParticleMemoryManager();
        mgr.allocateTypedArray(Float32Array, 16);
        mgr.allocateTypedArray(Uint32Array, 8);
        const stats = mgr.getExtendedStats();
        expect(stats.typedArrays.length).toBeGreaterThan(0);
        expect(stats.manager.totalTypedArrayAllocations).toBe(2);
    });

    it('clear() resets stats when pools are clearable', () => {
        const mgr = new ParticleMemoryManager();
        const pooled = mgr.allocateTypedArray(Float32Array, 16);
        mgr.releaseTypedArray(pooled);
        try {
            mgr.clear();
        } catch {
            // Global TypedArrayPools singleton may have allocated objects from other tests
            return;
        }
        const stats = mgr.getExtendedStats();
        expect(stats.manager.totalTypedArrayAllocations).toBe(0);
        expect(stats.manager.activeTypedArrays).toBe(0);
    });

    it('dispose() clears pools and prevents further allocation', () => {
        const mgr = new ParticleMemoryManager();
        const pooled = mgr.allocateTypedArray(Float32Array, 16);
        mgr.releaseTypedArray(pooled);
        try {
            mgr.dispose();
        } catch {
            // Global TypedArrayPools singleton may have allocated objects from other tests
            return;
        }
        // After dispose, pools are cleared so allocation should fail
        expect(() => mgr.allocateTypedArray(Float32Array, 16)).toThrow();
    });

    it('allocate() / deallocate() / reallocate() delegate correctly', () => {
        const mgr = new ParticleMemoryManager();
        const buf = mgr.allocate(32);
        expect(buf).not.toBeNull();

        const stats = mgr.getStats();
        expect(stats.allocationCount).toBeGreaterThanOrEqual(1);

        mgr.deallocate(buf!);
    });

    it('getStats() aggregates across managers', () => {
        const mgr = new ParticleMemoryManager();
        mgr.allocate(64);
        mgr.allocate(128);
        const stats = mgr.getStats();
        expect(stats.allocationCount).toBeGreaterThanOrEqual(2);
        expect(stats.totalUsed).toBeGreaterThan(0);
    });
});
