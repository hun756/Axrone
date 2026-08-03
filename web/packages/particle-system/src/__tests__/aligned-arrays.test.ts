import { describe, it, expect } from 'vitest';
import {
    AlignedArrayError,
    MemoryError,
    IndexError,
    ValidationError,
    Vec2Array,
    Vec3Array,
    Vec4Array,
    AlignedArrayFactory,
    VectorOperations,
    MemoryPool,
    asVectorIndex,
    isVectorIndex,
    validateCapacity,
    validateAlignment,
    calculateAlignedSize,
    createAlignedBuffer,
    createVec2,
    createVec3,
    createVec4,
    vectorOps,
    memoryPool,
} from '../aligned-arrays';
import type { Alignment, VectorIndex } from '../aligned-arrays';

// ─── Error Classes ──────────────────────────────────────────────────────────

describe('AlignedArrayError', () => {
    it('stores message, code, and context', () => {
        const err = new AlignedArrayError('test error', 'TEST_CODE', { key: 42 });
        expect(err.message).toBe('test error');
        expect(err.code).toBe('TEST_CODE');
        expect(err.context).toEqual({ key: 42 });
        expect(err.name).toBe('AlignedArrayError');
        expect(err).toBeInstanceOf(Error);
    });

    it('works without context', () => {
        const err = new AlignedArrayError('msg', 'CODE');
        expect(err.context).toBeUndefined();
    });
});

describe('MemoryError', () => {
    it('has code MEMORY_ERROR', () => {
        const err = new MemoryError('oom');
        expect(err.code).toBe('MEMORY_ERROR');
        expect(err.name).toBe('MemoryError');
        expect(err).toBeInstanceOf(AlignedArrayError);
    });
});

describe('IndexError', () => {
    it('has code INDEX_ERROR', () => {
        const err = new IndexError('oob');
        expect(err.code).toBe('INDEX_ERROR');
        expect(err.name).toBe('IndexError');
        expect(err).toBeInstanceOf(AlignedArrayError);
    });
});

describe('ValidationError', () => {
    it('has code VALIDATION_ERROR', () => {
        const err = new ValidationError('bad');
        expect(err.code).toBe('VALIDATION_ERROR');
        expect(err.name).toBe('ValidationError');
        expect(err).toBeInstanceOf(AlignedArrayError);
    });
});

// ─── Helper Functions ───────────────────────────────────────────────────────

describe('asVectorIndex', () => {
    it('returns valid index for non-negative integers', () => {
        expect(asVectorIndex(0)).toBe(0);
        expect(asVectorIndex(5)).toBe(5);
    });

    it('throws IndexError for negative numbers', () => {
        expect(() => asVectorIndex(-1)).toThrow(IndexError);
    });

    it('throws IndexError for non-integers', () => {
        expect(() => asVectorIndex(1.5)).toThrow(IndexError);
    });
});

describe('isVectorIndex', () => {
    it('returns true for valid indices', () => {
        expect(isVectorIndex(0)).toBe(true);
        expect(isVectorIndex(100)).toBe(true);
    });

    it('returns false for invalid values', () => {
        expect(isVectorIndex(-1)).toBe(false);
        expect(isVectorIndex(1.5)).toBe(false);
    });
});

describe('validateCapacity', () => {
    it('accepts positive integers', () => {
        expect(() => validateCapacity(1)).not.toThrow();
        expect(() => validateCapacity(1000)).not.toThrow();
    });

    it('throws ValidationError for zero/negative', () => {
        expect(() => validateCapacity(0)).toThrow(ValidationError);
        expect(() => validateCapacity(-5)).toThrow(ValidationError);
    });

    it('throws ValidationError for non-integers', () => {
        expect(() => validateCapacity(1.5)).toThrow(ValidationError);
    });

    it('throws MemoryError for excessively large capacity', () => {
        expect(() => validateCapacity(Math.floor(0x7fffffff / 16) + 1)).toThrow(MemoryError);
    });
});

describe('validateAlignment', () => {
    it('accepts 16, 32, 64', () => {
        expect(validateAlignment(16)).toBe(true);
        expect(validateAlignment(32)).toBe(true);
        expect(validateAlignment(64)).toBe(true);
    });

    it('rejects other values', () => {
        expect(validateAlignment(8)).toBe(false);
        expect(validateAlignment(128)).toBe(false);
    });
});

