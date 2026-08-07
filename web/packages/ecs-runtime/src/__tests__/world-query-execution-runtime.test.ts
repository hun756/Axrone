import { describe, expect, it, vi } from 'vitest';
import { WorldQueryExecutionRuntime } from '../component-system/core/world-query-execution-runtime';
import { Component } from '../component-system/core/component';

class Position extends Component {
    x = 0;
    y = 0;
}
class Velocity extends Component {
    dx = 0;
    dy = 0;
}

type TestRegistry = {
    Position: typeof Position;
    Velocity: typeof Velocity;
};

function createMockArchetype(
    id: string,
    entityCount: number,
    components: Record<string, any[]>
) {
    const componentMap = new Map<string, any>();
    for (const [name, dense] of Object.entries(components)) {
        componentMap.set(name, { dense });
    }

    return {
        id,
        entityCount,
        entities: Array.from({ length: entityCount }, (_, i) => i + 1),
        components: componentMap,
    };
}

describe('WorldQueryExecutionRuntime', () => {
    // ─── Empty results ──────────────────────────────────────────────

    describe('execute with no matching archetypes', () => {
        it('returns frozen empty array', () => {
            const runtime = new WorldQueryExecutionRuntime<TestRegistry>({
                queryRuntime: {
                    resolveMatchingArchetypes: () => [],
                } as any,
                getArchetype: () => undefined,
            });

            const result = runtime.execute('Position');
            expect(result).toEqual([]);
            expect(Object.isFrozen(result)).toBe(true);
        });
    });

    describe('execute with matching archetypes but all empty', () => {
        it('returns frozen empty array', () => {
            const runtime = new WorldQueryExecutionRuntime<TestRegistry>({
                queryRuntime: {
                    resolveMatchingArchetypes: () => ['Position'],
                } as any,
                getArchetype: () => createMockArchetype('Position', 0, {}) as any,
            });

            const result = runtime.execute('Position');
            expect(result).toEqual([]);
            expect(Object.isFrozen(result)).toBe(true);
        });
    });

    // ─── Archetype filtering ────────────────────────────────────────

    describe('execute with archetype missing some components', () => {
        it('skips archetypes that lack required components', () => {
            const archetypes = new Map<string, any>();
            archetypes.set(
                'Position',
                createMockArchetype('Position', 1, {
                    Position: [{ x: 1, y: 2 }],
                })
            );
            archetypes.set(
                'Velocity',
                createMockArchetype('Velocity', 1, {
                    Velocity: [{ dx: 3, dy: 4 }],
                })
            );

            const runtime = new WorldQueryExecutionRuntime<TestRegistry>({
                queryRuntime: {
                    resolveMatchingArchetypes: () => ['Position', 'Velocity'],
                } as any,
                getArchetype: (id: string) => archetypes.get(id),
            });

            // Query for Position+Velocity, but no archetype has both
            const result = runtime.execute('Position', 'Velocity');
            expect(result).toEqual([]);
        });
    });

    // ─── Happy path ─────────────────────────────────────────────────

    describe('execute happy path', () => {
        it('resolves archetypes and builds correct QueryResult array', () => {
            const posPool = [{ x: 1, y: 2 }, { x: 3, y: 4 }];
            const velPool = [{ dx: 10, dy: 20 }, { dx: 30, dy: 40 }];

            const archetype = createMockArchetype('Position|Velocity', 2, {
                Position: posPool,
                Velocity: velPool,
            });

            const runtime = new WorldQueryExecutionRuntime<TestRegistry>({
                queryRuntime: {
                    resolveMatchingArchetypes: () => ['Position|Velocity'],
                } as any,
                getArchetype: () => archetype as any,
            });

            const result = runtime.execute('Position', 'Velocity');

            expect(result).toHaveLength(2);
            expect(result[0]).toEqual({
                entity: 1,
                components: {
                    Position: { x: 1, y: 2 },
                    Velocity: { dx: 10, dy: 20 },
                },
            });
            expect(result[1]).toEqual({
                entity: 2,
                components: {
                    Position: { x: 3, y: 4 },
                    Velocity: { dx: 30, dy: 40 },
                },
            });
        });

        it('handles multiple matching archetypes', () => {
            const arch1 = createMockArchetype('Position', 1, {
                Position: [{ x: 1, y: 1 }],
            });
            const arch2 = createMockArchetype('Position|Velocity', 1, {
                Position: [{ x: 2, y: 2 }],
                Velocity: [{ dx: 5, dy: 5 }],
            });

            const archetypes = new Map<string, any>([
                ['Position', arch1],
                ['Position|Velocity', arch2],
            ]);

            const runtime = new WorldQueryExecutionRuntime<TestRegistry>({
                queryRuntime: {
                    resolveMatchingArchetypes: () => ['Position', 'Position|Velocity'],
                } as any,
                getArchetype: (id: string) => archetypes.get(id),
            });

            // Query for Position only - both archetypes match
            const result = runtime.execute('Position');

            expect(result).toHaveLength(2);
            expect(result[0]!.entity).toBe(1);
            expect(result[1]!.entity).toBe(1);
        });
    });

    // ─── onQueryResolved callback ───────────────────────────────────

    describe('onQueryResolved callback', () => {
        it('is called on every execute()', () => {
            const onQueryResolved = vi.fn();
            const runtime = new WorldQueryExecutionRuntime<TestRegistry>({
                queryRuntime: {
                    resolveMatchingArchetypes: () => [],
                } as any,
                getArchetype: () => undefined,
                onQueryResolved,
            });

            runtime.execute('Position');
            runtime.execute('Velocity');
            runtime.execute('Position', 'Velocity');

            expect(onQueryResolved).toHaveBeenCalledTimes(3);
        });

        it('is called even when no archetypes match', () => {
            const onQueryResolved = vi.fn();
            const runtime = new WorldQueryExecutionRuntime<TestRegistry>({
                queryRuntime: {
                    resolveMatchingArchetypes: () => [],
                } as any,
                getArchetype: () => undefined,
                onQueryResolved,
            });

            runtime.execute('Position');
            expect(onQueryResolved).toHaveBeenCalledTimes(1);
        });
    });

    // ─── _archetypeHasAllComponents ─────────────────────────────────

    describe('component validation', () => {
        it('skips archetype when getArchetype returns undefined', () => {
            const runtime = new WorldQueryExecutionRuntime<TestRegistry>({
                queryRuntime: {
                    resolveMatchingArchetypes: () => ['GhostArchetype'],
                } as any,
                getArchetype: () => undefined,
            });

            const result = runtime.execute('Position');
            expect(result).toEqual([]);
        });
    });

    // ─── _appendArchetypeResults ────────────────────────────────────

    describe('result building', () => {
        it('handles missing pool gracefully by returning early', () => {
            // Archetype claims to have Position but pool is missing
            const archetype = {
                id: 'Position',
                entityCount: 1,
                entities: [1],
                components: new Map(), // no pools!
            };

            const runtime = new WorldQueryExecutionRuntime<TestRegistry>({
                queryRuntime: {
                    resolveMatchingArchetypes: () => ['Position'],
                } as any,
                getArchetype: () => archetype as any,
            });

            // _archetypeHasAllComponents will return false since components.has('Position') is false
            const result = runtime.execute('Position');
            expect(result).toEqual([]);
        });
    });
});
