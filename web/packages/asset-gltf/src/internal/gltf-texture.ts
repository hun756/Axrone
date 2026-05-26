/**
 * @fileoverview gltf-texture.ts
 *
 * Texture and sampler mapping utilities for GLTF import.
 * Maps GLTF sampler/filter values to engine enums and handles texture format inference.
 *
 * @packageDocumentation
 */

import { FilterMode, TextureFormat, WrapMode } from '@axrone/render-webgl2';
import type {
    GltfMaterialTextureBinding,
    GltfSamplerJson,
    GltfTextureBindingJson,
    GltfTexturePayload,
    GltfTextureTransform,
    GltfTextureUsage,
} from '../types';

export const mapWrapMode = (
    value: GltfSamplerJson['wrapS'] | GltfSamplerJson['wrapT'] | undefined
): WrapMode => {
    switch (value) {
        case 33071:
            return WrapMode.CLAMP_TO_EDGE;
        case 33648:
            return WrapMode.MIRRORED_REPEAT;
        case 10497:
        default:
            return WrapMode.REPEAT;
    }
};

export const mapMinFilter = (value: GltfSamplerJson['minFilter'] | undefined): FilterMode => {
    switch (value) {
        case 9728:
            return FilterMode.NEAREST;
        case 9729:
            return FilterMode.LINEAR;
        case 9984:
            return FilterMode.NEAREST_MIPMAP_NEAREST;
        case 9985:
            return FilterMode.LINEAR_MIPMAP_NEAREST;
        case 9986:
            return FilterMode.NEAREST_MIPMAP_LINEAR;
        case 9987:
        default:
            return FilterMode.LINEAR_MIPMAP_LINEAR;
    }
};

export const mapMagFilter = (value: GltfSamplerJson['magFilter'] | undefined): FilterMode => {
    switch (value) {
        case 9728:
            return FilterMode.NEAREST;
        case 9729:
        default:
            return FilterMode.LINEAR;
    }
};

export const inferTextureFormat = (payload: GltfTexturePayload): TextureFormat | undefined => {
    if (payload.kind === 'compressed') {
        return payload.targetFormat;
    }

    const mimeType = payload.mimeType?.toLowerCase();
    if (mimeType === 'image/jpeg' || mimeType === 'image/jpg') {
        return TextureFormat.RGB8;
    }
    if (mimeType === 'image/png' || mimeType === 'image/webp') {
        return TextureFormat.RGBA8;
    }
    if (mimeType === 'image/ktx2') {
        return TextureFormat.RGBA8;
    }
    return undefined;
};

export const inferCompressedContainer = (
    mimeType: string | undefined,
    uri: string | undefined
): 'ktx2' | 'basisu' | undefined => {
    const normalizedMime = mimeType?.toLowerCase();
    const normalizedUri = uri?.toLowerCase();
    if (normalizedMime === 'image/ktx2' || normalizedUri?.endsWith('.ktx2')) {
        return 'ktx2';
    }
    if (normalizedMime === 'image/basis' || normalizedUri?.endsWith('.basis')) {
        return 'basisu';
    }
    return undefined;
};

export const createTextureTransform = (
    binding: GltfTextureBindingJson | undefined
): GltfTextureTransform | undefined => {
    const transform = binding?.extensions?.KHR_texture_transform;
    if (!transform && binding?.texCoord === undefined) {
        return undefined;
    }

    return Object.freeze({
        offset: Object.freeze([...(transform?.offset ?? [0, 0])]) as readonly [number, number],
        scale: Object.freeze([...(transform?.scale ?? [1, 1])]) as readonly [number, number],
        rotation: transform?.rotation ?? 0,
        texCoord: transform?.texCoord ?? binding?.texCoord ?? 0,
    });
};

export const createMaterialTextureBinding = (
    usage: GltfTextureUsage,
    key: string,
    json: GltfTextureBindingJson,
    colorSpace: 'linear' | 'srgb'
): GltfMaterialTextureBinding =>
    Object.freeze({
        textureKey: key,
        usage,
        texCoord: json.texCoord ?? 0,
        colorSpace,
        transform: createTextureTransform(json),
        ...(json.scale !== undefined ? { scale: json.scale } : {}),
        ...(json.strength !== undefined ? { strength: json.strength } : {}),
    });