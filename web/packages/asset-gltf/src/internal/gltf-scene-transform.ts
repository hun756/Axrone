import type { GltfNodeJson } from '../types';
import type { GltfActorSnapshot, GltfComponentSnapshot } from '../asset-ir';

export const decomposeNodeTransform = (
    node: GltfNodeJson
): {
    readonly position: readonly [number, number, number];
    readonly rotation: readonly [number, number, number, number];
    readonly scale: readonly [number, number, number];
} => {
    if (node.matrix && node.matrix.length === 16) {
        const m = node.matrix;
        const sx = Math.hypot(m[0], m[1], m[2]);
        const sy = Math.hypot(m[4], m[5], m[6]);
        const sz = Math.hypot(m[8], m[9], m[10]);
        const rm00 = sx === 0 ? 1 : m[0] / sx;
        const rm01 = sx === 0 ? 0 : m[1] / sx;
        const rm02 = sx === 0 ? 0 : m[2] / sx;
        const rm10 = sy === 0 ? 0 : m[4] / sy;
        const rm11 = sy === 0 ? 1 : m[5] / sy;
        const rm12 = sy === 0 ? 0 : m[6] / sy;
        const rm20 = sz === 0 ? 0 : m[8] / sz;
        const rm21 = sz === 0 ? 0 : m[9] / sz;
        const rm22 = sz === 0 ? 1 : m[10] / sz;
        const trace = rm00 + rm11 + rm22;
        let x = 0;
        let y = 0;
        let z = 0;
        let w = 1;

        if (trace > 0) {
            const s = Math.sqrt(trace + 1) * 2;
            w = 0.25 * s;
            x = (rm21 - rm12) / s;
            y = (rm02 - rm20) / s;
            z = (rm10 - rm01) / s;
        } else if (rm00 > rm11 && rm00 > rm22) {
            const s = Math.sqrt(1 + rm00 - rm11 - rm22) * 2;
            w = (rm21 - rm12) / s;
            x = 0.25 * s;
            y = (rm01 + rm10) / s;
            z = (rm02 + rm20) / s;
        } else if (rm11 > rm22) {
            const s = Math.sqrt(1 + rm11 - rm00 - rm22) * 2;
            w = (rm02 - rm20) / s;
            x = (rm01 + rm10) / s;
            y = 0.25 * s;
            z = (rm12 + rm21) / s;
        } else {
            const s = Math.sqrt(1 + rm22 - rm00 - rm11) * 2;
            w = (rm10 - rm01) / s;
            x = (rm02 + rm20) / s;
            y = (rm12 + rm21) / s;
            z = 0.25 * s;
        }

        return {
            position: [m[12], m[13], m[14]],
            rotation: [x, y, z, w],
            scale: [sx || 1, sy || 1, sz || 1],
        };
    }

    return {
        position: node.translation ?? [0, 0, 0],
        rotation: node.rotation ?? [0, 0, 0, 1],
        scale: node.scale ?? [1, 1, 1],
    };
};

export const createTransformSnapshot = (node: GltfNodeJson): GltfComponentSnapshot => {
    const transform = decomposeNodeTransform(node);
    return Object.freeze({
        type: 'Transform',
        data: Object.freeze({
            position: Object.freeze([...transform.position]),
            rotation: Object.freeze([...transform.rotation]),
            scale: Object.freeze([...transform.scale]),
        }),
    });
};

export const createActorSnapshot = (
    nodeId: string,
    parentNodeId: string | null,
    name: string,
    components: readonly GltfComponentSnapshot[]
): GltfActorSnapshot =>
    Object.freeze({
        nodeId,
        parentNodeId,
        name,
        layer: 0,
        tag: 'Default',
        active: true,
        persistent: false,
        pooled: false,
        components,
    });

export const nodeIdFromIndex = (nodeIndex: number): string => `node/${nodeIndex}`;
