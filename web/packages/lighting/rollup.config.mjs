import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createMultiEntryConfig } from '../../build/create-package-config.mjs';

const packageDir = path.dirname(fileURLToPath(import.meta.url));

export default createMultiEntryConfig({
    packageDir,
    entries: {
        index: 'src/index.ts',
        'core/index': 'src/core.ts',
        'frame/index': 'src/frame.ts',
        'serialization/index': 'src/serialization.ts',
    },
});
