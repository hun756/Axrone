import { INPUT_CORE_CAPABILITY_PACKAGE } from '@axrone/input-core';
import { RENDER_3D_CAPABILITY_PACKAGE } from '@axrone/render-3d';

export const RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES = Object.freeze([
    '@axrone/scene-runtime',
    '@axrone/scene-3d',
    INPUT_CORE_CAPABILITY_PACKAGE,
    '@axrone/asset-core',
    '@axrone/asset-gltf',
    '@axrone/asset-ui',
    RENDER_3D_CAPABILITY_PACKAGE,
    '@axrone/render-webgl2',
    '@axrone/physics-core',
    '@axrone/physics-3d',
    '@axrone/ui',
    '@axrone/ui-webgl2',
]) as readonly string[];