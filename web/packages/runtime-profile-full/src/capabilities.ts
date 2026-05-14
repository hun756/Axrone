import { RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES } from '@axrone/runtime-profile-2d/capabilities';
import { RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES } from '@axrone/runtime-profile-3d/capabilities';

export const RUNTIME_PROFILE_FULL_CAPABILITY_PACKAGES = Object.freeze([
    ...new Set([
        ...RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES,
        ...RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES,
    ]),
]) as readonly string[];