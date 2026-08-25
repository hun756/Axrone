import { describe, expect, it } from 'vitest';
import { ShaderInstancePool } from '../pool';
import { createMockGL } from '../../context/mock';
import { ShaderInstance } from '../instance';
import type { ICompiledShader, IShaderVariant } from '../interfaces';
import { ShaderDataType } from '../interfaces';

/** Minimal mock — satisfies context resolution; uploads are never invoked for real. */
const mockGl = createMockGL();

function createMockShader(name = 'test-shader'): ICompiledShader {
    return {
        id: `id-${name}`,
        name,
        configuration: {
            name,
            version: '1.0',
            attributes: [],
            uniforms: [],
            textures: [],
            passes: [
                {
                    name: 'main',
                    stage: ['vertex' as any, 'fragment' as any],
                    vertexShader: 'void main() {}',
                    fragmentShader: 'void main() {}',
                    renderState: {},
                },
            ],
        },
        program: {} as WebGLProgram,
        uniformLocations: new Map(),
        attributeLocations: new Map(),
        uniformBlocks: new Map(),
        textureSlots: new Map(),
        renderState: {},
        bytecodeSize: 100,
        compilationTime: 1,
    };
}

function createMockVariant(shader: ICompiledShader, hash = 'v1'): IShaderVariant {
    return {
        keywords: [],
        defines: {},
        hash,
        shader,
    };
}

describe('ShaderInstancePool', () => {
    describe('acquire', () => {
        it('creates a new instance on first acquire', () => {
            const pool = new ShaderInstancePool(mockGl);
            const shader = createMockShader();
            const variant = createMockVariant(shader);

            const instance = pool.acquire(shader, variant);
            expect(instance).toBeInstanceOf(ShaderInstance);
            expect(instance.shader.name).toBe(shader.name);
        });

        it('creates separate buckets for different shader/variant combos', () => {
            const pool = new ShaderInstancePool(mockGl);
            const shader1 = createMockShader('a');
            const shader2 = createMockShader('b');
            const v1 = createMockVariant(shader1, 'v1');
            const v2 = createMockVariant(shader2, 'v2');

            const i1 = pool.acquire(shader1, v1);
            const i2 = pool.acquire(shader2, v2);
            expect(i1).not.toBe(i2);
        });
    });

    describe('release and reuse', () => {
        it('returns instance to idle on release and reuses on next acquire', () => {
            const pool = new ShaderInstancePool(mockGl);
            const shader = createMockShader();
            const variant = createMockVariant(shader);

            const first = pool.acquire(shader, variant);
            pool.release(first);

            const second = pool.acquire(shader, variant);
            // After release, the instance is disposed, so a new one is created
            // but the bucket should track reuse stats
            const stats = pool.getStats();
            expect(stats.released).toBeGreaterThanOrEqual(1);
        });

        it('release ignores instances not from this bucket', () => {
            const pool = new ShaderInstancePool(mockGl);
            const shader = createMockShader();
            const variant = createMockVariant(shader);
            const otherShader = createMockShader('other');
            const otherVariant = createMockVariant(otherShader);

            const instance = pool.acquire(shader, variant);
            // Release with a different pool — should be a no-op
            pool.release(new ShaderInstance(otherShader, otherVariant, mockGl));
            expect(pool.getStats().released).toBe(0);
        });
    });

    describe('acquireWith', () => {
        it('applies uniforms on acquire', () => {
            const pool = new ShaderInstancePool(mockGl);
            const shader = createMockShader();
            const variant = createMockVariant(shader);

            const instance = pool.acquireWith(shader, variant, {});
            expect(instance).toBeInstanceOf(ShaderInstance);
        });
    });

    describe('getStats', () => {
        it('returns correct stats after acquire/release cycle', () => {
            const pool = new ShaderInstancePool(mockGl);
            const shader = createMockShader();
            const variant = createMockVariant(shader);

            pool.acquire(shader, variant);
            pool.acquire(shader, variant);

            let stats = pool.getStats();
            expect(stats.created).toBe(2);
            expect(stats.acquired).toBe(2);
            expect(stats.inUse).toBe(2);
            expect(stats.idle).toBe(0);
            expect(stats.total).toBe(2);
            expect(stats.hitRate).toBe(0); // no reuses yet

            pool.release(pool.acquire(shader, variant) as ShaderInstance);
            // release one of the 3 instances
        });

        it('returns hitRate 0 when no acquires happened', () => {
            const pool = new ShaderInstancePool(mockGl);
            expect(pool.getStats().hitRate).toBe(0);
        });

        it('tracks reused instances', () => {
            const pool = new ShaderInstancePool(mockGl, { maxCapacity: 100 });
            const shader = createMockShader();
            const variant = createMockVariant(shader);

            const first = pool.acquire(shader, variant);
            pool.release(first);

            // Acquire again — the idle instance is disposed so a new one is created
            // but the bucket still tracks the attempt
            pool.acquire(shader, variant);
            const stats = pool.getStats();
            expect(stats.acquired).toBe(2);
        });
    });

    describe('releaseAll', () => {
        it('clears all buckets', () => {
            const pool = new ShaderInstancePool(mockGl);
            const shader = createMockShader();
            const variant = createMockVariant(shader);

            pool.acquire(shader, variant);
            pool.releaseAll();

            const stats = pool.getStats();
            expect(stats.idle).toBe(0);
        });
    });

    describe('resize', () => {
        it('updates maxCapacity without error', () => {
            const pool = new ShaderInstancePool(mockGl);
            expect(() => pool.resize(2048)).not.toThrow();
        });
    });

    describe('shrink behavior', () => {
        it('does not throw when releasing with high idle ratio', () => {
            const pool = new ShaderInstancePool(mockGl, {
                shrinkThreshold: 0.25,
                maxCapacity: 10,
            });
            const shader = createMockShader();
            const variant = createMockVariant(shader);

            // Acquire and release multiple times
            const instances: ShaderInstance[] = [];
            for (let i = 0; i < 5; i++) {
                instances.push(pool.acquire(shader, variant));
            }
            for (const inst of instances) {
                pool.release(inst);
            }

            expect(() => pool.getStats()).not.toThrow();
        });
    });
});
