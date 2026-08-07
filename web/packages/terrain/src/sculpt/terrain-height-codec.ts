import { TerrainError, TerrainErrorCode } from '../errors';
import { encode as encodeBytesToBase64, decode as decodeBase64ToBytes } from '@axrone/utility';
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

    return `${HEIGHT_DATA_PREFIX}${encodeBytesToBase64(bytes)}`;
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

    let bytes: Uint8Array;
    try {
        bytes = decodeBase64ToBytes(value.slice(HEIGHT_DATA_PREFIX.length));
    } catch {
        throw new TerrainError(
            'Payload contains an invalid base64 character.',
            TerrainErrorCode.SOURCE_DECODE_FAILED,
        );
    }
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
