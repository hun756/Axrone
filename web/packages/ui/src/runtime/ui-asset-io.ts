import { clamp } from '@axrone/numeric';
import { InvalidUIAssetError } from '../errors';
import type { UICanvasConfig, UICanvasScaleMode, UIAsset, UISafeAreaInset } from '../types/ui-asset';
import type { WidgetSerializableKey } from '../types/foundation';
import type { WidgetSnapshot } from '../types/render-frame';

const VALID_SCALE_MODES: ReadonlySet<UICanvasScaleMode> = new Set<UICanvasScaleMode>([
    'match-width',
    'match-height',
    'match-width-or-height',
    'fill',
    'fixed',
]);

const CURRENT_ASSET_VERSION = 1;

function isPlainObject(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * Recursively freezes an object/array subtree so that no nested property can
 * be mutated. Returns the same reference cast to Readonly for convenience.
 */
function deepFreeze<T>(value: T): Readonly<T> {
    if (value === null || typeof value !== 'object') return value;
    Object.freeze(value);
    if (Array.isArray(value)) {
        for (const item of value) deepFreeze(item);
    } else {
        for (const key of Object.keys(value)) {
            deepFreeze((value as Record<string, unknown>)[key]);
        }
    }
    return value;
}

function requireString(obj: Record<string, unknown>, key: string, context: string): string {
    const value = obj[key];
    if (typeof value !== 'string' || value.length === 0) {
        throw new InvalidUIAssetError(`${context}: "${key}" must be a non-empty string.`, { key, value });
    }
    return value;
}

function requirePositiveNumber(obj: Record<string, unknown>, key: string, context: string): number {
    const value = obj[key];
    if (typeof value !== 'number' || !Number.isFinite(value) || value <= 0) {
        throw new InvalidUIAssetError(`${context}: "${key}" must be a positive finite number.`, { key, value });
    }
    return value;
}

function requireNonNegativeNumber(obj: Record<string, unknown>, key: string, context: string): number {
    const value = obj[key];
    if (typeof value !== 'number' || !Number.isFinite(value) || value < 0) {
        throw new InvalidUIAssetError(`${context}: "${key}" must be a non-negative finite number.`, { key, value });
    }
    return value;
}

function parseSafeAreaInset(value: unknown, context: string): UISafeAreaInset | undefined {
    if (value === undefined || value === null) {
        return undefined;
    }
    if (!isPlainObject(value)) {
        throw new InvalidUIAssetError(`${context}: "safeAreaInset" must be an object.`, { value });
    }
    return {
        top: requireNonNegativeNumber(value, 'top', `${context}.safeAreaInset`),
        right: requireNonNegativeNumber(value, 'right', `${context}.safeAreaInset`),
        bottom: requireNonNegativeNumber(value, 'bottom', `${context}.safeAreaInset`),
        left: requireNonNegativeNumber(value, 'left', `${context}.safeAreaInset`),
    };
}

function parseCanvasConfig(value: unknown, context: string): UICanvasConfig {
    if (!isPlainObject(value)) {
        throw new InvalidUIAssetError(`${context}: "canvas" must be an object.`, { value });
    }
    const referenceWidth = requirePositiveNumber(value, 'referenceWidth', `${context}.canvas`);
    const referenceHeight = requirePositiveNumber(value, 'referenceHeight', `${context}.canvas`);
    const scaleMode = value['scaleMode'];
    if (typeof scaleMode !== 'string' || !VALID_SCALE_MODES.has(scaleMode as UICanvasScaleMode)) {
        throw new InvalidUIAssetError(
            `${context}.canvas: "scaleMode" must be one of: ${[...VALID_SCALE_MODES].join(', ')}.`,
            { scaleMode }
        );
    }
    const rawBias = value['matchBias'];
    const matchBias = typeof rawBias === 'number' && Number.isFinite(rawBias)
        ? clamp(rawBias, 0, 1)
        : 0.5;
    const safeAreaInset = parseSafeAreaInset(value['safeAreaInset'], `${context}.canvas`);

    const config: Record<string, unknown> = {
        referenceWidth,
        referenceHeight,
        scaleMode,
        matchBias,
    };
    if (safeAreaInset) {
        config['safeAreaInset'] = safeAreaInset;
    }
    return config as unknown as UICanvasConfig;
}

function parseWidgetSnapshot(value: unknown, context: string): WidgetSnapshot {
    if (!isPlainObject(value)) {
        throw new InvalidUIAssetError(`${context}: "root" must be an object.`, { value });
    }
    const role = typeof value['role'] === 'string' ? value['role'] : 'root';
    const enabled = value['enabled'] !== false;
    const interactive = value['interactive'] !== false;
    const children = Array.isArray(value['children'])
        ? value['children'].map((child, index) => parseWidgetSnapshot(child, `${context}.root.children[${index}]`))
        : [];

    return {
        role,
        controller: typeof value['controller'] === 'string' ? value['controller'] : undefined,
        key: (typeof value['key'] === 'string' || typeof value['key'] === 'number') ? value['key'] : undefined,
        props: isPlainObject(value['props']) ? (value['props'] as Record<string, unknown>) : undefined,
        enabled,
        interactive,
        layout: isPlainObject(value['layout']) ? (value['layout'] as WidgetSnapshot['layout']) : undefined,
        style: isPlainObject(value['style']) ? (value['style'] as WidgetSnapshot['style']) : undefined,
        text: isPlainObject(value['text']) ? (value['text'] as unknown as WidgetSnapshot['text']) : undefined,
        image: isPlainObject(value['image']) ? (value['image'] as unknown as WidgetSnapshot['image']) : undefined,
        focus: isPlainObject(value['focus']) ? (value['focus'] as WidgetSnapshot['focus']) : undefined,
        material: isPlainObject(value['material']) ? deepFreeze({ ...value['material'] }) as Record<string, unknown> : undefined,
        children,
    } as WidgetSnapshot;
}

function parseBindings(
    value: unknown,
    context: string
): Readonly<Record<string, WidgetSerializableKey>> | undefined {
    if (value === undefined || value === null) {
        return undefined;
    }
    if (!isPlainObject(value)) {
        throw new InvalidUIAssetError(`${context}: "bindings" must be an object.`, { value });
    }
    const result: Record<string, WidgetSerializableKey> = {};
    for (const [key, raw] of Object.entries(value)) {
        const isValidKey =
            (typeof raw === 'string' && raw.length > 0) ||
            (typeof raw === 'number' && Number.isFinite(raw));
        if (!isValidKey) {
            throw new InvalidUIAssetError(
                `${context}.bindings: "${key}" must be a non-empty string or finite number widget key.`,
                { key, raw }
            );
        }
        result[key] = raw as WidgetSerializableKey;
    }
    return result;
}

/**
 * Validates that an unknown value conforms to the UIAsset schema.
 * Throws `InvalidUIAssetError` on validation failure.
 */
export function validateUIAsset(data: unknown): data is UIAsset {
    if (!isPlainObject(data)) {
        return false;
    }
    try {
        parseUIAssetInternal(data);
        return true;
    } catch {
        return false;
    }
}

function parseUIAssetInternal(data: Record<string, unknown>): UIAsset {
    const context = 'UIAsset';
    const id = requireString(data, 'id', context);
    const name = requireString(data, 'name', context);
    const rawVersion = data['version'];
    const version = typeof rawVersion === 'number' && Number.isFinite(rawVersion) && rawVersion >= 1
        ? Math.floor(rawVersion)
        : CURRENT_ASSET_VERSION;
    const canvas = parseCanvasConfig(data['canvas'], context);
    const root = parseWidgetSnapshot(data['root'], context);
    const bindings = parseBindings(data['bindings'], context);

    const asset: Record<string, unknown> = { id, name, version, canvas, root };
    if (bindings) {
        asset['bindings'] = bindings;
    }
    return asset as unknown as UIAsset;
}

/**
 * Deserializes a JSON string into a UIAsset.
 * Throws `InvalidUIAssetError` if the JSON is malformed or fails validation.
 */
export function deserializeUIAsset(json: string): UIAsset {
    let parsed: unknown;
    try {
        parsed = JSON.parse(json);
    } catch (error) {
        throw new InvalidUIAssetError('Failed to parse UI asset JSON.', { originalError: error });
    }
    return parseUIAssetInternal(parsed as Record<string, unknown>);
}

/**
 * Serializes a UIAsset into a JSON string.
 * The output is pretty-printed for human readability.
 */
export function serializeUIAsset(asset: UIAsset): string {
    // Validate before serializing to ensure output is always valid
    parseUIAssetInternal(asset as unknown as Record<string, unknown>);
    return JSON.stringify(asset, null, 2);
}
