import { RADIANS_TO_DEGREES } from './gltf-constants';
import { GltfSchemaError } from '../errors';
import type { GltfCameraJson } from '../types';
import type { GltfComponentSnapshot } from '../asset-ir';

export const createCameraSnapshot = (
    camera: GltfCameraJson,
    isPrimary: boolean
): GltfComponentSnapshot => {
    if (camera.type === 'orthographic') {
        if (!camera.orthographic) {
            throw new GltfSchemaError('Orthographic glTF camera is missing orthographic settings');
        }

        return Object.freeze({
            type: 'Camera',
            data: Object.freeze({
                primary: isPrimary,
                near: camera.orthographic.znear,
                far: camera.orthographic.zfar,
                orthographic: true,
                orthographicSize: camera.orthographic.ymag,
            }),
        });
    }

    if (!camera.perspective) {
        throw new GltfSchemaError('Perspective glTF camera is missing perspective settings');
    }

    return Object.freeze({
        type: 'Camera',
        data: Object.freeze({
            primary: isPrimary,
            near: camera.perspective.znear,
            ...(camera.perspective.zfar !== undefined
                ? { far: camera.perspective.zfar }
                : {}),
            fieldOfView: camera.perspective.yfov * RADIANS_TO_DEGREES,
            orthographic: false,
        }),
    });
};
