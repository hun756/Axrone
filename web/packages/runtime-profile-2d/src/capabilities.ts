import { ASSET_2D_CAPABILITY_PACKAGE } from '@axrone/asset-2d';
import { INPUT_CAPABILITY_PACKAGE } from '@axrone/input';
import { RENDER_2D_CAPABILITY_PACKAGE } from '@axrone/render-2d';

export const RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES = Object.freeze([
    '@axrone/scene-runtime',
    '@axrone/scene-2d',
    INPUT_CAPABILITY_PACKAGE,
    ASSET_2D_CAPABILITY_PACKAGE,
    RENDER_2D_CAPABILITY_PACKAGE,
    '@axrone/physics-core',
    // TODO(physics-2d-bridge): @axrone/physics-2d is declared as a capability but
    // not wired to scene-runtime — no PhysicsBridge2D exists (contrast with
    // physics-bridge-3d in scene-runtime). Consequence: 2D collision/sensor events
    // do not reach user @script components, and the physics-2d bundle is included
    // in playable-2d without being instantiated at runtime.
    '@axrone/physics-2d',
    '@axrone/ui',
]) as readonly string[];