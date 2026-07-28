import { describe, expect, it } from 'vitest';
import {
    DEFAULT_TERRAIN_FOLIAGE_SHADER_ID,
    createTerrainFoliageShaderDefinition,
} from '../terrain-foliage-shader';

describe('createTerrainFoliageShaderDefinition', () => {
    it('compiles a double-sided definition with tint and lighting uniforms', () => {
        const definition = createTerrainFoliageShaderDefinition();

        expect(definition.id).toBe(DEFAULT_TERRAIN_FOLIAGE_SHADER_ID);
        expect(definition.vertexSource).toContain('uniform mat4 u_Model;');
        expect(definition.fragmentSource).toContain('uniform vec4 u_TintColor;');
        expect(definition.fragmentSource).toContain('uniform vec3 u_DirectionalLightDirection[1];');
        expect(definition.fragmentSource).toContain('uniform bool u_ReceiveLighting;');
        expect(definition.attributes).toEqual({
            position: 'a_Position',
            normal: 'a_Normal',
            uv0: 'a_UV',
        });
        expect(definition.uniforms).toEqual(
            expect.arrayContaining([
                'u_Model',
                'u_TintColor',
                'u_DirectionalLightCount',
                'u_AmbientLight',
            ])
        );
        expect(definition.cull).toBe(false);
    });

    it('honors a custom shader id', () => {
        const definition = createTerrainFoliageShaderDefinition('editor/terrain-foliage');
        expect(definition.id).toBe('editor/terrain-foliage');
    });
});
