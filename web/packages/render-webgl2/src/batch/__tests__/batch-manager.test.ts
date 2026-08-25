import { describe, expect, it } from 'vitest';
import { BatchManager } from '../batch-manager';
import { createMockGL } from '../../context/mock';

function createMinimalGL(): WebGL2RenderingContext {
    return createMockGL({
        parameters: {
            0x0d33: 16384,
        },
    });
}

describe('BatchManager', () => {
    describe('constructor', () => {
        it('creates with default configuration', () => {
            const gl = createMinimalGL();
            const manager = new BatchManager(gl);
            const stats = manager.getStats();
            expect(stats.totalRenderers).toBe(0);
            expect(stats.totalBatches).toBe(0);
        });

        it('creates with custom configuration', () => {
            const gl = createMinimalGL();
            const manager = new BatchManager(gl, {
                maxBatchSize: 512,
                maxRenderers: 8,
                enableInstancing: true,
            });
            expect(manager.getStats().totalRenderers).toBe(0);
        });
    });

    describe('createRenderer', () => {
        it('creates a new renderer', () => {
            const gl = createMinimalGL();
            const manager = new BatchManager(gl);
            const renderer = manager.createRenderer();
            expect(renderer).toBeDefined();
            expect(renderer.maxBatchSize).toBe(1024);
        });

        it('creates renderer with custom batch size', () => {
            const gl = createMinimalGL();
            const manager = new BatchManager(gl);
            const renderer = manager.createRenderer(256);
            expect(renderer.maxBatchSize).toBe(256);
        });

        it('increments renderer count in stats', () => {
            const gl = createMinimalGL();
            const manager = new BatchManager(gl);
            manager.createRenderer();
            expect(manager.getStats().totalRenderers).toBe(1);
        });

        it('throws when max renderers exceeded', () => {
            const gl = createMinimalGL();
            const manager = new BatchManager(gl, { maxRenderers: 2 });
            manager.createRenderer();
            manager.createRenderer();
            expect(() => manager.createRenderer()).toThrow(/Maximum/);
        });

        it('throws after dispose', () => {
            const gl = createMinimalGL();
            const manager = new BatchManager(gl);
            manager.dispose();
            expect(() => manager.createRenderer()).toThrow(/disposed/);
        });
    });

    describe('getStats', () => {
        it('returns zero stats after dispose', () => {
            const gl = createMinimalGL();
            const manager = new BatchManager(gl);
            manager.createRenderer();
            manager.dispose();
            const stats = manager.getStats();
            expect(stats.totalRenderers).toBe(0);
            expect(stats.totalBatches).toBe(0);
            expect(stats.memoryUsage).toBe(0);
        });

        it('calculates memory usage', () => {
            const gl = createMinimalGL();
            const manager = new BatchManager(gl);
            manager.createRenderer();
            const stats = manager.getStats();
            expect(stats.memoryUsage).toBeGreaterThanOrEqual(0);
        });
    });

    describe('optimizeBatches', () => {
        it('does not throw when called repeatedly', () => {
            const gl = createMinimalGL();
            const manager = new BatchManager(gl);
            manager.createRenderer();
            expect(() => {
                for (let i = 0; i < 65; i++) {
                    manager.optimizeBatches();
                }
            }).not.toThrow();
        });

        it('is a no-op after dispose', () => {
            const gl = createMinimalGL();
            const manager = new BatchManager(gl);
            manager.dispose();
            expect(() => manager.optimizeBatches()).not.toThrow();
        });
    });

    describe('dispose', () => {
        it('is idempotent', () => {
            const gl = createMinimalGL();
            const manager = new BatchManager(gl);
            manager.dispose();
            expect(() => manager.dispose()).not.toThrow();
        });

        it('clears all renderers', () => {
            const gl = createMinimalGL();
            const manager = new BatchManager(gl);
            manager.createRenderer();
            manager.createRenderer();
            manager.dispose();
            expect(manager.getStats().totalRenderers).toBe(0);
        });
    });

    describe('getBestRenderer', () => {
        it('returns null after dispose', () => {
            const gl = createMinimalGL();
            const manager = new BatchManager(gl);
            manager.dispose();
            const mockMaterial = {
                shader: {
                    shader: { name: 'test' },
                },
                getProperty: () => 'opaque',
            } as any;
            expect(manager.getBestRenderer(mockMaterial)).toBeNull();
        });

        it('creates a new renderer when none exist', () => {
            const gl = createMinimalGL();
            const manager = new BatchManager(gl);
            const mockMaterial = {
                shader: {
                    shader: { name: 'test' },
                },
                getProperty: () => 'opaque',
            } as any;
            const renderer = manager.getBestRenderer(mockMaterial);
            expect(renderer).not.toBeNull();
        });
    });
});
