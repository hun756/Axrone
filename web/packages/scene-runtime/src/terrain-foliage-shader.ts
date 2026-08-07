import { createLightingUniformLayout } from '@axrone/lighting';
import type {
    RenderShaderEffectDefinition,
    RenderShaderPropertyDefinition,
} from '@axrone/render-core/shader-effect';
import { createSceneShaderDefinitionFromEffect } from './shader-effect';
import type { SceneShaderDefinition } from './types';

/**
 * Double-sided foliage shader for batched terrain vegetation: a flat tint
 * color modulated by ambient + directional lighting through the standard
 * lighting uniform contract (`SceneLightingUniformBinder`). Culling is
 * disabled so cross-quad grass cards stay visible from both sides.
 */
export const DEFAULT_TERRAIN_FOLIAGE_SHADER_ID = 'Scene/TerrainFoliage';

const FOLIAGE_LIGHTING_LAYOUT = createLightingUniformLayout({
    maxDirectionalLights: 1,
    maxPointLights: 0,
    maxSpotLights: 0,
    maxLocalLights: 0,
});

const FOLIAGE_LIGHTING_UNIFORM_NAMES = new Set([
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

const createFoliageLightingProperties = (): readonly RenderShaderPropertyDefinition[] =>
    FOLIAGE_LIGHTING_LAYOUT.properties
        .filter((property) => FOLIAGE_LIGHTING_UNIFORM_NAMES.has(property.name))
        .map((property) => ({
            name: property.name,
            type: property.type,
            ...(property.arrayLength !== undefined ? { arrayLength: property.arrayLength } : {}),
            stages: ['fragment'] as const,
            scope: property.scope,
            inspector: HIDDEN_INSPECTOR,
        }));

const createTerrainFoliageShaderEffect = (id: string): RenderShaderEffectDefinition => ({
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
            name: 'u_TintColor',
            type: 'vec4',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: [0.35, 0.62, 0.3, 1],
            inspector: { label: 'Tint Color', group: 'Foliage', control: 'color' },
        },
        {
            name: 'u_Albedo',
            type: 'sampler2D',
            stages: ['fragment'],
            scope: 'material',
            inspector: { label: 'Albedo', group: 'Foliage', control: 'texture' },
        },
        {
            name: 'u_UseAlbedo',
            type: 'bool',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: false,
            inspector: HIDDEN_INSPECTOR,
        },
        {
            name: 'u_AlphaCutoff',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0.35,
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
        ...createFoliageLightingProperties(),
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
        precision: 'highp',
        inputs: [
            { name: 'v_Normal', type: 'vec3' },
            { name: 'v_UV', type: 'vec2' },
        ],
        outputs: [{ name: 'o_Color', type: 'vec4' }],
        main: [
            '// Ozel model dokusu varsa onu ornekle; yoksa karta dikey degrade uygula.',
            'vec4 sampled = texture(u_Albedo, v_UV);',
            'float verticalShade = mix(0.72, 1.0, 1.0 - v_UV.y);',
            'vec3 base = u_UseAlbedo ? sampled.rgb : vec3(verticalShade);',
            'float alpha = u_UseAlbedo ? sampled.a : 1.0;',
            'if (u_UseAlbedo && alpha < u_AlphaCutoff) {',
            '    discard;',
            '}',
            'vec3 albedo = u_TintColor.rgb * base;',
            '',
            'vec3 lighting = vec3(1.0);',
            'if (u_ReceiveLighting) {',
            '    lighting = u_AmbientLight;',
            '    if (u_DirectionalLightCount > 0) {',
            '        vec3 lightDirection = normalize(-u_DirectionalLightDirection[0]);',
            '        // Cift tarafli kart: normalin isiga bakan yuzunu kullan.',
            '        float incidence = abs(dot(normalize(v_Normal), lightDirection));',
            '        lighting += u_DirectionalLightAmbientColor[0];',
            '        lighting += u_DirectionalLightColor[0] * u_DirectionalLightIntensity[0] * incidence;',
            '    }',
            '}',
            '',
            'o_Color = vec4(albedo * lighting, u_TintColor.a * alpha);',
        ],
    },
    renderState: {
        depthTest: true,
        cull: false,
        blend: false,
    },
});

export const createTerrainFoliageShaderDefinition = (
    id: string = DEFAULT_TERRAIN_FOLIAGE_SHADER_ID
): SceneShaderDefinition =>
    createSceneShaderDefinitionFromEffect(createTerrainFoliageShaderEffect(id), {
        attributes: {
            position: 'a_Position',
            normal: 'a_Normal',
            uv0: 'a_UV',
        },
    });
