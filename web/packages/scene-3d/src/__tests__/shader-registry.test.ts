import { describe, expect, it } from 'vitest';
import {
    cloneSceneShaderDefinition,
    SceneShaderRegistry,
    type SceneShaderResource,
} from '@axrone/scene-3d';

const createShaderResource = (id: string): SceneShaderResource => ({
    id,
    program: { id } as unknown as WebGLProgram,
    uniformLocations: new Map(),
    uniformTypes: new Map(),
    uniformNames: ['u_Model', 'u_View'],
    attributeNames: {
        position: 'a_Position',
        normal: 'a_Normal',
        uv0: 'a_UV0',
        uv1: 'a_UV1',
        tangent: 'a_Tangent',
        color0: 'a_Color0',
        joints0: 'a_Joints0',
        weights0: 'a_Weights0',
    },
    depthTest: true,
    cull: true,
    blend: false,
});

describe('SceneShaderRegistry', () => {
    it('stores shader resources and returns handles', () => {
        const registry = new SceneShaderRegistry();
        const result = registry.register(
            {
                id: 'basic',
                vertexSource: 'void main() {}',
                fragmentSource: 'void main() {}',
                uniforms: ['u_Model', 'u_View'],
            },
            createShaderResource('basic')
        );

        expect(result.previous).toBeNull();
        expect(result.handle).toEqual({
            id: 'basic',
            uniformNames: ['u_Model', 'u_View'],
        });
        expect(registry.getHandle('basic')).toEqual(result.handle);
        expect(registry.get('basic')?.program).toEqual({ id: 'basic' });
    });

    it('returns the replaced shader resource on re-registration', () => {
        const registry = new SceneShaderRegistry();
        const first = createShaderResource('basic');
        const second = createShaderResource('basic');

        registry.register(
            {
                id: 'basic',
                vertexSource: 'void main() {}',
                fragmentSource: 'void main() {}',
            },
            first
        );

        const result = registry.register(
            {
                id: 'basic',
                vertexSource: 'void main() { gl_Position = vec4(1.0); }',
                fragmentSource: 'void main() {}',
            },
            second
        );

        expect(result.previous).toBe(first);
        expect(registry.get('basic')).toBe(second);
    });

    it('clones shader definitions and clears resources deterministically', () => {
        const registry = new SceneShaderRegistry();
        const definition = {
            id: 'basic',
            vertexSource: 'void main() {}',
            fragmentSource: 'void main() {}',
            uniforms: ['u_Model'],
            attributes: {
                position: 'a_Position',
            },
        };

        registry.register(definition, createShaderResource('basic'));
        definition.uniforms.push('u_View');
        definition.attributes.position = 'mutated';

        const [storedDefinition] = registry.getDefinitions();
        const cleared = registry.clear();

        expect(storedDefinition?.uniforms).toEqual(['u_Model']);
        expect(storedDefinition?.attributes?.position).toBe('a_Position');
        expect(cleared).toHaveLength(1);
        expect(registry.getDefinitions()).toEqual([]);
        expect(registry.getResources()).toEqual([]);
    });

    it('clones definitions with helper utility', () => {
        const definition = {
            id: 'basic',
            vertexSource: 'void main() {}',
            fragmentSource: 'void main() {}',
            uniforms: ['u_Model'],
            attributes: {
                position: 'a_Position',
            },
        };

        const cloned = cloneSceneShaderDefinition(definition);
        definition.uniforms.push('u_View');
        definition.attributes.position = 'mutated';

        expect(cloned).not.toBe(definition);
        expect(cloned.uniforms).toEqual(['u_Model']);
        expect(cloned.attributes?.position).toBe('a_Position');
    });

    it('clones shader effect metadata without leaking nested mutations', () => {
        const definition = {
            id: 'effect-basic',
            effect: {
                format: 'axrone.shader/effect' as const,
                version: 1 as const,
                id: 'effect-basic',
                properties: [
                    {
                        name: 'u_Color',
                        type: 'vec4' as const,
                        scope: 'material' as const,
                        inspector: {
                            control: 'color' as const,
                        },
                    },
                ],
                vertex: {
                    main: ['gl_Position = vec4(1.0);'],
                },
                fragment: {
                    precision: 'highp' as const,
                    outputs: [{ name: 'o_Color', type: 'vec4' as const }],
                    main: ['o_Color = u_Color;'],
                },
            },
        };

        const cloned = cloneSceneShaderDefinition(definition);
        const mutableDefinition = definition as any;
        mutableDefinition.effect.properties[0].inspector.control = 'slider';
        mutableDefinition.effect.vertex.main.push('gl_Position = vec4(0.0);');

        expect(cloned.effect?.properties?.[0]?.inspector?.control).toBe('color');
        expect(cloned.effect?.vertex.main).toEqual(['gl_Position = vec4(1.0);']);
    });

    describe('variant storage', () => {
        it('stores and retrieves shader variants', () => {
            const registry = new SceneShaderRegistry();
            registry.register(
                { id: 'basic', vertexSource: '', fragmentSource: '' },
                createShaderResource('basic')
            );

            const variant = createShaderResource('basic:ALBEDO_MAP');
            expect(registry.registerVariant('basic', 'basic:ALBEDO_MAP', variant)).toBe(true);

            expect(registry.getVariant('basic', 'basic:ALBEDO_MAP')).toBe(variant);
            expect(registry.variantCount).toBe(1);
        });

        it('returns false when registering duplicate variant', () => {
            const registry = new SceneShaderRegistry();
            const v1 = createShaderResource('basic:FOG');
            const v2 = createShaderResource('basic:FOG-v2');

            expect(registry.registerVariant('basic', 'basic:FOG', v1)).toBe(true);
            expect(registry.registerVariant('basic', 'basic:FOG', v2)).toBe(false);
            expect(registry.getVariant('basic', 'basic:FOG')).toBe(v1);
            expect(registry.variantCount).toBe(1);
        });

        it('returns undefined for unknown variant', () => {
            const registry = new SceneShaderRegistry();
            expect(registry.getVariant('basic', 'basic:FOG')).toBeUndefined();
        });

        it('stores multiple variants per shader', () => {
            const registry = new SceneShaderRegistry();
            registry.register(
                { id: 'basic', vertexSource: '', fragmentSource: '' },
                createShaderResource('basic')
            );

            const v1 = createShaderResource('basic:ALBEDO_MAP');
            const v2 = createShaderResource('basic:NORMAL_MAPPING');
            const v3 = createShaderResource('basic:ALBEDO_MAP,NORMAL_MAPPING');

            registry.registerVariant('basic', 'basic:ALBEDO_MAP', v1);
            registry.registerVariant('basic', 'basic:NORMAL_MAPPING', v2);
            registry.registerVariant('basic', 'basic:ALBEDO_MAP,NORMAL_MAPPING', v3);

            expect(registry.variantCount).toBe(3);
            expect(registry.getVariant('basic', 'basic:ALBEDO_MAP')).toBe(v1);
            expect(registry.getVariant('basic', 'basic:NORMAL_MAPPING')).toBe(v2);
            expect(registry.getVariant('basic', 'basic:ALBEDO_MAP,NORMAL_MAPPING')).toBe(v3);
        });

        it('clearVariants removes all variants but keeps base shaders', () => {
            const registry = new SceneShaderRegistry();
            registry.register(
                { id: 'basic', vertexSource: '', fragmentSource: '' },
                createShaderResource('basic')
            );
            registry.registerVariant('basic', 'basic:FOG', createShaderResource('basic:FOG'));

            registry.clearVariants();

            expect(registry.variantCount).toBe(0);
            expect(registry.get('basic')).toBeDefined();
            expect(registry.getVariant('basic', 'basic:FOG')).toBeUndefined();
        });

        it('clearVariantsForShader removes only variants for that shader', () => {
            const registry = new SceneShaderRegistry();
            registry.register(
                { id: 'a', vertexSource: '', fragmentSource: '' },
                createShaderResource('a')
            );
            registry.register(
                { id: 'b', vertexSource: '', fragmentSource: '' },
                createShaderResource('b')
            );
            registry.registerVariant('a', 'a:FOG', createShaderResource('a:FOG'));
            registry.registerVariant('b', 'b:FOG', createShaderResource('b:FOG'));

            expect(registry.clearVariantsForShader('a')).toBe(true);
            expect(registry.variantCount).toBe(1);
            expect(registry.getVariant('a', 'a:FOG')).toBeUndefined();
            expect(registry.getVariant('b', 'b:FOG')).toBeDefined();
        });

        it('clear removes variants along with base resources', () => {
            const registry = new SceneShaderRegistry();
            registry.register(
                { id: 'basic', vertexSource: '', fragmentSource: '' },
                createShaderResource('basic')
            );
            registry.registerVariant('basic', 'basic:FOG', createShaderResource('basic:FOG'));

            registry.clear();

            expect(registry.variantCount).toBe(0);
            expect(registry.size).toBe(0);
        });

        it('getDefinition returns cloned definition for single lookup', () => {
            const registry = new SceneShaderRegistry();
            const definition = {
                id: 'basic',
                vertexSource: 'void main() {}',
                fragmentSource: 'void main() {}',
                uniforms: ['u_Model'],
            };
            registry.register(definition, createShaderResource('basic'));

            const retrieved = registry.getDefinition('basic');
            expect(retrieved).toBeDefined();
            expect(retrieved?.id).toBe('basic');
            expect(retrieved).not.toBe(definition);

            definition.uniforms.push('u_View');
            expect(retrieved?.uniforms).toEqual(['u_Model']);
        });

        it('getDefinition returns undefined for unknown shader', () => {
            const registry = new SceneShaderRegistry();
            expect(registry.getDefinition('nonexistent')).toBeUndefined();
        });
    });
});
