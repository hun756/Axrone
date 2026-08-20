import type { GltfMeshSemantic, GltfShaderDefinition } from '@axrone/asset-gltf';
import { createLightingUniformLayout } from '@axrone/lighting';
import {
    createSceneShaderDefinitionFromEffect,
    type RenderShaderEffectDefinition,
    type RenderShaderPropertyDefinition,
} from '@axrone/scene-runtime';
import type {
    SceneMaterialPassDefinition,
    SceneMaterialSurfaceDefinition,
    SceneMaterialSurfaceFeaturesDefinition,
} from '@axrone/scene-runtime';

const GLTF_SHADER_PBR_ID = 'gltf/pbr';
const GLTF_SHADER_UNLIT_ID = 'gltf/unlit';
const GLTF_SHADER_DOUBLE_SIDED_SUFFIX = '/double-sided';
const GLTF_SHADER_BLEND_SUFFIX = '/blend';
const MAX_GLTF_SKIN_JOINTS = 128;
const GLTF_LIGHTING_LAYOUT = createLightingUniformLayout({
    maxDirectionalLights: 1,
    maxPointLights: 4,
    maxSpotLights: 4,
    maxLocalLights: 4,
});
const GLTF_LIGHTING_UNIFORMS = GLTF_LIGHTING_LAYOUT.names;
const MAX_GLTF_DIRECTIONAL_LIGHTS = GLTF_LIGHTING_LAYOUT.capacity.maxDirectionalLights;
const MAX_GLTF_POINT_LIGHTS = GLTF_LIGHTING_LAYOUT.capacity.maxPointLights;
const MAX_GLTF_SPOT_LIGHTS = GLTF_LIGHTING_LAYOUT.capacity.maxSpotLights;

export type GltfMaterialUniformMap = Readonly<Record<string, unknown>>;

const HIDDEN_INSPECTOR = Object.freeze({ hidden: true } as const);
const GLTF_ALPHA_MODE_OPTIONS = Object.freeze([
    { label: 'Opaque', value: 0 },
    { label: 'Mask', value: 1 },
    { label: 'Blend', value: 2 },
] as const);

const createLightingProperties = (): readonly RenderShaderPropertyDefinition[] =>
    GLTF_LIGHTING_LAYOUT.properties
        .filter(
            (property) =>
                property.name !== 'u_Exposure' &&
                property.name !== 'u_Gamma' &&
                !property.name.startsWith('u_Local')
        )
        .map((property) => ({
        name: property.name,
        type: property.type,
        ...(property.arrayLength !== undefined ? { arrayLength: property.arrayLength } : {}),
        stages: ['fragment'],
        scope: property.scope,
        inspector: HIDDEN_INSPECTOR,
    }));

const GLTF_UNLIT_ATTRIBUTES = Object.freeze({
    position: 'a_Position',
    uv0: 'a_UV0',
    uv1: 'a_UV1',
    joints0: 'a_Joints0',
    weights0: 'a_Weights0',
} satisfies Partial<Record<GltfMeshSemantic, string>>);

const GLTF_PBR_ATTRIBUTES = Object.freeze({
    position: 'a_Position',
    normal: 'a_Normal',
    uv0: 'a_UV0',
    tangent: 'a_Tangent',
    uv1: 'a_UV1',
    joints0: 'a_Joints0',
    weights0: 'a_Weights0',
} satisfies Partial<Record<GltfMeshSemantic, string>>);

const createSurfaceTextureProperties = (
    uniformName: string,
    label: string,
    group: string,
    options: {
        readonly scale?: {
            readonly label: string;
            readonly min: number;
            readonly max: number;
            readonly step?: number;
            readonly defaultValue: number;
        };
        readonly strength?: {
            readonly label: string;
            readonly min: number;
            readonly max: number;
            readonly step?: number;
            readonly defaultValue: number;
        };
    } = {}
): readonly RenderShaderPropertyDefinition[] => {
    const properties: RenderShaderPropertyDefinition[] = [
        {
            name: uniformName,
            type: 'sampler2D',
            stages: ['fragment'],
            scope: 'material',
            inspector: {
                label,
                group,
                control: 'texture',
            },
        },
        {
            name: `${uniformName}_ST`,
            type: 'vec4',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: [1, 1, 0, 0],
            inspector: HIDDEN_INSPECTOR,
        },
        {
            name: `${uniformName}_Rotation`,
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0,
            inspector: HIDDEN_INSPECTOR,
        },
        {
            name: `${uniformName}_TexCoord`,
            type: 'int',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: -1,
            inspector: HIDDEN_INSPECTOR,
        },
    ];

    if (options.scale) {
        properties.push({
            name: `${uniformName}_Scale`,
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: options.scale.defaultValue,
            inspector: {
                label: options.scale.label,
                group,
                control: 'slider',
                min: options.scale.min,
                max: options.scale.max,
                step: options.scale.step,
            },
        });
    }

    if (options.strength) {
        properties.push({
            name: `${uniformName}_Strength`,
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: options.strength.defaultValue,
            inspector: {
                label: options.strength.label,
                group,
                control: 'slider',
                min: options.strength.min,
                max: options.strength.max,
                step: options.strength.step,
            },
        });
    }

    return properties;
};

const createSharedObjectProperties = (): readonly RenderShaderPropertyDefinition[] => [
    {
        name: 'u_Model',
        type: 'mat4',
        stages: ['vertex'],
        scope: 'object',
    },
    {
        name: 'u_View',
        type: 'mat4',
        stages: ['vertex'],
        scope: 'camera',
    },
    {
        name: 'u_Projection',
        type: 'mat4',
        stages: ['vertex'],
        scope: 'camera',
    },
    {
        name: 'u_Skinning',
        type: 'bool',
        stages: ['vertex'],
        scope: 'object',
        inspector: HIDDEN_INSPECTOR,
    },
    {
        name: 'u_SkinJointCount',
        type: 'int',
        stages: ['vertex'],
        scope: 'object',
        inspector: HIDDEN_INSPECTOR,
    },
    {
        name: 'u_JointMatrices',
        type: 'mat4',
        arrayLength: MAX_GLTF_SKIN_JOINTS,
        stages: ['vertex'],
        scope: 'object',
        inspector: HIDDEN_INSPECTOR,
    },
];

const createSharedAlphaProperties = (): readonly RenderShaderPropertyDefinition[] => [
    {
        name: '_AlphaMode',
        type: 'float',
        stages: ['fragment'],
        scope: 'material',
        defaultValue: 0,
        inspector: {
            label: 'Alpha Mode',
            group: 'Surface',
            control: 'select',
            options: GLTF_ALPHA_MODE_OPTIONS,
        },
    },
    {
        name: '_AlphaCutoff',
        type: 'float',
        stages: ['fragment'],
        scope: 'material',
        defaultValue: 0.5,
        inspector: {
            label: 'Alpha Cutoff',
            group: 'Surface',
            control: 'slider',
            min: 0,
            max: 1,
            step: 0.01,
        },
    },
    {
        name: '_DoubleSided',
        type: 'float',
        stages: ['fragment'],
        scope: 'material',
        defaultValue: 0,
        inspector: {
            label: 'Double Sided',
            group: 'Surface',
            control: 'toggle',
        },
    },
];

