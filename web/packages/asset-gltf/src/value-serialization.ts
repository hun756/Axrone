import { encodeValue } from '@axrone/numeric';
import type { GltfSerializedValue } from './asset-ir';

/**
 * Encodes a runtime value into a GLTF-compatible serialized form.
 *
 * Thin wrapper around the canonical {@link encodeValue} from `@axrone/numeric`.
 * The return type is narrowed to `GltfSerializedValue` for backwards
 * compatibility with existing GLTF pipeline consumers.
 */
export const encodeGltfValue = (value: unknown): GltfSerializedValue =>
    encodeValue(value) as GltfSerializedValue;
