import { describe, expect, it, vi } from 'vitest';
import {
    generateSceneShaderVariantKey,
    SceneShaderVariantResolver,
    type SceneShaderResource,
} from '@axrone/scene-3d';

const createMockResource = (id: string): SceneShaderResource =>
    ({
        id,
        program: { id } as unknown as WebGLProgram,
        uniformLocations: new Map(),
        uniformTypes: new Map(),
        uniformNames: [],
        attributeNames: {},
        depthTest: true,
        cull: true,
        blend: false,
    }) as SceneShaderResource;

describe('generateSceneShaderVariantKey', () => {
    it('returns shaderId when no keywords', () => {
        expect(generateSceneShaderVariantKey('basic', [])).toBe('basic');
    });

    it('returns composite key with sorted keywords', () => {
        expect(generateSceneShaderVariantKey('basic', ['FOG', 'ALBEDO_MAP'])).toBe(
            'basic:ALBEDO_MAP,FOG'
        );
    });

    it('sorts keywords deterministically', () => {
        const key1 = generateSceneShaderVariantKey('s', ['Z', 'A', 'M']);
        const key2 = generateSceneShaderVariantKey('s', ['A', 'M', 'Z']);
        expect(key1).toBe(key2);
        expect(key1).toBe('s:A,M,Z');
    });
});

describe('SceneShaderVariantResolver', () => {
    it('returns base shader when no keywords', () => {
        const baseResource = createMockResource('basic');
        const resolver = new SceneShaderVariantResolver({
            shaders: {
                get: (id) => (id === 'basic' ? baseResource : undefined),
                getDefinition: () => undefined,
                getVariant: () => undefined,
                registerVariant: () => {},
            },
            compileVariant: vi.fn(),
        });

        expect(resolver.resolve('basic', [])).toBe(baseResource);
    });

    it('returns cached variant from registry', () => {
        const variantResource = createMockResource('basic:FOG');
        const resolver = new SceneShaderVariantResolver({
            shaders: {
                get: () => undefined,
                getDefinition: () => ({ id: 'basic', vertexSource: '', fragmentSource: '' }),
                getVariant: (shaderId, variantKey) =>
                    shaderId === 'basic' && variantKey === 'basic:FOG'
                        ? variantResource
                        : undefined,
                registerVariant: vi.fn(),
            },
            compileVariant: vi.fn(),
        });

        expect(resolver.resolve('basic', ['FOG'])).toBe(variantResource);
    });

    it('compiles and caches new variant on cache miss', () => {
        const definition = { id: 'basic', vertexSource: '', fragmentSource: '' };
        const compiledVariant = createMockResource('basic:FOG');
        const registerVariant = vi.fn().mockReturnValue(true);
        const compileVariant = vi.fn().mockReturnValue(compiledVariant);

        const resolver = new SceneShaderVariantResolver({
            shaders: {
                get: () => undefined,
                getDefinition: () => definition,
                getVariant: () => undefined,
                registerVariant,
            },
            compileVariant,
        });

        const result = resolver.resolve('basic', ['FOG']);

        expect(result).toBe(compiledVariant);
        expect(compileVariant).toHaveBeenCalledWith(definition, ['FOG']);
        expect(registerVariant).toHaveBeenCalledWith(
            'basic',
            'basic:FOG',
            compiledVariant
        );
    });

    it('returns undefined when base shader definition is not found', () => {
        const resolver = new SceneShaderVariantResolver({
            shaders: {
                get: () => undefined,
                getDefinition: () => undefined,
                getVariant: () => undefined,
                registerVariant: vi.fn(),
            },
            compileVariant: vi.fn(),
        });

        expect(resolver.resolve('nonexistent', ['FOG'])).toBeUndefined();
    });

    it('returns undefined for base shader when no resolver and no base', () => {
        const resolver = new SceneShaderVariantResolver({
            shaders: {
                get: () => undefined,
                getDefinition: () => undefined,
                getVariant: () => undefined,
                registerVariant: vi.fn(),
            },
            compileVariant: vi.fn(),
        });

        expect(resolver.resolve('missing', [])).toBeUndefined();
    });
});
