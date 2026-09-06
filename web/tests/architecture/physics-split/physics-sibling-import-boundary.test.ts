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

const physics2dSrcDir = path.resolve(packagesDir, 'physics-2d/src');
const physics3dSrcDir = path.resolve(packagesDir, 'physics-3d/src');
const raycastSrcDir = path.resolve(packagesDir, 'raycast/src');

/**
 * Each split physics package may import from @axrone/physics-core (shared contracts)
 * but must NOT import from sibling split packages by name.
 *
 * The facade (@axrone/physics) is excluded — it is tested separately in
 * physics-ownership-boundary.test.ts (pure facade, index.ts only).
 *
 * The existing physics-ownership-boundary.test.ts already blocks relative-path
 * bypasses (../physics-3d/src/...). This test closes the package-name gap:
 * @axrone/physics-2d must not appear in physics-3d sources, etc.
 */
const allowedSiblingSpecifiers: Record<string, ReadonlySet<string>> = {
    'physics-2d': new Set(['@axrone/physics-3d', '@axrone/raycast']),
    'physics-3d': new Set(['@axrone/physics-2d', '@axrone/raycast']),
    raycast: new Set(['@axrone/physics-2d', '@axrone/physics-3d']),
};

const packageDirs: ReadonlyArray<{ readonly name: string; readonly srcDir: string }> = [
    { name: 'physics-2d', srcDir: physics2dSrcDir },
    { name: 'physics-3d', srcDir: physics3dSrcDir },
    { name: 'raycast', srcDir: raycastSrcDir },
];

describe('physics sibling import boundary', () => {
    it('keeps split physics packages free of cross-sibling package-name imports', () => {
        const violations: string[] = [];

        for (const pkg of packageDirs) {
            const forbidden = allowedSiblingSpecifiers[pkg.name];
            const sourceFiles = collectTypeScriptFiles(pkg.srcDir, {
                exclude: isTestSourceFile,
            });

            for (const filePath of sourceFiles) {
                const specifiers = listModuleSpecifiers(filePath);

                for (const specifier of specifiers) {
                    if (
                        forbidden.has(specifier) ||
                        [...forbidden].some(
                            (f) => specifier === f || specifier.startsWith(`${f}/`)
                        )
                    ) {
                        violations.push(
                            `${toWorkspaceRelativePath(workspaceDir, filePath)}: imports forbidden sibling ${specifier}`
                        );
                    }
                }
            }
        }

        expect(violations).toEqual([]);
    });
});
