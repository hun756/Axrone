import { describe, expect, it } from 'vitest';
import {
    DEFAULT_TERRAIN_SPLAT_SHADER_ID,
    createTerrainSplatShaderDefinition,
} from '../terrain-splat-shader';

describe('createTerrainSplatShaderDefinition', () => {
    it('compiles a shader definition with splat and lighting uniforms', () => {
        const definition = createTerrainSplatShaderDefinition();

        expect(definition.id).toBe(DEFAULT_TERRAIN_SPLAT_SHADER_ID);
        expect(definition.vertexSource).toContain('uniform mat4 u_Model;');
        expect(definition.fragmentSource).toContain('uniform sampler2D u_SplatMap;');
        expect(definition.fragmentSource).toContain('uniform sampler2D u_LayerMap0;');
        expect(definition.fragmentSource).toContain('uniform sampler2D u_LayerMap3;');
        expect(definition.fragmentSource).toContain('uniform vec4 u_LayerTiling;');
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
                'u_SplatMap',
                'u_LayerMap0',
                'u_LayerCount',
                'u_DirectionalLightCount',
                'u_AmbientLight',
            ])
        );
    });

    it('honors a custom shader id', () => {
        const definition = createTerrainSplatShaderDefinition('editor/terrain-splat');
        expect(definition.id).toBe('editor/terrain-splat');
    });
});
