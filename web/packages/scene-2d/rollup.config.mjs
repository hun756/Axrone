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
        inputRelativePath: 'src/facade.ts',
        outputBasename: 'facade',
    }),
    ...createPackageConfig({
        packageDir,
        inputRelativePath: 'src/support.ts',
        outputBasename: 'support',
    }),
    ...createPackageConfig({
        packageDir,
        inputRelativePath: 'src/profile.ts',
        outputBasename: 'profile',
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
];