describe('calculateAlignedSize', () => {
    it('rounds up to alignment boundary', () => {
        expect(calculateAlignedSize(10, 16)).toBe(16);
        expect(calculateAlignedSize(16, 16)).toBe(16);
        expect(calculateAlignedSize(17, 16)).toBe(32);
        expect(calculateAlignedSize(33, 32)).toBe(64);
    });
});

describe('createAlignedBuffer', () => {
    it('returns an ArrayBuffer of aligned size', () => {
        const buf = createAlignedBuffer(10, 16);
        expect(buf).toBeInstanceOf(ArrayBuffer);
        expect(buf.byteLength).toBe(16);
    });
});

// ─── Vec2Array ──────────────────────────────────────────────────────────────

describe('Vec2Array', () => {
    it('constructs with capacity and default alignment', () => {
        const arr = new Vec2Array(10);
        expect(arr.capacity).toBe(10);
        expect(arr.componentCount).toBe(2);
        expect(arr.alignment).toBe(16);
        expect(arr.isDisposed).toBe(false);
    });

    it('set/get components', () => {
        const arr = new Vec2Array(5);
        arr.set(0 as VectorIndex, 1.0, 2.0);
        expect(arr.get(0 as VectorIndex)).toEqual([1.0, 2.0]);
    });

    it('x/y component views work', () => {
        const arr = new Vec2Array(3);
        arr.set(0 as VectorIndex, 10, 20);
        arr.set(1 as VectorIndex, 30, 40);
        expect(arr.x[0]).toBe(10);
        expect(arr.y[0]).toBe(20);
        expect(arr.x[1]).toBe(30);
        expect(arr.y[1]).toBe(40);
    });

    it('z/w are undefined for vec2', () => {
        const arr = new Vec2Array(3);
        expect(arr.z).toBeUndefined();
        expect(arr.w).toBeUndefined();
    });

    it('setXY is alias for set', () => {
        const arr = new Vec2Array(3);
        arr.setXY(0 as VectorIndex, 5, 6);
        expect(arr.getX(0 as VectorIndex)).toBe(5);
        expect(arr.getY(0 as VectorIndex)).toBe(6);
    });

    it('setX/setY individual components', () => {
        const arr = new Vec2Array(3);
        arr.set(0 as VectorIndex, 1, 2);
        arr.setX(0 as VectorIndex, 99);
        expect(arr.getX(0 as VectorIndex)).toBe(99);
        expect(arr.getY(0 as VectorIndex)).toBe(2);
    });

    it('setVector/getVector', () => {
        const arr = new Vec2Array(3);
        arr.setVector(0 as VectorIndex, [7, 8]);
        expect(arr.getVector(0 as VectorIndex)).toEqual([7, 8]);
    });

    it('setVector throws on insufficient length', () => {
        const arr = new Vec2Array(3);
        expect(() => arr.setVector(0 as VectorIndex, [1])).toThrow(ValidationError);
    });

    it('getVector uses output array', () => {
        const arr = new Vec2Array(3);
        arr.set(0 as VectorIndex, 3, 4);
        const out = [0, 0];
        const result = arr.getVector(0 as VectorIndex, out);
        expect(result).toBe(out);
        expect(out).toEqual([3, 4]);
    });

    it('copy between indices', () => {
        const arr = new Vec2Array(5);
        arr.set(0 as VectorIndex, 11, 22);
        arr.copy(0 as VectorIndex, 3 as VectorIndex);
        expect(arr.get(3 as VectorIndex)).toEqual([11, 22]);
    });

    it('fill sets all components', () => {
        const arr = new Vec2Array(3);
        arr.fill(5);
        expect(arr.get(0 as VectorIndex)).toEqual([5, 5]);
        expect(arr.get(1 as VectorIndex)).toEqual([5, 5]);
        expect(arr.get(2 as VectorIndex)).toEqual([5, 5]);
    });

    it('fill with range', () => {
        const arr = new Vec2Array(4);
        arr.fill(9, 1 as VectorIndex, 3 as VectorIndex);
        expect(arr.get(0 as VectorIndex)).toEqual([0, 0]);
        expect(arr.get(1 as VectorIndex)).toEqual([9, 9]);
        expect(arr.get(2 as VectorIndex)).toEqual([9, 9]);
        expect(arr.get(3 as VectorIndex)).toEqual([0, 0]);
    });

    it('clear zeroes everything', () => {
        const arr = new Vec2Array(3);
        arr.fill(7);
        arr.clear();
        expect(arr.get(0 as VectorIndex)).toEqual([0, 0]);
        expect(arr.get(2 as VectorIndex)).toEqual([0, 0]);
    });

    it('resize preserves existing data', () => {
        const arr = new Vec2Array(3);
        arr.set(0 as VectorIndex, 1, 2);
        arr.set(1 as VectorIndex, 3, 4);
        arr.resize(5);
        expect(arr.capacity).toBe(5);
        expect(arr.get(0 as VectorIndex)).toEqual([1, 2]);
        expect(arr.get(1 as VectorIndex)).toEqual([3, 4]);
        expect(arr.get(4 as VectorIndex)).toEqual([0, 0]);
    });

    it('resize to same capacity is no-op', () => {
        const arr = new Vec2Array(5);
        arr.set(0 as VectorIndex, 1, 2);
        const result = arr.resize(5);
        expect(result).toBe(arr);
        expect(arr.get(0 as VectorIndex)).toEqual([1, 2]);
    });

    it('slice creates a new array', () => {
        const arr = new Vec2Array(5);
        arr.set(1 as VectorIndex, 10, 20);
        arr.set(2 as VectorIndex, 30, 40);
        const sliced = arr.slice(1 as VectorIndex, 3 as VectorIndex);
        expect(sliced.capacity).toBe(2);
        expect(sliced.get(0 as VectorIndex)).toEqual([10, 20]);
        expect(sliced.get(1 as VectorIndex)).toEqual([30, 40]);
    });

    it('slice throws on invalid range', () => {
        const arr = new Vec2Array(5);
        expect(() => arr.slice(3 as VectorIndex, 2 as VectorIndex)).toThrow(ValidationError);
    });

    it('forEach iterates all elements', () => {
        const arr = new Vec2Array(3);
        arr.set(0 as VectorIndex, 1, 2);
        arr.set(1 as VectorIndex, 3, 4);
        arr.set(2 as VectorIndex, 5, 6);
        const collected: number[][] = [];
        arr.forEach((x, y, _z, _w, idx) => {
            collected.push([x, y, idx]);
        });
        expect(collected).toEqual([[1, 2, 0], [3, 4, 1], [5, 6, 2]]);
    });

    it('map transforms elements', () => {
        const arr = new Vec2Array(2);
        arr.set(0 as VectorIndex, 1, 2);
        arr.set(1 as VectorIndex, 3, 4);
        const result = arr.map((x, y) => x + y);
        expect(result).toEqual([3, 7]);
    });

    it('validate returns true for healthy array', () => {
        const arr = new Vec2Array(5);
        expect(arr.validate()).toBe(true);
    });

    it('serialize/deserialize round-trip', () => {
        const arr = new Vec2Array(3);
        arr.set(0 as VectorIndex, 1, 2);
        arr.set(1 as VectorIndex, 3, 4);
        arr.set(2 as VectorIndex, 5, 6);
        const data = arr.serialize();
        const arr2 = new Vec2Array(3);
        arr2.deserialize(data);
        expect(arr2.get(0 as VectorIndex)).toEqual([1, 2]);
        expect(arr2.get(1 as VectorIndex)).toEqual([3, 4]);
        expect(arr2.get(2 as VectorIndex)).toEqual([5, 6]);
    });

    it('deserialize throws on size mismatch', () => {
        const arr = new Vec2Array(3);
        expect(() => arr.deserialize(new ArrayBuffer(999))).toThrow(ValidationError);
    });

    it('clone creates independent copy', () => {
        const arr = new Vec2Array(3);
        arr.set(0 as VectorIndex, 10, 20);
        const cloned = arr.clone();
        expect(cloned.get(0 as VectorIndex)).toEqual([10, 20]);
        cloned.set(0 as VectorIndex, 99, 99);
        expect(arr.get(0 as VectorIndex)).toEqual([10, 20]);
    });

    it('dispose marks as disposed', () => {
        const arr = new Vec2Array(3);
        expect(arr.isDisposed).toBe(false);
        arr.dispose();
        expect(arr.isDisposed).toBe(true);
    });

    it('dispose is idempotent', () => {
        const arr = new Vec2Array(3);
        arr.dispose();
        arr.dispose();
        expect(arr.isDisposed).toBe(true);
    });

    it('operations on disposed array throw', () => {
        const arr = new Vec2Array(3);
        arr.dispose();
        expect(() => arr.get(0 as VectorIndex)).toThrow(AlignedArrayError);
        expect(() => arr.set(0 as VectorIndex, 1, 2)).toThrow(AlignedArrayError);
        expect(() => arr.fill(0)).toThrow(AlignedArrayError);
    });

    it('out-of-bounds index throws IndexError', () => {
        const arr = new Vec2Array(3);
        expect(() => arr.get(5 as VectorIndex)).toThrow(IndexError);
        expect(() => arr.set(10 as VectorIndex, 0, 0)).toThrow(IndexError);
    });

    it('negative index throws IndexError', () => {
        const arr = new Vec2Array(3);
        expect(() => arr.get(-1 as VectorIndex)).toThrow(IndexError);
    });

    it('constructor rejects invalid capacity', () => {
        expect(() => new Vec2Array(0)).toThrow(ValidationError);
        expect(() => new Vec2Array(-1)).toThrow(ValidationError);
    });

    it('constructor rejects invalid alignment', () => {
        expect(() => new Vec2Array(10, Float32Array as any, 8 as any)).toThrow(ValidationError);
    });

    it('supports alignment 32', () => {
        const arr = new Vec2Array(10, Float32Array as any, 32);
        expect(arr.alignment).toBe(32);
        arr.set(0 as VectorIndex, 1, 2);
        expect(arr.get(0 as VectorIndex)).toEqual([1, 2]);
    });

    it('buffer property returns the underlying ArrayBuffer', () => {
        const arr = new Vec2Array(5);
        expect(arr.buffer).toBeInstanceOf(ArrayBuffer);
        expect(arr.buffer.byteLength).toBeGreaterThan(0);
    });

    it('components property returns component views', () => {
        const arr = new Vec2Array(5);
        const comps = arr.components;
        expect(comps.x).toBeDefined();
        expect(comps.y).toBeDefined();
        expect(comps.x.length).toBe(5);
    });
});

