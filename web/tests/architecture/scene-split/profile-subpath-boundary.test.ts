import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { listModuleSpecifiers, toWorkspaceRelativePath } from '../_helpers/import-specifiers';

const testDir = path.dirname(fileURLToPath(import.meta.url));
const workspaceDir = path.resolve(testDir, '../../..');

interface ProfileImportRule {
    readonly filePath: string;
    readonly requiredSpecifier: string;
}

const rules: readonly ProfileImportRule[] = [
    {
        filePath: path.resolve(
            workspaceDir,
            'packages/runtime-profile-core/src/profile.ts'
        ),
        requiredSpecifier: '@axrone/scene-runtime/scene-core-profile',
    },
    {
        filePath: path.resolve(
            workspaceDir,
            'packages/runtime-profile-2d/src/profile.ts'
        ),
        requiredSpecifier: '@axrone/scene-runtime/scene-2d-profile',
    },
    {
        filePath: path.resolve(
            workspaceDir,
            'packages/runtime-profile-3d/src/profile.ts'
        ),
        requiredSpecifier: '@axrone/scene-runtime/scene-3d-profile',
    },
    {
        filePath: path.resolve(workspaceDir, 'packages/scene-2d/src/scene-2d.ts'),
        requiredSpecifier: '@axrone/scene-runtime/scene-2d-profile',
    },
] as const;

describe('scene profile subpath boundary', () => {
    it('keeps bundle-sensitive profile entrypoints off the shared scene-profile barrel', () => {
        const violatingFiles = rules
            .filter(({ filePath, requiredSpecifier }) => {
                const specifiers = new Set(listModuleSpecifiers(filePath));
                return (
                    specifiers.has('@axrone/scene-runtime/scene-profile') ||
                    specifiers.has(requiredSpecifier) === false
                );
            })
            .map(({ filePath }) => toWorkspaceRelativePath(workspaceDir, filePath))
            .sort((left, right) => left.localeCompare(right));

        expect(violatingFiles).toEqual([]);
    });
});