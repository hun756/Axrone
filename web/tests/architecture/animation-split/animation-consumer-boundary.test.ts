import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import {
    collectTypeScriptFiles,
    isTestSourceFile,
    listModuleSpecifiers,
    toWorkspaceRelativePath,
} from '../_helpers/import-specifiers';

const testDir = path.dirname(fileURLToPath(import.meta.url));
const workspaceDir = path.resolve(testDir, '../../..');
const packagesDir = path.resolve(workspaceDir, 'packages');
const animationPackageJsonPath = path.resolve(packagesDir, 'animation/package.json');
const consumerDirs = [
    path.resolve(packagesDir, 'scene-runtime/src'),
    path.resolve(packagesDir, 'asset-gltf/src'),
] as const;

const readPublicPackageSpecifiers = (packageName: string, packageJsonPath: string): ReadonlySet<string> => {
    const packageJson = JSON.parse(fs.readFileSync(packageJsonPath, 'utf8')) as {
        exports?: Record<string, unknown>;
    };
    const publicSpecifiers = new Set<string>([packageName]);

    for (const exportKey of Object.keys(packageJson.exports ?? {})) {
        if (!exportKey.startsWith('./')) {
            continue;
        }

        publicSpecifiers.add(`${packageName}/${exportKey.slice(2)}`);
    }

    return publicSpecifiers;
};

const publicAnimationSpecifiers = readPublicPackageSpecifiers(
    '@axrone/animation',
    animationPackageJsonPath
);

const isDisallowedAnimationSpecifier = (specifier: string): boolean =>
    specifier === '@axrone/animation' ||
    specifier.includes('animation/src/') ||
    (specifier.startsWith('@axrone/animation/') && !publicAnimationSpecifiers.has(specifier));

describe('animation consumer boundary', () => {
    it('keeps scene-runtime and asset-gltf on focused @axrone/animation subpath imports', () => {
        const violatingFiles = consumerDirs
            .flatMap((dirPath) =>
                collectTypeScriptFiles(dirPath, {
                    exclude: (filePath) => isTestSourceFile(filePath),
                })
            )
            .filter((filePath) =>
                listModuleSpecifiers(filePath).some((specifier) => isDisallowedAnimationSpecifier(specifier))
            )
            .map((filePath) => toWorkspaceRelativePath(workspaceDir, filePath))
            .sort((left, right) => left.localeCompare(right));

        expect(violatingFiles).toEqual([]);
    });
});