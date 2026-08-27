import type {
    AssetKind,
    AssetLookupByKey,
    AssetRecord,
    AssetSchema,
} from '../types';

export const isLookupByKey = <TSchema extends AssetSchema>(
    value: unknown
): value is AssetLookupByKey<TSchema> =>
    value !== null &&
    typeof value === 'object' &&
    typeof (value as AssetLookupByKey<TSchema>).key === 'string';

export const isAssetRecordValue = <TSchema extends AssetSchema>(
    value: unknown
): value is AssetRecord<TSchema> =>
    value !== null &&
    typeof value === 'object' &&
    typeof (value as AssetRecord<TSchema>).kind === 'string' &&
    typeof (value as AssetRecord<TSchema>).id === 'string' &&
    typeof (value as AssetRecord<TSchema>).key === 'string' &&
    'reference' in (value as AssetRecord<TSchema>);
