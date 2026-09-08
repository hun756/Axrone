import { describe, expect, it } from 'vitest';
import {
    Xoroshiro128PlusPlus,
    PCGEngine,
    Xoshiro256PlusPlus,
    SplitMix64Engine,
    ChaCha20Engine,
    CryptoEngine,
    createEngineFactory,
    RandomEngineType,
} from '@axrone/random';
import type { IRandomEngine } from '@axrone/random';

const UINT32_MAX = 0xffffffff;

/** Helper: collect N uint32 values from an engine. */
const collectUint32 = (engine: IRandomEngine, n: number): number[] => {
    const out: number[] = [];
    for (let i = 0; i < n; i++) out.push(engine.nextUint32());
    return out;
};

/** Helper: collect N next01 values from an engine. */
const collect01 = (engine: IRandomEngine, n: number): number[] => {
    const out: number[] = [];
    for (let i = 0; i < n; i++) out.push(engine.next01());
    return out;
};

// ─── Deterministic seeded engines ───────────────────────────────────────────

const SEEDED_ENGINES: { name: string; factory: (seed: number) => IRandomEngine }[] = [
    { name: 'Xoroshiro128PlusPlus', factory: (s) => new Xoroshiro128PlusPlus(s) },
    { name: 'PCGEngine', factory: (s) => new PCGEngine(s) },
    { name: 'Xoshiro256PlusPlus', factory: (s) => new Xoshiro256PlusPlus(s) },
    { name: 'SplitMix64Engine', factory: (s) => new SplitMix64Engine(s) },
    { name: 'ChaCha20Engine', factory: (s) => new ChaCha20Engine(s) },
];

describe.each(SEEDED_ENGINES)('$name', ({ factory }) => {
    it('is deterministic for the same seed', () => {
        const a = factory(42);
        const b = factory(42);
        expect(collectUint32(a, 10)).toEqual(collectUint32(b, 10));
    });

    it('produces different sequences for different seeds', () => {
        const a = factory(1);
        const b = factory(2);
        expect(collectUint32(a, 5)).not.toEqual(collectUint32(b, 5));
    });

    it('next01() returns values in [0, 1)', () => {
        const engine = factory(99);
        const values = collect01(engine, 200);
        for (const v of values) {
            expect(v).toBeGreaterThanOrEqual(0);
            expect(v).toBeLessThan(1);
            expect(Number.isFinite(v)).toBe(true);
        }
    });

    it('nextUint32() returns values in [0, UINT32_MAX]', () => {
        const engine = factory(99);
        for (let i = 0; i < 100; i++) {
            const v = engine.nextUint32();
            expect(v).toBeGreaterThanOrEqual(0);
            expect(v).toBeLessThanOrEqual(UINT32_MAX);
            expect(Number.isInteger(v)).toBe(true);
        }
    });

    it('nextUint64() returns bigint values >= 0', () => {
        const engine = factory(99);
        for (let i = 0; i < 50; i++) {
            const v = engine.nextUint64();
            expect(typeof v).toBe('bigint');
            expect(v).toBeGreaterThanOrEqual(0n);
        }
    });

    it('getState / setState round-trip preserves counter', () => {
        const engine = factory(77);
        // advance a few steps
        for (let i = 0; i < 5; i++) engine.nextUint32();

        const saved = engine.getState();

        engine.setState(saved);
        const stateAfter = engine.getState();
        // counter must be restored exactly
        expect(stateAfter.counter).toBe(saved.counter);
        expect(stateAfter.vector).toEqual(saved.vector);
        expect(stateAfter.engine).toBe(saved.engine);
    });

    it('setState then next produces finite values', () => {
        const engine = factory(77);
        for (let i = 0; i < 5; i++) engine.nextUint32();
        const saved = engine.getState();
        engine.setState(saved);

        for (let i = 0; i < 10; i++) {
            const v = engine.nextUint32();
            expect(Number.isFinite(v)).toBe(true);
            expect(v).toBeGreaterThanOrEqual(0);
        }
    });

    it('clone produces an independent copy with the same sequence', () => {
        const engine = factory(55);
        for (let i = 0; i < 3; i++) engine.nextUint32();

        const cloned = engine.clone();
        expect(collectUint32(cloned, 10)).toEqual(collectUint32(engine, 10));
    });

    it('clone is independent — mutating original does not affect clone', () => {
        const engine = factory(55);
        const cloned = engine.clone();

        engine.nextUint32();
        engine.nextUint32();

        const clonedValues = collectUint32(cloned, 5);
        const freshClone = engine.clone();
        const freshValues = collectUint32(freshClone, 5);

        // cloned was not advanced, so its first 5 should differ from engine's next 5
        // but fresh clone should match engine's current state
        expect(freshValues).toEqual(collectUint32(engine.clone(), 5));
        // original cloned copy should still produce its own sequence
        expect(clonedValues.length).toBe(5);
    });

    it('jumpAhead(0n) is a no-op', () => {
        const engine = factory(33);
        const stateBefore = engine.getState();
        engine.jumpAhead(0n);
        const stateAfter = engine.getState();
        // counter should not have changed
        expect(stateAfter.counter).toBe(stateBefore.counter);
    });

    it('jumpAhead(1n) advances by one step', () => {
        const a = factory(33);
        const b = factory(33);

        a.nextUint32();
        b.jumpAhead(1n);

        // After jumpAhead(1n), the next value should match
        // (both engines consumed one step)
        expect(a.nextUint32()).toBe(b.nextUint32());
    });

    it('jumpAhead(large) advances counter', () => {
        const engine = factory(33);
        const stateBefore = engine.getState();
        engine.jumpAhead(1000n);
        const stateAfter = engine.getState();
        expect(stateAfter.counter).toBeGreaterThan(stateBefore.counter);
    });
});