const GLTF_SHADER_LIBRARIES = Object.freeze([
    {
        id: 'gltf.skinning',
        code: [
            'mat4 resolveSkinMatrix() {',
            '    if (!u_Skinning || u_SkinJointCount <= 0) {',
            '        return mat4(1.0);',
            '    }',
            '    mat4 skin = mat4(0.0);',
            '    skin += u_JointMatrices[int(a_Joints0.x)] * a_Weights0.x;',
            '    skin += u_JointMatrices[int(a_Joints0.y)] * a_Weights0.y;',
            '    skin += u_JointMatrices[int(a_Joints0.z)] * a_Weights0.z;',
            '    skin += u_JointMatrices[int(a_Joints0.w)] * a_Weights0.w;',
            '    return skin;',
            '}',
        ],
    },
    {
        id: 'gltf.uv',
        code: [
            'vec2 selectUV(int texCoord) {',
            '    return texCoord == 1 ? v_UV1 : v_UV0;',
            '}',
            '',
            'vec2 transformUV(vec2 uv, vec4 st, float rotation) {',
            '    vec2 scaled = uv * st.xy;',
            '    float c = cos(rotation);',
            '    float s = sin(rotation);',
            '    vec2 rotated = vec2(c * scaled.x - s * scaled.y, s * scaled.x + c * scaled.y);',
            '    return rotated + st.zw;',
            '}',
        ],
    },
    {
        id: 'gltf.color-space',
        code: [
            'vec3 linearToSrgb(vec3 color) {',
            '    vec3 clamped = clamp(color, vec3(0.0), vec3(1.0));',
            '    vec3 cutoff = step(vec3(0.0031308), clamped);',
            '    vec3 lower = clamped * 12.92;',
            '    vec3 higher = 1.055 * pow(clamped, vec3(1.0 / 2.4)) - 0.055;',
            '    return mix(lower, higher, cutoff);',
            '}',
            '',
            'vec3 srgbToLinear(vec3 color) {',
            '    vec3 clamped = clamp(color, vec3(0.0), vec3(1.0));',
            '    vec3 cutoff = step(vec3(0.04045), clamped);',
            '    vec3 lower = clamped / 12.92;',
            '    vec3 higher = pow((clamped + 0.055) / 1.055, vec3(2.4));',
            '    return mix(lower, higher, cutoff);',
            '}',
        ],
    },
    {
        id: 'gltf.pbr-lighting',
        code: [
            'const float PI = 3.14159265359;',
            'const float MIN_ROUGHNESS = 0.04;',
            'const float MAX_ROUGHNESS = 1.0;',
            '',
            'vec3 resolveNormal() {',
            '    vec3 normal = length(v_WorldNormal) > 0.0001 ? normalize(v_WorldNormal) : vec3(0.0, 0.0, 1.0);',
            '    if (_NormalTexture_TexCoord < 0 || length(v_WorldTangent.xyz) <= 0.0001) {',
            '        return normal;',
            '    }',
            '    vec2 uv = transformUV(selectUV(_NormalTexture_TexCoord), _NormalTexture_ST, _NormalTexture_Rotation);',
            '    vec3 tangentNormal = texture(_NormalTexture, uv).xyz * 2.0 - 1.0;',
            '    tangentNormal.xy *= _NormalTexture_Scale;',
            '    vec3 tangent = normalize(v_WorldTangent.xyz);',
            '    vec3 bitangent = normalize(cross(normal, tangent)) * (v_WorldTangent.w == 0.0 ? 1.0 : v_WorldTangent.w);',
            '    mat3 tbn = mat3(tangent, bitangent, normal);',
            '    return normalize(tbn * tangentNormal);',
            '}',
            '',
            'vec3 resolveClearcoatNormal(vec3 baseNormal) {',
            '    if (_ClearcoatNormalTexture_TexCoord < 0 || length(v_WorldTangent.xyz) <= 0.0001) {',
            '        return baseNormal;',
            '    }',
            '    vec2 uv = transformUV(selectUV(_ClearcoatNormalTexture_TexCoord), _ClearcoatNormalTexture_ST, _ClearcoatNormalTexture_Rotation);',
            '    vec3 tangentNormal = texture(_ClearcoatNormalTexture, uv).xyz * 2.0 - 1.0;',
            '    tangentNormal.xy *= _ClearcoatNormalTexture_Scale;',
            '    vec3 tangent = normalize(v_WorldTangent.xyz);',
            '    vec3 bitangent = normalize(cross(baseNormal, tangent)) * (v_WorldTangent.w == 0.0 ? 1.0 : v_WorldTangent.w);',
            '    mat3 tbn = mat3(tangent, bitangent, baseNormal);',
            '    return normalize(tbn * tangentNormal);',
            '}',
            '',
            'float rangeAttenuation(float distanceToLight, float range) {',
            '    if (range <= 0.0) {',
            '        return 1.0;',
            '    }',
            '    float atten = clamp(1.0 - distanceToLight / range, 0.0, 1.0);',
            '    return atten * atten;',
            '}',
            '',
            'float spotAttenuation(vec3 lightDir, vec3 spotDir, float innerConeCosine, float outerConeCosine) {',
            '    float cd = dot(normalize(-lightDir), normalize(spotDir));',
            '    return smoothstep(outerConeCosine, innerConeCosine, cd);',
            '}',
            '',
            'float distributionGgx(float NdotH, float roughness) {',
            '    float a = roughness * roughness;',
            '    float a2 = a * a;',
            '    float NdotH2 = NdotH * NdotH;',
            '    float denom = NdotH2 * (a2 - 1.0) + 1.0;',
            '    denom = PI * denom * denom;',
            '    return a2 / max(denom, 1e-7);',
            '}',
            '',
            'float geometrySchlickGgx(float NdotV, float roughness) {',
            '    float r = roughness + 1.0;',
            '    float k = (r * r) / 8.0;',
            '    return NdotV / (NdotV * (1.0 - k) + k);',
            '}',
            '',
            'float geometrySmith(float NdotV, float NdotL, float roughness) {',
            '    return geometrySchlickGgx(NdotV, roughness) * geometrySchlickGgx(NdotL, roughness);',
            '}',
            '',
            'vec3 fresnelSchlick(float cosTheta, vec3 F0) {',
            '    float t = 1.0 - cosTheta;',
            '    float t2 = t * t;',
            '    float t5 = t2 * t2 * t;',
            '    return F0 + (1.0 - F0) * t5;',
            '}',
            '',
            'vec3 fresnelSchlickRoughness(float cosTheta, vec3 F0, float roughness) {',
            '    float t = 1.0 - cosTheta;',
            '    float t2 = t * t;',
            '    float t5 = t2 * t2 * t;',
            '    return F0 + (max(vec3(1.0 - roughness), F0) - F0) * t5;',
            '}',
            '',
            'vec3 evaluateSpecularBrdf(vec3 N, vec3 V, vec3 L, float roughness) {',
            '    vec3 H = normalize(V + L);',
            '    float NdotH = max(dot(N, H), 0.0);',
            '    float NdotV = max(dot(N, V), 0.001);',
            '    float NdotL = max(dot(N, L), 0.0);',
            '    float VdotH = max(dot(V, H), 0.0);',
            '    if (NdotL <= 0.0) {',
            '        return vec3(0.0);',
            '    }',
            '    float D = distributionGgx(NdotH, roughness);',
            '    float G = geometrySmith(NdotV, NdotL, roughness);',
            '    vec3 F = fresnelSchlick(VdotH, vec3(0.04));',
            '    vec3 numerator = D * G * F;',
            '    float denominator = 4.0 * NdotV * NdotL + 0.001;',
            '    return numerator / denominator;',
            '}',
            '',
            'vec3 evaluateLight(vec3 normal, vec3 viewDir, vec3 albedo, float metallic, float roughness, vec3 lightDir, vec3 lightColor, float intensity) {',
            '    float NdotL = max(dot(normal, lightDir), 0.0);',
            '    if (NdotL <= 0.0) {',
            '        return vec3(0.0);',
            '    }',
            '    vec3 F0 = mix(vec3(0.04), albedo, metallic);',
            '    vec3 F = fresnelSchlick(max(dot(normalize(viewDir + lightDir), viewDir), 0.0), F0);',
            '    vec3 kS = F;',
            '    vec3 kD = (vec3(1.0) - kS) * (1.0 - metallic);',
            '    vec3 diffuse = kD * albedo / PI;',
            '    vec3 specular = evaluateSpecularBrdf(normal, viewDir, lightDir, roughness);',
            '    vec3 kDenergy = (vec3(1.0) - F) * (1.0 - metallic);',
            '    vec3 specContrib = F0 * specular;',
            '    return (kDenergy * albedo / PI + specContrib) * lightColor * intensity * NdotL;',
            '}',
            '',
            'vec3 evaluateClearcoatLight(vec3 normal, vec3 viewDir, float clearcoat, float roughness, vec3 lightDir, vec3 lightColor, float intensity) {',
            '    if (clearcoat <= 0.0) {',
            '        return vec3(0.0);',
            '    }',
            '    float NdotL = max(dot(normal, lightDir), 0.0);',
            '    if (NdotL <= 0.0) {',
            '        return vec3(0.0);',
            '    }',
            '    vec3 H = normalize(viewDir + lightDir);',
            '    float NdotH = max(dot(normal, H), 0.0);',
            '    float NdotV = max(dot(normal, viewDir), 0.001);',
            '    float VdotH = max(dot(viewDir, H), 0.0);',
            '    float D = distributionGgx(NdotH, roughness);',
            '    float G = geometrySchlickGgx(NdotV, roughness) * geometrySchlickGgx(NdotL, roughness);',
            '    float F = 0.04 + 0.96 * pow(1.0 - VdotH, 5.0);',
            '    float spec = (D * G * F) / (4.0 * NdotV * NdotL + 0.001);',
            '    return vec3(spec * clearcoat) * lightColor * intensity * NdotL;',
            '}',
        ],
    },
    {
        id: 'gltf.ibl',
        code: [
            'vec3 evaluateIBLDiffuse(vec3 N, vec3 F0, float roughness, vec3 albedo, float metallic) {',
            '    vec3 F = fresnelSchlickRoughness(max(dot(N, normalize(u_CameraPosition - v_WorldPosition)), 0.0), F0, roughness);',
            '    vec3 kD = (1.0 - F) * (1.0 - metallic);',
            '    float hemiFactor = clamp(N.y * 0.5 + 0.5, 0.0, 1.0);',
            '    vec3 irradiance = mix(u_GroundLight, u_SkyLight, hemiFactor);',
            '    return kD * irradiance * albedo;',
            '}',
            '',
            'vec3 evaluateIBLSpecular(vec3 N, vec3 V, vec3 F0, float roughness) {',
            '    vec3 R = reflect(-V, N);',
            '    vec3 F = fresnelSchlickRoughness(max(dot(N, V), 0.0), F0, roughness);',
            '    float hemiFactor = clamp(R.y * 0.5 + 0.5, 0.0, 1.0);',
            '    vec3 prefilteredColor = mix(u_GroundLight * 0.5, u_SkyLight, hemiFactor);',
            '    float NdotV = max(dot(N, V), 0.001);',
            '    vec2 envBRDF = vec2(roughness * roughness * 0.25, NdotV * 0.5);',
            '    return prefilteredColor * (F * envBRDF.x + envBRDF.y);',
            '}',
        ],
    },
    {
        id: 'gltf.shadow',
        code: [
            'float sampleShadowMap(vec2 uv, float compareDepth) {',
            '    return step(compareDepth, texture(u_ShadowMap, uv).r);',
            '}',
            '',
            'float shadowPcf2x2(vec2 baseUv, float compareDepth, vec2 texelSize) {',
            '    float shadow = 0.0;',
            '    shadow += step(compareDepth, texture(u_ShadowMap, baseUv + vec2(-0.5, -0.5) * texelSize).r);',
            '    shadow += step(compareDepth, texture(u_ShadowMap, baseUv + vec2( 0.5, -0.5) * texelSize).r);',
            '    shadow += step(compareDepth, texture(u_ShadowMap, baseUv + vec2(-0.5,  0.5) * texelSize).r);',
            '    shadow += step(compareDepth, texture(u_ShadowMap, baseUv + vec2( 0.5,  0.5) * texelSize).r);',
            '    return shadow * 0.25;',
            '}',
            '',
            'float shadowPcf3x3(vec2 baseUv, float compareDepth, vec2 texelSize) {',
            '    float shadow = 0.0;',
            '    for (int x = -1; x <= 1; x++) {',
            '        for (int y = -1; y <= 1; y++) {',
            '            vec2 offset = vec2(float(x), float(y)) * texelSize;',
            '            shadow += step(compareDepth, texture(u_ShadowMap, baseUv + offset).r);',
            '        }',
            '    }',
            '    return shadow / 9.0;',
            '}',
            '',
            'float resolveShadowFactor(vec3 worldPosition, vec3 worldNormal, vec3 lightDirection) {',
            '    if (u_ShadowEnabled <= 0) return 1.0;',
            '    int cascadeIndex = 0;',
            '    float viewDepth = length(u_CameraPosition - worldPosition);',
            '    for (int i = 0; i < 4; i++) {',
            '        if (viewDepth < u_ShadowCascadeSplits[i]) {',
            '            cascadeIndex = i;',
            '            break;',
            '        }',
            '        cascadeIndex = i;',
            '    }',
            '    if (cascadeIndex >= u_ShadowCascadeCount) return 1.0;',
            '    vec4 lightSpacePos = u_LightSpaceMatrices[cascadeIndex] * vec4(worldPosition, 1.0);',
            '    vec3 projCoords = lightSpacePos.xyz / lightSpacePos.w;',
            '    projCoords = projCoords * 0.5 + 0.5;',
            '    if (projCoords.x < 0.0 || projCoords.x > 1.0 || projCoords.y < 0.0 || projCoords.y > 1.0) return 1.0;',
            '    if (projCoords.z > 1.0) return 1.0;',
            '    float NdotL = max(dot(worldNormal, lightDirection), 0.0);',
            '    float bias = max(u_ShadowBias * (1.0 - NdotL), u_ShadowBias * 0.1);',
            '    vec3 biasedPos = worldPosition + worldNormal * u_ShadowNormalBias;',
            '    vec4 biasedLightPos = u_LightSpaceMatrices[cascadeIndex] * vec4(biasedPos, 1.0);',
            '    vec3 biasedProj = biasedLightPos.xyz / biasedLightPos.w * 0.5 + 0.5;',
            '    float currentDepth = biasedProj.z - bias;',
            '    vec2 texelSize = vec2(1.0 / 2048.0);',
            '    return shadowPcf3x3(biasedProj.xy, currentDepth, texelSize);',
            '}',
        ],
    },
    {
        id: 'gltf.advanced-shading',
        code: [
            'vec3 evaluateAnisotropicSpecular(vec3 N, vec3 V, vec3 L, float roughness, float anisotropy, vec3 tangent, vec3 bitangent) {',
            '    float at = max(roughness * (1.0 + anisotropy), 0.001);',
            '    float ab = max(roughness * (1.0 - anisotropy), 0.001);',
            '    vec3 H = normalize(V + L);',
            '    float NdotH = max(dot(N, H), 0.0);',
            '    float TdotH = dot(tangent, H);',
            '    float BdotH = dot(bitangent, H);',
            '    float NdotV = max(dot(N, V), 0.001);',
            '    float NdotL = max(dot(N, L), 0.0);',
            '    if (NdotL <= 0.0) return vec3(0.0);',
            '    float a2 = at * ab;',
            '    vec3 v = vec3(ab * TdotH, at * BdotH, a2 * NdotH);',
            '    float v2 = dot(v, v);',
            '    float w2 = a2 / max(v2, 1e-7);',
            '    float D = a2 * w2 * w2 * (1.0 / PI);',
            '    float G = geometrySmith(NdotV, NdotL, roughness);',
            '    float VdotH = max(dot(V, H), 0.0);',
            '    vec3 F = fresnelSchlick(VdotH, vec3(0.04));',
            '    return (D * G * F) / (4.0 * NdotV * NdotL + 0.001);',
            '}',
            '',
            'vec3 evaluateSheen(vec3 N, vec3 V, vec3 L, vec3 sheenColor, float sheenRoughness) {',
            '    vec3 H = normalize(V + L);',
            '    float NdotL = max(dot(N, L), 0.0);',
            '    float NdotH = max(dot(N, H), 0.0);',
            '    float NdotV = max(dot(N, V), 0.001);',
            '    if (NdotL <= 0.0) return vec3(0.0);',
            '    float roughnessSq = max(sheenRoughness * sheenRoughness, 1e-4);',
            '    float invRoughnessSq = 1.0 / roughnessSq;',
            '    float cos2h = NdotH * NdotH;',
            '    float sin2h = max(1.0 - cos2h, 0.0);',
            '    float D = (2.0 + invRoughnessSq) * pow(sin2h, invRoughnessSq * 0.5) / (2.0 * PI);',
            '    float VdotH = max(dot(V, H), 0.0);',
            '    float F = VdotH * exp2((-5.55473 * VdotH - 6.98316) * VdotH);',
            '    return sheenColor * D * F * NdotL;',
            '}',
            '',
            'vec3 evaluateSubsurface(vec3 albedo, vec3 N, vec3 V, vec3 L, vec3 lightColor, float intensity, float thickness, vec3 subsurfaceColor) {',
            '    vec3 H = normalize(V + L);',
            '    float VdotH = max(dot(V, H), 0.0);',
            '    float NdotL = max(dot(N, L), 0.0);',
            '    float scatter = pow(clamp(1.0 - VdotH + 0.4, 0.0, 1.0), 3.0) * 0.5;',
            '    vec3 sssColor = mix(albedo, subsurfaceColor, thickness);',
            '    return sssColor * (scatter + NdotL * 0.2) * lightColor * intensity;',
            '}',
            '',
            'vec3 evaluateTransmission(vec3 albedo, vec3 N, vec3 V, vec3 L, float ior, float thickness) {',
            '    vec3 refracted = refract(-V, N, 1.0 / max(ior, 1.001));',
            '    float NdotL = max(dot(N, L), 0.0);',
            '    float VdotL = max(dot(V, -L), 0.0);',
            '    float transmission = pow(clamp(VdotL, 0.0, 1.0), 4.0);',
            '    float absorption = exp(-thickness * 2.0);',
            '    return albedo * transmission * absorption * (1.0 - NdotL * 0.3);',
            '}',
            '',
            'vec3 evaluateIridescence(float cosTheta, float iridescence, float ior, float thickness) {',
            '    float x = cosTheta;',
            '    float x2 = x * x;',
            '    float x3 = x2 * x;',
            '    float x4 = x3 * x;',
            '    float x5 = x4 * x;',
            '    float theta = acos(clamp(cosTheta, -1.0, 1.0));',
            '    float sinTheta2 = sin(theta) * sin(theta);',
            '    float cosTheta2 = cosTheta * cosTheta;',
            '    float eta2 = ior * ior;',
            '    float t0 = sqrt(eta2 - sinTheta2);',
            '    float t1 = t0 * cosTheta;',
            '    float t2 = (2.0 * PI * thickness * t0) / 550.0;',
            '    vec3 wavelengths = vec3(650.0, 550.0, 450.0);',
            '    vec3 phase = 2.0 * PI * thickness * t0 / wavelengths;',
            '    vec3 reflectance = vec3(',
            '        0.5 * (1.0 + cos(phase.x)),',
            '        0.5 * (1.0 + cos(phase.y)),',
            '        0.5 * (1.0 + cos(phase.z))',
            '    );',
            '    float f0 = pow((ior - 1.0) / (ior + 1.0), 2.0);',
            '    reflectance *= f0 * 2.0;',
            '    return mix(vec3(1.0), reflectance, iridescence);',
            '}',
        ],
    },
] as const);

