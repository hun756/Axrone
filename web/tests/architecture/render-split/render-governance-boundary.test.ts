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
const examplesDir = path.resolve(workspaceDir, 'examples');
const renderCoreDir = path.resolve(packagesDir, 'render-core/src');
const renderCorePackageJsonPath = path.resolve(packagesDir, 'render-core/package.json');

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

const publicRenderCoreSpecifiers = readPublicPackageSpecifiers(
    '@axrone/render-core',
    renderCorePackageJsonPath
);

const collectWorkspaceSourceFiles = (): readonly string[] =>
    [packagesDir, examplesDir].flatMap((dirPath) =>
        collectTypeScriptFiles(dirPath, {
            exclude: (filePath) => isTestSourceFile(filePath),
        })
    );

const isPrivateRenderCoreSpecifier = (specifier: string): boolean =>
    specifier.includes('render-core/src/') ||
    (specifier.startsWith('@axrone/render-core/') && !publicRenderCoreSpecifiers.has(specifier));

const isPrivateRenderWebgl2Specifier = (specifier: string): boolean =>
    specifier.includes('render-webgl2/src/');

describe('render governance boundary', () => {
    it('keeps render-core planning sources independent from render-webgl2 executor ownership', () => {
        const violatingFiles = collectTypeScriptFiles(renderCoreDir, {
            exclude: (filePath) => isTestSourceFile(filePath),
        })
            .filter((filePath) =>
                listModuleSpecifiers(filePath).some(
                    (specifier) =>
                        specifier === '@axrone/render-webgl2' ||
                        specifier.startsWith('@axrone/render-webgl2/') ||
                        isPrivateRenderWebgl2Specifier(specifier)
                )
            )
            .map((filePath) => toWorkspaceRelativePath(workspaceDir, filePath))
            .sort((left, right) => left.localeCompare(right));

        expect(violatingFiles).toEqual([]);
    });

    it('keeps engine consumers on public render-core and render-webgl2 entrypoints', () => {
        const violatingFiles = collectWorkspaceSourceFiles()
            .filter((filePath) => !filePath.startsWith(renderCoreDir))
            .filter((filePath) =>
                listModuleSpecifiers(filePath).some(
                    (specifier) =>
                        isPrivateRenderCoreSpecifier(specifier) ||
                        isPrivateRenderWebgl2Specifier(specifier)
                )
            )
            .map((filePath) => toWorkspaceRelativePath(workspaceDir, filePath))
            .sort((left, right) => left.localeCompare(right));

        expect(violatingFiles).toEqual([]);
    });
});