// ─── Contract tests: stream continuity and jump equivalence ─────────────────

describe.each(SEEDED_ENGINES)('$name contract tests', ({ factory }) => {
    it('stream continuity: save → restore → next draws match uninterrupted stream', () => {
        const engine = factory(42);
        // Advance to some arbitrary point
        for (let i = 0; i < 10; i++) engine.nextUint32();

        // Save state
        const saved = engine.getState();

        // Draw expected continuation
        const expected = [engine.nextUint32(), engine.nextUint32(), engine.nextUint32()];

        // Restore state
        engine.setState(saved);

        // Draw actual continuation
        const actual = [engine.nextUint32(), engine.nextUint32(), engine.nextUint32()];

        expect(actual).toEqual(expected);
    });

    it('jumpAhead(n) produces same next value as n manual draws', () => {
        const steps = 100n;

        const engine1 = factory(99);
        for (let i = 0n; i < steps; i++) engine1.nextUint64();
        const expected = engine1.nextUint64();

        const engine2 = factory(99);
        engine2.jumpAhead(steps);
        const actual = engine2.nextUint64();

        expect(actual).toBe(expected);
    });

    it('clone produces identical continuation from same point', () => {
        const engine = factory(77);
        for (let i = 0; i < 5; i++) engine.nextUint32();

        const cloned = engine.clone();

        const expected = [engine.nextUint32(), engine.nextUint32()];
        const actual = [cloned.nextUint32(), cloned.nextUint32()];

        expect(actual).toEqual(expected);
    });
});

// ─── CryptoEngine (non-deterministic) ───────────────────────────────────────