const createGltfShaderDefinitionFromEffect = (
    effect: RenderShaderEffectDefinition,
    attributes: Partial<Record<GltfMeshSemantic, string>>
): GltfShaderDefinition => {
    const definition = createSceneShaderDefinitionFromEffect(effect);

    return {
        id: definition.id,
        vertexSource: definition.vertexSource ?? '',
        fragmentSource: definition.fragmentSource ?? '',
        effect: definition.effect,
        attributes: { ...attributes },
        uniforms: definition.uniforms ? [...definition.uniforms] : undefined,
        depthTest: definition.depthTest,
        cull: definition.cull,
        blend: definition.blend,
    };
};

const normalizeNumericUniform = (value: unknown, fallback: number): number => {
    if (typeof value === 'number' && Number.isFinite(value)) {
        return value;
    }

    return fallback;
};

const isGltfBlendAlphaMode = (uniforms: GltfMaterialUniformMap | undefined): boolean =>
    normalizeNumericUniform(uniforms?._AlphaMode, 0) >= 1.5;

const isGltfDoubleSided = (uniforms: GltfMaterialUniformMap | undefined): boolean =>
    normalizeNumericUniform(uniforms?._DoubleSided, 0) >= 0.5;

export const createGltfRuntimeSurfaceFeatures = (
    shaderId: string,
    uniforms?: GltfMaterialUniformMap
): SceneMaterialSurfaceFeaturesDefinition => {
    const unlit = shaderId.startsWith(GLTF_SHADER_UNLIT_ID);
    const useClearcoatMap =
        !unlit && normalizeNumericUniform(uniforms?._ClearcoatTexture_TexCoord, -1) >= 0;
    const useClearcoatRoughnessMap =
        !unlit && normalizeNumericUniform(uniforms?._ClearcoatRoughnessTexture_TexCoord, -1) >= 0;
    const useClearcoatNormalMap =
        !unlit && normalizeNumericUniform(uniforms?._ClearcoatNormalTexture_TexCoord, -1) >= 0;

    return {
        useVertexColor: false,
        hasSecondUv: normalizeNumericUniform(uniforms?._BaseColorTexture_TexCoord, 0) === 1 ||
            normalizeNumericUniform(uniforms?._MetallicRoughnessTexture_TexCoord, 0) === 1 ||
            normalizeNumericUniform(uniforms?._NormalTexture_TexCoord, 0) === 1 ||
            normalizeNumericUniform(uniforms?._OcclusionTexture_TexCoord, 0) === 1 ||
            normalizeNumericUniform(uniforms?._EmissiveTexture_TexCoord, 0) === 1 ||
            normalizeNumericUniform(uniforms?._ClearcoatTexture_TexCoord, 0) === 1 ||
            normalizeNumericUniform(uniforms?._ClearcoatRoughnessTexture_TexCoord, 0) === 1 ||
            normalizeNumericUniform(uniforms?._ClearcoatNormalTexture_TexCoord, 0) === 1,
        useNormalMap: !unlit && normalizeNumericUniform(uniforms?._NormalTexture_TexCoord, -1) >= 0,
        useTwoSided: isGltfDoubleSided(uniforms),
        useAlbedoMap: normalizeNumericUniform(uniforms?._BaseColorTexture_TexCoord, -1) >= 0,
        usePbrMap: false,
        useMetallicRoughnessMap:
            !unlit && normalizeNumericUniform(uniforms?._MetallicRoughnessTexture_TexCoord, -1) >= 0,
        useOcclusionMap: !unlit && normalizeNumericUniform(uniforms?._OcclusionTexture_TexCoord, -1) >= 0,
        useEmissiveMap: !unlit && normalizeNumericUniform(uniforms?._EmissiveTexture_TexCoord, -1) >= 0,
        useClearcoat:
            !unlit &&
            (normalizeNumericUniform(uniforms?._ClearcoatFactor, 0) > 0 ||
                useClearcoatMap ||
                useClearcoatRoughnessMap ||
                useClearcoatNormalMap),
        useClearcoatMap,
        useClearcoatRoughnessMap,
        useClearcoatNormalMap,
        useAlphaTest: normalizeNumericUniform(uniforms?._AlphaMode, 0) >= 0.5 && normalizeNumericUniform(uniforms?._AlphaMode, 0) < 1.5,
        useAnisotropy: !unlit && Math.abs(normalizeNumericUniform(uniforms?._AnisotropyFactor, 0)) > 0.001,
        useSheen: !unlit && normalizeNumericUniform(uniforms?._SheenFactor, 0) > 0.001,
        useSubsurface: !unlit && normalizeNumericUniform(uniforms?._SubsurfaceFactor, 0) > 0.001,
        useTransmission: !unlit && normalizeNumericUniform(uniforms?._TransmissionFactor, 0) > 0.001,
        useIridescence: !unlit && normalizeNumericUniform(uniforms?._IridescenceFactor, 0) > 0.001,
    };
};

