import type { ChunkSnapshot } from '@axrone/tilemap';
import type { TilesetDefinition } from './tileset';
import type { TilemapAssetDefinition } from './tilemap-asset';

export * from './errors';
export * from './tileset';
export * from './tilemap-asset';
export * from './tiled/tmx-parser';
export * from './tiled/tsx-parser';
export * from './tiled/tiled-importer';

/** Asset database schema used by the tilemap asset pipeline. */
export interface AssetTilemapSchema {
    readonly [key: string]: unknown;
    readonly tilemap: TilemapAssetDefinition;
    readonly tileset: TilesetDefinition;
    readonly 'tilemap-chunk': ChunkSnapshot;
}

export type AssetTilemapKind = keyof AssetTilemapSchema;
