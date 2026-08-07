import { describe, expect, it } from 'vitest';
import { Component } from '../../index';
import { ArchetypeStore } from '../index';

class TestComponent extends Component {}
class AnotherComponent extends Component {}
class ThirdComponent extends Component {}

describe('ArchetypeStore', () => {
    it('reuses sorted-signature archetypes', () => {
        const store = new ArchetypeStore({
            TestComponent,
            AnotherComponent,
        });

        const first = store.getOrCreateArchetype(['TestComponent', 'AnotherComponent']);
        const second = store.getOrCreateArchetype(['AnotherComponent', 'TestComponent']);

        expect(first.created).toBe(true);
        expect(second.created).toBe(false);
        expect(second.archetype).toBe(first.archetype);
        expect(first.archetype.signature).toEqual(['AnotherComponent', 'TestComponent']);
    });

    it('caches add and remove transitions on archetype edges', () => {
        const store = new ArchetypeStore({
            TestComponent,
            AnotherComponent,
        });

        const base = store.getOrCreateArchetype(['TestComponent']).archetype;
        const added = store.resolveAddComponentArchetype(base, 'AnotherComponent');

        expect(added.created).toBe(true);
        expect(base.edges.get('add:AnotherComponent')).toBe(added.archetype.id);
        expect(added.archetype.edges.get('remove:AnotherComponent')).toBe(base.id);

        const removed = store.resolveRemoveComponentArchetype(added.archetype, 'AnotherComponent');
        expect(removed.created).toBe(false);
        expect(removed.archetype).toBe(base);
    });

    // ─── createBitMask ──────────────────────────────────────────────

    describe('createBitMask', () => {
        it('creates correct mask for registered components', () => {
            const store = new ArchetypeStore({
                TestComponent,
                AnotherComponent,
            });

            const mask = store.createBitMask(['TestComponent']);
            expect(mask).toBe(0b01n);

            const mask2 = store.createBitMask(['AnotherComponent']);
            expect(mask2).toBe(0b10n);

            const maskBoth = store.createBitMask(['TestComponent', 'AnotherComponent']);
            expect(maskBoth).toBe(0b11n);
        });

        it('ignores unregistered component names', () => {
            const store = new ArchetypeStore({ TestComponent });
            const mask = store.createBitMask(['UnknownComponent']);
            expect(mask).toBe(0n);
        });

        it('returns 0n for empty array', () => {
            const store = new ArchetypeStore({ TestComponent });
            expect(store.createBitMask([])).toBe(0n);
        });
    });

    // ─── getOrCreateArchetype ───────────────────────────────────────

    describe('getOrCreateArchetype', () => {
        it('creates EMPTY archetype for empty signature', () => {
            const store = new ArchetypeStore({ TestComponent });
            const result = store.getOrCreateArchetype([]);

            expect(result.created).toBe(true);
            expect(result.archetype.id).toBe('EMPTY');
            expect(result.archetype.signature).toEqual([]);
        });

        it('creates single-component archetype', () => {
            const store = new ArchetypeStore({ TestComponent });
            const result = store.getOrCreateArchetype(['TestComponent']);

            expect(result.created).toBe(true);
            expect(result.archetype.signature).toEqual(['TestComponent']);
        });

        it('creates multi-component archetype with sorted signature', () => {
            const store = new ArchetypeStore({
                TestComponent,
                AnotherComponent,
                ThirdComponent,
            });
            const result = store.getOrCreateArchetype([
                'ThirdComponent',
                'TestComponent',
                'AnotherComponent',
            ]);

            expect(result.created).toBe(true);
            expect(result.archetype.signature).toEqual([
                'AnotherComponent',
                'TestComponent',
                'ThirdComponent',
            ]);
        });

        it('returns existing archetype without creating', () => {
            const store = new ArchetypeStore({ TestComponent });
            const first = store.getOrCreateArchetype(['TestComponent']);
            const second = store.getOrCreateArchetype(['TestComponent']);

            expect(second.created).toBe(false);
            expect(second.archetype).toBe(first.archetype);
        });
    });

    // ─── registerComponent ──────────────────────────────────────────

    describe('registerComponent', () => {
        it('adds to component mask so createBitMask includes it', () => {
            const store = new ArchetypeStore({ TestComponent });

            // Before registration, UnknownComponent has no bit
            expect(store.createBitMask(['NewComponent'])).toBe(0n);

            store.registerComponent('NewComponent');

            // After registration, it should have a bit assigned
            const mask = store.createBitMask(['NewComponent']);
            expect(mask).not.toBe(0n);
        });
    });

    // ─── reset ──────────────────────────────────────────────────────

    describe('reset', () => {
        it('clears all archetypes', () => {
            const store = new ArchetypeStore({ TestComponent });
            store.getOrCreateArchetype(['TestComponent']);
            expect(store.archetypeCount).toBe(1);

            store.reset();
            expect(store.archetypeCount).toBe(0);
        });
    });

    // ─── getDebugInfo ───────────────────────────────────────────────

    describe('getDebugInfo', () => {
        it('returns correct debug info for archetypes', () => {
            const store = new ArchetypeStore({ TestComponent, AnotherComponent });
            const archetype = store.getOrCreateArchetype(['TestComponent']).archetype;
            archetype.addEntity(1);
            archetype.addEntity(2);

            const info = store.getDebugInfo();
            expect(info).toHaveLength(1);
            expect(info[0]).toEqual({
                id: 'TestComponent',
                signature: ['TestComponent'],
                entityCount: 2,
                mask: expect.any(String),
            });
        });

        it('returns empty array when no archetypes exist', () => {
            const store = new ArchetypeStore({ TestComponent });
            store.reset();
            expect(store.getDebugInfo()).toEqual([]);
        });
    });

    // ─── _createAddSignature (tested via resolveAddComponentArchetype) ──

    describe('add signature sorting', () => {
        it('inserts at beginning when component sorts first', () => {
            const store = new ArchetypeStore({
                TestComponent,
                AnotherComponent,
            });
            const base = store.getOrCreateArchetype(['TestComponent']).archetype;
            const result = store.resolveAddComponentArchetype(base, 'AnotherComponent');

            expect(result.archetype.signature).toEqual(['AnotherComponent', 'TestComponent']);
        });

        it('inserts at end when component sorts last', () => {
            const store = new ArchetypeStore({
                TestComponent,
                AnotherComponent,
            });
            const base = store.getOrCreateArchetype(['AnotherComponent']).archetype;
            const result = store.resolveAddComponentArchetype(base, 'TestComponent');

            expect(result.archetype.signature).toEqual(['AnotherComponent', 'TestComponent']);
        });

        it('inserts in middle for multi-component archetype', () => {
            const store = new ArchetypeStore({
                TestComponent,
                AnotherComponent,
                ThirdComponent,
            });
            const base = store.getOrCreateArchetype(['AnotherComponent', 'ThirdComponent']).archetype;
            const result = store.resolveAddComponentArchetype(base, 'TestComponent');

            expect(result.archetype.signature).toEqual([
                'AnotherComponent',
                'TestComponent',
                'ThirdComponent',
            ]);
        });

        it('adds to empty signature', () => {
            const store = new ArchetypeStore({ TestComponent });
            const empty = store.getOrCreateArchetype([]).archetype;
            const result = store.resolveAddComponentArchetype(empty, 'TestComponent');

            expect(result.archetype.signature).toEqual(['TestComponent']);
        });
    });

    // ─── _createRemoveSignature (tested via resolveRemoveComponentArchetype) ──

    describe('remove signature', () => {
        it('removes from beginning', () => {
            const store = new ArchetypeStore({
                TestComponent,
                AnotherComponent,
            });
            const base = store
                .getOrCreateArchetype(['AnotherComponent', 'TestComponent'])
                .archetype;
            const result = store.resolveRemoveComponentArchetype(base, 'AnotherComponent');

            expect(result.archetype.signature).toEqual(['TestComponent']);
        });

        it('removes from end', () => {
            const store = new ArchetypeStore({
                TestComponent,
                AnotherComponent,
            });
            const base = store
                .getOrCreateArchetype(['AnotherComponent', 'TestComponent'])
                .archetype;
            const result = store.resolveRemoveComponentArchetype(base, 'TestComponent');

            expect(result.archetype.signature).toEqual(['AnotherComponent']);
        });

        it('returns empty signature when removing last component', () => {
            const store = new ArchetypeStore({ TestComponent });
            const base = store.getOrCreateArchetype(['TestComponent']).archetype;
            const result = store.resolveRemoveComponentArchetype(base, 'TestComponent');

            expect(result.archetype.signature).toEqual([]);
            expect(result.archetype.id).toBe('EMPTY');
        });
    });

    // ─── Edge cache invalidation ────────────────────────────────────

    describe('edge cache invalidation', () => {
        it('cleans up stale edge when cached archetype is removed from store', () => {
            const store = new ArchetypeStore({
                TestComponent,
                AnotherComponent,
            });

            const base = store.getOrCreateArchetype(['TestComponent']).archetype;
            store.resolveAddComponentArchetype(base, 'AnotherComponent');

            // Verify edge was cached
            expect(base.edges.get('add:AnotherComponent')).toBeDefined();

            // Reset store (clears all archetypes)
            store.reset();

            // Re-create base archetype - the edge should not point to a missing archetype
            const newBase = store.getOrCreateArchetype(['TestComponent']).archetype;
            expect(newBase.edges.get('add:AnotherComponent')).toBeUndefined();
        });
    });

    // ─── archetypeCount ─────────────────────────────────────────────

    describe('archetypeCount', () => {
        it('tracks correctly through create and reset', () => {
            const store = new ArchetypeStore({ TestComponent, AnotherComponent });
            expect(store.archetypeCount).toBe(0);

            store.getOrCreateArchetype(['TestComponent']);
            expect(store.archetypeCount).toBe(1);

            store.getOrCreateArchetype(['AnotherComponent']);
            expect(store.archetypeCount).toBe(2);

            // Re-fetching same archetype doesn't increase count
            store.getOrCreateArchetype(['TestComponent']);
            expect(store.archetypeCount).toBe(2);

            store.reset();
            expect(store.archetypeCount).toBe(0);
        });
    });
});
