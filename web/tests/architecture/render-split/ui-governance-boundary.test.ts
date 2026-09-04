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

/**
 * Enumerate all workspace packages whose package.json lists @axrone/ui as a
 * dependency (regular, dev, or peer). Returns the package directory names.
 */
const discoverUiConsumerPackages = (): string[] => {
    const consumers: string[] = [];

    for (const entry of fs.readdirSync(packagesDir, { withFileTypes: true })) {
        if (!entry.isDirectory()) continue;

        const pkgJsonPath = path.resolve(packagesDir, entry.name, 'package.json');
        if (!fs.existsSync(pkgJsonPath)) continue;

        const pkgJson = JSON.parse(fs.readFileSync(pkgJsonPath, 'utf8')) as {
            name?: string;
            dependencies?: Record<string, string>;
            devDependencies?: Record<string, string>;
            peerDependencies?: Record<string, string>;
        };

        const allDeps = {
            ...pkgJson.dependencies,
            ...pkgJson.devDependencies,
            ...pkgJson.peerDependencies,
        };

        if ('@axrone/ui' in allDeps) {
            consumers.push(entry.name);
        }
    }

    return consumers.sort();
};

const publicUiSpecifiers = readPublicPackageSpecifiers('@axrone/ui', uiPackageJsonPath);

const isDisallowedUiSpecifier = (specifier: string): boolean =>
    specifier === '@axrone/ui' ||
    specifier.includes('ui/src/') ||
    (specifier.startsWith('@axrone/ui/') && !publicUiSpecifiers.has(specifier));

describe('ui governance boundary', () => {
    const consumerPackages = discoverUiConsumerPackages();

    it('discovers at least ui-webgl2 and asset-ui as @axrone/ui consumers', () => {
        expect(consumerPackages).toContain('ui-webgl2');
        expect(consumerPackages).toContain('asset-ui');
    });

    for (const pkgName of consumerPackages) {
        it(`keeps ${pkgName} on focused @axrone/ui subpath imports instead of the root barrel`, () => {
            const pkgSrcDir = path.resolve(packagesDir, pkgName, 'src');
            const violatingFiles = collectTypeScriptFiles(pkgSrcDir, {
                exclude: (filePath) => isTestSourceFile(filePath),
            })
                .filter((filePath) =>
                    listModuleSpecifiers(filePath).some((specifier) => isDisallowedUiSpecifier(specifier))
                )
                .map((filePath) => toWorkspaceRelativePath(workspaceDir, filePath))
                .sort((left, right) => left.localeCompare(right));

            expect(violatingFiles).toEqual([]);
        });
    }
});
