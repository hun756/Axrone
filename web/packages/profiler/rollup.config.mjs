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
    inputRelativePath: 'src/capabilities.ts',
    outputBasename: 'capabilities',
  }),
  ...createPackageConfig({
    packageDir,
    inputRelativePath: 'src/profile.ts',
    outputBasename: 'profile',
  }),
  ...createPackageConfig({
    packageDir,
    inputRelativePath: 'src/core/clock/index.ts',
    outputBasename: 'core/clock/index',
  }),
];