// ─── Vec3Array ──────────────────────────────────────────────────────────────

describe('Vec3Array', () => {
    it('constructs with 3 components', () => {
        const arr = new Vec3Array(10);
        expect(arr.componentCount).toBe(3);
        expect(arr.z).toBeDefined();
        expect(arr.w).toBeUndefined();
    });

    it('set/get with 3 components', () => {
        const arr = new Vec3Array(5);
        arr.set(0 as VectorIndex, 1, 2, 3);
        expect(arr.get(0 as VectorIndex)).toEqual([1, 2, 3]);
    });

    it('setXYZ alias works', () => {
        const arr = new Vec3Array(3);
        arr.setXYZ(0 as VectorIndex, 4, 5, 6);
        expect(arr.getX(0 as VectorIndex)).toBe(4);
        expect(arr.getY(0 as VectorIndex)).toBe(5);
        expect(arr.getZ(0 as VectorIndex)).toBe(6);
    });

    it('setZ works', () => {
        const arr = new Vec3Array(3);
        arr.set(0 as VectorIndex, 1, 2, 3);
        arr.setZ(0 as VectorIndex, 99);
        expect(arr.getZ(0 as VectorIndex)).toBe(99);
    });

    it('setVector with 3 components', () => {
        const arr = new Vec3Array(3);
        arr.setVector(0 as VectorIndex, [10, 20, 30]);
        expect(arr.getVector(0 as VectorIndex)).toEqual([10, 20, 30]);
    });

    it('setVector throws if too few components', () => {
        const arr = new Vec3Array(3);
        expect(() => arr.setVector(0 as VectorIndex, [1, 2])).toThrow(ValidationError);
    });

    it('copy preserves all 3 components', () => {
        const arr = new Vec3Array(5);
        arr.set(0 as VectorIndex, 1, 2, 3);
        arr.copy(0 as VectorIndex, 4 as VectorIndex);
        expect(arr.get(4 as VectorIndex)).toEqual([1, 2, 3]);
    });

    it('fill sets all 3 components', () => {
        const arr = new Vec3Array(3);
        arr.fill(7);
        expect(arr.get(0 as VectorIndex)).toEqual([7, 7, 7]);
    });

    it('clone creates independent copy', () => {
        const arr = new Vec3Array(3);
        arr.set(0 as VectorIndex, 1, 2, 3);
        const cloned = arr.clone();
        expect(cloned.get(0 as VectorIndex)).toEqual([1, 2, 3]);
        cloned.set(0 as VectorIndex, 99, 99, 99);
        expect(arr.get(0 as VectorIndex)).toEqual([1, 2, 3]);
    });

    it('forEach provides z component', () => {
        const arr = new Vec3Array(2);
        arr.set(0 as VectorIndex, 1, 2, 3);
        arr.set(1 as VectorIndex, 4, 5, 6);
        const collected: (number | undefined)[][] = [];
        arr.forEach((x, y, z, _w) => {
            collected.push([x, y, z]);
        });
        expect(collected).toEqual([[1, 2, 3], [4, 5, 6]]);
    });

    it('serialize/deserialize round-trip', () => {
        const arr = new Vec3Array(2);
        arr.set(0 as VectorIndex, 1, 2, 3);
        arr.set(1 as VectorIndex, 4, 5, 6);
        const data = arr.serialize();
        const arr2 = new Vec3Array(2);
        arr2.deserialize(data);
        expect(arr2.get(0 as VectorIndex)).toEqual([1, 2, 3]);
        expect(arr2.get(1 as VectorIndex)).toEqual([4, 5, 6]);
    });
});

