/**
 * Canonical rectangle-like interface with position (x, y) and size (width, height).
 *
 * This is the single source of truth for rectangular bounds across the engine.
 * Both `@axrone/ui` (as `RectLike`) and `@axrone/render-2d` (as `Render2DRectLike`)
 * re-export this interface under their own public names to preserve API
 * compatibility while eliminating structural duplication.
 */
export interface IRectLike {
    readonly x: number;
    readonly y: number;
    readonly width: number;
    readonly height: number;
}
