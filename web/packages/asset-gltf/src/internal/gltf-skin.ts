import type { GltfSkinAsset } from '../types';
import type { GltfSkinBinding } from './gltf-constants';

export const createSkinBinding = (skin: GltfSkinAsset | undefined): GltfSkinBinding | undefined => {
    if (!skin) {
        return undefined;
    }

    return Object.freeze({
        jointNodeIds: Object.freeze([...skin.jointNodeIds]),
        ...(skin.skeletonNodeId ? { skeletonNodeId: skin.skeletonNodeId } : {}),
        ...(skin.inverseBindMatrices
            ? { inverseBindMatrices: new Float32Array(skin.inverseBindMatrices) }
            : {}),
    });
};
