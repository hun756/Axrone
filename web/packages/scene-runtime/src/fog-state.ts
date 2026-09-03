import { Vec3, clamp, Color } from '@axrone/numeric';

/**
 * Fog density mode enumeration matching Unity/Godot conventions.
 *  - 0 = Linear (uses fogRange start/end)
 *  - 1 = Exponential (uses fogDensity)
 *  - 2 = ExponentialSquared (uses fogDensity)
 */
export type SceneFogMode = 0 | 1 | 2;

export interface SceneFogState {
    readonly enabled: boolean;
    readonly color: Readonly<Vec3>;
    readonly mode: SceneFogMode;
    readonly density: number;
    readonly range: readonly [number, number];
}

export const DEFAULT_SCENE_FOG_STATE: SceneFogState = Object.freeze({
    enabled: false,
    color: Object.freeze(new Vec3(0.533, 0.6, 0.667)),
    mode: 1,
    density: 0.015,
    range: Object.freeze([0, 300]),
}) as SceneFogState;

const asBoolean = (value: unknown, fallback: boolean): boolean =>
    typeof value === 'boolean' ? value : fallback;

const asNumber = (value: unknown, fallback: number): number => {
    if (typeof value === 'number' && Number.isFinite(value)) {
        return value;
    }
    return fallback;
};

const asString = (value: unknown, fallback: string): string =>
    typeof value === 'string' ? value : fallback;

const parseHexColorToVec3 = (value: string, fallback: Vec3): Vec3 => {
    try {
        const c = Color.fromHex(value);
        return new Vec3(c.r, c.g, c.b);
    } catch {
        return fallback;
    }
};

const resolveFogMode = (value: unknown): SceneFogMode => {
    const normalized = asString(value, 'Exponential').trim().toLowerCase();
    if (normalized === 'linear') {
        return 0;
    }
    if (normalized === 'exponential') {
        return 1;
    }
    return 2;
};

const resolveFogRange = (value: unknown, fallback: readonly [number, number]): [number, number] => {
    if (Array.isArray(value) && value.length === 2) {
        const start = asNumber(value[0], fallback[0]);
        const end = asNumber(value[1], fallback[1]);
        return [Math.min(start, end), Math.max(start, end)];
    }
    return [fallback[0], fallback[1]];
};

/**
 * Resolves fog state from a raw scene environment settings bag (e.g. scene JSON
 * `settings.environment`). Missing or invalid keys fall back to safe defaults
 * (fog disabled).
 */
export const resolveSceneFogState = (
    environment: Record<string, unknown> | null | undefined
): SceneFogState => {
    if (!environment) {
        return DEFAULT_SCENE_FOG_STATE;
    }

    const defaults = DEFAULT_SCENE_FOG_STATE;

    return {
        enabled: asBoolean(environment.fogEnabled, defaults.enabled),
        color: parseHexColorToVec3(
            asString(environment.fogColor, '#8899aa'),
            defaults.color as Vec3
        ),
        mode: resolveFogMode(environment.fogMode),
        density: clamp(asNumber(environment.fogDensity, defaults.density), 0, 1),
        range: resolveFogRange(environment.fogRange, defaults.range),
    };
};
