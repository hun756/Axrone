/**
 * A catalog entry maps a code to a human-readable message template.
 */
export type MessageCatalogEntry<TCode extends string> = {
    readonly code: TCode;
    readonly message: string;
};

/**
 * A catalog is a readonly map from code to message template.
 */
export type MessageCatalog<TCode extends string> = Readonly<Record<TCode, string>>;

/**
 * Creates a type-safe message catalog from a record of code → message template.
 *
 * Usage:
 * ```ts
 * const catalog = createMessageCatalog({
 *     'loop.invalid-fixed-delta': 'fixedDelta must be a positive finite number',
 *     'loop.invalid-scheduler': 'scheduler must be a valid GameLoopScheduler',
 * });
 * // catalog['loop.invalid-fixed-delta'] → string
 * ```
 *
 * The plan says: "Ortak createMessageCatalog helper'ı @axrone/utility'ye eklenir;
 * yeni kod onu kullanır (mevcut 6 paketin katalogları fırsat buldukça geçirilir, zorunlu değil)."
 */
export function createMessageCatalog<TCode extends string>(
    entries: Record<TCode, string>
): MessageCatalog<TCode> {
    return Object.freeze({ ...entries });
}

/**
 * Resolves a message from a catalog using a code. Returns the message template
 * if found, or a fallback string if the code is not in the catalog.
 */
export function resolveFromCatalog<TCode extends string>(
    catalog: MessageCatalog<TCode>,
    code: TCode
): string {
    return catalog[code] ?? `Unknown message code: ${String(code)}`;
}