export const createGltfRuntimeSurfaceDefinition = (
    shaderId: string,
    uniforms?: GltfMaterialUniformMap
): SceneMaterialSurfaceDefinition => ({
    shadingModel: shaderId.startsWith(GLTF_SHADER_UNLIT_ID) ? 'unlit' : 'pbr',
    alphaMode:
        normalizeNumericUniform(uniforms?._AlphaMode, 0) >= 1.5
            ? 'blend'
            : normalizeNumericUniform(uniforms?._AlphaMode, 0) >= 0.5
              ? 'mask'
              : 'opaque',
    alphaCutoff: normalizeNumericUniform(uniforms?._AlphaCutoff, 0.5),
    pbrUvSet: 0,
    features: createGltfRuntimeSurfaceFeatures(shaderId, uniforms),
    tilingOffset: [1, 1, 0, 0],
    albedo: Array.isArray(uniforms?._BaseColorFactor)
        ? [
              Number((uniforms?._BaseColorFactor as readonly unknown[])[0] ?? 1),
              Number((uniforms?._BaseColorFactor as readonly unknown[])[1] ?? 1),
              Number((uniforms?._BaseColorFactor as readonly unknown[])[2] ?? 1),
              Number((uniforms?._BaseColorFactor as readonly unknown[])[3] ?? 1),
          ]
        : [1, 1, 1, 1],
    normalScale: normalizeNumericUniform(uniforms?._NormalTexture_Scale, 1),
    occlusion: normalizeNumericUniform(uniforms?._OcclusionTexture_Strength, 1),
    roughness: normalizeNumericUniform(uniforms?._RoughnessFactor, 1),
    metallic: normalizeNumericUniform(uniforms?._MetallicFactor, 1),
    clearcoat: normalizeNumericUniform(uniforms?._ClearcoatFactor, 0),
    clearcoatRoughness: normalizeNumericUniform(uniforms?._ClearcoatRoughnessFactor, 0),
    clearcoatNormalScale: normalizeNumericUniform(uniforms?._ClearcoatNormalTexture_Scale, 1),
    specularIntensity: 1,
    emissive: Array.isArray(uniforms?._EmissiveFactor)
        ? [
              Number((uniforms?._EmissiveFactor as readonly unknown[])[0] ?? 0),
              Number((uniforms?._EmissiveFactor as readonly unknown[])[1] ?? 0),
              Number((uniforms?._EmissiveFactor as readonly unknown[])[2] ?? 0),
          ]
        : [0, 0, 0],
    emissiveScale: [1, 1, 1],
    sheenFactor: normalizeNumericUniform(uniforms?._SheenFactor, 0),
    sheenColor: Array.isArray(uniforms?._SheenColorFactor)
        ? [
              Number((uniforms?._SheenColorFactor as readonly unknown[])[0] ?? 0),
              Number((uniforms?._SheenColorFactor as readonly unknown[])[1] ?? 0),
              Number((uniforms?._SheenColorFactor as readonly unknown[])[2] ?? 0),
          ]
        : [0, 0, 0],
    sheenRoughness: normalizeNumericUniform(uniforms?._SheenRoughnessFactor, 0.5),
    anisotropy: normalizeNumericUniform(uniforms?._AnisotropyFactor, 0),
    anisotropyRotation: normalizeNumericUniform(uniforms?._AnisotropyRotation, 0),
    subsurfaceFactor: normalizeNumericUniform(uniforms?._SubsurfaceFactor, 0),
    subsurfaceColor: Array.isArray(uniforms?._SubsurfaceColorFactor)
        ? [
              Number((uniforms?._SubsurfaceColorFactor as readonly unknown[])[0] ?? 1),
              Number((uniforms?._SubsurfaceColorFactor as readonly unknown[])[1] ?? 0.8),
              Number((uniforms?._SubsurfaceColorFactor as readonly unknown[])[2] ?? 0.6),
          ]
        : [1, 0.8, 0.6],
    subsurfaceThickness: normalizeNumericUniform(uniforms?._SubsurfaceThickness, 1),
    transmissionFactor: normalizeNumericUniform(uniforms?._TransmissionFactor, 0),
    ior: normalizeNumericUniform(uniforms?._IorFactor, 1.5),
    thickness: normalizeNumericUniform(uniforms?._ThicknessFactor, 0),
    iridescenceFactor: normalizeNumericUniform(uniforms?._IridescenceFactor, 0),
    iridescenceIor: normalizeNumericUniform(uniforms?._IridescenceIor, 1.3),
    iridescenceThickness: normalizeNumericUniform(uniforms?._IridescenceThickness, 400),
});

export const createGltfRuntimeMaterialPasses = (
    uniforms?: GltfMaterialUniformMap
): readonly SceneMaterialPassDefinition[] => {
    const alphaMode = normalizeNumericUniform(uniforms?._AlphaMode, 0);
    const blendEnabled = alphaMode >= 1.5;
    const alphaTestEnabled = alphaMode >= 0.5 && alphaMode < 1.5;
    const doubleSided = isGltfDoubleSided(uniforms);
    const cullMode = doubleSided ? 'none' : 'back';

    return Object.freeze([
        {
            id: 'main',
            phase: 'default',
            primitive: 'triangle-list',
            rasterizerState: {
                cullMode,
                frontFace: 'ccw',
            },
            depthStencilState: {
                depthTest: true,
                depthWrite: !blendEnabled,
                depthFunc: 'less',
            },
            blendState: {
                targets: [
                    {
                        blend: blendEnabled,
                        srcColorFactor: 'src-alpha',
                        dstColorFactor: 'one-minus-src-alpha',
                        colorOp: 'add',
                        srcAlphaFactor: 'one',
                        dstAlphaFactor: 'one-minus-src-alpha',
                        alphaOp: 'add',
                    },
                ],
            },
        },
        {
            id: 'forward-add',
            phase: 'forward-add',
            primitive: 'triangle-list',
            rasterizerState: {
                cullMode,
                frontFace: 'ccw',
            },
            depthStencilState: {
                depthTest: true,
                depthWrite: false,
                depthFunc: 'lequal',
            },
            blendState: {
                targets: [
                    {
                        blend: true,
                        srcColorFactor: 'one',
                        dstColorFactor: 'one',
                        colorOp: 'add',
                        srcAlphaFactor: 'one',
                        dstAlphaFactor: 'one',
                        alphaOp: 'add',
                    },
                ],
            },
        },
        {
            id: 'shadow-caster',
            phase: 'shadow-caster',
            primitive: 'triangle-list',
            rasterizerState: {
                cullMode,
                frontFace: 'ccw',
            },
            depthStencilState: {
                depthTest: true,
                depthWrite: true,
                depthFunc: 'less',
            },
            blendState: {
                targets: [
                    {
                        blend: false,
                    },
                ],
            },
            ...(alphaTestEnabled
                ? {
                      priority: 1,
                  }
                : {}),
        },
    ]);
};

const createVariantShaderId = (
    baseId: string,
    options: {
        readonly blend: boolean;
        readonly doubleSided: boolean;
    }
): string => {
    let variantId = baseId;
    if (options.blend) {
        variantId += GLTF_SHADER_BLEND_SUFFIX;
    }
    if (options.doubleSided) {
        variantId += GLTF_SHADER_DOUBLE_SIDED_SUFFIX;
    }
    return variantId;
};

const resolveVariantRenderState = (uniforms: GltfMaterialUniformMap | undefined): {
    readonly blend: boolean;
    readonly cull: boolean;
} => {
    const blend = isGltfBlendAlphaMode(uniforms);
    const doubleSided = isGltfDoubleSided(uniforms);
    return {
        blend,
        cull: !doubleSided,
    };
};

