/**
 * Lean `@axrone/scene-3d` entry point.
 *
 * Exposes only the 3D scene facade surface a render-only consumer needs:
 * `Scene`, the renderable-actor runtime and its instance types, the geometry
 * mesh builder, the built-in unlit color shader, and the accompanying config
 * and resource types.
 *
 * The root entry (`@axrone/scene-3d`) keeps every existing export. This
 * subpath is additive and safe to adopt incrementally; it never changes the
 * behavior of the root entry.
 *
 * @module lean
 */

export { Scene } from './scene-lean';
export {
    Scene3DActorRuntime,
    type Scene3DActorRuntimeOptions,
    type SceneProfilerProbe,
    type SceneRenderableActorCreateOptions,
    type SceneRenderableActorInstance,
} from './scene-3d-actor-runtime';
export {
    MeshRenderer,
    SceneGeometryMeshBuilder,
    type MeshRendererConfig,
} from '@axrone/scene-runtime/scene-3d-support';
export { Camera, type CameraConfig } from '@axrone/scene-runtime/scene-facade';
export { createUnlitColorShaderDefinition } from './scene-default-shaders';

export type {
    SceneMaterialAlphaMode,
    SceneMaterialDefinition,
    SceneMaterialHandle,
    SceneMaterialSurfaceDefinition,
    SceneMeshDefinition,
    SceneMeshHandle,
    SceneOptions,
    SceneRegistry,
    SceneRenderPlanningStats,
    SceneRenderStats,
    SceneShaderDefinition,
    SceneShaderHandle,
    SceneTextureBindingDefinition,
    SceneTextureDefinition,
    SceneTextureHandle,
    SceneUniformValue,
} from '@axrone/scene-runtime/types';
