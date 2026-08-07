import { describe, expect, it } from 'vitest';
import { AssetDatabase, AssetImportError } from '@axrone/asset-core';
import { createUIAssetResolver, uiAssetImporter, type UIAssetSchema } from '@axrone/asset-ui';

const VALID_UI_ASSET_JSON = JSON.stringify({
    id: 'ui.main-menu',
    name: 'Main Menu',
    version: 1,
    canvas: {
        referenceWidth: 1920,
        referenceHeight: 1080,
        scaleMode: 'match-width-or-height',
        matchBias: 0.5,
    },
    root: {
        role: 'root',
        children: [
            {
                role: 'panel',
                children: [],
            },
        ],
    },
});

describe('uiAssetImporter', () => {
    it('imports a .ui.json text source as a ui-asset record', async () => {
        const database = new AssetDatabase<UIAssetSchema>({
            importers: [uiAssetImporter],
        });

        const receipt = await database.import({
            kind: 'text',
            data: VALID_UI_ASSET_JSON,
            uri: 'assets/main-menu.ui.json',
            mimeType: 'application/json',
        });

        expect(receipt.importerId).toBe('axrone.ui-asset');
        expect(receipt.primary.kind).toBe('ui-asset');
        expect(receipt.primary.data.id).toBe('ui.main-menu');
        expect(receipt.primary.data.canvas.referenceWidth).toBe(1920);
        expect(receipt.primary.data.root.children).toHaveLength(1);
    });

    it('rejects malformed UI asset JSON with a validation error', async () => {
        const database = new AssetDatabase<UIAssetSchema>({
            importers: [uiAssetImporter],
        });

        await expect(
            database.import({
                kind: 'text',
                data: '{"id":"broken"}',
                uri: 'assets/broken.ui.json',
                mimeType: 'application/json',
            })
        ).rejects.toThrowError(AssetImportError);
    });
});

describe('createUIAssetResolver', () => {
    it('resolves imported assets by key and returns null for unknown ids', async () => {
        const database = new AssetDatabase<UIAssetSchema>({
            importers: [uiAssetImporter],
        });

        const receipt = await database.import({
            kind: 'text',
            data: VALID_UI_ASSET_JSON,
            uri: 'assets/main-menu.ui.json',
            mimeType: 'application/json',
        });

        const resolve = createUIAssetResolver(database);
        const resolved = resolve(receipt.primary.key);

        expect(resolved).not.toBeNull();
        expect(resolved!.id).toBe('ui.main-menu');
        expect(resolved!.canvas.referenceWidth).toBe(1920);

        expect(resolve('assets/does-not-exist.ui.json')).toBeNull();
    });
});
