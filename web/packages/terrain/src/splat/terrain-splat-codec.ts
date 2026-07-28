import { TerrainError, TerrainErrorCode } from '../errors';
import { decodeBase64ToBytes, encodeBytesToBase64 } from '../internal/base64';
import type { TerrainSplatResolution } from '../types';
import { isTerrainSplatResolution } from '../types';

/**
 * Persistence codec for splat weight buffers: raw RGBA bytes, base64-encoded
 * behind a version prefix (mirrors the sculpted-height `athf1:` format).
 */
const SPLAT_DATA_PREFIX = 'atsp1:';

/** Serializes an RGBA weight buffer into a versioned base64 payload. */
export const encodeTerrainSplat = (splat: Readonly<Uint8Array>): string =>
    `${SPLAT_DATA_PREFIX}${encodeBytesToBase64(splat as Uint8Array)}`;

/** Parses a payload produced by {@link encodeTerrainSplat}. */
export const decodeTerrainSplat = (
    value: string,
    resolution: TerrainSplatResolution
): Uint8Array => {
    if (!isTerrainSplatResolution(resolution)) {
        throw new TerrainError(
            `Invalid splat resolution: ${resolution}.`,
            TerrainErrorCode.INVALID_RESOLUTION,
            { resolution }
        );
    }

    if (!value.startsWith(SPLAT_DATA_PREFIX)) {
        throw new TerrainError(
            'Splat data payload is missing the expected version prefix.',
            TerrainErrorCode.SOURCE_DECODE_FAILED,
            { prefix: value.slice(0, 8) }
        );
    }

    const bytes = decodeBase64ToBytes(value.slice(SPLAT_DATA_PREFIX.length));
    const expectedLength = resolution * resolution * 4;
    if (bytes.length !== expectedLength) {
        throw new TerrainError(
            `Splat data payload length ${bytes.length} does not match resolution ${resolution} (expected ${expectedLength} bytes).`,
            TerrainErrorCode.SPLAT_SIZE_MISMATCH,
            { resolution, expectedBytes: expectedLength, actualBytes: bytes.length }
        );
    }

    return bytes;
};

/** Cheap shape check usable by editors before attempting a full decode. */
export const isTerrainSplatDataPayload = (value: unknown): value is string =>
    typeof value === 'string' && value.startsWith(SPLAT_DATA_PREFIX);
