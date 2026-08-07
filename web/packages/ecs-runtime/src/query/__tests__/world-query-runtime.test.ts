import { describe, expect, it } from 'vitest';
import { OptimizedQueryCache, WorldQueryRuntime } from '../index';

describe('WorldQueryRuntime', () => {
    it('reuses cached archetype matches for repeated single-component queries', () => {
        const cache = new OptimizedQueryCache();
        const runtime = new WorldQueryRuntime({
            cache,
            getArchetypes: () =>
                [
                    { id: 'Transform', mask: 0b001n },
                    { id: 'Transform|Mesh', mask: 0b011n },
                    { id: 'Camera', mask: 0b100n },
                ] as any,
            createBitMask: (components) => (components[0] === 'Transform' ? 0b001n : 0b100n),
        });

        const first = runtime.resolveMatchingArchetypes(['Transform']);
        const second = runtime.resolveMatchingArchetypes(['Transform']);

        expect(first).toBe(second);
        expect(first).toEqual(['Transform', 'Transform|Mesh']);
    });

    it('normalizes multi-component query keys so cache hits survive argument order changes', () => {
        const cache = new OptimizedQueryCache();
        const runtime = new WorldQueryRuntime({
            cache,
            getArchetypes: () => [{ id: 'A|B', mask: 0b011n }] as any,
            createBitMask: () => 0b011n,
        });

        const first = runtime.resolveMatchingArchetypes(['B', 'A']);
        const second = runtime.resolveMatchingArchetypes(['A', 'B']);

        expect(first).toBe(second);
        expect(first).toEqual(['A|B']);
    });

    it('hydrates bit-mask cache so repeated queries avoid key normalization work', () => {
        const cache = new OptimizedQueryCache();
        const runtime = new WorldQueryRuntime({
            cache,
            getArchetypes: () => [{ id: 'A|B', mask: 0b011n }] as any,
            createBitMask: () => 0b011n,
        });

        const first = runtime.resolveMatchingArchetypes(['A', 'B']);

        expect(cache.getBitQuery(0b011n)).toBe(first);
        expect(runtime.resolveMatchingArchetypes(['B', 'A'])).toBe(first);
    });

    // ─── No matching archetypes ─────────────────────────────────────

    describe('no matching archetypes', () => {
        it('returns empty array when no archetypes match', () => {
            const cache = new OptimizedQueryCache();
            const runtime = new WorldQueryRuntime({
                cache,
                getArchetypes: () =>
                    [
                        { id: 'Transform', mask: 0b001n },
                        { id: 'Camera', mask: 0b100n },
                    ] as any,
                createBitMask: () => 0b010n, // no archetype has bit 1 set
            });

            const result = runtime.resolveMatchingArchetypes(['NonExistent']);
            expect(result).toEqual([]);
        });
    });

    // ─── _createQueryKey ────────────────────────────────────────────

    describe('query key creation', () => {
        it('single component uses name directly as key', () => {
            const cache = new OptimizedQueryCache();
            const getArchetypes = () => [{ id: 'Transform', mask: 0b001n }] as any;
            const createBitMask = () => 0b001n;

            const runtime = new WorldQueryRuntime({ cache, getArchetypes, createBitMask });

            // Single component query should work and be cached
            const result = runtime.resolveMatchingArchetypes(['Transform']);
            expect(result).toEqual(['Transform']);
        });

        it('multi-component query uses sorted+joined key', () => {
            const cache = new OptimizedQueryCache();
            const runtime = new WorldQueryRuntime({
                cache,
                getArchetypes: () => [{ id: 'A|B', mask: 0b011n }] as any,
                createBitMask: () => 0b011n,
            });

            // Different order should produce same result via sorting
            const r1 = runtime.resolveMatchingArchetypes(['B', 'A']);
            const r2 = runtime.resolveMatchingArchetypes(['A', 'B']);

            expect(r1).toBe(r2); // Same cached reference
        });
    });

    // ─── Cache miss then hit ────────────────────────────────────────

    describe('cache progression', () => {
        it('first call computes, second uses string cache, third uses bit cache', () => {
            const cache = new OptimizedQueryCache();
            let archetypeCallCount = 0;
            const getArchetypes = () => {
                archetypeCallCount++;
                return [{ id: 'A|B', mask: 0b011n }] as any;
            };
            const createBitMask = () => 0b011n;

            const runtime = new WorldQueryRuntime({ cache, getArchetypes, createBitMask });

            // First call: cache miss on both string and bit, computes from archetypes
            const first = runtime.resolveMatchingArchetypes(['A', 'B']);
            expect(first).toEqual(['A|B']);
            expect(archetypeCallCount).toBe(1);

            // Second call: bit cache hit (same mask)
            const second = runtime.resolveMatchingArchetypes(['A', 'B']);
            expect(second).toBe(first);
            expect(archetypeCallCount).toBe(1); // no new archetype iteration

            // Third call with different order: still bit cache hit
            const third = runtime.resolveMatchingArchetypes(['B', 'A']);
            expect(third).toBe(first);
            expect(archetypeCallCount).toBe(1);
        });
    });

    // ─── Bit-mask matching ──────────────────────────────────────────

    describe('bit-mask matching', () => {
        it('only returns archetypes with all query bits set', () => {
            const cache = new OptimizedQueryCache();
            const runtime = new WorldQueryRuntime({
                cache,
                getArchetypes: () =>
                    [
                        { id: 'A', mask: 0b001n },
                        { id: 'A|B', mask: 0b011n },
                        { id: 'B', mask: 0b010n },
                        { id: 'A|B|C', mask: 0b111n },
                    ] as any,
                createBitMask: (components) => {
                    let mask = 0n;
                    for (const c of components) {
                        if (c === 'A') mask |= 0b001n;
                        if (c === 'B') mask |= 0b010n;
                    }
                    return mask;
                },
            });

            // Query for A+B: should match A|B and A|B|C
            const result = runtime.resolveMatchingArchetypes(['A', 'B']);
            expect(result).toEqual(['A|B', 'A|B|C']);

            // Query for just A: should match A, A|B, A|B|C
            const resultA = runtime.resolveMatchingArchetypes(['A']);
            expect(resultA).toEqual(['A', 'A|B', 'A|B|C']);

            // Query for just B: should match A|B, B, A|B|C (iteration order)
            const resultB = runtime.resolveMatchingArchetypes(['B']);
            expect(resultB).toEqual(['A|B', 'B', 'A|B|C']);
        });
    });
});
