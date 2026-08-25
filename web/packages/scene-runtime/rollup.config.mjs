import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createMultiEntryConfig } from '../../build/create-package-config.mjs';

const packageDir = path.dirname(fileURLToPath(import.meta.url));

export default createMultiEntryConfig({
    packageDir,
    entries: {
        index: 'src/index.ts',
        types: 'src/types.ts',
        errors: 'src/errors.ts',
        'scene-registry': 'src/scene-registry.ts',
        'scene-profile': 'src/scene-profile.ts',
        'scene-profile-contract': 'src/scene-profile-contract.ts',
        'scene-manifest-profile': 'src/scene-manifest-profile.ts',
        'scene-core-profile': 'src/scene-core-profile.ts',
        'scene-2d-profile': 'src/scene-2d-profile.ts',
        'scene-3d-profile': 'src/scene-3d-profile.ts',
        'scene-full-profile': 'src/scene-full-profile.ts',
        'scene-facade': 'src/scene-facade.ts',
        prefab: 'src/prefab.ts',
        'scene-3d-support': 'src/scene-3d-support.ts',
        'scene-2d-support': 'src/scene-2d-support.ts',
        rendering: 'src/rendering/index.ts',
    },
});