const createGltfUnlitShaderEffect = (id: string): RenderShaderEffectDefinition => ({
    format: 'axrone.shader/effect',
    version: 1,
    id,
    attributes: [
        { name: 'a_Position', type: 'vec3', location: 0 },
        { name: 'a_UV0', type: 'vec2', location: 2 },
        { name: 'a_UV1', type: 'vec2', location: 5 },
        { name: 'a_Joints0', type: 'uvec4', location: 9 },
        { name: 'a_Weights0', type: 'vec4', location: 10 },
    ],
    varyings: [
        { name: 'v_UV0', type: 'vec2' },
        { name: 'v_UV1', type: 'vec2' },
    ],
    properties: [
        ...createSharedObjectProperties(),
        {
            name: '_BaseColorFactor',
            type: 'vec4',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: [1, 1, 1, 1],
            inspector: {
                label: 'Base Color',
                group: 'Surface',
                control: 'color',
            },
        },
        ...createSurfaceTextureProperties('_BaseColorTexture', 'Base Color Map', 'Maps'),
        ...createSharedAlphaProperties(),
    ],
    libraries: GLTF_SHADER_LIBRARIES,
    vertex: {
        includes: ['gltf.skinning'],
        main: [
            'v_UV0 = a_UV0;',
            'v_UV1 = a_UV1;',
            'vec4 localPosition = vec4(a_Position, 1.0);',
            'if (u_Skinning && u_SkinJointCount > 0) {',
            '    localPosition = resolveSkinMatrix() * localPosition;',
            '}',
            'gl_Position = u_Projection * u_View * u_Model * localPosition;',
        ],
    },
    fragment: {
        precision: 'mediump',
        outputs: [{ name: 'o_Color', type: 'vec4' }],
        includes: ['gltf.uv', 'gltf.color-space'],
        main: [
            'vec4 baseColor = _BaseColorFactor;',
            'if (_BaseColorTexture_TexCoord >= 0) {',
            '    vec2 uv = transformUV(selectUV(_BaseColorTexture_TexCoord), _BaseColorTexture_ST, _BaseColorTexture_Rotation);',
            '    baseColor *= texture(_BaseColorTexture, uv);',
            '}',
            'int alphaMode = int(_AlphaMode + 0.5);',
            'if (alphaMode == 1 && baseColor.a < _AlphaCutoff) {',
            '    discard;',
            '}',
            'if (alphaMode == 0 || alphaMode == 1) {',
            '    baseColor.a = 1.0;',
            '}',
            'o_Color = vec4(linearToSrgb(baseColor.rgb), baseColor.a);',
        ],
    },
    renderState: {
        depthTest: true,
        cull: true,
        blend: false,
    },
});