// ─── Vec4Array ──────────────────────────────────────────────────────────────

describe('Vec4Array', () => {
    it('constructs with 4 components', () => {
        const arr = new Vec4Array(10);
        expect(arr.componentCount).toBe(4);
        expect(arr.z).toBeDefined();
        expect(arr.w).toBeDefined();
    });

    it('set/get with 4 components', () => {
        const arr = new Vec4Array(5);
        arr.set(0 as VectorIndex, 1, 2, 3, 4);
        expect(arr.get(0 as VectorIndex)).toEqual([1, 2, 3, 4]);
    });

    it('setXYZW alias works', () => {
        const arr = new Vec4Array(3);
        arr.setXYZW(0 as VectorIndex, 1, 2, 3, 4);
        expect(arr.getX(0 as VectorIndex)).toBe(1);
        expect(arr.getY(0 as VectorIndex)).toBe(2);
        expect(arr.getZ(0 as VectorIndex)).toBe(3);
        expect(arr.getW(0 as VectorIndex)).toBe(4);
    });

    it('setW works', () => {
        const arr = new Vec4Array(3);
        arr.set(0 as VectorIndex, 1, 2, 3, 4);
        arr.setW(0 as VectorIndex, 99);
        expect(arr.getW(0 as VectorIndex)).toBe(99);
    });

    it('setVector with 4 components', () => {
        const arr = new Vec4Array(3);
        arr.setVector(0 as VectorIndex, [10, 20, 30, 40]);
        expect(arr.getVector(0 as VectorIndex)).toEqual([10, 20, 30, 40]);
    });

    it('copy preserves all 4 components', () => {
        const arr = new Vec4Array(5);
        arr.set(0 as VectorIndex, 1, 2, 3, 4);
        arr.copy(0 as VectorIndex, 3 as VectorIndex);
        expect(arr.get(3 as VectorIndex)).toEqual([1, 2, 3, 4]);
    });

    it('fill sets all 4 components', () => {
        const arr = new Vec4Array(3);
        arr.fill(5);
        expect(arr.get(0 as VectorIndex)).toEqual([5, 5, 5, 5]);
    });

    it('clone creates independent copy', () => {
        const arr = new Vec4Array(3);
        arr.set(0 as VectorIndex, 1, 2, 3, 4);
        const cloned = arr.clone();
        expect(cloned.get(0 as VectorIndex)).toEqual([1, 2, 3, 4]);
        cloned.set(0 as VectorIndex, 99, 99, 99, 99);
        expect(arr.get(0 as VectorIndex)).toEqual([1, 2, 3, 4]);
    });

    it('forEach provides z and w components', () => {
        const arr = new Vec4Array(1);
        arr.set(0 as VectorIndex, 1, 2, 3, 4);
        const collected: (number | undefined)[][] = [];
        arr.forEach((x, y, z, w) => {
            collected.push([x, y, z, w]);
        });
        expect(collected).toEqual([[1, 2, 3, 4]]);
    });

    it('serialize/deserialize round-trip', () => {
        const arr = new Vec4Array(2);
        arr.set(0 as VectorIndex, 1, 2, 3, 4);
        arr.set(1 as VectorIndex, 5, 6, 7, 8);
        const data = arr.serialize();
        const arr2 = new Vec4Array(2);
        arr2.deserialize(data);
        expect(arr2.get(0 as VectorIndex)).toEqual([1, 2, 3, 4]);
        expect(arr2.get(1 as VectorIndex)).toEqual([5, 6, 7, 8]);
    });
});

