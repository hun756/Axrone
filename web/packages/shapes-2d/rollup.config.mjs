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
        paint: 'src/paint.ts',
        shape: 'src/shape.ts',
        queries: 'src/queries.ts',
        mesh: 'src/mesh.ts',
        serialization: 'src/serialization.ts',
        registry: 'src/registry.ts',
    },
});