const createGltfPbrShaderEffect = (id: string): RenderShaderEffectDefinition => ({
    format: 'axrone.shader/effect',
    version: 1,
    id,
    attributes: [
        { name: 'a_Position', type: 'vec3', location: 0 },
        { name: 'a_Normal', type: 'vec3', location: 1 },
        { name: 'a_UV0', type: 'vec2', location: 2 },
        { name: 'a_Tangent', type: 'vec4', location: 4 },
        { name: 'a_UV1', type: 'vec2', location: 5 },
        { name: 'a_Joints0', type: 'uvec4', location: 9 },
        { name: 'a_Weights0', type: 'vec4', location: 10 },
    ],
    varyings: [
        { name: 'v_UV0', type: 'vec2' },
        { name: 'v_UV1', type: 'vec2' },
        { name: 'v_WorldPosition', type: 'vec3' },
        { name: 'v_WorldNormal', type: 'vec3' },
        { name: 'v_WorldTangent', type: 'vec4' },
    ],
    properties: [
        ...createSharedObjectProperties(),
        {
            name: 'u_ReceiveLighting',
            type: 'bool',
            stages: ['fragment'],
            scope: 'system',
            inspector: HIDDEN_INSPECTOR,
        },
        {
            name: 'u_ShadowEnabled',
            type: 'int',
            stages: ['fragment'],
            scope: 'frame',
            defaultValue: 0,
            inspector: HIDDEN_INSPECTOR,
        },
        {
            name: 'u_ShadowCascadeCount',
            type: 'int',
            stages: ['fragment'],
            scope: 'frame',
            defaultValue: 0,
            inspector: HIDDEN_INSPECTOR,
        },
        {
            name: 'u_ShadowBias',
            type: 'float',
            stages: ['fragment'],
            scope: 'frame',
            defaultValue: 0.0015,
            inspector: HIDDEN_INSPECTOR,
        },
        {
            name: 'u_ShadowNormalBias',
            type: 'float',
            stages: ['fragment'],
            scope: 'frame',
            defaultValue: 0.25,
            inspector: HIDDEN_INSPECTOR,
        },
        {
            name: 'u_ShadowCascadeSplits',
            type: 'float',
            arrayLength: 4,
            stages: ['fragment'],
            scope: 'frame',
            inspector: HIDDEN_INSPECTOR,
        },
        {
            name: 'u_LightSpaceMatrices',
            type: 'mat4',
            arrayLength: 4,
            stages: ['fragment'],
            scope: 'frame',
            inspector: HIDDEN_INSPECTOR,
        },
        {
            name: 'u_ShadowMap',
            type: 'sampler2D',
            stages: ['fragment'],
            scope: 'system',
            inspector: HIDDEN_INSPECTOR,
        },
        ...createLightingProperties(),
        {
            name: 'u_CameraPosition',
            type: 'vec3',
            stages: ['fragment'],
            scope: 'camera',
            inspector: HIDDEN_INSPECTOR,
        },
        {
            name: '_BaseColorFactor',
            type: 'vec4',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: [1, 1, 1, 1],
            inspector: {
                label: 'Base Color',
                group: 'Surface',
                control: 'color',
            },
        },
        ...createSurfaceTextureProperties('_BaseColorTexture', 'Base Color Map', 'Maps'),
        {
            name: '_MetallicFactor',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 1,
            inspector: {
                label: 'Metallic',
                group: 'Surface',
                control: 'slider',
                min: 0,
                max: 1,
                step: 0.01,
            },
        },
        {
            name: '_RoughnessFactor',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 1,
            inspector: {
                label: 'Roughness',
                group: 'Surface',
                control: 'slider',
                min: 0,
                max: 1,
                step: 0.01,
            },
        },
        ...createSurfaceTextureProperties(
            '_MetallicRoughnessTexture',
            'Metallic Roughness Map',
            'Maps'
        ),
        {
            name: '_ClearcoatFactor',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0,
            inspector: {
                label: 'Clear Coat',
                group: 'Clear Coat',
                control: 'slider',
                min: 0,
                max: 1,
                step: 0.01,
            },
        },
        {
            name: '_ClearcoatRoughnessFactor',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0,
            inspector: {
                label: 'Clear Coat Roughness',
                group: 'Clear Coat',
                control: 'slider',
                min: 0,
                max: 1,
                step: 0.01,
            },
        },
        ...createSurfaceTextureProperties('_ClearcoatTexture', 'Clear Coat Map', 'Clear Coat'),
        ...createSurfaceTextureProperties(
            '_ClearcoatRoughnessTexture',
            'Clear Coat Roughness Map',
            'Clear Coat'
        ),
        ...createSurfaceTextureProperties('_ClearcoatNormalTexture', 'Clear Coat Normal Map', 'Clear Coat', {
            scale: {
                label: 'Clear Coat Normal Scale',
                min: 0,
                max: 2,
                step: 0.01,
                defaultValue: 1,
            },
        }),
        ...createSurfaceTextureProperties('_NormalTexture', 'Normal Map', 'Maps', {
            scale: {
                label: 'Normal Scale',
                min: 0,
                max: 2,
                step: 0.01,
                defaultValue: 1,
            },
        }),
        ...createSurfaceTextureProperties('_OcclusionTexture', 'Occlusion Map', 'Maps', {
            strength: {
                label: 'Occlusion Strength',
                min: 0,
                max: 1,
                step: 0.01,
                defaultValue: 1,
            },
        }),
        {
            name: '_EmissiveFactor',
            type: 'vec3',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: [0, 0, 0],
            inspector: {
                label: 'Emissive',
                group: 'Emission',
                control: 'color',
            },
        },
        ...createSurfaceTextureProperties('_EmissiveTexture', 'Emissive Map', 'Emission'),
        {
            name: '_SheenFactor',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0,
            inspector: {
                label: 'Sheen Intensity',
                group: 'Sheen',
                control: 'slider',
                min: 0,
                max: 1,
                step: 0.01,
            },
        },
        {
            name: '_SheenColorFactor',
            type: 'vec3',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: [0, 0, 0],
            inspector: {
                label: 'Sheen Color',
                group: 'Sheen',
                control: 'color',
            },
        },
        {
            name: '_SheenRoughnessFactor',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0.5,
            inspector: {
                label: 'Sheen Roughness',
                group: 'Sheen',
                control: 'slider',
                min: 0,
                max: 1,
                step: 0.01,
            },
        },
        {
            name: '_AnisotropyFactor',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0,
            inspector: {
                label: 'Anisotropy',
                group: 'Anisotropy',
                control: 'slider',
                min: -1,
                max: 1,
                step: 0.01,
            },
        },
        {
            name: '_AnisotropyRotation',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0,
            inspector: {
                label: 'Anisotropy Rotation',
                group: 'Anisotropy',
                control: 'slider',
                min: 0,
                max: 6.283,
                step: 0.01,
            },
        },
        {
            name: '_SubsurfaceFactor',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0,
            inspector: {
                label: 'Subsurface',
                group: 'Subsurface',
                control: 'slider',
                min: 0,
                max: 1,
                step: 0.01,
            },
        },
        {
            name: '_SubsurfaceColorFactor',
            type: 'vec3',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: [1, 0.8, 0.6],
            inspector: {
                label: 'Subsurface Color',
                group: 'Subsurface',
                control: 'color',
            },
        },
        {
            name: '_SubsurfaceThickness',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 1,
            inspector: {
                label: 'Subsurface Thickness',
                group: 'Subsurface',
                control: 'slider',
                min: 0,
                max: 5,
                step: 0.01,
            },
        },
        {
            name: '_TransmissionFactor',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0,
            inspector: {
                label: 'Transmission',
                group: 'Transmission',
                control: 'slider',
                min: 0,
                max: 1,
                step: 0.01,
            },
        },
        {
            name: '_IorFactor',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 1.5,
            inspector: {
                label: 'Index of Refraction',
                group: 'Transmission',
                control: 'slider',
                min: 1,
                max: 3,
                step: 0.01,
            },
        },
        {
            name: '_ThicknessFactor',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0,
            inspector: {
                label: 'Thickness',
                group: 'Transmission',
                control: 'slider',
                min: 0,
                max: 5,
                step: 0.01,
            },
        },
        {
            name: '_IridescenceFactor',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0,
            inspector: {
                label: 'Iridescence',
                group: 'Iridescence',
                control: 'slider',
                min: 0,
                max: 1,
                step: 0.01,
            },
        },
        {
            name: '_IridescenceIor',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 1.3,
            inspector: {
                label: 'Iridescence IOR',
                group: 'Iridescence',
                control: 'slider',
                min: 1,
                max: 3,
                step: 0.01,
            },
        },
        {
            name: '_IridescenceThickness',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 400,
            inspector: {
                label: 'Iridescence Thickness',
                group: 'Iridescence',
                control: 'slider',
                min: 100,
                max: 1000,
                step: 1,
            },
        },
        ...createSharedAlphaProperties(),
    ],
    libraries: GLTF_SHADER_LIBRARIES,
    vertex: {
        includes: ['gltf.skinning'],
        main: [
            'v_UV0 = a_UV0;',
            'v_UV1 = a_UV1;',
            'mat4 skin = resolveSkinMatrix();',
            'vec4 localPosition = vec4(a_Position, 1.0);',
            'vec3 localNormal = a_Normal;',
            'vec3 localTangent = a_Tangent.xyz;',
            'if (u_Skinning && u_SkinJointCount > 0) {',
            '    localPosition = skin * localPosition;',
            '    localNormal = mat3(skin) * localNormal;',
            '    localTangent = mat3(skin) * localTangent;',
            '}',
            'vec4 worldPosition = u_Model * localPosition;',
            'v_WorldPosition = worldPosition.xyz;',
            'v_WorldNormal = normalize(mat3(u_Model) * localNormal);',
            'v_WorldTangent = vec4(normalize(mat3(u_Model) * localTangent), a_Tangent.w);',
            'gl_Position = u_Projection * u_View * worldPosition;',
        ],
    },
    fragment: {
        precision: 'highp',
        outputs: [{ name: 'o_Color', type: 'vec4' }],
        includes: ['gltf.uv', 'gltf.pbr-lighting', 'gltf.ibl', 'gltf.shadow', 'gltf.advanced-shading'],
        main: [
            'vec4 baseColor = _BaseColorFactor;',
            'if (_BaseColorTexture_TexCoord >= 0) {',
            '    vec2 uv = transformUV(selectUV(_BaseColorTexture_TexCoord), _BaseColorTexture_ST, _BaseColorTexture_Rotation);',
            '    baseColor *= texture(_BaseColorTexture, uv);',
            '}',
            'int alphaMode = int(_AlphaMode + 0.5);',
            'if (alphaMode == 1 && baseColor.a < _AlphaCutoff) {',
            '    discard;',
            '}',
            '',
            'vec3 normal = resolveNormal();',
            'float clearcoat = clamp(_ClearcoatFactor, 0.0, 1.0);',
            'if (_ClearcoatTexture_TexCoord >= 0) {',
            '    vec2 uv = transformUV(selectUV(_ClearcoatTexture_TexCoord), _ClearcoatTexture_ST, _ClearcoatTexture_Rotation);',
            '    clearcoat *= texture(_ClearcoatTexture, uv).r;',
            '}',
            'float clearcoatRoughness = clamp(_ClearcoatRoughnessFactor, 0.0, 1.0);',
            'if (_ClearcoatRoughnessTexture_TexCoord >= 0) {',
            '    vec2 uv = transformUV(selectUV(_ClearcoatRoughnessTexture_TexCoord), _ClearcoatRoughnessTexture_ST, _ClearcoatRoughnessTexture_Rotation);',
            '    clearcoatRoughness *= texture(_ClearcoatRoughnessTexture, uv).g;',
            '}',
            'clearcoatRoughness = clamp(clearcoatRoughness, 0.04, 1.0);',
            'vec3 clearcoatNormal = clearcoat > 0.0 ? resolveClearcoatNormal(normal) : normal;',
            'float baseLayerEnergy = 1.0 - (clearcoat * 0.25);',
            'vec3 viewDir = normalize(u_CameraPosition - v_WorldPosition);',
            'vec2 mrUv = transformUV(selectUV(max(_MetallicRoughnessTexture_TexCoord, 0)), _MetallicRoughnessTexture_ST, _MetallicRoughnessTexture_Rotation);',
            'vec4 mrSample = _MetallicRoughnessTexture_TexCoord >= 0 ? texture(_MetallicRoughnessTexture, mrUv) : vec4(1.0);',
            'float roughness = clamp(_RoughnessFactor * mrSample.g, 0.04, 1.0);',
            'float metallic = clamp(_MetallicFactor * mrSample.b, 0.0, 1.0);',
            'vec3 F0 = mix(vec3(0.04), baseColor.rgb, metallic);',
            '',
            'float anisotropy = clamp(_AnisotropyFactor, -1.0, 1.0);',
            'vec3 tangent = normalize(v_WorldTangent.xyz);',
            'vec3 bitangent = normalize(cross(normal, tangent)) * (v_WorldTangent.w == 0.0 ? 1.0 : v_WorldTangent.w);',
            'if (_AnisotropyRotation > 0.001) {',
            '    float c = cos(_AnisotropyRotation);',
            '    float s = sin(_AnisotropyRotation);',
            '    vec3 rotTangent = c * tangent + s * bitangent;',
            '    bitangent = -s * tangent + c * bitangent;',
            '    tangent = rotTangent;',
            '}',
            '',
            'float sheenFactor = clamp(_SheenFactor, 0.0, 1.0);',
            'vec3 sheenColor = _SheenColorFactor * sheenFactor;',
            'float sheenRoughness = clamp(_SheenRoughnessFactor, 0.04, 1.0);',
            '',
            'float subsurfaceFactor = clamp(_SubsurfaceFactor, 0.0, 1.0);',
            'vec3 subsurfaceColor = _SubsurfaceColorFactor;',
            'float subsurfaceThickness = max(_SubsurfaceThickness, 0.0);',
            '',
            'float transmissionFactor = clamp(_TransmissionFactor, 0.0, 1.0);',
            'float ior = max(_IorFactor, 1.001);',
            'float thickness = max(_ThicknessFactor, 0.0);',
            '',
            'float iridescenceFactor = clamp(_IridescenceFactor, 0.0, 1.0);',
            'float iridescenceIor = max(_IridescenceIor, 1.0);',
            'float iridescenceThickness = clamp(_IridescenceThickness, 100.0, 1000.0);',
            '',
            'float hemiFactor = clamp(normal.y * 0.5 + 0.5, 0.0, 1.0);',
            `vec3 ambient = mix(${GLTF_LIGHTING_UNIFORMS.groundLight}, ${GLTF_LIGHTING_UNIFORMS.skyLight}, hemiFactor) + (${GLTF_LIGHTING_UNIFORMS.ambientLight} * 0.45);`,
            'vec3 lighting = vec3(0.0);',
            '',
            'if (u_ReceiveLighting) {',
            `    for (int index = 0; index < ${MAX_GLTF_DIRECTIONAL_LIGHTS}; index += 1) {`,
            `        if (index >= ${GLTF_LIGHTING_UNIFORMS.directionalLightCount}) {`,
            '            break;',
            '        }',
            `        ambient += ${GLTF_LIGHTING_UNIFORMS.directionalLightAmbientColor}[index];`,
            `        vec3 lightDir = normalize(-${GLTF_LIGHTING_UNIFORMS.directionalLightDirection}[index]);`,
            `        vec3 lightColor = ${GLTF_LIGHTING_UNIFORMS.directionalLightColor}[index];`,
            `        float intensity = ${GLTF_LIGHTING_UNIFORMS.directionalLightIntensity}[index];`,
            '        vec3 diffuse = baseColor.rgb * (1.0 - metallic) / PI;',
            '        vec3 spec;',
            '        if (abs(anisotropy) > 0.001) {',
            '            spec = evaluateAnisotropicSpecular(normal, viewDir, lightDir, roughness, anisotropy, tangent, bitangent);',
            '        } else {',
            '            spec = evaluateSpecularBrdf(normal, viewDir, lightDir, roughness);',
            '        }',
            '        float NdotL = max(dot(normal, lightDir), 0.0);',
            '        vec3 F = fresnelSchlick(max(dot(normalize(viewDir + lightDir), viewDir), 0.0), F0);',
            '        vec3 kD = (vec3(1.0) - F) * (1.0 - metallic);',
            '        vec3 directLight = (kD * diffuse + spec) * lightColor * intensity * NdotL;',
            '        if (sheenFactor > 0.001) {',
            '            directLight += evaluateSheen(normal, viewDir, lightDir, sheenColor, sheenRoughness) * lightColor * intensity;',
            '        }',
            '        if (subsurfaceFactor > 0.001) {',
            '            directLight += evaluateSubsurface(baseColor.rgb, normal, viewDir, lightDir, lightColor, intensity, subsurfaceThickness * subsurfaceFactor, subsurfaceColor) * subsurfaceFactor;',
            '        }',
            '        if (transmissionFactor > 0.001) {',
            '            directLight += evaluateTransmission(baseColor.rgb, normal, viewDir, lightDir, ior, thickness) * lightColor * intensity * transmissionFactor;',
            '        }',
            '        vec3 directionalClearcoat = evaluateClearcoatLight(clearcoatNormal, viewDir, clearcoat, clearcoatRoughness, lightDir, lightColor, intensity);',
            '        float shadowFactor = resolveShadowFactor(v_WorldPosition, normal, lightDir);',
            '        lighting += (directLight * baseLayerEnergy + directionalClearcoat) * shadowFactor;',
            '    }',
            '    vec3 iblDiffuse = evaluateIBLDiffuse(normal, F0, roughness, baseColor.rgb, metallic);',
            '    vec3 iblSpecular = evaluateIBLSpecular(normal, viewDir, F0, roughness);',
            '    vec3 ibl = iblDiffuse + iblSpecular;',
            '    if (iridescenceFactor > 0.001) {',
            '        float NdotV = max(dot(normal, viewDir), 0.001);',
            '        vec3 iridescenceFresnel = evaluateIridescence(NdotV, iridescenceFactor, iridescenceIor, iridescenceThickness);',
            '        ibl *= iridescenceFresnel;',
            '    }',
            '    lighting += ibl * baseLayerEnergy;',
            `    for (int index = 0; index < ${MAX_GLTF_POINT_LIGHTS}; index += 1) {`,
            `        if (index >= ${GLTF_LIGHTING_UNIFORMS.pointLightCount}) {`,
            '            break;',
            '        }',
            `        vec3 toLight = ${GLTF_LIGHTING_UNIFORMS.pointLightPosition}[index] - v_WorldPosition;`,
            '        float distanceToLight = length(toLight);',
            '        vec3 lightDir = distanceToLight > 0.0 ? toLight / distanceToLight : vec3(0.0, 1.0, 0.0);',
            `        float attenuation = rangeAttenuation(distanceToLight, ${GLTF_LIGHTING_UNIFORMS.pointLightRange}[index]);`,
            `        vec3 lightColor = ${GLTF_LIGHTING_UNIFORMS.pointLightColor}[index];`,
            `        float intensity = ${GLTF_LIGHTING_UNIFORMS.pointLightIntensity}[index] * attenuation;`,
            '        vec3 diffuse = baseColor.rgb * (1.0 - metallic) / PI;',
            '        vec3 spec;',
            '        if (abs(anisotropy) > 0.001) {',
            '            spec = evaluateAnisotropicSpecular(normal, viewDir, lightDir, roughness, anisotropy, tangent, bitangent);',
            '        } else {',
            '            spec = evaluateSpecularBrdf(normal, viewDir, lightDir, roughness);',
            '        }',
            '        float NdotL = max(dot(normal, lightDir), 0.0);',
            '        vec3 F = fresnelSchlick(max(dot(normalize(viewDir + lightDir), viewDir), 0.0), F0);',
            '        vec3 kD = (vec3(1.0) - F) * (1.0 - metallic);',
            '        vec3 pointLightContrib = (kD * diffuse + spec) * lightColor * intensity * NdotL;',
            '        if (sheenFactor > 0.001) {',
            '            pointLightContrib += evaluateSheen(normal, viewDir, lightDir, sheenColor, sheenRoughness) * lightColor * intensity;',
            '        }',
            '        if (subsurfaceFactor > 0.001) {',
            '            pointLightContrib += evaluateSubsurface(baseColor.rgb, normal, viewDir, lightDir, lightColor, intensity, subsurfaceThickness * subsurfaceFactor, subsurfaceColor) * subsurfaceFactor;',
            '        }',
            '        if (transmissionFactor > 0.001) {',
            '            pointLightContrib += evaluateTransmission(baseColor.rgb, normal, viewDir, lightDir, ior, thickness) * lightColor * intensity * transmissionFactor;',
            '        }',
            `        vec3 pointClearcoat = evaluateClearcoatLight(clearcoatNormal, viewDir, clearcoat, clearcoatRoughness, lightDir, lightColor, intensity);`,
            '        lighting += pointLightContrib * baseLayerEnergy + pointClearcoat;',
            '    }',
            `    for (int index = 0; index < ${MAX_GLTF_SPOT_LIGHTS}; index += 1) {`,
            `        if (index >= ${GLTF_LIGHTING_UNIFORMS.spotLightCount}) {`,
            '            break;',
            '        }',
            `        vec3 toLight = ${GLTF_LIGHTING_UNIFORMS.spotLightPosition}[index] - v_WorldPosition;`,
            '        float distanceToLight = length(toLight);',
            '        vec3 lightDir = distanceToLight > 0.0 ? toLight / distanceToLight : vec3(0.0, 1.0, 0.0);',
            `        float attenuation = rangeAttenuation(distanceToLight, ${GLTF_LIGHTING_UNIFORMS.spotLightRange}[index]);`,
            `        attenuation *= spotAttenuation(lightDir, ${GLTF_LIGHTING_UNIFORMS.spotLightDirection}[index], ${GLTF_LIGHTING_UNIFORMS.spotLightInnerConeCosine}[index], ${GLTF_LIGHTING_UNIFORMS.spotLightOuterConeCosine}[index]);`,
            `        vec3 lightColor = ${GLTF_LIGHTING_UNIFORMS.spotLightColor}[index];`,
            `        float intensity = ${GLTF_LIGHTING_UNIFORMS.spotLightIntensity}[index] * attenuation;`,
            '        vec3 diffuse = baseColor.rgb * (1.0 - metallic) / PI;',
            '        vec3 spec;',
            '        if (abs(anisotropy) > 0.001) {',
            '            spec = evaluateAnisotropicSpecular(normal, viewDir, lightDir, roughness, anisotropy, tangent, bitangent);',
            '        } else {',
            '            spec = evaluateSpecularBrdf(normal, viewDir, lightDir, roughness);',
            '        }',
            '        float NdotL = max(dot(normal, lightDir), 0.0);',
            '        vec3 F = fresnelSchlick(max(dot(normalize(viewDir + lightDir), viewDir), 0.0), F0);',
            '        vec3 kD = (vec3(1.0) - F) * (1.0 - metallic);',
            '        vec3 spotLightContrib = (kD * diffuse + spec) * lightColor * intensity * NdotL;',
            '        if (sheenFactor > 0.001) {',
            '            spotLightContrib += evaluateSheen(normal, viewDir, lightDir, sheenColor, sheenRoughness) * lightColor * intensity;',
            '        }',
            '        if (subsurfaceFactor > 0.001) {',
            '            spotLightContrib += evaluateSubsurface(baseColor.rgb, normal, viewDir, lightDir, lightColor, intensity, subsurfaceThickness * subsurfaceFactor, subsurfaceColor) * subsurfaceFactor;',
            '        }',
            '        if (transmissionFactor > 0.001) {',
            '            spotLightContrib += evaluateTransmission(baseColor.rgb, normal, viewDir, lightDir, ior, thickness) * lightColor * intensity * transmissionFactor;',
            '        }',
            `        vec3 spotClearcoat = evaluateClearcoatLight(clearcoatNormal, viewDir, clearcoat, clearcoatRoughness, lightDir, lightColor, intensity);`,
            '        lighting += spotLightContrib * baseLayerEnergy + spotClearcoat;',
            '    }',
            '} else {',
            '    vec3 iblDiffuse = evaluateIBLDiffuse(normal, F0, roughness, baseColor.rgb, metallic);',
            '    vec3 iblSpecular = evaluateIBLSpecular(normal, viewDir, F0, roughness);',
            '    lighting = (iblDiffuse + iblSpecular) * baseLayerEnergy;',
            '}',
            '',
            'if (_OcclusionTexture_TexCoord >= 0) {',
            '    vec2 uv = transformUV(selectUV(_OcclusionTexture_TexCoord), _OcclusionTexture_ST, _OcclusionTexture_Rotation);',
            '    float occlusion = texture(_OcclusionTexture, uv).r;',
            '    lighting *= mix(1.0, occlusion, clamp(_OcclusionTexture_Strength, 0.0, 1.0));',
            '}',
            '',
            'vec3 emissive = _EmissiveFactor;',
            'if (_EmissiveTexture_TexCoord >= 0) {',
            '    vec2 uv = transformUV(selectUV(_EmissiveTexture_TexCoord), _EmissiveTexture_ST, _EmissiveTexture_Rotation);',
            '    emissive *= texture(_EmissiveTexture, uv).rgb;',
            '}',
            '',
            'float alpha = alphaMode == 2 ? baseColor.a : 1.0;',
            'o_Color = vec4(lighting + emissive, alpha);',
        ],
    },
    renderState: {
        depthTest: true,
        cull: true,
        blend: false,
    },
});

