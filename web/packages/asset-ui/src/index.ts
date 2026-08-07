import type { AssetDatabase, AssetImporter, AssetImportSource } from '@axrone/asset-core';
import { deserializeUIAsset } from '@axrone/ui/runtime';
import type { UIAsset } from '@axrone/ui/types';

/**
 * Asset schema fragment contributed by the UI asset importer.
 * Compose it into an application schema to store `.ui.json` documents.
 */
export interface UIAssetSchema extends Readonly<Record<string, unknown>> {
    readonly 'ui-asset': UIAsset;
}

type UIAssetSource = Extract<AssetImportSource, { readonly kind: 'text' }>;

/**
 * Imports `.ui.json` documents into the asset pipeline as `ui-asset` records.
 *
 * Register this importer on an `AssetDatabase` whose schema includes
 * {@link UIAssetSchema}. Validation is delegated to `deserializeUIAsset`,
 * which throws `InvalidUIAssetError` on malformed input.
 */
export const uiAssetImporter: AssetImporter<UIAssetSchema, UIAssetSource, 'ui-asset'> = {
    id: 'axrone.ui-asset',
    sourceKinds: ['text'],
    extensions: ['.ui.json'],
    import: ({ source }) => {
        const asset = deserializeUIAsset(source.data);
        return {
            primary: {
                kind: 'ui-asset',
                data: asset,
            },
        };
    },
};

/**
 * Builds an assetId -> UIAsset resolver backed by an AssetDatabase.
 * The returned function plugs directly into `bindUIHostToScene({ resolveAsset })`
 * (see @axrone/ui-webgl2/scene-host), keeping scene binding decoupled from
 * the asset pipeline.
 */
export function createUIAssetResolver<TSchema extends UIAssetSchema>(
    database: AssetDatabase<TSchema>
): (assetId: string) => UIAsset | null {
    return (assetId) => {
        const record = database.get({ key: assetId, kind: 'ui-asset' });
        return record ? (record.data as UIAsset) : null;
    };
}
