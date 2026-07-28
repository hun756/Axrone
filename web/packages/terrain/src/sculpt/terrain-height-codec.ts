import { TerrainError, TerrainErrorCode } from '../errors';
import type { TerrainResolution } from '../types';
import { isTerrainResolution } from '../types';

/**
 * Compact, environment-agnostic persistence codec for sculpted heightmaps.
 *
 * Normalized heights are quantized to unsigned 16-bit (1/65535 precision —
 * far below a visible step at any supported maxHeight) and base64-encoded
 * with a version prefix so future layout changes stay detectable.
 */
const HEIGHT_DATA_PREFIX = 'athf1:';
const QUANTIZATION_MAX = 0xffff;
const BASE64_ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';

const BASE64_LOOKUP: Int16Array = (() => {
    const lookup = new Int16Array(128).fill(-1);
    for (let index = 0; index < BASE64_ALPHABET.length; index += 1) {
        lookup[BASE64_ALPHABET.charCodeAt(index)] = index;
    }
    return lookup;
})();

const encodeBase64 = (bytes: Uint8Array): string => {
    let output = '';
    for (let index = 0; index < bytes.length; index += 3) {
        const first = bytes[index]!;
        const second = index + 1 < bytes.length ? bytes[index + 1]! : 0;
        const third = index + 2 < bytes.length ? bytes[index + 2]! : 0;

        output += BASE64_ALPHABET[first >> 2];
        output += BASE64_ALPHABET[((first & 0x03) << 4) | (second >> 4)];
        output += index + 1 < bytes.length ? BASE64_ALPHABET[((second & 0x0f) << 2) | (third >> 6)] : '=';
        output += index + 2 < bytes.length ? BASE64_ALPHABET[third & 0x3f] : '=';
    }

    return output;
};

const decodeBase64 = (value: string): Uint8Array => {
    const sanitized = value.replace(/=+$/u, '');
    const byteLength = Math.floor((sanitized.length * 3) / 4);
    const bytes = new Uint8Array(byteLength);

    let byteIndex = 0;
    let buffer = 0;
    let bitsCollected = 0;

    for (let index = 0; index < sanitized.length; index += 1) {
        const code = sanitized.charCodeAt(index);
        const sextet = code < 128 ? BASE64_LOOKUP[code]! : -1;
        if (sextet < 0) {
            throw new TerrainError(
                `Height data contains an invalid base64 character at index ${index}.`,
                TerrainErrorCode.SOURCE_DECODE_FAILED,
                { index }
            );
        }

        buffer = (buffer << 6) | sextet;
        bitsCollected += 6;
        if (bitsCollected >= 8) {
            bitsCollected -= 8;
            bytes[byteIndex] = (buffer >> bitsCollected) & 0xff;
            byteIndex += 1;
        }
    }

    return bytes;
};

/** Serializes normalized heights into a versioned base64 payload. */
export const encodeTerrainHeights = (heights: Readonly<Float32Array>): string => {
    const bytes = new Uint8Array(heights.length * 2);
    for (let index = 0; index < heights.length; index += 1) {
        const value = heights[index]!;
        const clamped = value < 0 ? 0 : value > 1 ? 1 : value;
        const quantized = Math.round(clamped * QUANTIZATION_MAX);
        bytes[index * 2] = quantized & 0xff;
        bytes[index * 2 + 1] = (quantized >> 8) & 0xff;
    }

    return `${HEIGHT_DATA_PREFIX}${encodeBase64(bytes)}`;
};

/** Parses a payload produced by {@link encodeTerrainHeights}. */
export const decodeTerrainHeights = (
    value: string,
    resolution: TerrainResolution
): Float32Array => {
    if (!isTerrainResolution(resolution)) {
        throw new TerrainError(
            `Invalid resolution: ${resolution}.`,
            TerrainErrorCode.INVALID_RESOLUTION,
            { resolution }
        );
    }

    if (!value.startsWith(HEIGHT_DATA_PREFIX)) {
        throw new TerrainError(
            'Height data payload is missing the expected version prefix.',
            TerrainErrorCode.SOURCE_DECODE_FAILED,
            { prefix: value.slice(0, 8) }
        );
    }

    const bytes = decodeBase64(value.slice(HEIGHT_DATA_PREFIX.length));
    const expectedLength = resolution * resolution;
    if (bytes.length !== expectedLength * 2) {
        throw new TerrainError(
            `Height data payload length ${bytes.length} does not match resolution ${resolution} (expected ${expectedLength * 2} bytes).`,
            TerrainErrorCode.HEIGHTMAP_SIZE_MISMATCH,
            { resolution, expectedBytes: expectedLength * 2, actualBytes: bytes.length }
        );
    }

    const heights = new Float32Array(expectedLength);
    for (let index = 0; index < expectedLength; index += 1) {
        const quantized = bytes[index * 2]! | (bytes[index * 2 + 1]! << 8);
        heights[index] = quantized / QUANTIZATION_MAX;
    }

    return heights;
};

/** Cheap shape check usable by editors before attempting a full decode. */
export const isTerrainHeightDataPayload = (value: unknown): value is string =>
    typeof value === 'string' && value.startsWith(HEIGHT_DATA_PREFIX);