export const GLTF_UNLIT_SHADER_EFFECT = createGltfUnlitShaderEffect(GLTF_SHADER_UNLIT_ID);
export const GLTF_PBR_SHADER_EFFECT = createGltfPbrShaderEffect(GLTF_SHADER_PBR_ID);

const GLTF_SHADER_TOON_ID = 'gltf/toon';

const createGltfToonShaderEffect = (id: string): RenderShaderEffectDefinition => ({
    format: 'axrone.shader/effect',
    version: 1,
    id,
    attributes: [
        { name: 'a_Position', type: 'vec3', location: 0 },
        { name: 'a_Normal', type: 'vec3', location: 1 },
        { name: 'a_UV0', type: 'vec2', location: 2 },
        { name: 'a_Tangent', type: 'vec4', location: 4 },
        { name: 'a_Joints0', type: 'uvec4', location: 9 },
        { name: 'a_Weights0', type: 'vec4', location: 10 },
    ],
    varyings: [
        { name: 'v_UV0', type: 'vec2' },
        { name: 'v_WorldPosition', type: 'vec3' },
        { name: 'v_WorldNormal', type: 'vec3' },
    ],
    properties: [
        ...createSharedObjectProperties(),
        {
            name: 'u_ReceiveLighting',
            type: 'bool',
            stages: ['fragment'],
            scope: 'system',
            inspector: HIDDEN_INSPECTOR,
        },
        ...createLightingProperties(),
        {
            name: 'u_CameraPosition',
            type: 'vec3',
            stages: ['fragment'],
            scope: 'camera',
            inspector: HIDDEN_INSPECTOR,
        },
        {
            name: '_BaseColorFactor',
            type: 'vec4',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: [1, 1, 1, 1],
            inspector: { label: 'Base Color', group: 'Surface', control: 'color' },
        },
        ...createSurfaceTextureProperties('_BaseColorTexture', 'Base Color Map', 'Maps'),
        {
            name: '_ShadowColor',
            type: 'vec3',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: [0.2, 0.2, 0.3],
            inspector: { label: 'Shadow Color', group: 'Toon', control: 'color' },
        },
        {
            name: '_ShadowThreshold',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0.5,
            inspector: { label: 'Shadow Threshold', group: 'Toon', control: 'slider', min: 0, max: 1, step: 0.01 },
        },
        {
            name: '_ShadowSmoothness',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0.05,
            inspector: { label: 'Shadow Smoothness', group: 'Toon', control: 'slider', min: 0, max: 0.5, step: 0.01 },
        },
        {
            name: '_HighlightThreshold',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0.8,
            inspector: { label: 'Highlight Threshold', group: 'Toon', control: 'slider', min: 0, max: 1, step: 0.01 },
        },
        {
            name: '_HighlightColor',
            type: 'vec3',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: [1, 1, 1],
            inspector: { label: 'Highlight Color', group: 'Toon', control: 'color' },
        },
        {
            name: '_RimColor',
            type: 'vec3',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: [0.8, 0.9, 1.0],
            inspector: { label: 'Rim Color', group: 'Toon', control: 'color' },
        },
        {
            name: '_RimThreshold',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0.6,
            inspector: { label: 'Rim Threshold', group: 'Toon', control: 'slider', min: 0, max: 1, step: 0.01 },
        },
        {
            name: '_RimSmoothness',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0.1,
            inspector: { label: 'Rim Smoothness', group: 'Toon', control: 'slider', min: 0, max: 0.5, step: 0.01 },
        },
        {
            name: '_OutlineWidth',
            type: 'float',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: 0,
            inspector: { label: 'Outline Width', group: 'Toon', control: 'slider', min: 0, max: 5, step: 0.1 },
        },
        {
            name: '_OutlineColor',
            type: 'vec3',
            stages: ['fragment'],
            scope: 'material',
            defaultValue: [0, 0, 0],
            inspector: { label: 'Outline Color', group: 'Toon', control: 'color' },
        },
        ...createSharedAlphaProperties(),
    ],
    libraries: GLTF_SHADER_LIBRARIES,
    vertex: {
        includes: ['gltf.skinning'],
        main: [
            'v_UV0 = a_UV0;',
            'vec4 localPosition = vec4(a_Position, 1.0);',
            'vec3 localNormal = a_Normal;',
            'if (u_Skinning && u_SkinJointCount > 0) {',
            '    mat4 skin = resolveSkinMatrix();',
            '    localPosition = skin * localPosition;',
            '    localNormal = mat3(skin) * localNormal;',
            '}',
            'vec4 worldPosition = u_Model * localPosition;',
            'v_WorldPosition = worldPosition.xyz;',
            'v_WorldNormal = normalize(mat3(u_Model) * localNormal);',
            'gl_Position = u_Projection * u_View * worldPosition;',
        ],
    },
    fragment: {
        precision: 'mediump',
        outputs: [{ name: 'o_Color', type: 'vec4' }],
        includes: ['gltf.uv', 'gltf.pbr-lighting'],
        main: [
            'vec4 baseColor = _BaseColorFactor;',
            'if (_BaseColorTexture_TexCoord >= 0) {',
            '    vec2 uv = transformUV(selectUV(_BaseColorTexture_TexCoord), _BaseColorTexture_ST, _BaseColorTexture_Rotation);',
            '    baseColor *= texture(_BaseColorTexture, uv);',
            '}',
            'int alphaMode = int(_AlphaMode + 0.5);',
            'if (alphaMode == 1 && baseColor.a < _AlphaCutoff) {',
            '    discard;',
            '}',
            '',
            'vec3 normal = length(v_WorldNormal) > 0.0001 ? normalize(v_WorldNormal) : vec3(0.0, 0.0, 1.0);',
            'vec3 viewDir = normalize(u_CameraPosition - v_WorldPosition);',
            '',
            'float NdotV = max(dot(normal, viewDir), 0.0);',
            'float rim = 1.0 - NdotV;',
            'float rimFactor = smoothstep(_RimThreshold - _RimSmoothness, _RimThreshold + _RimSmoothness, rim);',
            '',
            'vec3 lighting = vec3(0.0);',
            'if (u_ReceiveLighting) {',
            `    for (int index = 0; index < ${MAX_GLTF_DIRECTIONAL_LIGHTS}; index += 1) {`,
            `        if (index >= ${GLTF_LIGHTING_UNIFORMS.directionalLightCount}) break;`,
            `        vec3 lightDir = normalize(-${GLTF_LIGHTING_UNIFORMS.directionalLightDirection}[index]);`,
            `        vec3 lightColor = ${GLTF_LIGHTING_UNIFORMS.directionalLightColor}[index];`,
            `        float intensity = ${GLTF_LIGHTING_UNIFORMS.directionalLightIntensity}[index];`,
            '        float NdotL = max(dot(normal, lightDir), 0.0);',
            '        float toonShade = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, NdotL);',
            '        vec3 shadowedColor = mix(_ShadowColor * baseColor.rgb, baseColor.rgb, toonShade);',
            '        vec3 halfDir = normalize(viewDir + lightDir);',
            '        float NdotH = max(dot(normal, halfDir), 0.0);',
            '        float highlight = smoothstep(_HighlightThreshold - 0.05, _HighlightThreshold + 0.05, NdotH);',
            '        shadowedColor = mix(shadowedColor, _HighlightColor, highlight * toonShade);',
            '        shadowedColor = mix(shadowedColor, _RimColor, rimFactor * 0.5);',
            '        lighting += shadowedColor * lightColor * intensity;',
            '    }',
            `    for (int index = 0; index < ${MAX_GLTF_POINT_LIGHTS}; index += 1) {`,
            `        if (index >= ${GLTF_LIGHTING_UNIFORMS.pointLightCount}) break;`,
            `        vec3 toLight = ${GLTF_LIGHTING_UNIFORMS.pointLightPosition}[index] - v_WorldPosition;`,
            '        float dist = length(toLight);',
            '        vec3 lightDir = dist > 0.0 ? toLight / dist : vec3(0.0, 1.0, 0.0);',
            `        float atten = rangeAttenuation(dist, ${GLTF_LIGHTING_UNIFORMS.pointLightRange}[index]);`,
            `        vec3 lightColor = ${GLTF_LIGHTING_UNIFORMS.pointLightColor}[index];`,
            `        float intensity = ${GLTF_LIGHTING_UNIFORMS.pointLightIntensity}[index] * atten;`,
            '        float NdotL = max(dot(normal, lightDir), 0.0);',
            '        float toonShade = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, NdotL);',
            '        lighting += mix(_ShadowColor * baseColor.rgb, baseColor.rgb, toonShade) * lightColor * intensity;',
            '    }',
            '} else {',
            '    lighting = baseColor.rgb;',
            '}',
            '',
            'vec3 emissive = _EmissiveFactor;',
            'if (_EmissiveTexture_TexCoord >= 0) {',
            '    vec2 uv = transformUV(selectUV(_EmissiveTexture_TexCoord), _EmissiveTexture_ST, _EmissiveTexture_Rotation);',
            '    emissive *= texture(_EmissiveTexture, uv).rgb;',
            '}',
            '',
            'float alpha = alphaMode == 2 ? baseColor.a : 1.0;',
            'o_Color = vec4(lighting + emissive, alpha);',
        ],
    },
    renderState: {
        depthTest: true,
        cull: true,
        blend: false,
    },
});

