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
const memorySrcDir = path.resolve(packagesDir, 'memory/src');

const ALLOWED_AXRONE_PACKAGES = new Set([
    '@axrone/utility',
    '@axrone/hash',
]);

const HIGHER_LEVEL_PACKAGES = [
    'scene',
    'render',
    'ecs',
    'event',
    'particle-system',
    'animation',
    'geometry',
    'physics',
    'audio',
    'ui',
    'tween',
    'asset',
    'input',
    'lighting',
    'terrain',
    'shapes-2d',
    'numeric',
    'random',
    'raycast',
    'profiler',
    'game-loop',
];

const isAxronePackage = (specifier: string): boolean =>
    specifier.startsWith('@axrone/');

const getAxronePackageName = (specifier: string): string => {
    const parts = specifier.split('/');
    return parts.slice(0, 2).join('/');
};

const isHigherLevelPackage = (specifier: string): boolean =>
    HIGHER_LEVEL_PACKAGES.some((pkg) => specifier.startsWith(`@axrone/${pkg}`));

describe('memory architecture boundary', () => {
    it('does not import from higher-level packages', () => {
        const violatingFiles = collectTypeScriptFiles(memorySrcDir, {
            exclude: (filePath) => isTestSourceFile(filePath),
        })
            .filter((filePath) => {
                const specifiers = listModuleSpecifiers(filePath);
                return specifiers.some(
                    (specifier) => isAxronePackage(specifier) && isHigherLevelPackage(specifier)
                );
            })
            .map((filePath) => toWorkspaceRelativePath(workspaceDir, filePath))
            .sort((left, right) => left.localeCompare(right));

        expect(violatingFiles).toEqual([]);
    });

    it('only imports from @axrone/utility and @axrone/hash', () => {
        const violatingFiles = collectTypeScriptFiles(memorySrcDir, {
            exclude: (filePath) => isTestSourceFile(filePath),
        })
            .filter((filePath) => {
                const specifiers = listModuleSpecifiers(filePath);
                return specifiers.some((specifier) => {
                    if (!isAxronePackage(specifier)) {
                        return false;
                    }
                    const packageName = getAxronePackageName(specifier);
                    return !ALLOWED_AXRONE_PACKAGES.has(packageName);
                });
            })
            .map((filePath) => toWorkspaceRelativePath(workspaceDir, filePath))
            .sort((left, right) => left.localeCompare(right));

        expect(violatingFiles).toEqual([]);
    });

    it('has no circular dependencies with other packages', () => {
        const memoryPackageJsonPath = path.resolve(packagesDir, 'memory/package.json');
        const memoryPackageJson = JSON.parse(fs.readFileSync(memoryPackageJsonPath, 'utf8')) as {
            dependencies?: Record<string, string>;
        };

        const memoryDependencies = Object.keys(memoryPackageJson.dependencies ?? {});

        const circularDependencies = memoryDependencies.filter((dep) => {
            const depPackageJsonPath = path.resolve(
                packagesDir,
                `${dep.replace('@axrone/', '')}/package.json`
            );

            if (!fs.existsSync(depPackageJsonPath)) {
                return false;
            }

            const depPackageJson = JSON.parse(fs.readFileSync(depPackageJsonPath, 'utf8')) as {
                dependencies?: Record<string, string>;
            };

            const depDependencies = Object.keys(depPackageJson.dependencies ?? {});
            return depDependencies.includes('@axrone/memory');
        });

        expect(circularDependencies).toEqual([]);
    });
});
