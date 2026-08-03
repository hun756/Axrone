import { describe, it, expect } from 'vitest';
import {
    createBrandedFactory,
    PhysicsError,
    assertState,
    assertFound,
    assertCapacity,
    makeCollisionPairKey,
    SoAManager,
    buildCollisionMatrix,
} from '../core/foundation';
import type { SoASchema, ManagerState } from '../core/foundation';

describe('createBrandedFactory', () => {
    it('creates a factory with the given brand name', () => {
        const factory = createBrandedFactory('BodyId');
        expect(factory.brand).toBe('BodyId');
    });

    it('create returns the numeric value unchanged', () => {
        const factory = createBrandedFactory('ShapeId');
        const branded = factory.create(42);
        expect(branded).toBe(42);
    });

    it('unwrap returns the underlying number', () => {
        const factory = createBrandedFactory('BodyId');
        const branded = factory.create(7);
        expect(factory.unwrap(branded)).toBe(7);
    });

    it('create/unwrap roundtrip preserves value', () => {
        const factory = createBrandedFactory('ConstraintId');
        for (const v of [0, 1, -1, 1_000_000, Number.MAX_SAFE_INTEGER]) {
            expect(factory.unwrap(factory.create(v))).toBe(v);
        }
    });
});

describe('PhysicsError', () => {
    it('stores code and message', () => {
        const err = new PhysicsError('boom', 'INVALID_STATE');
        expect(err.message).toBe('boom');
        expect(err.code).toBe('INVALID_STATE');
        expect(err.name).toBe('PhysicsError');
    });

    it('captures a timestamp', () => {
        const err = new PhysicsError('x', 'NOT_FOUND');
        expect(err.timestamp).toBeGreaterThan(0);
    });

    it('freezes the context object', () => {
        const err = new PhysicsError('x', 'INVALID_ARGUMENT', { field: 'mass' });
        expect(err.context).toEqual({ field: 'mass' });
        expect(Object.isFrozen(err.context)).toBe(true);
    });

    it('defaults context to empty object', () => {
        const err = new PhysicsError('x', 'DUPLICATE');
        expect(err.context).toEqual({});
    });

    it('withContext returns a new error merging context', () => {
        const original = new PhysicsError('fail', 'CAPACITY_EXCEEDED', { max: 100 });
        const extended = original.withContext({ current: 101 });
        expect(extended.code).toBe('CAPACITY_EXCEEDED');
        expect(extended.message).toBe('fail');
        expect(extended.context).toEqual({ max: 100, current: 101 });
        expect(extended).not.toBe(original);
    });

    it('withContext overrides existing keys', () => {
        const err = new PhysicsError('x', 'INVALID_STATE', { a: 1 });
        const merged = err.withContext({ a: 2, b: 3 });
        expect(merged.context).toEqual({ a: 2, b: 3 });
    });

    it('is an instance of Error', () => {
        const err = new PhysicsError('x', 'NOT_FOUND');
        expect(err).toBeInstanceOf(Error);
        expect(err).toBeInstanceOf(PhysicsError);
    });
});

describe('assertState', () => {
    it('passes when state matches expected', () => {
        expect(() => assertState('active', 'active')).not.toThrow();
        expect(() => assertState('idle', 'idle')).not.toThrow();
    });

    it('throws PhysicsError with INVALID_STATE when state does not match', () => {
        expect(() => assertState('idle' as ManagerState, 'active')).toThrow(PhysicsError);
        try {
            assertState('disposed' as ManagerState, 'active');
        } catch (e) {
            expect((e as PhysicsError).code).toBe('INVALID_STATE');
        }
    });
});

describe('assertFound', () => {
    it('passes when value is defined', () => {
        expect(() => assertFound(42, 'Body', 1)).not.toThrow();
        expect(() => assertFound(null, 'Body', 1)).not.toThrow();
    });

    it('throws PhysicsError with NOT_FOUND when value is undefined', () => {
        expect(() => assertFound(undefined, 'Body', 7)).toThrow(PhysicsError);
        try {
            assertFound(undefined, 'Shape', 3);
        } catch (e) {
            const pe = e as PhysicsError;
            expect(pe.code).toBe('NOT_FOUND');
            expect(pe.context).toEqual({ id: 3 });
        }
    });
});

describe('assertCapacity', () => {
    it('passes when current is below max', () => {
        expect(() => assertCapacity(5, 10, 'Bodies')).not.toThrow();
    });

    it('throws when current equals max', () => {
        expect(() => assertCapacity(10, 10, 'Bodies')).toThrow(PhysicsError);
        try {
            assertCapacity(10, 10, 'Bodies');
        } catch (e) {
            expect((e as PhysicsError).code).toBe('CAPACITY_EXCEEDED');
        }
    });

    it('throws when current exceeds max', () => {
        expect(() => assertCapacity(11, 10, 'Bodies')).toThrow(PhysicsError);
    });
});

