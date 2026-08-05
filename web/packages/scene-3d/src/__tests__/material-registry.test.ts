import { Vec4 } from '@axrone/numeric';
import { describe, expect, it } from 'vitest';
import {
    cloneSceneMaterialDefinition,
    normalizeSceneTextureBinding,
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
});
