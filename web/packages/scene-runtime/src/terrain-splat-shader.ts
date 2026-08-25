import { createLightingUniformLayout } from '@axrone/lighting';
import type {
    RenderShaderEffectDefinition,
    RenderShaderPropertyDefinition,
} from '@axrone/render-core/shader-effect';
import { createSceneShaderDefinitionFromEffect } from './shader-effect';
import type { SceneShaderDefinition } from './types';

/**
 * Splat-blended terrain shader: up to four albedo layers weighted by an RGBA
 * splat map, lit by the scene's ambient + directional lighting through the
 * standard lighting uniform contract (`SceneLightingUniformBinder`).
 */
export const DEFAULT_TERRAIN_SPLAT_SHADER_ID = 'Scene/TerrainSplat';

const TERRAIN_LIGHTING_LAYOUT = createLightingUniformLayout({
    maxDirectionalLights: 1,
    maxPointLights: 0,
    maxSpotLights: 0,
    maxLocalLights: 0,
});

const TERRAIN_LIGHTING_UNIFORM_NAMES = new Set([
    'u_AmbientLight',
    'u_SkyLight',
    'u_GroundLight',
    'u_DirectionalLightCount',
    'u_DirectionalLightDirection',
    'u_DirectionalLightColor',
    'u_DirectionalLightAmbientColor',
    'u_DirectionalLightIntensity',
]);

const HIDDEN_INSPECTOR = Object.freeze({ hidden: true } as const);

const createTerrainLightingProperties = (): readonly RenderShaderPropertyDefinition[] =>
    TERRAIN_LIGHTING_LAYOUT.properties
        .filter((property) => TERRAIN_LIGHTING_UNIFORM_NAMES.has(property.name))
        .map((property) => ({
            name: property.name,
            type: property.type,
            ...(property.arrayLength !== undefined ? { arrayLength: property.arrayLength } : {}),
            stages: ['fragment'] as const,
            scope: property.scope,
            inspector: HIDDEN_INSPECTOR,
        }));

const createTerrainLayerMapProperties = (): readonly RenderShaderPropertyDefinition[] =>
    [0, 1, 2, 3].map((layerIndex) => ({
        name: `u_LayerMap${layerIndex}`,
        type: 'sampler2D' as const,
        stages: ['fragment'] as const,
        scope: 'material' as const,
        inspector: {
            label: `Layer ${layerIndex + 1} Albedo`,
            group: 'Layers',
            control: 'texture' as const,
        },
    }));

const createTerrainSplatShaderEffect = (id: string): RenderShaderEffectDefinition => ({
    format: 'axrone.shader/effect',
    version: 1,
    id,
    attributes: [
        { name: 'a_Position', type: 'vec3', location: 0 },
        { name: 'a_Normal', type: 'vec3', location: 1 },
        { name: 'a_UV', type: 'vec2', location: 2 },
    ],
    properties: [
        { name: 'u_Model', type: 'mat4', stages: ['vertex'], scope: 'object' },
        { name: 'u_View', type: 'mat4', stages: ['vertex'], scope: 'camera' },
        { name: 'u_Projection', type: 'mat4', stages: ['vertex'], scope: 'camera' },
        {
            name: 'u_SplatMap',
            type: 'sampler2D',
            stages: ['fragment'],
            scope: 'material',
            inspector: { label: 'Splat Map', group: 'Layers', control: 'texture' },
        },
        ...createTerrainLayerMapProperties(),
        {
            name: 'u_LayerTiling',
            type: 'vec4',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: [8, 8, 8, 8],
            inspector: HIDDEN_INSPECTOR,
        },
        {
            name: 'u_LayerCount',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 1,
            inspector: HIDDEN_INSPECTOR,
        },
        {
            name: 'u_ReceiveLighting',
            type: 'bool',
            stages: ['fragment'],
            scope: 'object',
            defaultValue: true,
            inspector: HIDDEN_INSPECTOR,
        },
        ...createTerrainLightingProperties(),
    ],
    vertex: {
        outputs: [
            { name: 'v_Normal', type: 'vec3' },
            { name: 'v_UV', type: 'vec2' },
        ],
        main: [
            'v_Normal = normalize(mat3(u_Model) * a_Normal);',
            'v_UV = a_UV;',
            'gl_Position = u_Projection * u_View * u_Model * vec4(a_Position, 1.0);',
        ],
    },
    fragment: {
        precision: 'mediump',
        inputs: [
            { name: 'v_Normal', type: 'vec3' },
            { name: 'v_UV', type: 'vec2' },
        ],
        outputs: [{ name: 'o_Color', type: 'vec4' }],
        declarations: [
            [
                'vec3 sampleTerrainLayer(sampler2D layerMap, float tiling, float layerSlot, float layerCount) {',
                '    if (layerSlot >= layerCount) {',
                '        return vec3(0.0);',
                '    }',
                '    return texture(layerMap, v_UV * tiling).rgb;',
                '}',
            ],
        ],
        main: [
            'vec4 splat = texture(u_SplatMap, v_UV);',
            '// Kullanilmayan katman kanallarini sifirla ve agirliklari normalize et.',
            'vec4 layerMask = vec4(',
            '    u_LayerCount > 0.5 ? 1.0 : 0.0,',
            '    u_LayerCount > 1.5 ? 1.0 : 0.0,',
            '    u_LayerCount > 2.5 ? 1.0 : 0.0,',
            '    u_LayerCount > 3.5 ? 1.0 : 0.0',
            ');',
            'vec4 weights = splat * layerMask;',
            'float weightTotal = max(weights.r + weights.g + weights.b + weights.a, 0.0001);',
            'weights /= weightTotal;',
            '',
            'vec3 albedo =',
            '    sampleTerrainLayer(u_LayerMap0, u_LayerTiling.x, 0.0, u_LayerCount) * weights.r +',
            '    sampleTerrainLayer(u_LayerMap1, u_LayerTiling.y, 1.0, u_LayerCount) * weights.g +',
            '    sampleTerrainLayer(u_LayerMap2, u_LayerTiling.z, 2.0, u_LayerCount) * weights.b +',
            '    sampleTerrainLayer(u_LayerMap3, u_LayerTiling.w, 3.0, u_LayerCount) * weights.a;',
            '',
            'vec3 lighting = vec3(1.0);',
            'if (u_ReceiveLighting) {',
            '    lighting = u_AmbientLight;',
            '    if (u_DirectionalLightCount > 0) {',
            '        vec3 lightDirection = normalize(-u_DirectionalLightDirection[0]);',
            '        float incidence = max(dot(normalize(v_Normal), lightDirection), 0.0);',
            '        lighting += u_DirectionalLightAmbientColor[0];',
            '        lighting += u_DirectionalLightColor[0] * u_DirectionalLightIntensity[0] * incidence;',
            '    }',
            '}',
            '',
            'o_Color = vec4(albedo * lighting, 1.0);',
        ],
    },
    renderState: {
        depthTest: true,
        cull: true,
        blend: false,
    },
});

export const createTerrainSplatShaderDefinition = (
    id: string = DEFAULT_TERRAIN_SPLAT_SHADER_ID
): SceneShaderDefinition =>
    createSceneShaderDefinitionFromEffect(createTerrainSplatShaderEffect(id), {
        attributes: {
            position: 'a_Position',
            normal: 'a_Normal',
            uv0: 'a_UV',
        },
    });
