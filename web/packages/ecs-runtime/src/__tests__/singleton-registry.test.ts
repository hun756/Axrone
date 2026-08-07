import { beforeEach, describe, expect, it } from 'vitest';
import { SingletonRegistry } from '../support/singleton-registry';

describe('SingletonRegistry', () => {
    let registry: SingletonRegistry;

    beforeEach(() => {
        registry = new SingletonRegistry();
    });

    // ─── set / get ──────────────────────────────────────────────────

    describe('set / get', () => {
        it('stores and retrieves a SingletonEntry', () => {
            const instance = { value: 42 };
            registry.set('TestComponent', 1, instance);

            const entry = registry.get('TestComponent');
            expect(entry).toBeDefined();
            expect(entry?.entity).toBe(1);
            expect(entry?.instance).toBe(instance);
        });

        it('returns undefined for unknown component name', () => {
            expect(registry.get('Unknown')).toBeUndefined();
        });

        it('overwrites existing entry for same component name', () => {
            registry.set('TestComponent', 1, { value: 'first' });
            registry.set('TestComponent', 2, { value: 'second' });

            const entry = registry.get('TestComponent');
            expect(entry?.entity).toBe(2);
            expect(entry?.instance).toEqual({ value: 'second' });
        });
    });

    // ─── getEntity ──────────────────────────────────────────────────

    describe('getEntity', () => {
        it('returns entity for registered component', () => {
            registry.set('TestComponent', 5, {});
            expect(registry.getEntity('TestComponent')).toBe(5);
        });

        it('returns undefined for unknown component', () => {
            expect(registry.getEntity('Unknown')).toBeUndefined();
        });
    });

    // ─── clearComponent ─────────────────────────────────────────────

    describe('clearComponent', () => {
        it('removes entry when entity matches', () => {
            registry.set('TestComponent', 1, {});
            registry.clearComponent('TestComponent', 1);

            expect(registry.get('TestComponent')).toBeUndefined();
        });

        it('does nothing when entity does not match', () => {
            registry.set('TestComponent', 1, {});
            registry.clearComponent('TestComponent', 999);

            expect(registry.get('TestComponent')).toBeDefined();
        });

        it('is safe for unknown component names', () => {
            expect(() => registry.clearComponent('Unknown', 1)).not.toThrow();
        });
    });

    // ─── clearEntity ────────────────────────────────────────────────

    describe('clearEntity', () => {
        it('clears all components for the given entity', () => {
            registry.set('CompA', 1, {});
            registry.set('CompB', 1, {});
            registry.set('CompC', 2, {});

            registry.clearEntity(1, ['CompA', 'CompB']);

            expect(registry.get('CompA')).toBeUndefined();
            expect(registry.get('CompB')).toBeUndefined();
            expect(registry.get('CompC')).toBeDefined();
        });

        it('skips components owned by different entities', () => {
            registry.set('CompA', 1, {});
            registry.set('CompB', 2, {});

            registry.clearEntity(1, ['CompA', 'CompB']);

            expect(registry.get('CompA')).toBeUndefined();
            expect(registry.get('CompB')).toBeDefined();
        });
    });

    // ─── clear ──────────────────────────────────────────────────────

    describe('clear', () => {
        it('clears all entries', () => {
            registry.set('CompA', 1, {});
            registry.set('CompB', 2, {});

            registry.clear();

            expect(registry.get('CompA')).toBeUndefined();
            expect(registry.get('CompB')).toBeUndefined();
        });

        it('is a no-op on empty registry', () => {
            expect(() => registry.clear()).not.toThrow();
        });
    });
});
