import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const testDir = path.dirname(fileURLToPath(import.meta.url));
const ecsWorldSupportSrcDir = path.resolve(testDir, '../../../packages/ecs-runtime/src/support');
// Support is a leaf module inside ecs-runtime: it must not reach any
// @axrone/ecs or @axrone/core package surface, the component-system runtime
// internals, or the sibling storage/query modules.
const disallowedImportPattern =
    /(?:from ['"]|import\(['"])(?:[^'"]*@axrone\/(?:ecs|core)|[^'"]*component-system|(?:\.\.\/)+(?:storage|query))(?:\/[^'"]*)?['"]/g;

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

describe('ecs-runtime support module boundary', () => {
    it('keeps support-owned sources off runtime internals and sibling modules', () => {
        const violatingFiles = collectTypeScriptFiles(ecsWorldSupportSrcDir)
            .filter((filePath) => {
                const content = fs.readFileSync(filePath, 'utf8');
                const hasDisallowedImport = disallowedImportPattern.test(content);
                disallowedImportPattern.lastIndex = 0;
                return hasDisallowedImport;
            })
            .map((filePath) => path.relative(ecsWorldSupportSrcDir, filePath).replace(/\\/g, '/'))
            .sort((left, right) => left.localeCompare(right));

        expect(violatingFiles).toEqual([]);
    });
});