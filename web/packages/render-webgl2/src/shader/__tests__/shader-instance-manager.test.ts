import { describe, expect, it } from 'vitest';
import { ShaderInstance } from '../instance';
import { createMockGL } from '../../context/mock';
import type { ICompiledShader, IShaderVariant } from '../interfaces';

/** Minimal truthy mock — satisfies assertContext but is never called for upload. */
const mockGl = createMockGL();

function createMockShader(name = 'test-shader'): ICompiledShader {
    return {
        id: `id-${name}`,
        name,
        configuration: {
            name,
            version: '1.0',
            attributes: [],
            uniforms: [
                {
                    name: 'u_color',
                    type: 'vec3' as any,
                    qualifier: 'uniform' as any,
                    category: 'material',
                },
                {
                    name: 'u_opacity',
                    type: 'float' as any,
                    qualifier: 'uniform' as any,
                    category: 'material',
                    defaultValue: 1.0,
                },
            ],
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

describe('ShaderInstance (Manager)', () => {
    describe('constructor', () => {
        it('creates an instance with shader and variant references', () => {
            const shader = createMockShader();
            const variant = createMockVariant(shader);
            const instance = new ShaderInstance(shader, variant, mockGl);
            expect(instance.shader).toBe(shader);
            expect(instance.variant).toBe(variant);
        });

        it('initializes default uniform values', () => {
            const shader = createMockShader();
            const variant = createMockVariant(shader);
            const instance = new ShaderInstance(shader, variant, mockGl);
            expect(instance.getUniform('u_opacity')).toBe(1.0);
        });
    });

    describe('hasUniform', () => {
        it('returns true for declared uniforms', () => {
            const shader = createMockShader();
            const variant = createMockVariant(shader);
            const instance = new ShaderInstance(shader, variant, mockGl);
            expect(instance.hasUniform('u_color')).toBe(true);
            expect(instance.hasUniform('u_opacity')).toBe(true);
        });

        it('returns false for unknown uniforms', () => {
            const shader = createMockShader();
            const variant = createMockVariant(shader);
            const instance = new ShaderInstance(shader, variant, mockGl);
            expect(instance.hasUniform('u_nonexistent')).toBe(false);
        });
    });

    describe('getUniform', () => {
        it('returns null for uniforms without values', () => {
            const shader = createMockShader();
            const variant = createMockVariant(shader);
            const instance = new ShaderInstance(shader, variant, mockGl);
            expect(instance.getUniform('u_color')).toBeNull();
        });

        it('returns default value when set', () => {
            const shader = createMockShader();
            const variant = createMockVariant(shader);
            const instance = new ShaderInstance(shader, variant, mockGl);
            expect(instance.getUniform('u_opacity')).toBe(1.0);
        });
    });

    describe('dispose', () => {
        it('marks instance as disposed', () => {
            const shader = createMockShader();
            const variant = createMockVariant(shader);
            const instance = new ShaderInstance(shader, variant, mockGl);
            expect(instance.isDisposed()).toBe(false);
            instance.dispose();
            expect(instance.isDisposed()).toBe(true);
        });

        it('is idempotent', () => {
            const shader = createMockShader();
            const variant = createMockVariant(shader);
            const instance = new ShaderInstance(shader, variant, mockGl);
            instance.dispose();
            expect(() => instance.dispose()).not.toThrow();
        });

        it('clears uniforms and textures', () => {
            const shader = createMockShader();
            const variant = createMockVariant(shader);
            const instance = new ShaderInstance(shader, variant, mockGl);
            instance.dispose();
            expect(instance.uniforms.size).toBe(0);
            expect(instance.textures.size).toBe(0);
        });
    });

    describe('getStats', () => {
        it('returns initial stats', () => {
            const shader = createMockShader();
            const variant = createMockVariant(shader);
            const instance = new ShaderInstance(shader, variant, mockGl);
            const stats = instance.getStats();
            expect(stats.uniformUpdateCount).toBe(0);
            expect(stats.dirtyUniforms).toBe(0);
            expect(stats.programBinds).toBe(0);
        });
    });

    describe('getUniformNames / getAttributeNames', () => {
        it('returns declared uniform names', () => {
            const shader = createMockShader();
            const variant = createMockVariant(shader);
            const instance = new ShaderInstance(shader, variant, mockGl);
            const names = instance.getUniformNames();
            expect(names).toContain('u_color');
            expect(names).toContain('u_opacity');
        });
    });

    describe('toString', () => {
        it('returns descriptive string', () => {
            const shader = createMockShader('my-shader');
            const variant = createMockVariant(shader, 'abc');
            const instance = new ShaderInstance(shader, variant, mockGl);
            expect(instance.toString()).toContain('my-shader');
            expect(instance.toString()).toContain('abc');
        });
    });

    describe('static isShaderInstance', () => {
        it('returns true for ShaderInstance', () => {
            const shader = createMockShader();
            const variant = createMockVariant(shader);
            const instance = new ShaderInstance(shader, variant, mockGl);
            expect(ShaderInstance.isShaderInstance(instance)).toBe(true);
        });

        it('returns false for non-instances', () => {
            expect(ShaderInstance.isShaderInstance({})).toBe(false);
            expect(ShaderInstance.isShaderInstance(null)).toBe(false);
        });
    });

    describe('describe', () => {
        it('returns frozen description', () => {
            const shader = createMockShader();
            const variant = createMockVariant(shader);
            const instance = new ShaderInstance(shader, variant, mockGl);
            const desc = instance.describe();
            expect(Object.isFrozen(desc)).toBe(true);
            expect(desc.shader).toBe(shader.name);
            expect(desc.variant).toBe(variant.hash);
            expect(Array.isArray(desc.uniforms)).toBe(true);
        });
    });
});
