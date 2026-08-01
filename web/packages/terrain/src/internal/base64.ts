import { TerrainError, TerrainErrorCode } from '../errors';
import { encode, decode } from '@axrone/utility';

/**
 * Re-exports the centralized base64 codec with terrain-specific error translation.
 * The centralized module throws `Base64Error` subclasses; terrain codecs expect
 * `TerrainError` with `SOURCE_DECODE_FAILED`, so we translate here.
 */
export const encodeBytesToBase64 = (bytes: Uint8Array): string => encode(bytes);

export const decodeBase64ToBytes = (value: string): Uint8Array => {
    try {
        return decode(value);
    } catch {
        throw new TerrainError(
            'Payload contains an invalid base64 character.',
            TerrainErrorCode.SOURCE_DECODE_FAILED,
        );
    }
};
