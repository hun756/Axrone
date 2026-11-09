import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createMultiEntryConfig } from '../../build/create-package-config.mjs';

const packageDir = path.dirname(fileURLToPath(import.meta.url));

export default createMultiEntryConfig({
    packageDir,
    entries: {
        index: 'src/index.ts',
        codec: 'src/codec/index.ts',
        grid: 'src/grid/index.ts',
        profiler: 'src/profiler/index.ts',
        errors: 'src/errors.ts'
    },
});
