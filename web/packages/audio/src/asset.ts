import type { AssetRecord } from '@axrone/asset-core';
import { isObject } from './internal/shared';
import {
    isAudioClipAssetRecord,
    normalizeAudioClipId,
} from './reference';
import type {
    AudioAssetSchema,
    AudioClipAssetData,
    AudioClipAssetSelector,
    AudioClipInput,
    AudioClipSelector,
    AudioInlineClipSelector,
    AudioRegisteredClipSelector,
} from './types';

const hasFiniteNumber = (value: unknown): value is number =>
    typeof value === 'number' && Number.isFinite(value);

const isFloat32ArrayList = (value: unknown): value is readonly Float32Array[] =>
    Array.isArray(value) && value.every((entry) => entry instanceof Float32Array);

export const AUDIO_CLIP_ASSET_KIND = 'audioClip' as const;

export const isAudioClipAssetData = (value: unknown): value is AudioClipAssetData => {
    if (!isObject(value) || typeof value.kind !== 'string') {
        return false;
    }

    switch (value.kind) {
        case 'buffer':
            return typeof AudioBuffer !== 'undefined' ? value.buffer instanceof AudioBuffer : 'buffer' in value;
        case 'pcm':
            return hasFiniteNumber(value.sampleRate) && isFloat32ArrayList(value.channelData);
        case 'encoded':
            return value.data instanceof ArrayBuffer || ArrayBuffer.isView(value.data);
        case 'url':
            return typeof value.url === 'string' && value.url.length > 0;
        default:
            return false;
    }
};

// AssetSelector<TSchema> is a closed union (asset-core types.ts:126-134):
//   string | AssetReference | AssetVersionedReference | AssetRecord | AssetLookupByKey
// so membership is decidable from the shape. A custom AudioAssetResolver is typed to
// receive that union and cannot legally handle anything else, which is what makes this
// check safe to enforce here rather than deferring to the resolver.
const isAssetSelectorShape = (value: unknown): boolean => {
    if (typeof value === 'string') {
        return value.trim().length > 0;
    }
    if (!isObject(value) || Array.isArray(value)) {
        return false;
    }

    const isReference = 'token' in value && typeof value.token === 'string';
    const isLookupByKey = 'key' in value && typeof value.key === 'string';
    const isRecord = 'kind' in value && 'data' in value;

    return isReference || isLookupByKey || isRecord;
};

export const isAudioClipSelector = <TSchema extends AudioAssetSchema = AudioAssetSchema>(
    value: unknown
): value is AudioClipSelector<TSchema> => {
    if (!isObject(value) || typeof value.kind !== 'string') {
        return false;
    }

    switch (value.kind) {
        case 'registered':
            return typeof value.clipId === 'string' && value.clipId.trim().length > 0;
        case 'asset':
            return 'selector' in value && isAssetSelectorShape(value.selector);
        case 'inline':
            return isAudioClipAssetData(value.clip);
        default:
            return false;
    }
};

export const createRegisteredAudioClipSelector = (
    clipId: string
): AudioRegisteredClipSelector => ({
    kind: 'registered',
    clipId: normalizeAudioClipId(clipId),
});

export const createInlineAudioClipSelector = (
    clip: AudioClipAssetData
): AudioInlineClipSelector => ({
    kind: 'inline',
    clip,
});

export const toAudioClipSelector = <TSchema extends AudioAssetSchema = AudioAssetSchema>(
    value: AudioClipInput<TSchema> | undefined
): AudioClipSelector<TSchema> | undefined => {
    if (value === undefined) {
        return undefined;
    }

    if (typeof AudioBuffer !== 'undefined' && value instanceof AudioBuffer) {
        return createInlineAudioClipSelector({ kind: 'buffer', buffer: value }) as AudioClipSelector<TSchema>;
    }

    if (isAudioClipSelector<TSchema>(value)) {
        return value;
    }

    if (isAudioClipAssetData(value)) {
        return createInlineAudioClipSelector(value) as AudioClipSelector<TSchema>;
    }

    // Anything that is not a recognisable asset selector stops here: returning undefined
    // makes registerClip/resolveClip throw audio.invalid-clip synchronously, at the call
    // that caused it, instead of wrapping garbage and failing during async decode.
    if (!isAssetSelectorShape(value)) {
        return undefined;
    }

    return {
        kind: 'asset',
        selector: value as AudioClipAssetSelector<TSchema>,
    };
};

/**
 * Whether two resolved clip selectors point at the same clip. A per-frame descriptor re-upsert
 * must not look like a clip change, so `registered` compares by id while the inline and asset
 * payloads compare by reference — a stable caller hands back the same object every time.
 */
export const isClipSelectorEqual = <TSchema extends AudioAssetSchema = AudioAssetSchema>(
    left: AudioClipSelector<TSchema> | undefined,
    right: AudioClipSelector<TSchema> | undefined
): boolean => {
    if (left === right) {
        return true;
    }
    if (!left || !right || left.kind !== right.kind) {
        return false;
    }

    switch (left.kind) {
        case 'registered':
            return right.kind === 'registered' && left.clipId === right.clipId;
        case 'inline':
            return right.kind === 'inline' && left.clip === right.clip;
        case 'asset':
            return right.kind === 'asset' && left.selector === right.selector;
        default:
            return false;
    }
};

export const toAudioClipSelectorFromRecord = <TSchema extends AudioAssetSchema = AudioAssetSchema>(
    value: AudioClipAssetSelector<TSchema> | AssetRecord<TSchema>
): AudioClipSelector<TSchema> => {
    if (isAudioClipAssetRecord(value)) {
        return {
            kind: 'asset',
            selector: value as AudioClipAssetSelector<TSchema>,
        };
    }

    return {
        kind: 'asset',
        selector: value,
    };
};