import { describe, expect, it } from 'vitest';
import {
    createCircleShape,
    createRectangleShape,
    ShapeRegistry,
    ShapeRegistryError,
} from '../index';

describe('@axrone/shapes-2d registry (comprehensive)', () => {
    describe('constructor', () => {
        it('creates with default options', () => {
            const registry = new ShapeRegistry();
            expect(registry.stats.shapeCount).toBe(0);
            expect(registry.stats.disposed).toBe(false);
            registry.dispose();
        });

        it('creates with custom options', () => {
            const registry = new ShapeRegistry({ maxShapes: 4, maxCompiledEntries: 2 });
            expect(registry.stats.shapeCount).toBe(0);
            registry.dispose();
        });

        it('clamps maxShapes to minimum 1', () => {
            const registry = new ShapeRegistry({ maxShapes: 0 });
            // Should still be able to register at least 1
            const id = registry.register(createRectangleShape({ x: 0, y: 0, width: 1, height: 1 }));
            expect(id).toBeTruthy();
            registry.dispose();
        });
    });

    describe('stats', () => {
        it('reflects initial state', () => {
            const registry = new ShapeRegistry();
            const stats = registry.stats;
            expect(stats.shapeCount).toBe(0);
            expect(stats.fingerprintCount).toBe(0);
            expect(stats.compiledCount).toBe(0);
            expect(stats.disposed).toBe(false);
            registry.dispose();
        });

        it('reflects after register', () => {
            const registry = new ShapeRegistry();
            registry.register(createRectangleShape({ x: 0, y: 0, width: 1, height: 1 }));
            expect(registry.stats.shapeCount).toBe(1);
            expect(registry.stats.fingerprintCount).toBe(1);
            registry.dispose();
        });

        it('reflects after compile', () => {
            const registry = new ShapeRegistry();
            const id = registry.register(createRectangleShape({ x: 0, y: 0, width: 1, height: 1 }));
            registry.compile(id);
            expect(registry.stats.compiledCount).toBe(1);
            registry.dispose();
        });

        it('reflects disposed state', () => {
            const registry = new ShapeRegistry();
            registry.dispose();
            expect(registry.stats.disposed).toBe(true);
        });
    });

    describe('has / get', () => {
        it('has returns true for registered shape', () => {
            const registry = new ShapeRegistry();
            const id = registry.register(createRectangleShape({ x: 0, y: 0, width: 1, height: 1 }));
            expect(registry.has(id)).toBe(true);
            registry.dispose();
        });

        it('has returns false for unknown id', () => {
            const registry = new ShapeRegistry();
            expect(registry.has('shape_999' as any)).toBe(false);
            registry.dispose();
        });

        it('get returns the shape', () => {
            const registry = new ShapeRegistry();
            const shape = createRectangleShape({ x: 0, y: 0, width: 5, height: 5 });
            const id = registry.register(shape);
            expect(registry.get(id)).toBe(shape);
            registry.dispose();
        });

        it('get returns null for unknown id', () => {
            const registry = new ShapeRegistry();
            expect(registry.get('shape_999' as any)).toBeNull();
            registry.dispose();
        });
    });

    describe('register', () => {
        it('returns a ShapeId string', () => {
            const registry = new ShapeRegistry();
            const id = registry.register(createRectangleShape({ x: 0, y: 0, width: 1, height: 1 }));
            expect(id).toMatch(/^shape_\d+$/);
            registry.dispose();
        });

        it('deduplicates identical shapes by fingerprint', () => {
            const registry = new ShapeRegistry();
            const shapeA = createRectangleShape({ x: 0, y: 0, width: 1, height: 1 });
            const shapeB = createRectangleShape({ x: 0, y: 0, width: 1, height: 1 });
            const idA = registry.register(shapeA);
            const idB = registry.register(shapeB);
            expect(idA).toBe(idB);
            expect(registry.stats.shapeCount).toBe(1);
            registry.dispose();
        });

        it('assigns different ids to different shapes', () => {
            const registry = new ShapeRegistry();
            const idA = registry.register(createRectangleShape({ x: 0, y: 0, width: 1, height: 1 }));
            const idB = registry.register(createRectangleShape({ x: 0, y: 0, width: 2, height: 2 }));
            expect(idA).not.toBe(idB);
            registry.dispose();
        });

        it('throws CAPACITY_EXCEEDED when full', () => {
            const registry = new ShapeRegistry({ maxShapes: 2 });
            registry.register(createRectangleShape({ x: 0, y: 0, width: 1, height: 1 }));
            registry.register(createRectangleShape({ x: 0, y: 0, width: 2, height: 2 }));
            expect(() =>
                registry.register(createRectangleShape({ x: 0, y: 0, width: 3, height: 3 }))
            ).toThrow(ShapeRegistryError);
            registry.dispose();
        });
    });

    describe('unregister', () => {
        it('returns true for existing shape', () => {
            const registry = new ShapeRegistry();
            const id = registry.register(createRectangleShape({ x: 0, y: 0, width: 1, height: 1 }));
            expect(registry.unregister(id)).toBe(true);
            expect(registry.has(id)).toBe(false);
            expect(registry.stats.shapeCount).toBe(0);
            registry.dispose();
        });

        it('returns false for unknown id', () => {
            const registry = new ShapeRegistry();
            expect(registry.unregister('shape_999' as any)).toBe(false);
            registry.dispose();
        });

        it('clears compiled cache entries for unregistered shape', () => {
            const registry = new ShapeRegistry();
            const id = registry.register(createRectangleShape({ x: 0, y: 0, width: 1, height: 1 }));
            registry.compile(id);
            expect(registry.stats.compiledCount).toBe(1);
            registry.unregister(id);
            expect(registry.stats.compiledCount).toBe(0);
            registry.dispose();
        });
    });

    describe('compile', () => {
        it('compiles by id', () => {
            const registry = new ShapeRegistry();
            const id = registry.register(createRectangleShape({ x: 0, y: 0, width: 10, height: 5 }));
            const compiled = registry.compile(id);
            expect(compiled.shape.kind).toBe('rectangle');
            expect(compiled.fingerprint.startsWith('rectangle:')).toBe(true);
            expect(compiled.area).toBe(50);
            registry.dispose();
        });

        it('compiles by shape reference', () => {
            const registry = new ShapeRegistry();
            const shape = createRectangleShape({ x: 0, y: 0, width: 10, height: 5 });
            const compiled = registry.compile(shape);
            expect(compiled.shape.kind).toBe('rectangle');
            registry.dispose();
        });

        it('returns cached result on second call', () => {
            const registry = new ShapeRegistry();
            const id = registry.register(createRectangleShape({ x: 0, y: 0, width: 10, height: 5 }));
            const first = registry.compile(id);
            const second = registry.compile(id);
            expect(first).toBe(second);
            registry.dispose();
        });

        it('caches separately for different options', () => {
            const registry = new ShapeRegistry({ maxCompiledEntries: 10 });
            const id = registry.register(createRectangleShape({ x: 0, y: 0, width: 10, height: 5 }));
            const a = registry.compile(id, { curveTolerance: 0.1 });
            const b = registry.compile(id, { curveTolerance: 0.5 });
            expect(a).not.toBe(b);
            expect(registry.stats.compiledCount).toBe(2);
            registry.dispose();
        });

        it('evicts LRU entries when maxCompiledEntries exceeded', () => {
            const registry = new ShapeRegistry({ maxCompiledEntries: 2 });
            const id1 = registry.register(createRectangleShape({ x: 0, y: 0, width: 1, height: 1 }));
            const id2 = registry.register(createRectangleShape({ x: 0, y: 0, width: 2, height: 2 }));
            const id3 = registry.register(createRectangleShape({ x: 0, y: 0, width: 3, height: 3 }));
            registry.compile(id1);
            registry.compile(id2);
            registry.compile(id3); // should evict id1
            expect(registry.stats.compiledCount).toBe(2);
            registry.dispose();
        });

        it('throws for unknown id', () => {
            const registry = new ShapeRegistry();
            expect(() => registry.compile('shape_999' as any)).toThrow(ShapeRegistryError);
            registry.dispose();
        });
    });

    describe('serialize', () => {
        it('serializes by id', () => {
            const registry = new ShapeRegistry();
            const id = registry.register(createRectangleShape({ x: 0, y: 0, width: 10, height: 5 }));
            const serialized = registry.serialize(id);
            expect(serialized.type).toBe('shape/rectangle');
            registry.dispose();
        });

        it('serializes by shape reference', () => {
            const registry = new ShapeRegistry();
            const shape = createCircleShape({ cx: 0, cy: 0, radius: 5 });
            const serialized = registry.serialize(shape);
            expect(serialized.type).toBe('shape/circle');
            registry.dispose();
        });
    });

    describe('clear', () => {
        it('removes all shapes, fingerprints, and compiled entries', () => {
            const registry = new ShapeRegistry();
            const id = registry.register(createRectangleShape({ x: 0, y: 0, width: 1, height: 1 }));
            registry.compile(id);
            registry.clear();
            expect(registry.stats.shapeCount).toBe(0);
            expect(registry.stats.fingerprintCount).toBe(0);
            expect(registry.stats.compiledCount).toBe(0);
            expect(registry.has(id)).toBe(false);
            registry.dispose();
        });
    });

    describe('dispose', () => {
        it('is idempotent', () => {
            const registry = new ShapeRegistry();
            registry.dispose();
            expect(() => registry.dispose()).not.toThrow();
        });

        it('throws on get after dispose', () => {
            const registry = new ShapeRegistry();
            const id = registry.register(createRectangleShape({ x: 0, y: 0, width: 1, height: 1 }));
            registry.dispose();
            expect(() => registry.get(id)).toThrow(ShapeRegistryError);
        });

        it('throws on register after dispose', () => {
            const registry = new ShapeRegistry();
            registry.dispose();
            expect(() =>
                registry.register(createRectangleShape({ x: 0, y: 0, width: 1, height: 1 }))
            ).toThrow(ShapeRegistryError);
        });

        it('throws on compile after dispose', () => {
            const registry = new ShapeRegistry();
            registry.dispose();
            expect(() =>
                registry.compile(createRectangleShape({ x: 0, y: 0, width: 1, height: 1 }))
            ).toThrow(ShapeRegistryError);
        });

        it('throws on serialize after dispose', () => {
            const registry = new ShapeRegistry();
            registry.dispose();
            expect(() =>
                registry.serialize(createRectangleShape({ x: 0, y: 0, width: 1, height: 1 }))
            ).toThrow(ShapeRegistryError);
        });

        it('throws on unregister after dispose', () => {
            const registry = new ShapeRegistry();
            registry.dispose();
            expect(() => registry.unregister('shape_1' as any)).toThrow(ShapeRegistryError);
        });

        it('has returns false after dispose (no throw)', () => {
            const registry = new ShapeRegistry();
            const id = registry.register(createRectangleShape({ x: 0, y: 0, width: 1, height: 1 }));
            registry.dispose();
            // has() does not call ensureActive(), so it returns false instead of throwing
            expect(registry.has(id)).toBe(false);
        });
    });

    describe('Symbol.dispose', () => {
        it('calls dispose via Symbol.dispose', () => {
            const registry = new ShapeRegistry();
            registry[Symbol.dispose]();
            expect(registry.stats.disposed).toBe(true);
        });
    });
});
