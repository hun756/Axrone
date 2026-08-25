import { Vec4 } from '@axrone/numeric';
import { describe, expect, it, vi } from 'vitest';
import {
    cloneSceneMaterialDefinition,
    FEATURE_TO_KEYWORD,
    normalizeSceneTextureBinding,
    resolveSurfaceFeatures,
    SceneMaterialObservables,
    SceneMaterialRegistry,
} from '@axrone/scene-3d';

describe('SceneMaterialRegistry', () => {
    it('creates material resources and returns handles', () => {
        const registry = new SceneMaterialRegistry();
        const handle = registry.create({
            id: 'mat/basic',
            shaderId: 'shader/basic',
            textures: {
                u_MainTex: {
                    textureId: 'checker',
                    samplerId: 'linear',
                },
            },
        });

        expect(handle).toEqual({
            id: 'mat/basic',
            shaderId: 'shader/basic',
            textureBindings: ['u_MainTex'],
            passIds: [],
        });
        expect(registry.get('mat/basic')?.textureBindings.get('u_MainTex')).toEqual({
            textureId: 'checker',
            samplerId: 'linear',
        });
    });

    it('updates material uniforms and texture bindings in definitions', () => {
        const registry = new SceneMaterialRegistry();
        registry.create({
            id: 'mat/basic',
            shaderId: 'shader/basic',
        });

        const tint = new Vec4(0.1, 0.2, 0.3, 1);
        expect(registry.setUniform('mat/basic', 'u_Tint', tint)).toBe(true);
        expect(
            registry.setTexture('mat/basic', 'u_MainTex', {
                textureId: 'checker',
                samplerId: 'linear',
                unit: 2,
            })
        ).toBe(true);

        tint.x = 1;
        const [definition] = registry.getDefinitions();

        expect(definition?.uniforms?.u_Tint).toBeInstanceOf(Vec4);
        expect(definition?.uniforms?.u_Tint).not.toBe(tint);
        expect(definition?.textures?.u_MainTex).toEqual({
            textureId: 'checker',
            samplerId: 'linear',
            unit: 2,
        });
        expect(registry.getHandle('mat/basic')).toEqual({
            id: 'mat/basic',
            shaderId: 'shader/basic',
            textureBindings: ['u_MainTex'],
            passIds: [],
        });
        expect(registry.getTextureSlots('mat/basic')).toEqual([
            {
                uniformName: 'u_MainTex',
                binding: {
                    textureId: 'checker',
                    samplerId: 'linear',
                    unit: 2,
                },
                resolvedUnit: 2,
            },
        ]);
    });

    it('normalizes string texture bindings and clones material definitions', () => {
        const definition = {
            id: 'mat/basic',
            shaderId: 'shader/basic',
            uniforms: {
                u_Tint: new Vec4(0.1, 0.2, 0.3, 1),
            },
            textures: {
                u_MainTex: 'checker',
            },
        };

        const normalized = normalizeSceneTextureBinding('checker');
        const cloned = cloneSceneMaterialDefinition(definition);
        (definition.uniforms.u_Tint as Vec4).x = 1;

        expect(normalized).toEqual({
            textureId: 'checker',
            samplerId: null,
        });
        expect(cloned.uniforms?.u_Tint).toBeInstanceOf(Vec4);
        expect(cloned.uniforms?.u_Tint).not.toBe(definition.uniforms.u_Tint);
        expect(cloned.textures?.u_MainTex).toBe('checker');
    });

    it('clones and exposes material pass definitions through handles', () => {
        const registry = new SceneMaterialRegistry();
        const definition = {
            id: 'mat/passes',
            shaderId: 'shader/basic',
            passes: [
                {
                    id: 'main',
                    primitive: 'triangle-list' as const,
                    rasterizerState: {
                        cullMode: 'back' as const,
                    },
                    blendState: {
                        blendColor: [0.1, 0.2, 0.3, 0.4] as const,
                        targets: [
                            {
                                blend: true,
                                colorWriteMask: [true, false, true, false] as const,
                            },
                        ],
                    },
                },
            ],
        };

        const handle = registry.create(definition);
        const storedPass = registry.get('mat/passes')?.passes[0];
        definition.passes[0]!.blendState!.blendColor = [1, 1, 1, 1];

        expect(handle.passIds).toEqual(['main']);
        expect(storedPass?.blendState?.blendColor).toEqual([0.1, 0.2, 0.3, 0.4]);
        expect(storedPass?.blendState?.targets?.[0]?.colorWriteMask).toEqual([
            true,
            false,
            true,
            false,
        ]);
    });

    it('clears stored materials', () => {
        const registry = new SceneMaterialRegistry();
        registry.create({
            id: 'mat/basic',
            shaderId: 'shader/basic',
        });

        registry.clear();

        expect(registry.get('mat/basic')).toBeUndefined();
        expect(registry.getDefinitions()).toEqual([]);
    });

    it('caches deterministic texture slots for repeated lookups', () => {
        const registry = new SceneMaterialRegistry();
        registry.create({
            id: 'mat/basic',
            shaderId: 'shader/basic',
            textures: {
                u_Overlay: {
                    textureId: 'overlay',
                    unit: 4,
                },
                u_MainTex: {
                    textureId: 'checker',
                },
            },
        });

        const first = registry.getTextureSlots('mat/basic');
        const second = registry.getTextureSlots('mat/basic');

        expect(first).toBe(second);
        expect(first.map((slot) => [slot.uniformName, slot.resolvedUnit])).toEqual([
            ['u_Overlay', 4],
            ['u_MainTex', 0],
        ]);

        registry.setTexture('mat/basic', 'u_Detail', {
            textureId: 'detail',
            unit: 1,
        });
        const third = registry.getTextureSlots('mat/basic');

        expect(third).not.toBe(first);
        expect(third.map((slot) => [slot.uniformName, slot.resolvedUnit])).toEqual([
            ['u_Detail', 1],
            ['u_Overlay', 4],
            ['u_MainTex', 0],
        ]);
    });

    it('deletes material from all internal maps', () => {
        const registry = new SceneMaterialRegistry();
        registry.create({
            id: 'mat/basic',
            shaderId: 'shader/basic',
            uniforms: { u_Color: new Vec4(1, 0, 0, 1) },
            textures: { u_MainTex: { textureId: 'checker', samplerId: 'linear' } },
        });

        expect(registry.delete('mat/basic')).toBe(true);
        expect(registry.get('mat/basic')).toBeUndefined();
        expect(registry.getHandle('mat/basic')).toBeNull();
        expect(registry.getDefinitions()).toEqual([]);
        expect(registry.getTextureSlots('mat/basic')).toEqual([]);
        expect(registry.size).toBe(0);
    });

    it('returns false when deleting unknown material', () => {
        const registry = new SceneMaterialRegistry();
        expect(registry.delete('mat/nonexistent')).toBe(false);
    });

    it('allows recreating a material after deletion', () => {
        const registry = new SceneMaterialRegistry();
        registry.create({ id: 'mat/basic', shaderId: 'shader/basic' });
        registry.delete('mat/basic');

        const handle = registry.create({ id: 'mat/basic', shaderId: 'shader/new' });
        expect(handle.shaderId).toBe('shader/new');
        expect(registry.size).toBe(1);
    });

    it('clones material with deep independence', () => {
        const registry = new SceneMaterialRegistry();
        const sourceColor = new Vec4(1, 0, 0, 1);
        registry.create({
            id: 'mat/source',
            shaderId: 'shader/basic',
            uniforms: { u_Color: sourceColor },
            textures: { u_MainTex: { textureId: 'checker', samplerId: 'linear' } },
        });

        const cloneHandle = registry.clone('mat/source', 'mat/clone');

        expect(cloneHandle.id).toBe('mat/clone');
        expect(cloneHandle.shaderId).toBe('shader/basic');

        // Mutate clone's uniform
        registry.setUniform('mat/clone', 'u_Color', new Vec4(0, 1, 0, 1));

        // Source must be unaffected
        const sourceResource = registry.get('mat/source');
        const cloneResource = registry.get('mat/clone');
        expect(sourceResource?.uniforms.get('u_Color')).toBe(sourceColor);
        expect((sourceResource?.uniforms.get('u_Color') as Vec4).x).toBe(1);
        expect((cloneResource?.uniforms.get('u_Color') as Vec4).x).toBe(0);
    });

    it('throws when cloning unknown material', () => {
        const registry = new SceneMaterialRegistry();
        expect(() => registry.clone('mat/nonexistent', 'mat/clone')).toThrow(
            "Material 'mat/nonexistent' is not registered"
        );
    });

    it('throws when cloning to existing material id', () => {
        const registry = new SceneMaterialRegistry();
        registry.create({ id: 'mat/a', shaderId: 'shader/basic' });
        registry.create({ id: 'mat/b', shaderId: 'shader/basic' });

        expect(() => registry.clone('mat/a', 'mat/b')).toThrow(
            "Material 'mat/b' is already registered"
        );
    });

    it('recomputes texture slots independently for clones', () => {
        const registry = new SceneMaterialRegistry();
        registry.create({
            id: 'mat/source',
            shaderId: 'shader/basic',
            textures: {
                u_MainTex: { textureId: 'checker' },
                u_Normal: { textureId: 'normal', unit: 3 },
            },
        });

        registry.clone('mat/source', 'mat/clone');

        const sourceSlots = registry.getTextureSlots('mat/source');
        const cloneSlots = registry.getTextureSlots('mat/clone');

        expect(sourceSlots).not.toBe(cloneSlots);
        expect(cloneSlots.map((s) => [s.uniformName, s.resolvedUnit])).toEqual(
            sourceSlots.map((s) => [s.uniformName, s.resolvedUnit])
        );

        // Mutating clone's texture should not affect source
        registry.setTexture('mat/clone', 'u_MainTex', { textureId: 'different' });
        expect(registry.get('mat/source')?.textureBindings.get('u_MainTex')?.textureId).toBe('checker');
        expect(registry.get('mat/clone')?.textureBindings.get('u_MainTex')?.textureId).toBe('different');
    });

    it('reports has correctly', () => {
        const registry = new SceneMaterialRegistry();
        expect(registry.has('mat/basic')).toBe(false);

        registry.create({ id: 'mat/basic', shaderId: 'shader/basic' });
        expect(registry.has('mat/basic')).toBe(true);

        registry.delete('mat/basic');
        expect(registry.has('mat/basic')).toBe(false);
    });

    it('returns material ids as frozen array', () => {
        const registry = new SceneMaterialRegistry();
        expect(registry.getMaterialIds()).toEqual([]);

        registry.create({ id: 'mat/a', shaderId: 'shader/basic' });
        registry.create({ id: 'mat/b', shaderId: 'shader/basic' });

        const ids = registry.getMaterialIds();
        expect(ids).toEqual(['mat/a', 'mat/b']);
        expect(Object.isFrozen(ids)).toBe(true);

        registry.delete('mat/a');
        expect(registry.getMaterialIds()).toEqual(['mat/b']);
    });

    it('supports full lifecycle: create, clone, mutate, delete', () => {
        const registry = new SceneMaterialRegistry();

        // Create
        registry.create({
            id: 'mat/source',
            shaderId: 'shader/basic',
            uniforms: { u_Color: new Vec4(1, 0, 0, 1) },
        });

        // Clone
        registry.clone('mat/source', 'mat/clone');
        expect(registry.size).toBe(2);

        // Mutate clone
        registry.setUniform('mat/clone', 'u_Color', new Vec4(0, 1, 0, 1));

        // Verify source unchanged
        expect((registry.get('mat/source')?.uniforms.get('u_Color') as Vec4).x).toBe(1);
        expect((registry.get('mat/clone')?.uniforms.get('u_Color') as Vec4).x).toBe(0);

        // Delete clone
        expect(registry.delete('mat/clone')).toBe(true);
        expect(registry.size).toBe(1);
        expect(registry.has('mat/clone')).toBe(false);
        expect(registry.has('mat/source')).toBe(true);
    });

    describe('keywords', () => {
        it('sets and gets keyword state', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });

            expect(registry.getKeyword('mat/test', 'EMISSIVE')).toBe(false);
            expect(registry.setKeyword('mat/test', 'EMISSIVE', true)).toBe(true);
            expect(registry.getKeyword('mat/test', 'EMISSIVE')).toBe(true);
        });

        it('toggles keyword state', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });

            expect(registry.toggleKeyword('mat/test', 'FOG')).toBe(true);
            expect(registry.getKeyword('mat/test', 'FOG')).toBe(true);
            expect(registry.toggleKeyword('mat/test', 'FOG')).toBe(true);
            expect(registry.getKeyword('mat/test', 'FOG')).toBe(false);
        });

        it('returns enabled keywords as frozen array', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });
            registry.setKeyword('mat/test', 'FOG', true);
            registry.setKeyword('mat/test', 'EMISSIVE', true);
            registry.setKeyword('mat/test', 'SHADOWS', false);

            const enabled = registry.getEnabledKeywords('mat/test');
            expect(enabled).toContain('FOG');
            expect(enabled).toContain('EMISSIVE');
            expect(enabled).not.toContain('SHADOWS');
            expect(Object.isFrozen(enabled)).toBe(true);
        });

        it('returns null for keyword on unknown material', () => {
            const registry = new SceneMaterialRegistry();
            expect(registry.getKeyword('mat/nonexistent', 'FOG')).toBeNull();
        });

        it('returns false for setKeyword on unknown material', () => {
            const registry = new SceneMaterialRegistry();
            expect(registry.setKeyword('mat/nonexistent', 'FOG', true)).toBe(false);
        });

        it('copies keywords during clone', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({ id: 'mat/source', shaderId: 'shader/basic' });
            registry.setKeyword('mat/source', 'FOG', true);
            registry.setKeyword('mat/source', 'EMISSIVE', true);

            registry.clone('mat/source', 'mat/clone');
            expect(registry.getKeyword('mat/clone', 'FOG')).toBe(true);
            expect(registry.getKeyword('mat/clone', 'EMISSIVE')).toBe(true);

            // Independence: toggling clone doesn't affect source
            registry.toggleKeyword('mat/clone', 'FOG');
            expect(registry.getKeyword('mat/source', 'FOG')).toBe(true);
            expect(registry.getKeyword('mat/clone', 'FOG')).toBe(false);
        });
    });

    describe('property aliases', () => {
        it('resolves alias in setUniform', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });

            registry.setUniform('mat/test', 'color', new Vec4(1, 0, 0, 1));
            expect(registry.getUniform('mat/test', 'u_Color')).toEqual(new Vec4(1, 0, 0, 1));
        });

        it('resolves alias in getUniform', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });

            registry.setUniform('mat/test', 'u_Color', new Vec4(0, 1, 0, 1));
            expect(registry.getUniform('mat/test', 'color')).toEqual(new Vec4(0, 1, 0, 1));
        });

        it('passes through unknown names unchanged', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });

            registry.setUniform('mat/test', 'u_Custom', 42);
            expect(registry.getUniform('mat/test', 'u_Custom')).toBe(42);
        });

        it('alias and explicit name resolve to same uniform', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });

            registry.setUniform('mat/test', 'color', new Vec4(1, 0, 0, 1));
            registry.setUniform('mat/test', 'u_Color', new Vec4(0, 0, 1, 1));

            // Both resolve to u_Color, so last write wins
            expect(registry.getUniform('mat/test', 'color')).toEqual(new Vec4(0, 0, 1, 1));
            expect(registry.getUniform('mat/test', 'u_Color')).toEqual(new Vec4(0, 0, 1, 1));
        });

        it('resolves aliases in batch setUniforms', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });

            registry.setUniforms('mat/test', {
                color: new Vec4(1, 0, 0, 1),
                metallic: 0.5,
                roughness: 0.3,
            });

            expect(registry.getUniform('mat/test', 'u_Color')).toEqual(new Vec4(1, 0, 0, 1));
            expect(registry.getUniform('mat/test', 'u_Metallic')).toBe(0.5);
            expect(registry.getUniform('mat/test', 'u_Roughness')).toBe(0.3);
        });
    });

    describe('batch uniform operations', () => {
        it('setUniforms applies multiple uniforms', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });

            expect(
                registry.setUniforms('mat/test', {
                    u_Color: new Vec4(1, 0, 0, 1),
                    u_Metallic: 0.8,
                    u_Roughness: 0.2,
                })
            ).toBe(true);

            expect(registry.getUniform('mat/test', 'u_Color')).toEqual(new Vec4(1, 0, 0, 1));
            expect(registry.getUniform('mat/test', 'u_Metallic')).toBe(0.8);
            expect(registry.getUniform('mat/test', 'u_Roughness')).toBe(0.2);
        });

        it('setUniforms returns false for unknown material', () => {
            const registry = new SceneMaterialRegistry();
            expect(registry.setUniforms('mat/nonexistent', { u_Color: 1 })).toBe(false);
        });

        it('getUniforms returns all uniforms as frozen Record', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });
            registry.setUniform('mat/test', 'u_Color', new Vec4(1, 0, 0, 1));
            registry.setUniform('mat/test', 'u_Metallic', 0.5);

            const uniforms = registry.getUniforms('mat/test');
            expect(uniforms).not.toBeNull();
            expect(Object.isFrozen(uniforms)).toBe(true);
            expect(uniforms!['u_Color']).toEqual(new Vec4(1, 0, 0, 1));
            expect(uniforms!['u_Metallic']).toBe(0.5);
        });

        it('getUniforms returns null for unknown material', () => {
            const registry = new SceneMaterialRegistry();
            expect(registry.getUniforms('mat/nonexistent')).toBeNull();
        });

        it('getUniform returns null for unset uniform', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });
            expect(registry.getUniform('mat/test', 'u_NonExistent')).toBeNull();
        });
    });

    describe('surface feature to keyword bridge', () => {
        it('FEATURE_TO_KEYWORD maps all 19 surface features', () => {
            expect(Object.keys(FEATURE_TO_KEYWORD)).toHaveLength(19);
            expect(FEATURE_TO_KEYWORD).toHaveProperty('useVertexColor', 'VERTEX_COLOR');
            expect(FEATURE_TO_KEYWORD).toHaveProperty('hasSecondUv', 'SECOND_UV');
            expect(FEATURE_TO_KEYWORD).toHaveProperty('useNormalMap', 'NORMAL_MAPPING');
            expect(FEATURE_TO_KEYWORD).toHaveProperty('useTwoSided', 'TWO_SIDED');
            expect(FEATURE_TO_KEYWORD).toHaveProperty('useAlbedoMap', 'ALBEDO_MAP');
            expect(FEATURE_TO_KEYWORD).toHaveProperty('usePbrMap', 'PBR_MAP');
            expect(FEATURE_TO_KEYWORD).toHaveProperty(
                'useMetallicRoughnessMap',
                'METALLIC_ROUGHNESS_MAP'
            );
            expect(FEATURE_TO_KEYWORD).toHaveProperty('useOcclusionMap', 'OCCLUSION');
            expect(FEATURE_TO_KEYWORD).toHaveProperty('useEmissiveMap', 'EMISSION');
            expect(FEATURE_TO_KEYWORD).toHaveProperty('useClearcoat', 'CLEARCOAT');
            expect(FEATURE_TO_KEYWORD).toHaveProperty('useClearcoatMap', 'CLEARCOAT_MAP');
            expect(FEATURE_TO_KEYWORD).toHaveProperty(
                'useClearcoatRoughnessMap',
                'CLEARCOAT_ROUGHNESS_MAP'
            );
            expect(FEATURE_TO_KEYWORD).toHaveProperty(
                'useClearcoatNormalMap',
                'CLEARCOAT_NORMAL_MAP'
            );
            expect(FEATURE_TO_KEYWORD).toHaveProperty('useAlphaTest', 'ALPHA_TEST');
            expect(FEATURE_TO_KEYWORD).toHaveProperty('useAnisotropy', 'ANISOTROPY');
            expect(FEATURE_TO_KEYWORD).toHaveProperty('useSheen', 'SHEEN');
            expect(FEATURE_TO_KEYWORD).toHaveProperty('useSubsurface', 'SUBSURFACE');
            expect(FEATURE_TO_KEYWORD).toHaveProperty('useTransmission', 'TRANSMISSION');
            expect(FEATURE_TO_KEYWORD).toHaveProperty('useIridescence', 'IRIDESCENCE');
            expect(Object.isFrozen(FEATURE_TO_KEYWORD)).toBe(true);
        });

        it('resolveSurfaceFeatures maps true/false features and skips undefined', () => {
            const result = resolveSurfaceFeatures({
                useVertexColor: true,
                useNormalMap: false,
                useAlbedoMap: true,
            });

            expect(result['VERTEX_COLOR']).toBe(true);
            expect(result['NORMAL_MAPPING']).toBe(false);
            expect(result['ALBEDO_MAP']).toBe(true);
            expect(result).not.toHaveProperty('SECOND_UV');
            expect(result).not.toHaveProperty('PBR_MAP');
            expect(Object.keys(result)).toHaveLength(3);
        });

        it('resolveSurfaceFeatures returns empty object for empty features', () => {
            expect(resolveSurfaceFeatures({})).toEqual({});
        });

        it('auto-syncs surface features to keywords on create()', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({
                id: 'mat/pbr',
                shaderId: 'shader/pbr',
                surface: {
                    features: {
                        useAlbedoMap: true,
                        useNormalMap: true,
                        usePbrMap: true,
                        useOcclusionMap: false,
                    },
                },
            });

            expect(registry.getKeyword('mat/pbr', 'ALBEDO_MAP')).toBe(true);
            expect(registry.getKeyword('mat/pbr', 'NORMAL_MAPPING')).toBe(true);
            expect(registry.getKeyword('mat/pbr', 'PBR_MAP')).toBe(true);
            expect(registry.getKeyword('mat/pbr', 'OCCLUSION')).toBe(false);
            expect(registry.getKeyword('mat/pbr', 'EMISSION')).toBe(false);
        });

        it('materials without surface features have no auto-synced keywords', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({ id: 'mat/plain', shaderId: 'shader/basic' });

            const enabled = registry.getEnabledKeywords('mat/plain');
            expect(enabled).toEqual([]);
        });

        it('materials with surface but no features have no auto-synced keywords', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({
                id: 'mat/surface-only',
                shaderId: 'shader/basic',
                surface: { shadingModel: 'lit' },
            });

            expect(registry.getEnabledKeywords('mat/surface-only')).toEqual([]);
        });

        it('explicit setKeyword overrides auto-synced keyword', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({
                id: 'mat/override',
                shaderId: 'shader/pbr',
                surface: {
                    features: {
                        useAlbedoMap: true,
                    },
                },
            });

            expect(registry.getKeyword('mat/override', 'ALBEDO_MAP')).toBe(true);

            registry.setKeyword('mat/override', 'ALBEDO_MAP', false);
            expect(registry.getKeyword('mat/override', 'ALBEDO_MAP')).toBe(false);
        });

        it('clone copies auto-synced keywords with independence', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({
                id: 'mat/source',
                shaderId: 'shader/pbr',
                surface: {
                    features: {
                        useClearcoat: true,
                        useSheen: true,
                    },
                },
            });

            registry.clone('mat/source', 'mat/clone');

            expect(registry.getKeyword('mat/clone', 'CLEARCOAT')).toBe(true);
            expect(registry.getKeyword('mat/clone', 'SHEEN')).toBe(true);

            registry.setKeyword('mat/clone', 'CLEARCOAT', false);
            expect(registry.getKeyword('mat/source', 'CLEARCOAT')).toBe(true);
            expect(registry.getKeyword('mat/clone', 'CLEARCOAT')).toBe(false);
        });

        it('auto-synced keywords appear in getEnabledKeywords', () => {
            const registry = new SceneMaterialRegistry();
            registry.create({
                id: 'mat/enabled',
                shaderId: 'shader/pbr',
                surface: {
                    features: {
                        useVertexColor: true,
                        useTwoSided: true,
                        useAlphaTest: false,
                    },
                },
            });

            const enabled = registry.getEnabledKeywords('mat/enabled');
            expect(enabled).toContain('VERTEX_COLOR');
            expect(enabled).toContain('TWO_SIDED');
            expect(enabled).not.toContain('ALPHA_TEST');
        });
    });

    it('warns and notifies deletion when creating a material with a duplicate id', () => {
        const observables = new SceneMaterialObservables();
        const registry = new SceneMaterialRegistry({ observables });

        registry.create({ id: 'test-mat', shaderId: 'shader/first' });

        const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
        const deletedCallback = vi.fn();
        observables.materialDeleted.addObserver(deletedCallback);

        const handle = registry.create({ id: 'test-mat', shaderId: 'shader/second' });

        // Overwrite happened — second material's shaderId is stored
        expect(handle.shaderId).toBe('shader/second');
        expect(registry.get('test-mat')?.shaderId).toBe('shader/second');

        // console.warn was called with the material id
        expect(warnSpy).toHaveBeenCalledWith(
            expect.stringContaining('test-mat')
        );

        // _notifyMaterialDeleted was called for the old material
        expect(deletedCallback).toHaveBeenCalledWith(
            expect.objectContaining({ materialId: 'test-mat' }),
            expect.anything()
        );

        warnSpy.mockRestore();
    });

    describe('material observables integration', () => {
        it('fires materialCreated on create', () => {
            const observables = new SceneMaterialObservables();
            const registry = new SceneMaterialRegistry({ observables });
            const callback = vi.fn();
            observables.materialCreated.addObserver(callback);

            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });

            expect(callback).toHaveBeenCalledWith(
                expect.objectContaining({ materialId: 'mat/test' }),
                expect.anything()
            );
        });

        it('fires materialDeleted on delete', () => {
            const observables = new SceneMaterialObservables();
            const registry = new SceneMaterialRegistry({ observables });
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });

            const callback = vi.fn();
            observables.materialDeleted.addObserver(callback);

            registry.delete('mat/test');

            expect(callback).toHaveBeenCalledWith(
                expect.objectContaining({ materialId: 'mat/test' }),
                expect.anything()
            );
        });

        it('does not fire materialDeleted when deleting unknown material', () => {
            const observables = new SceneMaterialObservables();
            const registry = new SceneMaterialRegistry({ observables });
            const callback = vi.fn();
            observables.materialDeleted.addObserver(callback);

            registry.delete('mat/nonexistent');

            expect(callback).not.toHaveBeenCalled();
        });

        it('fires materialCloned on clone', () => {
            const observables = new SceneMaterialObservables();
            const registry = new SceneMaterialRegistry({ observables });
            registry.create({ id: 'mat/source', shaderId: 'shader/basic' });

            const callback = vi.fn();
            observables.materialCloned.addObserver(callback);

            registry.clone('mat/source', 'mat/clone');

            expect(callback).toHaveBeenCalledWith(
                expect.objectContaining({
                    sourceId: 'mat/source',
                    cloneId: 'mat/clone',
                }),
                expect.anything()
            );
        });

        it('fires uniformChanged on setUniform', () => {
            const observables = new SceneMaterialObservables();
            const registry = new SceneMaterialRegistry({ observables });
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });

            const callback = vi.fn();
            observables.uniformChanged.addObserver(callback);

            registry.setUniform('mat/test', 'u_Color', new Vec4(1, 0, 0, 1));

            expect(callback).toHaveBeenCalledWith(
                expect.objectContaining({
                    materialId: 'mat/test',
                    uniformName: 'u_Color',
                }),
                expect.anything()
            );
        });

        it('fires keywordChanged on setKeyword', () => {
            const observables = new SceneMaterialObservables();
            const registry = new SceneMaterialRegistry({ observables });
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });

            const callback = vi.fn();
            observables.keywordChanged.addObserver(callback);

            registry.setKeyword('mat/test', 'FOG', true);

            expect(callback).toHaveBeenCalledWith(
                expect.objectContaining({
                    materialId: 'mat/test',
                    keyword: 'FOG',
                    enabled: true,
                }),
                expect.anything()
            );
        });

        it('fires keywordChanged on toggleKeyword', () => {
            const observables = new SceneMaterialObservables();
            const registry = new SceneMaterialRegistry({ observables });
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });

            const callback = vi.fn();
            observables.keywordChanged.addObserver(callback);

            registry.toggleKeyword('mat/test', 'FOG');

            expect(callback).toHaveBeenCalledWith(
                expect.objectContaining({
                    materialId: 'mat/test',
                    keyword: 'FOG',
                    enabled: true,
                }),
                expect.anything()
            );
        });

        it('fires textureChanged on setTexture', () => {
            const observables = new SceneMaterialObservables();
            const registry = new SceneMaterialRegistry({ observables });
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });

            const callback = vi.fn();
            observables.textureChanged.addObserver(callback);

            registry.setTexture('mat/test', 'u_MainTex', { textureId: 'checker' });

            expect(callback).toHaveBeenCalledWith(
                expect.objectContaining({
                    materialId: 'mat/test',
                    slotName: 'u_MainTex',
                }),
                expect.anything()
            );
        });

        it('no events fire when observables are not provided', () => {
            const registry = new SceneMaterialRegistry();

            expect(() =>
                registry.create({ id: 'mat/test', shaderId: 'shader/basic' })
            ).not.toThrow();
            expect(() => registry.delete('mat/test')).not.toThrow();
        });

        it('clone fires materialCloned but NOT materialCreated', () => {
            const observables = new SceneMaterialObservables();
            const registry = new SceneMaterialRegistry({ observables });
            registry.create({ id: 'mat/source', shaderId: 'shader/basic' });

            const createdCallback = vi.fn();
            const clonedCallback = vi.fn();
            observables.materialCreated.addObserver(createdCallback);
            observables.materialCloned.addObserver(clonedCallback);

            registry.clone('mat/source', 'mat/clone');

            expect(clonedCallback).toHaveBeenCalledWith(
                expect.objectContaining({
                    sourceId: 'mat/source',
                    cloneId: 'mat/clone',
                }),
                expect.anything()
            );
            expect(createdCallback).not.toHaveBeenCalled();
        });

        it('clear fires materialDeleted for each registered material', () => {
            const observables = new SceneMaterialObservables();
            const registry = new SceneMaterialRegistry({ observables });
            registry.create({ id: 'mat/a', shaderId: 'shader/basic' });
            registry.create({ id: 'mat/b', shaderId: 'shader/basic' });
            registry.create({ id: 'mat/c', shaderId: 'shader/basic' });

            const callback = vi.fn();
            observables.materialDeleted.addObserver(callback);

            registry.clear();

            expect(callback).toHaveBeenCalledTimes(3);
            const deletedIds = callback.mock.calls.map(
                (call: unknown[]) => (call[0] as { materialId: string }).materialId
            );
            expect(deletedIds).toContain('mat/a');
            expect(deletedIds).toContain('mat/b');
            expect(deletedIds).toContain('mat/c');
        });

        it('setUniforms fires uniformChanged for each uniform', () => {
            const observables = new SceneMaterialObservables();
            const registry = new SceneMaterialRegistry({ observables });
            registry.create({ id: 'mat/test', shaderId: 'shader/basic' });

            const callback = vi.fn();
            observables.uniformChanged.addObserver(callback);

            registry.setUniforms('mat/test', {
                u_Color: new Vec4(1, 0, 0, 1),
                u_Metallic: 0.5,
                u_Roughness: 0.8,
            });

            expect(callback).toHaveBeenCalledTimes(3);
            const changedNames = callback.mock.calls.map(
                (call: unknown[]) => (call[0] as { uniformName: string }).uniformName
            );
            expect(changedNames).toContain('u_Color');
            expect(changedNames).toContain('u_Metallic');
            expect(changedNames).toContain('u_Roughness');
        });
    });
});
