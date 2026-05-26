import type { GltfPunctualLightJson, GltfRootJson, GltfNodeJson } from '../types';
import type { GltfComponentSnapshot } from '../asset-ir';
import { GltfSchemaError } from '../errors';

export const createDirectionalLightSnapshot = (
    light: GltfPunctualLightJson,
    isPrimary: boolean
): GltfComponentSnapshot =>
    Object.freeze({
        type: 'DirectionalLight',
        data: Object.freeze({
            color: Object.freeze([...(light.color ?? [1, 1, 1])]),
            intensity: light.intensity ?? 1,
            primary: isPrimary,
        }),
    });

export const createPointLightSnapshot = (light: GltfPunctualLightJson): GltfComponentSnapshot =>
    Object.freeze({
        type: 'PointLight',
        data: Object.freeze({
            color: Object.freeze([...(light.color ?? [1, 1, 1])]),
            intensity: light.intensity ?? 1,
            ...(light.range !== undefined ? { range: light.range } : {}),
        }),
    });

export const createSpotLightSnapshot = (light: GltfPunctualLightJson): GltfComponentSnapshot =>
    Object.freeze({
        type: 'SpotLight',
        data: Object.freeze({
            color: Object.freeze([...(light.color ?? [1, 1, 1])]),
            intensity: light.intensity ?? 1,
            ...(light.range !== undefined ? { range: light.range } : {}),
            innerConeAngle: light.spot?.innerConeAngle ?? 0,
            outerConeAngle: light.spot?.outerConeAngle ?? Math.PI / 4,
        }),
    });

export const resolveNodeLight = (
    root: GltfRootJson,
    node: GltfNodeJson,
    nodeIndex: number
): { readonly index: number; readonly light: GltfPunctualLightJson } | undefined => {
    const lightIndex = node.extensions?.KHR_lights_punctual?.light;
    if (lightIndex === undefined) {
        return undefined;
    }

    const light = root.extensions?.KHR_lights_punctual?.lights?.[lightIndex];
    if (!light) {
        throw new GltfSchemaError(`Node ${nodeIndex} references missing punctual light ${lightIndex}`);
    }

    return Object.freeze({ index: lightIndex, light });
};
