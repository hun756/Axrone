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
    // @axrone/physics-2d is wired through PhysicsBridge2D in @axrone/scene-runtime.
    // The bridge is instantiated by SceneRuntimeKernel when the 2D scene profile is active.
    // NOTE: the playable-2d reference bundle (examples/playable-2d) does not yet include
    // the scene runtime kernel — physics is available only when a consuming artifact
    // instantiates Scene2D or passes the 2D profile to SceneRuntimeKernel directly.
    // 2D collision/sensor events are routed to user @script components via the bridge.
    '@axrone/physics-2d',
    '@axrone/ui',
]) as readonly string[];