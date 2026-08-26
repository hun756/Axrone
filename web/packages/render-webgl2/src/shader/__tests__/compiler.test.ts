import { describe, expect, it } from 'vitest';
import { WebGLShaderCompiler } from '../compiler';
import type { IShaderConfiguration } from '../interfaces';
import { ShaderStage } from '../interfaces';

function createMinimalGL(): WebGL2RenderingContext {
    const programs: any[] = [];
    const shaders: any[] = [];

    const gl = new Proxy(
        {
            VERTEX_SHADER: 0x8b31,
            FRAGMENT_SHADER: 0x8b30,
            COMPILE_STATUS: 0x8b81,
            LINK_STATUS: 0x8b82,
            INVALID_INDEX: 0xffffffff,
            _programs: programs,
            _shaders: shaders,
            createProgram() {
                const p = { _type: 'program' };
                programs.push(p);
                return p;
            },
            createShader() {
                const s = { _type: 'shader' };
                shaders.push(s);
                return s;
            },
            shaderSource() {},
            compileShader() {},
            getShaderParameter() { return true; },
            getShaderInfoLog() { return ''; },
            attachShader() {},
            linkProgram() {},
            getProgramParameter() { return true; },
            getProgramInfoLog() { return ''; },
            deleteProgram() {},
            deleteShader() {},
            getUniformLocation() { return {}; },
            getAttribLocation() { return 0; },
            getUniformBlockIndex() { return 0; },
            getAttachedShaders() { return []; },
            getShaderSource() { return 'source'; },
            useProgram() {},
        },
        {
            get: (target, property) => {
                if (property in target) return (target as any)[property];
                return 0;
            },
        }
    );
    return gl as unknown as WebGL2RenderingContext;
}

function createValidConfig(): IShaderConfiguration {
    return {
        name: 'test-shader',
        version: '1.0',
        attributes: [
            {
                name: 'a_position',
                type: 'vec3' as any,
                qualifier: 'in' as any,
                binding: 0,
            },
        ],
        uniforms: [
            {
                name: 'u_modelViewProjection',
                type: 'mat4' as any,
                qualifier: 'uniform' as any,
            },
        ],
        textures: [
            {
                name: 'u_diffuseMap',
                type: 'sampler2D' as any,
                slot: 0,
            },
        ],
        passes: [
            {
                name: 'main',
                stage: [ShaderStage.VERTEX, ShaderStage.FRAGMENT],
                vertexShader: 'void main() { gl_Position = vec4(0); }',
                fragmentShader: 'out vec4 o_FragColor; void main() { o_FragColor = vec4(1); }',
                renderState: {},
            },
        ],
    };
}

describe('WebGLShaderCompiler', () => {
    describe('validateConfiguration', () => {
        it('returns valid for a correct configuration', () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);
            const config = createValidConfig();
            const result = compiler.validateConfiguration(config);
            expect(result.isValid).toBe(true);
            expect(result.errors).toHaveLength(0);
        });

        it('reports error when name is missing', () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);
            const config = createValidConfig();
            const invalid = { ...config, name: '' };
            const result = compiler.validateConfiguration(invalid);
            expect(result.isValid).toBe(false);
            expect(result.errors.some((e) => e.includes('name'))).toBe(true);
        });

        it('reports error when passes are empty', () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);
            const config = createValidConfig();
            const invalid = { ...config, passes: [] };
            const result = compiler.validateConfiguration(invalid);
            expect(result.isValid).toBe(false);
            expect(result.errors.some((e) => e.includes('pass'))).toBe(true);
        });

        it('reports error for duplicate attribute bindings', () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);
            const config = createValidConfig();
            const invalid = {
                ...config,
                attributes: [
                    { name: 'a_pos', type: 'vec3' as any, qualifier: 'in' as any, binding: 0 },
                    { name: 'a_norm', type: 'vec3' as any, qualifier: 'in' as any, binding: 0 },
                ],
            };
            const result = compiler.validateConfiguration(invalid);
            expect(result.isValid).toBe(false);
            expect(result.errors.some((e) => e.includes('already used'))).toBe(true);
        });

        it('reports error for duplicate uniform names', () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);
            const config = createValidConfig();
            const invalid = {
                ...config,
                uniforms: [
                    { name: 'u_color', type: 'vec3' as any, qualifier: 'uniform' as any },
                    { name: 'u_color', type: 'vec3' as any, qualifier: 'uniform' as any },
                ],
            };
            const result = compiler.validateConfiguration(invalid);
            expect(result.isValid).toBe(false);
            expect(result.errors.some((e) => e.includes('already used'))).toBe(true);
        });

        it('reports error for duplicate texture slots', () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);
            const config = createValidConfig();
            const invalid = {
                ...config,
                textures: [
                    { name: 'u_tex1', type: 'sampler2D' as any, slot: 0 },
                    { name: 'u_tex2', type: 'sampler2D' as any, slot: 0 },
                ],
            };
            const result = compiler.validateConfiguration(invalid);
            expect(result.isValid).toBe(false);
            expect(result.errors.some((e) => e.includes('Slot'))).toBe(true);
        });

        it('reports error when vertex shader source is missing', () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);
            const config = createValidConfig();
            const invalid = {
                ...config,
                passes: [{
                    name: 'main',
                    stage: [ShaderStage.VERTEX, ShaderStage.FRAGMENT],
                    vertexShader: '',
                    fragmentShader: 'void main() {}',
                    renderState: {},
                }],
            };
            const result = compiler.validateConfiguration(invalid);
            expect(result.isValid).toBe(false);
            expect(result.errors.some((e) => e.includes('Vertex shader'))).toBe(true);
        });
    });

    describe('getCacheStats', () => {
        it('returns zero stats initially', () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);
            const stats = compiler.getCacheStats();
            expect(stats.compiledShaders).toBe(0);
            expect(stats.variants).toBe(0);
        });
    });

    describe('clearCache', () => {
        it('clears without error when empty', () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);
            expect(() => compiler.clearCache()).not.toThrow();
        });
    });
});
