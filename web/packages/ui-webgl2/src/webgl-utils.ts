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
import { normalizeColor, type ReadonlyColor } from '@axrone/ui/types';

/** Unit quad vertex positions for TRIANGLE_STRIP rendering (0,0 → 1,1). */
export const UNIT_QUAD = new Float32Array([0, 0, 1, 0, 0, 1, 1, 1]);

/** Multiplies an alpha value by an opacity factor. */
export const multiplyAlpha = (alpha: number, opacity: number): number => alpha * opacity;

/**
 * Writes a premultiplied-alpha color into a Float32Array batch at the given
 * offset. Handles both resolved color objects (r/g/b/a) and raw inputs
 * (hex string, packed number, tuple) via {@link writeStrokeColor}.
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
        writeStrokeColor(color, batch, offset);
        batch[offset + 3] = multiplyAlpha(batch[offset + 3], opacity);
        return;
    }
    batch[offset] = color.r;
    batch[offset + 1] = color.g;
    batch[offset + 2] = color.b;
    batch[offset + 3] = multiplyAlpha(color.a ?? 1, opacity);
};

/**
 * Writes a normalized [r, g, b, a] color directly into a caller-owned
 * Float32Array at the given offset, avoiding any module-scoped scratch
 * retention hazard.
 *
 * Delegates to the canonical {@link normalizeColor} from `@axrone/ui/types`
 * to ensure a single normalization path across the UI and WebGL packages.
 */
export const writeStrokeColor = (
    color: StrokeRenderCommand['strokes'][number]['color'],
    out: Float32Array,
    offset = 0
): void => {
    const WHITE: ReadonlyColor = { r: 1, g: 1, b: 1, a: 1 };
    const resolved = normalizeColor(color, WHITE);
    out[offset] = resolved.r;
    out[offset + 1] = resolved.g;
    out[offset + 2] = resolved.b;
    out[offset + 3] = resolved.a;
};