// ─── AlignedArrayFactory ────────────────────────────────────────────────────

describe('AlignedArrayFactory', () => {
    const factory = AlignedArrayFactory.instance;

    it('singleton instance exists', () => {
        expect(factory).toBeDefined();
    });

    it('createVec2 returns Vec2Array', () => {
        const arr = factory.createVec2(10);
        expect(arr.capacity).toBe(10);
        expect(arr.componentCount).toBe(2);
    });

    it('createVec3 returns Vec3Array', () => {
        const arr = factory.createVec3(10);
        expect(arr.capacity).toBe(10);
        expect(arr.componentCount).toBe(3);
    });

    it('createVec4 returns Vec4Array', () => {
        const arr = factory.createVec4(10);
        expect(arr.capacity).toBe(10);
        expect(arr.componentCount).toBe(4);
    });

    it('respects custom alignment', () => {
        const arr = factory.createVec3(10, Float32Array as any, 64);
        expect(arr.alignment).toBe(64);
    });
});

// ─── Convenience creators ───────────────────────────────────────────────────

describe('createVec2/3/4 helpers', () => {
    it('createVec2 works', () => {
        const arr = createVec2(5);
        expect(arr.componentCount).toBe(2);
    });

    it('createVec3 works', () => {
        const arr = createVec3(5);
        expect(arr.componentCount).toBe(3);
    });

    it('createVec4 works', () => {
        const arr = createVec4(5);
        expect(arr.componentCount).toBe(4);
    });
});

