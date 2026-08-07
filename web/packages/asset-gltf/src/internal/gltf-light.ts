import type { GltfPunctualLightJson, GltfRootJson, GltfNodeJson } from '../types';
import type { GltfComponentSnapshot, DirectionalLightComponentData, PointLightComponentData, SpotLightComponentData } from './gltf-component-snapshot';
import type { Vec3Tuple, LightReference } from './gltf-branded-types';
import { GltfSchemaError } from '../errors';

export const createDirectionalLightSnapshot = (
    light: GltfPunctualLightJson,
    isPrimary: boolean
): GltfComponentSnapshot =>
    Object.freeze({
        type: 'DirectionalLight' as const,
        data: Object.freeze({
            color: Object.freeze([...(light.color ?? [1, 1, 1])]) as Vec3Tuple,
            intensity: light.intensity ?? 1,
            primary: isPrimary,
        } satisfies DirectionalLightComponentData),
    });

export const createPointLightSnapshot = (light: GltfPunctualLightJson): GltfComponentSnapshot =>
    Object.freeze({
        type: 'PointLight' as const,
        data: Object.freeze({
            color: Object.freeze([...(light.color ?? [1, 1, 1])]) as Vec3Tuple,
            intensity: light.intensity ?? 1,
            ...(light.range !== undefined ? { range: light.range } : {}),
        } satisfies PointLightComponentData),
    });

export const createSpotLightSnapshot = (light: GltfPunctualLightJson): GltfComponentSnapshot =>
    Object.freeze({
        type: 'SpotLight' as const,
        data: Object.freeze({
            color: Object.freeze([...(light.color ?? [1, 1, 1])]) as Vec3Tuple,
            intensity: light.intensity ?? 1,
            ...(light.range !== undefined ? { range: light.range } : {}),
            innerConeAngle: light.spot?.innerConeAngle ?? 0,
            outerConeAngle: light.spot?.outerConeAngle ?? Math.PI / 4,
        } satisfies SpotLightComponentData),
    });

export const resolveNodeLight = (
    root: GltfRootJson,
    node: GltfNodeJson,
    nodeIndex: number
): LightReference | undefined => {
    const lightIndex = node.extensions?.KHR_lights_punctual?.light;
    if (lightIndex === undefined) return undefined;

    const light = root.extensions?.KHR_lights_punctual?.lights?.[lightIndex];
    if (!light) {
        throw new GltfSchemaError(`Node ${nodeIndex} references missing punctual light ${lightIndex}`);
    }

    return Object.freeze({ index: lightIndex, light }) as LightReference;
};