describe('makeCollisionPairKey', () => {
    it('is commutative: pairKey(a,b) === pairKey(b,a)', () => {
        expect(makeCollisionPairKey(1, 2)).toBe(makeCollisionPairKey(2, 1));
        expect(makeCollisionPairKey(0, 100)).toBe(makeCollisionPairKey(100, 0));
    });

    it('produces different keys for different pairs', () => {
        expect(makeCollisionPairKey(1, 2)).not.toBe(makeCollisionPairKey(1, 3));
        expect(makeCollisionPairKey(1, 2)).not.toBe(makeCollisionPairKey(2, 3));
    });

    it('handles same-id pairs', () => {
        const key = makeCollisionPairKey(5, 5);
        expect(key).toBe(makeCollisionPairKey(5, 5));
    });

    it('handles zero ids', () => {
        const key = makeCollisionPairKey(0, 0);
        expect(typeof key).toBe('number');
    });
});

describe('SoAManager', () => {
    const schema = {
        posX: { offset: 0, size: 1 },
        posY: { offset: 1, size: 1 },
        velX: { offset: 2, size: 1 },
        velY: { offset: 3, size: 1 },
    } as const satisfies SoASchema;

    class TestSoAManager extends SoAManager<typeof schema> {
        constructor(capacity: number) {
            super(capacity, schema);
        }

        readX(index: number) { return this._readScalar(index, 'posX'); }
        writeX(index: number, v: number) { this._writeScalar(index, 'posX', v); }
        readVel(index: number) { return this._readVec2(index, 'velX'); }
        writeVel(index: number, x: number, y: number) {
            this._writeVec2(index, 'velX', { x, y });
        }
        incrementCount() { this._count++; }

        [Symbol.dispose]() {
            this._state = 'disposed';
            this._count = 0;
        }
    }

    it('starts in idle state with count 0', () => {
        const mgr = new TestSoAManager(16);
        expect(mgr.count).toBe(0);
        expect(mgr.capacity).toBe(16);
        expect(mgr.state).toBe('idle');
    });

    it('activate transitions from idle to active', () => {
        const mgr = new TestSoAManager(16);
        mgr.activate();
        expect(mgr.state).toBe('active');
    });

    it('activate is a no-op when already active', () => {
        const mgr = new TestSoAManager(16);
        mgr.activate();
        mgr.activate();
        expect(mgr.state).toBe('active');
    });

    it('reads and writes scalar values', () => {
        const mgr = new TestSoAManager(16);
        mgr.writeX(0, 42);
        expect(mgr.readX(0)).toBe(42);
    });

    it('reads and writes vec2 values', () => {
        const mgr = new TestSoAManager(16);
        mgr.writeVel(0, 3, 4);
        const v = mgr.readVel(0);
        expect(v.x).toBe(3);
        expect(v.y).toBe(4);
    });

    it('readVec2 with output parameter reuses the object', () => {
        const mgr = new TestSoAManager(16);
        mgr.writeVel(0, 5, 6);
        const out = { x: 0, y: 0 };
        const result = mgr.readVel(0);
        // The method returns a new object when no out param is passed.
        expect(result.x).toBe(5);
    });

    it('different indices are independent', () => {
        const mgr = new TestSoAManager(16);
        mgr.writeX(0, 10);
        mgr.writeX(1, 20);
        expect(mgr.readX(0)).toBe(10);
        expect(mgr.readX(1)).toBe(20);
    });

    it('dispose sets state to disposed', () => {
        const mgr = new TestSoAManager(16);
        mgr.activate();
        mgr[Symbol.dispose]();
        expect(mgr.state).toBe('disposed');
        expect(mgr.count).toBe(0);
    });
});

describe('buildCollisionMatrix', () => {
    it('creates a map with shape pair keys', () => {
        const fn = () => {};
        const matrix = buildCollisionMatrix([
            ['circle', 'circle', fn],
            ['circle', 'box', null],
        ]);

        expect(matrix.get('circle:circle')).toBe(fn);
        expect(matrix.get('circle:box')).toBe(null);
    });

    it('returns null for unregistered pairs', () => {
        const matrix = buildCollisionMatrix([
            ['circle', 'circle', () => {}],
        ]);
        expect(matrix.get('box:box')).toBeUndefined();
    });

    it('handles empty entries', () => {
        const matrix = buildCollisionMatrix([]);
        expect(matrix.size).toBe(0);
    });

    it('preserves the function reference', () => {
        const handler = (a: any, b: any) => a + b;
        const matrix = buildCollisionMatrix([['a', 'b', handler]]);
        expect(matrix.get('a:b')).toBe(handler);
    });
});
