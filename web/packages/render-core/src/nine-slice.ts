/**
 * Shared nine-slice span math and rounded-rect SDF GLSL.
 *
 * Hosted in render-core so that both the WebGL2 UI renderer (ui-webgl2) and
 * the 2D sprite batch builder (render-2d) can share the same pure functions
 * and GLSL building block without duplicating the logic.
 */

// ─── Pure TS math ────────────────────────────────────────────────────────────

/** Result of computing the display-space border sizes for one axis. */
export type AxisBorderSizes = {
    startSize: number;
    endSize: number;
    centerSize: number;
};

/**
 * Computes display-space border and center sizes for a single nine-slice axis.
 *
 * When the combined borders exceed the target extent the borders are
 * proportionally scaled down so the center slice never goes negative.
 */
export const computeAxisBorderSizes = (
    targetSize: number,
    startBorder: number,
    endBorder: number,
    sourceSize: number,
): AxisBorderSizes => {
    const totalBorder = startBorder + endBorder;
    const scale = totalBorder > targetSize && totalBorder > 0 ? targetSize / totalBorder : 1;
    const startSize = startBorder * scale;
    const endSize = endBorder * scale;
    const centerSize = Math.max(0, targetSize - startSize - endSize);
    return { startSize, endSize, centerSize };
};

/** Result of computing the UV-space fractions for one nine-slice axis. */
export type AxisUvFractions = {
    startUv: number;
    endUv: number;
};

/**
 * Computes the UV fractions consumed by the start and end borders of a
 * nine-slice axis.
 */
export const computeAxisUvFractions = (
    startBorder: number,
    endBorder: number,
    sourceSize: number,
): AxisUvFractions => ({
    startUv: sourceSize > 0 ? startBorder / sourceSize : 0,
    endUv: sourceSize > 0 ? endBorder / sourceSize : 0,
});

// ─── Shared GLSL building block ──────────────────────────────────────────────

/**
 * Shared GLSL fragment code: `selectRadius` + `roundedRectSdf`.
 *
 * Computes the outside-signed-distance of a rounded rectangle whose per-corner
 * radii are selected based on the fragment's quadrant. Embed this block
 * verbatim inside any GLSL shader that needs rounded-rect SDF evaluation.
 */
export const ROUNDED_RECT_SDF_GLSL = `
float selectRadius(vec2 local, vec2 size, vec4 radii) {
    bool left = local.x <= size.x * 0.5;
    bool top = local.y <= size.y * 0.5;
    if (left && top) return radii.x;
    if (!left && top) return radii.y;
    if (!left && !top) return radii.z;
    return radii.w;
}
float roundedRectSdf(vec2 local, vec2 size, vec4 radii) {
    float radius = selectRadius(local, size, radii);
    vec2 center = size * 0.5;
    vec2 halfSize = max(center - vec2(radius), vec2(0.0));
    vec2 delta = abs(local - center) - halfSize;
    return length(max(delta, 0.0)) + min(max(delta.x, delta.y), 0.0) - radius;
}`;
