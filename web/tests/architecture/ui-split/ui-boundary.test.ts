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
const uiDir = path.resolve(packagesDir, 'ui/src');
const sceneRuntimeDir = path.resolve(packagesDir, 'scene-runtime/src');
const assetCoreDir = path.resolve(packagesDir, 'asset-core/src');

const readPackageDependencies = (packageDir: string): ReadonlySet<string> => {
    const packageJsonPath = path.resolve(packagesDir, packageDir, 'package.json');
    const packageJson = JSON.parse(fs.readFileSync(packageJsonPath, 'utf8')) as {
        dependencies?: Record<string, string>;
    };
    return new Set(Object.keys(packageJson.dependencies ?? {}));
};

const listAxroneDependencies = (packageDir: string): readonly string[] =>
    [...readPackageDependencies(packageDir)]
        .filter((dependency) => dependency.startsWith('@axrone/'))
        .sort((left, right) => left.localeCompare(right));

const collectViolatingFiles = (
    sourceDir: string,
    isForbiddenSpecifier: (specifier: string) => boolean
): readonly string[] =>
    collectTypeScriptFiles(sourceDir, {
        exclude: (filePath) => isTestSourceFile(filePath),
    })
        .filter((filePath) =>
            listModuleSpecifiers(filePath).some((specifier) => isForbiddenSpecifier(specifier))
        )
        .map((filePath) => toWorkspaceRelativePath(workspaceDir, filePath))
        .sort((left, right) => left.localeCompare(right));

describe('ui package boundary', () => {
    it('keeps @axrone/ui dependent only on @axrone/utility', () => {
        expect(listAxroneDependencies('ui')).toEqual(['@axrone/utility']);
    });

    it('keeps @axrone/ui-webgl2 dependencies within the allowed set', () => {
        expect(listAxroneDependencies('ui-webgl2')).toEqual([
            '@axrone/game-loop',
            '@axrone/render-core',
            '@axrone/scene-runtime',
            '@axrone/ui',
        ]);
    });

    it('keeps @axrone/asset-ui dependencies within the allowed set', () => {
        expect(listAxroneDependencies('asset-ui')).toEqual([
            '@axrone/asset-core',
            '@axrone/ui',
        ]);
    });

    it('keeps scene-runtime free of ui packages to prevent a scene-runtime <-> ui-webgl2 cycle', () => {
        const sceneRuntimeDependencies = readPackageDependencies('scene-runtime');
        expect(sceneRuntimeDependencies.has('@axrone/ui')).toBe(false);
        expect(sceneRuntimeDependencies.has('@axrone/ui-webgl2')).toBe(false);

        const violatingFiles = collectViolatingFiles(
            sceneRuntimeDir,
            (specifier) =>
                specifier === '@axrone/ui' ||
                specifier.startsWith('@axrone/ui/') ||
                specifier.startsWith('@axrone/ui-webgl2')
        );
        expect(violatingFiles).toEqual([]);
    });

    it('keeps asset-core free of @axrone/ui so UI import stays in @axrone/asset-ui', () => {
        expect(readPackageDependencies('asset-core').has('@axrone/ui')).toBe(false);

        const violatingFiles = collectViolatingFiles(
            assetCoreDir,
            (specifier) => specifier === '@axrone/ui' || specifier.startsWith('@axrone/ui/')
        );
        expect(violatingFiles).toEqual([]);
    });

    it('prevents @axrone/ui from importing @axrone/asset-core', () => {
        const violatingFiles = collectViolatingFiles(
            uiDir,
            (specifier) => specifier === '@axrone/asset-core'
        );
        expect(violatingFiles).toEqual([]);
    });

    it('prevents cross-import violations between ui and ui-webgl2', () => {
        const violatingFiles = collectViolatingFiles(uiDir, (specifier) =>
            specifier.startsWith('@axrone/ui-webgl2')
        );
        expect(violatingFiles).toEqual([]);
    });
});
