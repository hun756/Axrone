import type { AssetDisposerMap } from '@axrone/asset-core';
import type {
    GltfAssetSchema,
    GltfTextureAsset,
    GltfDocumentAsset,
    GltfPrefabAsset,
    GltfMeshAsset,
    GltfSkinAsset,
    GltfAnimationClipAsset,
    GltfMaterialAsset,
} from './types';

/**
 * Disposes resources held by a `gltf.texture` asset.
 *
 * Currently texture payloads carry `Uint8Array` bytes (raw / compressed) or
 * external URIs — neither requires explicit cleanup.  The disposer is
 * forward-compatible: if a payload ever carries an `ImageBitmap`-like resource
 * with a `close()` method, it will be released here.
 */
function disposeGltfTexture(data: GltfTextureAsset): void {
    const payload = data.payload as { close?: () => void } | undefined;
    if (payload && typeof payload.close === 'function') {
        payload.close();
    }
}

/**
 * No-op disposer for `gltf.document` assets.
 *
 * Document assets are pure JSON IR (scenes, meshes, materials, animations).
 * The disposer exists to satisfy the `AssetDisposerMap` contract and to provide
 * a hook for future resource tracking (e.g. shared buffer reference counting).
 */
function disposeGltfDocument(_data: GltfDocumentAsset): void {
    // Pure JSON IR — no GPU or native resources to release.
}

/**
 * No-op disposer for `gltf.prefab` assets.
 *
 * Prefab definitions are serializable JSON trees. The disposer is a forward-
 * compatibility hook for potential future native resource bindings.
 */
function disposeGltfPrefab(_data: GltfPrefabAsset): void {
    // Serializable JSON — no GPU or native resources to release.
}

/**
 * No-op disposer for `gltf.mesh` assets.
 * Mesh definitions are serializable JSON trees.
 */
function disposeGltfMesh(_data: GltfMeshAsset): void {
    // Serializable JSON — no GPU or native resources to release.
}

/**
 * No-op disposer for `gltf.skin` assets.
 * Skin definitions are serializable JSON trees.
 */
function disposeGltfSkin(_data: GltfSkinAsset): void {
    // Serializable JSON — no GPU or native resources to release.
}

/**
 * No-op disposer for `gltf.animation` assets.
 * Animation clip definitions are serializable JSON trees.
 */
function disposeGltfAnimation(_data: GltfAnimationClipAsset): void {
    // Serializable JSON — no GPU or native resources to release.
}

/**
 * No-op disposer for `gltf.material` assets.
 * Material definitions are serializable JSON trees.
 */
function disposeGltfMaterial(_data: GltfMaterialAsset): void {
    // Serializable JSON — no GPU or native resources to release.
}

/**
 * Creates an {@link AssetDisposerMap} for all glTF asset kinds.
 *
 * Pass the result to `AssetDatabase` via `options.disposers` to ensure that
 * decoded buffers, textures, and documents are properly cleaned up when assets
 * are removed or replaced in the database.
 *
 * @example
 * ```ts
 * const db = new AssetDatabase({
 *     disposers: createGltfAssetDisposers(),
 * });
 * ```
 */
export function createGltfAssetDisposers(): AssetDisposerMap<GltfAssetSchema> {
    return {
        'gltf.document': disposeGltfDocument,
        'gltf.prefab': disposeGltfPrefab,
        'gltf.mesh': disposeGltfMesh,
        'gltf.skin': disposeGltfSkin,
        'gltf.animation': disposeGltfAnimation,
        'gltf.material': disposeGltfMaterial,
        'gltf.texture': disposeGltfTexture,
    } as AssetDisposerMap<GltfAssetSchema>;
}
