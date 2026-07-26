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
const uiWebgl2Dir = path.resolve(packagesDir, 'ui-webgl2/src');
const uiPackageJsonPath = path.resolve(packagesDir, 'ui/package.json');

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

const publicUiSpecifiers = readPublicPackageSpecifiers('@axrone/ui', uiPackageJsonPath);

const isDisallowedUiSpecifier = (specifier: string): boolean =>
    specifier === '@axrone/ui' ||
    specifier.includes('ui/src/') ||
    (specifier.startsWith('@axrone/ui/') && !publicUiSpecifiers.has(specifier));

describe('ui governance boundary', () => {
    it('keeps ui-webgl2 on focused @axrone/ui subpath imports instead of the root barrel', () => {
        const violatingFiles = collectTypeScriptFiles(uiWebgl2Dir, {
            exclude: (filePath) => isTestSourceFile(filePath),
        })
            .filter((filePath) =>
                listModuleSpecifiers(filePath).some((specifier) => isDisallowedUiSpecifier(specifier))
            )
            .map((filePath) => toWorkspaceRelativePath(workspaceDir, filePath))
            .sort((left, right) => left.localeCompare(right));

        expect(violatingFiles).toEqual([]);
    });
});