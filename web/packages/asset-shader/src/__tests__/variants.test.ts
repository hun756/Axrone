import { describe, expect, it } from 'vitest';
import {
    buildShaderVariant,
    buildShaderVariants,
    defaultShaderKeywordSelection,
    enumerateShaderVariants,
    selectionToShaderDefines,
    shaderVariantCount,
} from '../variants';
import { defineShaderEffect, fragStage, glsl, keyword, prop, vtxStage, varying } from '../authoring';

const baseEffect = defineShaderEffect({
    id: 'variant-fixture',
    properties: [prop('u_density', 'float', 'material', 0.5, { stages: ['fragment'] })],
    keywords: [
        keyword('USE_FOG', ['fragment'], undefined, false),
        keyword('QUALITY', ['fragment'], ['LOW', 'MED', 'HIGH'], 'MED'),
    ],
    vertex: vtxStage(glsl`gl_Position = vec4(0.0);`),
    fragment: fragStage(
        glsl`
            vec3 col = vec3(u_density);
            #if USE_FOG
            col *= 0.5;
            #endif
            #ifdef HIGH
            col += vec3(1.0);
            #endif
            o_Color = vec4(col, 1.0);
        `,
        [],
        { outputs: [varying('o_Color', 'vec4')] }
    ),
});

describe('shader variant enumeration', () => {
    it('computes the variant count from the keyword space', () => {
        expect(shaderVariantCount(baseEffect.keywords ?? [])).toBe(6);
    });

    it('enumerates every selection in the product space', () => {
        const variants = enumerateShaderVariants(baseEffect.keywords ?? []);
        expect(variants).toHaveLength(6);
    });

    it('derives the default selection from declared defaults', () => {
        const selection = defaultShaderKeywordSelection(baseEffect.keywords ?? []);
        expect(selection.USE_FOG).toBe(false);
        expect(selection.QUALITY).toBe('MED');
    });
});

describe('shader define mapping', () => {
    it('defines enum options and the keyword index', () => {
        const defines = selectionToShaderDefines(baseEffect.keywords ?? [], {
            USE_FOG: true,
            QUALITY: 'HIGH',
        });
        expect(defines.USE_FOG).toBe(1);
        expect(defines.HIGH).toBe(1);
        expect(defines.LOW).toBeUndefined();
        expect(defines.QUALITY).toBe(2);
    });
});

describe('shader variant compilation', () => {
    it('compiles the default variant and resolves conditional blocks', () => {
        const variant = buildShaderVariant(baseEffect);
        expect(variant.fragmentSource).toContain('#version 300 es');
        expect(variant.fragmentSource).not.toContain('col *= 0.5');
        expect(variant.fragmentSource).not.toContain('col += vec3(1.0)');
    });

    it('resolves a keyword selection into the matching branch', () => {
        const variant = buildShaderVariant(baseEffect, {
            selection: { USE_FOG: true, QUALITY: 'HIGH' },
        });
        expect(variant.fragmentSource).toContain('col *= 0.5');
        expect(variant.fragmentSource).toContain('col += vec3(1.0)');
    });

    it('builds every variant in the keyword space', () => {
        const variants = buildShaderVariants(baseEffect);
        expect(variants).toHaveLength(6);
        const keys = new Set(variants.map((v) => v.key));
        expect(keys.size).toBe(6);
    });

    it('shares a stable variant key', () => {
        const a = buildShaderVariant(baseEffect, { selection: { USE_FOG: true, QUALITY: 'LOW' } });
        const b = buildShaderVariant(baseEffect, { selection: { QUALITY: 'LOW', USE_FOG: true } });
        expect(a.key).toBe(b.key);
    });
});
