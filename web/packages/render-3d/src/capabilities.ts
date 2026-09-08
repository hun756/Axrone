/**
 * @axrone/render-3d — Capability marker package.
 *
 * This package does NOT contain a 3D renderer implementation. It exists solely
 * to declare the "render/3d" capability for the engine's capability-package
 * governance system (see ADR-0001).
 *
 * The actual 3D rendering implementation lives in:
 * - @axrone/render-core (backend-agnostic pipeline, pass planner, frame graph)
 * - @axrone/render-webgl2 (WebGL2 backend: buffers, textures, shaders, FBOs)
 *
 * This marker satisfies the capability-package policy while keeping the
 * implementation split between the orchestration layer (render-core) and the
 * backend layer (render-webgl2).
 */

export const RENDER_3D_CAPABILITY_ID = 'render/3d';
export const RENDER_3D_CAPABILITY_PACKAGE = '@axrone/render-3d';
export const RENDER_3D_OWNER_PACKAGE = '@axrone/render-core';

const RENDER_3D_CAPABILITY = Object.freeze({
    id: RENDER_3D_CAPABILITY_ID,
    packageName: RENDER_3D_CAPABILITY_PACKAGE,
    ownerPackage: RENDER_3D_OWNER_PACKAGE,
});

export type Render3DCapability = typeof RENDER_3D_CAPABILITY;

export const getRender3DCapability = (): Render3DCapability => RENDER_3D_CAPABILITY;