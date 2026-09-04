import { Color } from '@axrone/numeric';
import type { ColorHexString } from './foundation';
import type { ColorInput, ColorLike, ReadonlyColor } from './layout';

/**
 * Sentinel transparent color returned when parsing fails or input is invalid.
 */
export const TRANSPARENT_COLOR: ReadonlyColor = Object.freeze({ r: 0, g: 0, b: 0, a: 0 });

/**
 * Converts a packed 32-bit integer (0xRRGGBBAA) to a normalized {@link ReadonlyColor}.
 */
const colorFromNumber = (value: number): ReadonlyColor => ({
    r: ((value >>> 24) & 0xff) / 255,
    g: ((value >>> 16) & 0xff) / 255,
    b: ((value >>> 8) & 0xff) / 255,
    a: (value & 0xff) / 255,
});

/**
 * Parses a hex color string to a normalized {@link ReadonlyColor}.
 * Returns {@link TRANSPARENT_COLOR} if parsing fails.
 */
const colorFromHex = (value: string): ReadonlyColor => {
    try {
        return Color.fromHex(value);
    } catch {
        return TRANSPARENT_COLOR;
    }
};

/**
 * Type guard for {@link ColorLike} objects (distinguishes from arrays).
 */
const isColorLike = (value: ColorInput): value is ColorLike =>
    typeof value === 'object' && value !== null && !Array.isArray(value);

/**
 * Canonical normalization of any {@link ColorInput} to a resolved {@link ReadonlyColor}.
 *
 * Handles all input forms:
 * - `number`: packed 32-bit RGBA (0xRRGGBBAA)
 * - `string`: hex color (#RGB, #RGBA, #RRGGBB, #RRGGBBAA)
 * - `readonly [number, number, number]`: RGB tuple (alpha defaults to 1)
 * - `readonly [number, number, number, number]`: RGBA tuple
 * - {@link ColorLike}: object with r/g/b and optional a (defaults to 1)
 *
 * @param input - The color input to normalize, or undefined to use fallback.
 * @param fallback - The color to return when input is undefined.
 * @returns A fully resolved color with r/g/b/a in [0, 1] range.
 */
export const normalizeColor = (
    input: ColorInput | undefined,
    fallback: ReadonlyColor,
): ReadonlyColor => {
    if (input === undefined) {
        return fallback;
    }
    if (typeof input === 'number') {
        return colorFromNumber(input >>> 0);
    }
    if (typeof input === 'string') {
        return colorFromHex(input);
    }
    if (Array.isArray(input)) {
        if (input.length >= 4) {
            return { r: input[0], g: input[1], b: input[2], a: input[3] };
        }
        if (input.length === 3) {
            return { r: input[0], g: input[1], b: input[2], a: 1 };
        }
        return fallback;
    }
    if (!isColorLike(input)) {
        return fallback;
    }
    return {
        r: input.r,
        g: input.g,
        b: input.b,
        a: input.a ?? 1,
    };
};

/**
 * Normalizes a {@link ColorInput} to a [r, g, b, a] tuple.
 *
 * This is a convenience wrapper around {@link normalizeColor} for callers
 * that need tuple output (e.g. WebGL batch buffers).
 *
 * @param input - The color input to normalize.
 * @param fallback - The color to return when input is undefined.
 * @returns A readonly [r, g, b, a] tuple with values in [0, 1].
 */
export const normalizeColorToTuple = (
    input: ColorInput | undefined,
    fallback: ReadonlyColor,
): readonly [number, number, number, number] => {
    const color = normalizeColor(input, fallback);
    return [color.r, color.g, color.b, color.a] as const;
};

// Re-export ColorHexString for convenience so callers can import from one place.
export type { ColorHexString };