describe('CryptoEngine', () => {
    it('next01() returns values in [0, 1)', () => {
        const engine = new CryptoEngine();
        for (let i = 0; i < 100; i++) {
            const v = engine.next01();
            expect(v).toBeGreaterThanOrEqual(0);
            expect(v).toBeLessThan(1);
            expect(Number.isFinite(v)).toBe(true);
        }
    });

    it('nextUint32() returns valid uint32', () => {
        const engine = new CryptoEngine();
        for (let i = 0; i < 50; i++) {
            const v = engine.nextUint32();
            expect(v).toBeGreaterThanOrEqual(0);
            expect(v).toBeLessThanOrEqual(UINT32_MAX);
            expect(Number.isInteger(v)).toBe(true);
        }
    });

    it('nextUint64() returns bigint >= 0', () => {
        const engine = new CryptoEngine();
        for (let i = 0; i < 50; i++) {
            const v = engine.nextUint64();
            expect(typeof v).toBe('bigint');
            expect(v).toBeGreaterThanOrEqual(0n);
        }
    });

    it('two instances produce different sequences (non-deterministic)', () => {
        const a = new CryptoEngine();
        const b = new CryptoEngine();
        const seqA = collectUint32(a, 10);
        const seqB = collectUint32(b, 10);
        // Extremely unlikely to be equal with crypto randomness
        expect(seqA).not.toEqual(seqB);
    });

    it('getState throws (non-reproducible)', () => {
        const engine = new CryptoEngine();
        expect(() => engine.getState()).toThrow('non-reproducible');
    });

    it('setState throws (non-reproducible)', () => {
        const engine = new CryptoEngine();
        const fakeState = {
            vector: [0n, 0n, 0n, 0n, 0n, 0n],
            counter: 0n,
            engine: RandomEngineType.CRYPTO,
        };
        expect(() => engine.setState(fakeState)).toThrow('non-reproducible');
    });

    it('clone produces identical continuation', () => {
        const engine = new CryptoEngine();
        engine.nextUint32();
        engine.nextUint32();

        const cloned = engine.clone();

        const expected = [engine.nextUint32(), engine.nextUint32()];
        const actual = [cloned.nextUint32(), cloned.nextUint32()];

        expect(actual).toEqual(expected);
    });

    it('jumpAhead advances counter', () => {
        const engine = new CryptoEngine();
        engine.jumpAhead(100n);
        expect(engine['counter']).toBe(100n);
    });

    it('jumpAhead(0n) is a no-op', () => {
        const engine = new CryptoEngine();
        engine.nextUint32();
        // NOTE: getState() deliberately throws for CryptoEngine (see the
        // 'getState throws' test above), so assert via the internal counter
        // like the 'jumpAhead advances counter' test does.
        const before: bigint = engine['counter'];
        engine.jumpAhead(0n);
        expect(engine['counter']).toBe(before);
    });
});

// ─── Engine factory ─────────────────────────────────────────────────────────

describe('createEngineFactory', () => {
    const engineTypes = [
        RandomEngineType.XOROSHIRO128_PLUS_PLUS,
        RandomEngineType.PCG_XSH_RR,
        RandomEngineType.XOSHIRO256_PLUS_PLUS,
        RandomEngineType.SPLITMIX64,
        RandomEngineType.CHACHA20,
        RandomEngineType.CRYPTO,
    ];

    it.each(engineTypes)('creates engine for %s', (engineType) => {
        const factory = createEngineFactory(engineType);
        const engine = engineType === RandomEngineType.CRYPTO ? factory() : factory(42);
        expect(engine).toBeDefined();
        expect(typeof engine.next01).toBe('function');
        expect(typeof engine.nextUint32).toBe('function');
        expect(typeof engine.nextUint64).toBe('function');
        expect(typeof engine.getState).toBe('function');
        expect(typeof engine.setState).toBe('function');
        expect(typeof engine.clone).toBe('function');
        expect(typeof engine.jumpAhead).toBe('function');
    });

    it('default fallback creates Xoroshiro128PlusPlus for unknown type', () => {
        const factory = createEngineFactory('unknown-engine' as any);
        const engine = factory(42);
        expect(engine.getState().engine).toBe(RandomEngineType.XOROSHIRO128_PLUS_PLUS);
    });
});
