import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const testDir = path.dirname(fileURLToPath(import.meta.url));
const packagesDir = path.resolve(testDir, '../../../packages');
const physicsBridgeSrcDir = path.resolve(packagesDir, 'physics/src');
const physicsCoreSrcDir = path.resolve(packagesDir, 'physics-core/src');
const physics2dSrcDir = path.resolve(packagesDir, 'physics-2d/src');
const physics3dSrcDir = path.resolve(packagesDir, 'physics-3d/src');
const raycastSrcDir = path.resolve(packagesDir, 'raycast/src');
const corePhysicsDir = path.resolve(testDir, '../../../packages/core/src/physics');
const coreComponentPhysicsDir = path.resolve(testDir, '../../../packages/core/src/component-system/components/physics');
const disallowedCoreSourceBypassPattern =
    /(?:from ['"]|import\(['"])(?:\.\.\/)+core\/src\/(?:input|physics|geometry|component-system)(?:\/[^'"]*)?['"]/g;
const disallowedSiblingSourceBypassPattern =
    /(?:from ['"]|import\(['"])(?:\.\.\/)+(?:geometry|physics|physics-core|physics-2d|physics-3d|raycast)\/src(?:\/[^'"]*)?['"]/g;
// The umbrella package is a pure facade since the raycast subsystem moved to
// @axrone/raycast (refactor plan phase 2.2).
const allowedPhysicsBridgeFiles = [
    'index.ts',
] as const;

const collectTypeScriptFiles = (dirPath: string): readonly string[] => {
    const files: string[] = [];

    for (const entry of fs.readdirSync(dirPath, { withFileTypes: true })) {
        const fullPath = path.resolve(dirPath, entry.name);
        if (entry.isDirectory()) {
            if (entry.name === '__tests__') {
                continue;
            }

            files.push(...collectTypeScriptFiles(fullPath));
            continue;
        }

        if (
            entry.isFile() &&
            entry.name.endsWith('.ts') &&
            !entry.name.endsWith('.test.ts') &&
            !entry.name.endsWith('.spec.ts')
        ) {
            files.push(fullPath);
        }
    }

    return files;
};

describe('physics ownership boundary', () => {
    it('keeps split physics packages on local files and public package dependencies', () => {
        const ownerPackageDirs = [
            physicsBridgeSrcDir,
            physicsCoreSrcDir,
            physics2dSrcDir,
            physics3dSrcDir,
            raycastSrcDir,
        ];
        const violatingFiles = ownerPackageDirs
            .flatMap((dirPath) => collectTypeScriptFiles(dirPath))
            .filter((filePath) => {
                const content = fs.readFileSync(filePath, 'utf8');
                const hasCoreBypass = disallowedCoreSourceBypassPattern.test(content);
                disallowedCoreSourceBypassPattern.lastIndex = 0;
                const hasSiblingBypass = disallowedSiblingSourceBypassPattern.test(content);
                disallowedSiblingSourceBypassPattern.lastIndex = 0;
                return hasCoreBypass || hasSiblingBypass;
            })
            .map((filePath) => path.relative(packagesDir, filePath).replace(/\\/g, '/'))
            .sort((left, right) => left.localeCompare(right));

        expect(violatingFiles).toEqual([]);
    });

    it('keeps @axrone/physics as a pure facade over the split packages', () => {
        const bridgeFiles = collectTypeScriptFiles(physicsBridgeSrcDir)
            .map((filePath) => path.relative(physicsBridgeSrcDir, filePath).replace(/\\/g, '/'))
            .sort((left, right) => left.localeCompare(right));

        expect(bridgeFiles).toEqual([...allowedPhysicsBridgeFiles].sort((left, right) => left.localeCompare(right)));
    });

    it('leaves core compatibility physics surfaces as thin facades only', () => {
        expect(fs.existsSync(corePhysicsDir)).toBe(false);
        expect(fs.existsSync(coreComponentPhysicsDir)).toBe(false);
    });
});
