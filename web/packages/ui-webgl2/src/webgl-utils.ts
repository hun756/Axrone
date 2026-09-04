/**
 * Shared low-level WebGL helpers for the UI renderer.
 *
 * Extracted from renderer.ts where these small utility functions lived
 * alongside the main renderer class. Keeping them in a dedicated module
 * makes them discoverable and testable in isolation.
 */
import type {
    QuadRenderCommand,
    StrokeRenderCommand,
    TextRenderCommand,
} from '@axrone/ui/types';
import { Color } from '@axrone/numeric';

/** Unit quad vertex positions for TRIANGLE_STRIP rendering (0,0 → 1,1). */
export const UNIT_QUAD = new Float32Array([0, 0, 1, 0, 0, 1, 1, 1]);

/** Multiplies an alpha value by an opacity factor. */
export const multiplyAlpha = (alpha: number, opacity: number): number => alpha * opacity;

/**
 * Writes a premultiplied-alpha color into a Float32Array batch at the given
 * offset. Handles both resolved color objects (r/g/b/a) and raw inputs
 * (hex string, packed number, tuple) via {@link normalizeStrokeColor}.
 */
export const writeBlendedColor = (
    batch: Float32Array,
    offset: number,
    color: QuadRenderCommand['color'] | TextRenderCommand['color'],
    opacity: number
): void => {
    // Runtime-authored commands always carry resolved color objects; the
    // scalar/array branch only serves hand-built commands.
    if (typeof color === 'string' || typeof color === 'number' || Array.isArray(color)) {
        const resolved = normalizeStrokeColor(color);
        batch[offset] = resolved[0];
        batch[offset + 1] = resolved[1];
        batch[offset + 2] = resolved[2];
        batch[offset + 3] = multiplyAlpha(resolved[3], opacity);
        return;
    }
    batch[offset] = color.r;
    batch[offset + 1] = color.g;
    batch[offset + 2] = color.b;
    batch[offset + 3] = multiplyAlpha(color.a ?? 1, opacity);
};

/**
 * Converts a ColorInput (hex string, number, array, or ColorLike) to a
 * normalized [r, g, b, a] tuple for stroke rendering.
 */
export const normalizeStrokeColor = (color: StrokeRenderCommand['strokes'][number]['color']): readonly [number, number, number, number] => {
    if (typeof color === 'string') {
        try {
            const c = Color.fromHex(color);
            return [c.r, c.g, c.b, c.a] as const;
        } catch {
            return [1, 1, 1, 1] as const;
        }
    }
    if (typeof color === 'number') {
        return [
            ((color >>> 24) & 0xff) / 255,
            ((color >>> 16) & 0xff) / 255,
            ((color >>> 8) & 0xff) / 255,
            (color & 0xff) / 255,
        ];
    }
    // Object branch first: Array.isArray cannot narrow readonly tuple members
    // out of the union, but the `in` check cleanly separates object from tuple.
    if ('r' in color) {
        return [color.r, color.g, color.b, color.a ?? 1];
    }
    return color.length === 3
        ? [color[0], color[1], color[2], 1]
        : [color[0], color[1], color[2], color[3]];
};