export const GLTF_TOON_SHADER_EFFECT = createGltfToonShaderEffect(GLTF_SHADER_TOON_ID);

export const createGltfUnlitShaderDefinition = (
    id: string = GLTF_SHADER_UNLIT_ID,
    uniforms?: GltfMaterialUniformMap
): GltfShaderDefinition => {
    const definition = createGltfShaderDefinitionFromEffect(
        id === GLTF_SHADER_UNLIT_ID ? GLTF_UNLIT_SHADER_EFFECT : createGltfUnlitShaderEffect(id),
        GLTF_UNLIT_ATTRIBUTES
    );
    const renderState = resolveVariantRenderState(uniforms);
    return {
        ...definition,
        cull: renderState.cull,
        blend: renderState.blend,
    };
};

export const createGltfPbrShaderDefinition = (
    id: string = GLTF_SHADER_PBR_ID,
    uniforms?: GltfMaterialUniformMap
): GltfShaderDefinition => {
    const definition = createGltfShaderDefinitionFromEffect(
        id === GLTF_SHADER_PBR_ID ? GLTF_PBR_SHADER_EFFECT : createGltfPbrShaderEffect(id),
        GLTF_PBR_ATTRIBUTES
    );
    const renderState = resolveVariantRenderState(uniforms);
    return {
        ...definition,
        cull: renderState.cull,
        blend: renderState.blend,
    };
};

export const resolveGltfRuntimeShaderId = (
    shaderId: string,
    uniforms?: GltfMaterialUniformMap
): string => {
    if (shaderId !== GLTF_SHADER_PBR_ID && shaderId !== GLTF_SHADER_UNLIT_ID) {
        return shaderId;
    }

    return createVariantShaderId(shaderId, {
        blend: isGltfBlendAlphaMode(uniforms),
        doubleSided: isGltfDoubleSided(uniforms),
    });
};

export const resolveGltfShaderDefinition = (
    shaderId: string,
    resolveShaderDefinition?: (shaderId: string) => GltfShaderDefinition | undefined
): GltfShaderDefinition | undefined => {
    if (shaderId.startsWith(GLTF_SHADER_PBR_ID)) {
        return createGltfPbrShaderDefinition(shaderId, {
            _AlphaMode: shaderId.includes(GLTF_SHADER_BLEND_SUFFIX) ? 2 : 0,
            _DoubleSided: shaderId.includes(GLTF_SHADER_DOUBLE_SIDED_SUFFIX) ? 1 : 0,
        });
    }
    if (shaderId.startsWith(GLTF_SHADER_UNLIT_ID)) {
        return createGltfUnlitShaderDefinition(shaderId, {
            _AlphaMode: shaderId.includes(GLTF_SHADER_BLEND_SUFFIX) ? 2 : 0,
            _DoubleSided: shaderId.includes(GLTF_SHADER_DOUBLE_SIDED_SUFFIX) ? 1 : 0,
        });
    }
    return resolveShaderDefinition?.(shaderId);
};
