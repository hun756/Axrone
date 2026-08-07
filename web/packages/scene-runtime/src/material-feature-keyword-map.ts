import type { SceneMaterialSurfaceFeaturesDefinition } from './types';

/**
 * Maps surface feature flags to shader keyword names.
 * Each entry connects a SceneMaterialSurfaceFeaturesDefinition boolean field
 * to the corresponding shader preprocessor keyword that controls the same
 * code path in GLSL.
 *
 * This bridge allows the scene layer's surface feature metadata to drive
 * shader variant selection through the keyword system.
 */
export const FEATURE_TO_KEYWORD: Readonly<Record<string, string>> = Object.freeze({
    useVertexColor: 'VERTEX_COLOR',
    hasSecondUv: 'SECOND_UV',
    useNormalMap: 'NORMAL_MAPPING',
    useTwoSided: 'TWO_SIDED',
    useAlbedoMap: 'ALBEDO_MAP',
    usePbrMap: 'PBR_MAP',
    useMetallicRoughnessMap: 'METALLIC_ROUGHNESS_MAP',
    useOcclusionMap: 'OCCLUSION',
    useEmissiveMap: 'EMISSION',
    useClearcoat: 'CLEARCOAT',
    useClearcoatMap: 'CLEARCOAT_MAP',
    useClearcoatRoughnessMap: 'CLEARCOAT_ROUGHNESS_MAP',
    useClearcoatNormalMap: 'CLEARCOAT_NORMAL_MAP',
    useAlphaTest: 'ALPHA_TEST',
    useAnisotropy: 'ANISOTROPY',
    useSheen: 'SHEEN',
    useSubsurface: 'SUBSURFACE',
    useTransmission: 'TRANSMISSION',
    useIridescence: 'IRIDESCENCE',
});

/**
 * Resolves surface features into a keyword enable/disable map.
 * Only features that are explicitly `true` or `false` produce entries;
 * `undefined` features are skipped (no opinion on the keyword).
 */
export const resolveSurfaceFeatures = (
    features: SceneMaterialSurfaceFeaturesDefinition
): Readonly<Record<string, boolean>> => {
    const result: Record<string, boolean> = {};

    for (const [feature, keyword] of Object.entries(FEATURE_TO_KEYWORD)) {
        const value = (features as Record<string, boolean | undefined>)[feature];
        if (value !== undefined) {
            result[keyword] = value;
        }
    }

    return result;
};
