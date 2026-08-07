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
    '@axrone/physics-2d',
    '@axrone/ui',
]) as readonly string[];