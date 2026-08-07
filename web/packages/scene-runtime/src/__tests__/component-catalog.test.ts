import { describe, expect, it } from 'vitest';
import { Component, script } from '@axrone/ecs-runtime';
import { SceneComponentCatalog, getSceneComponentTypeName } from '../component-catalog';

@script({ scriptName: 'TestAlpha' })
class TestAlpha extends Component {}

@script({ scriptName: 'TestBeta' })
class TestBeta extends Component {}

class UnnamedComponent extends Component {}

describe('getSceneComponentTypeName', () => {
    it('returns scriptName when @script decorator is present', () => {
        expect(getSceneComponentTypeName(TestAlpha)).toBe('TestAlpha');
    });

    it('falls back to class name when no @script decorator', () => {
        expect(getSceneComponentTypeName(UnnamedComponent)).toBe('UnnamedComponent');
    });
});

describe('SceneComponentCatalog', () => {
    it('registers and retrieves a component by scriptName', () => {
        const catalog = new SceneComponentCatalog();
        catalog.register(TestAlpha);
        expect(catalog.get('TestAlpha')).toBe(TestAlpha);
    });

    it('returns undefined for unregistered names', () => {
        const catalog = new SceneComponentCatalog();
        expect(catalog.get('NonExistent')).toBeUndefined();
    });

    it('reports has() correctly', () => {
        const catalog = new SceneComponentCatalog();
        catalog.register(TestAlpha);
        expect(catalog.has('TestAlpha')).toBe(true);
        expect(catalog.has('TestBeta')).toBe(false);
    });

    it('lists all registered names', () => {
        const catalog = new SceneComponentCatalog();
        catalog.register(TestAlpha);
        catalog.register(TestBeta);
        const names = catalog.names();
        expect(names).toContain('TestAlpha');
        expect(names).toContain('TestBeta');
        expect(names).toHaveLength(2);
    });

    it('registerAll registers multiple components at once', () => {
        const catalog = new SceneComponentCatalog();
        catalog.registerAll([TestAlpha, TestBeta]);
        expect(catalog.has('TestAlpha')).toBe(true);
        expect(catalog.has('TestBeta')).toBe(true);
    });

    it('registerAll returns this for chaining', () => {
        const catalog = new SceneComponentCatalog();
        const result = catalog.registerAll([TestAlpha]);
        expect(result).toBe(catalog);
    });

    it('constructor accepts a ComponentRegistry map', () => {
        const catalog = new SceneComponentCatalog({
            alpha: TestAlpha,
            beta: TestBeta,
        });
        expect(catalog.has('TestAlpha')).toBe(true);
        expect(catalog.has('TestBeta')).toBe(true);
    });

    it('overwrites previous registration for same scriptName', () => {
        vi.spyOn(console, 'warn').mockImplementation(() => {});

        @script({ scriptName: 'Shared' })
        class First extends Component {}

        @script({ scriptName: 'Shared' })
        class Second extends Component {}

        const catalog = new SceneComponentCatalog();
        catalog.register(First);
        catalog.register(Second);
        expect(catalog.get('Shared')).toBe(Second);
    });

    it('getName returns scriptName for decorated components', () => {
        const catalog = new SceneComponentCatalog();
        expect(catalog.getName(TestAlpha)).toBe('TestAlpha');
    });

    it('getName falls back to class name for undecorated components', () => {
        const catalog = new SceneComponentCatalog();
        expect(catalog.getName(UnnamedComponent)).toBe('UnnamedComponent');
    });
});
