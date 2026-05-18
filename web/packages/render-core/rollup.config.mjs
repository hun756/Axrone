import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createPackageConfig } from '../../build/create-package-config.mjs';

const packageDir = path.dirname(fileURLToPath(import.meta.url));

export default [
    ...createPackageConfig({
        packageDir,
    }),
    ...createPackageConfig({
        packageDir,
        inputRelativePath: 'src/types.ts',
        outputBasename: 'types',
    }),
    ...createPackageConfig({
        packageDir,
        inputRelativePath: 'src/errors.ts',
        outputBasename: 'errors',
    }),
    ...createPackageConfig({
        packageDir,
        inputRelativePath: 'src/memory.ts',
        outputBasename: 'memory',
    }),
    ...createPackageConfig({
        packageDir,
        inputRelativePath: 'src/graph.ts',
        outputBasename: 'graph',
    }),
    ...createPackageConfig({
        packageDir,
        inputRelativePath: 'src/post-process.ts',
        outputBasename: 'post-process',
    }),
    ...createPackageConfig({
        packageDir,
        inputRelativePath: 'src/pipeline.ts',
        outputBasename: 'pipeline',
    }),
    ...createPackageConfig({
        packageDir,
        inputRelativePath: 'src/planner.ts',
        outputBasename: 'planner',
    }),
    ...createPackageConfig({
        packageDir,
        inputRelativePath: 'src/shader-effect.ts',
        outputBasename: 'shader-effect',
    }),
];