// ─── VectorOperations ───────────────────────────────────────────────────────

describe('VectorOperations', () => {
    const ops = new VectorOperations();

    describe('add', () => {
        it('adds two Vec3Arrays component-wise', () => {
            const a = new Vec3Array(2);
            const b = new Vec3Array(2);
            a.set(0 as VectorIndex, 1, 2, 3);
            a.set(1 as VectorIndex, 4, 5, 6);
            b.set(0 as VectorIndex, 10, 20, 30);
            b.set(1 as VectorIndex, 40, 50, 60);
            const result = ops.add(a, b);
            expect(result.get(0 as VectorIndex)).toEqual([11, 22, 33]);
            expect(result.get(1 as VectorIndex)).toEqual([44, 55, 66]);
        });

        it('writes into provided result array', () => {
            const a = new Vec3Array(1);
            const b = new Vec3Array(1);
            const result = new Vec3Array(1);
            a.set(0 as VectorIndex, 1, 2, 3);
            b.set(0 as VectorIndex, 4, 5, 6);
            ops.add(a, b, result);
            expect(result.get(0 as VectorIndex)).toEqual([5, 7, 9]);
        });

        it('throws on mismatched structure', () => {
            const a = new Vec3Array(2);
            const b = new Vec3Array(3);
            expect(() => ops.add(a, b)).toThrow(ValidationError);
        });
    });

    describe('subtract', () => {
        it('subtracts component-wise', () => {
            const a = new Vec3Array(1);
            const b = new Vec3Array(1);
            a.set(0 as VectorIndex, 10, 20, 30);
            b.set(0 as VectorIndex, 1, 2, 3);
            const result = ops.subtract(a, b);
            expect(result.get(0 as VectorIndex)).toEqual([9, 18, 27]);
        });
    });

    describe('multiply', () => {
        it('multiplies component-wise', () => {
            const a = new Vec2Array(1);
            const b = new Vec2Array(1);
            a.set(0 as VectorIndex, 3, 4);
            b.set(0 as VectorIndex, 5, 6);
            const result = ops.multiply(a, b);
            expect(result.get(0 as VectorIndex)).toEqual([15, 24]);
        });
    });

    describe('multiplyScalar', () => {
        it('scales all components', () => {
            const a = new Vec3Array(1);
            a.set(0 as VectorIndex, 1, 2, 3);
            const result = ops.multiplyScalar(a, 3);
            expect(result.get(0 as VectorIndex)).toEqual([3, 6, 9]);
        });
    });

    describe('divide', () => {
        it('divides component-wise', () => {
            const a = new Vec2Array(1);
            const b = new Vec2Array(1);
            a.set(0 as VectorIndex, 10, 20);
            b.set(0 as VectorIndex, 2, 5);
            const result = ops.divide(a, b);
            expect(result.get(0 as VectorIndex)).toEqual([5, 4]);
        });
    });

    describe('divideScalar', () => {
        it('divides all components by scalar', () => {
            const a = new Vec3Array(1);
            a.set(0 as VectorIndex, 10, 20, 30);
            const result = ops.divideScalar(a, 10);
            expect(result.get(0 as VectorIndex)).toEqual([1, 2, 3]);
        });
    });

    describe('dot', () => {
        it('computes dot product per element', () => {
            const a = new Vec3Array(1);
            const b = new Vec3Array(1);
            a.set(0 as VectorIndex, 1, 2, 3);
            b.set(0 as VectorIndex, 4, 5, 6);
            const result = ops.dot(a, b);
            expect(result[0]).toBe(32); // 1*4 + 2*5 + 3*6
        });

        it('dot for Vec2 ignores z/w', () => {
            const a = new Vec2Array(1);
            const b = new Vec2Array(1);
            a.set(0 as VectorIndex, 3, 4);
            b.set(0 as VectorIndex, 5, 6);
            const result = ops.dot(a, b);
            expect(result[0]).toBe(39); // 3*5 + 4*6
        });
    });

    describe('length', () => {
        it('computes vector length per element', () => {
            const a = new Vec3Array(1);
            a.set(0 as VectorIndex, 3, 4, 0);
            const result = ops.length(a);
            expect(result[0]).toBeCloseTo(5);
        });
    });

    describe('lengthSquared', () => {
        it('computes squared length per element', () => {
            const a = new Vec3Array(1);
            a.set(0 as VectorIndex, 3, 4, 0);
            const result = ops.lengthSquared(a);
            expect(result[0]).toBe(25);
        });
    });

    describe('normalize', () => {
        it('normalizes to unit length', () => {
            const a = new Vec3Array(1);
            a.set(0 as VectorIndex, 3, 4, 0);
            const result = ops.normalize(a);
            const len = Math.sqrt(
                result.x[0] ** 2 + result.y[0] ** 2 + result.z![0] ** 2
            );
            expect(len).toBeCloseTo(1.0);
        });

        it('handles zero-length vector', () => {
            const a = new Vec3Array(1);
            a.set(0 as VectorIndex, 0, 0, 0);
            const result = ops.normalize(a);
            expect(result.get(0 as VectorIndex)).toEqual([0, 0, 0]);
        });
    });

    describe('lerp', () => {
        it('interpolates at t=0 returns a', () => {
            const a = new Vec3Array(1);
            const b = new Vec3Array(1);
            a.set(0 as VectorIndex, 1, 2, 3);
            b.set(0 as VectorIndex, 10, 20, 30);
            const result = ops.lerp(a, b, 0);
            expect(result.get(0 as VectorIndex)).toEqual([1, 2, 3]);
        });

        it('interpolates at t=1 returns b', () => {
            const a = new Vec3Array(1);
            const b = new Vec3Array(1);
            a.set(0 as VectorIndex, 1, 2, 3);
            b.set(0 as VectorIndex, 10, 20, 30);
            const result = ops.lerp(a, b, 1);
            expect(result.get(0 as VectorIndex)).toEqual([10, 20, 30]);
        });

        it('interpolates at t=0.5', () => {
            const a = new Vec3Array(1);
            const b = new Vec3Array(1);
            a.set(0 as VectorIndex, 0, 0, 0);
            b.set(0 as VectorIndex, 10, 20, 30);
            const result = ops.lerp(a, b, 0.5);
            expect(result.get(0 as VectorIndex)).toEqual([5, 10, 15]);
        });
    });

    describe('min/max', () => {
        it('min takes component-wise minimum', () => {
            const a = new Vec3Array(1);
            const b = new Vec3Array(1);
            a.set(0 as VectorIndex, 5, 1, 9);
            b.set(0 as VectorIndex, 2, 8, 3);
            const result = ops.min(a, b);
            expect(result.get(0 as VectorIndex)).toEqual([2, 1, 3]);
        });

        it('max takes component-wise maximum', () => {
            const a = new Vec3Array(1);
            const b = new Vec3Array(1);
            a.set(0 as VectorIndex, 5, 1, 9);
            b.set(0 as VectorIndex, 2, 8, 3);
            const result = ops.max(a, b);
            expect(result.get(0 as VectorIndex)).toEqual([5, 8, 9]);
        });
    });

    describe('clamp', () => {
        it('clamps each component between min and max', () => {
            const a = new Vec3Array(1);
            const lo = new Vec3Array(1);
            const hi = new Vec3Array(1);
            a.set(0 as VectorIndex, -5, 5, 15);
            lo.set(0 as VectorIndex, 0, 0, 0);
            hi.set(0 as VectorIndex, 10, 10, 10);
            const result = ops.clamp(a, lo, hi);
            expect(result.get(0 as VectorIndex)).toEqual([0, 5, 10]);
        });
    });

    describe('Vec4 operations', () => {
        it('add works for Vec4', () => {
            const a = new Vec4Array(1);
            const b = new Vec4Array(1);
            a.set(0 as VectorIndex, 1, 2, 3, 4);
            b.set(0 as VectorIndex, 10, 20, 30, 40);
            const result = ops.add(a, b);
            expect(result.get(0 as VectorIndex)).toEqual([11, 22, 33, 44]);
        });

        it('dot includes w component for Vec4', () => {
            const a = new Vec4Array(1);
            const b = new Vec4Array(1);
            a.set(0 as VectorIndex, 1, 2, 3, 4);
            b.set(0 as VectorIndex, 5, 6, 7, 8);
            const result = ops.dot(a, b);
            expect(result[0]).toBe(70); // 1*5+2*6+3*7+4*8
        });
    });

    describe('exported singleton vectorOps', () => {
        it('vectorOps is a VectorOperations instance', () => {
            expect(vectorOps).toBeInstanceOf(VectorOperations);
        });
    });
});

