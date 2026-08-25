import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createMultiEntryConfig } from '../../build/create-package-config.mjs';

const packageDir = path.dirname(fileURLToPath(import.meta.url));

export default createMultiEntryConfig({
    packageDir,
    entries: {
        index: 'src/index.ts',
        'renderer/index': 'src/renderer.ts',
        'pipeline/index': 'src/pipeline.ts',
        'scene/index': 'src/scene.ts',
        'scene-host/index': 'src/scene-host.ts',
    },
});