// ─── MemoryPool ─────────────────────────────────────────────────────────────

describe('MemoryPool', () => {
    it('acquire creates new array when pool is empty', () => {
        const pool = new MemoryPool();
        const arr = pool.acquire(Vec3Array, 10);
        expect(arr).toBeInstanceOf(Vec3Array);
        expect(arr.capacity).toBe(10);
    });

    it('release then acquire reuses from pool', () => {
        const pool = new MemoryPool();
        const arr = pool.acquire(Vec3Array, 10);
        arr.set(0 as VectorIndex, 99, 99, 99);
        pool.release(arr);
        expect(pool.size).toBe(1);

        const reused = pool.acquire(Vec3Array, 10);
        expect(reused.capacity).toBe(10);
        // Should be cleared after acquire
        expect(reused.get(0 as VectorIndex)).toEqual([0, 0, 0]);
    });

    it('release disposes if pool is full', () => {
        const pool = new MemoryPool(1);
        const a = pool.acquire(Vec3Array, 5);
        const b = pool.acquire(Vec3Array, 5);
        pool.release(a);
        pool.release(b); // pool already has 1, this should dispose b
        expect(pool.size).toBe(1);
        expect(b.isDisposed).toBe(true);
    });

    it('release ignores disposed arrays', () => {
        const pool = new MemoryPool();
        const arr = pool.acquire(Vec3Array, 5);
        arr.dispose();
        pool.release(arr);
        expect(pool.size).toBe(0);
    });

    it('clear disposes all pooled arrays', () => {
        const pool = new MemoryPool();
        const a = pool.acquire(Vec3Array, 5);
        const b = pool.acquire(Vec3Array, 5);
        pool.release(a);
        pool.release(b);
        expect(pool.size).toBe(2);
        pool.clear();
        expect(pool.size).toBe(0);
        expect(a.isDisposed).toBe(true);
        expect(b.isDisposed).toBe(true);
    });

    it('exported singleton memoryPool exists', () => {
        expect(memoryPool).toBeInstanceOf(MemoryPool);
    });
});
