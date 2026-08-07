import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const testDir = path.dirname(fileURLToPath(import.meta.url));
const packagesDir = path.resolve(testDir, '../../../packages');

// The ecs-storage / ecs-query / ecs-world-support micro packages were merged
// into @axrone/ecs-runtime as src/storage, src/query and src/support modules
// exposed through subpath exports (refactor plan phase 2.1).
describe('ecs micro-package merge', () => {
    it('removes the merged micro-package workspaces', () => {
        expect(fs.existsSync(path.resolve(packagesDir, 'ecs-storage'))).toBe(false);
        expect(fs.existsSync(path.resolve(packagesDir, 'ecs-query'))).toBe(false);
        expect(fs.existsSync(path.resolve(packagesDir, 'ecs-world-support'))).toBe(false);
    });

    it('exposes the merged modules as ecs-runtime subpath exports', () => {
        const packageJson = JSON.parse(
            fs.readFileSync(path.resolve(packagesDir, 'ecs-runtime/package.json'), 'utf8')
        ) as { exports: Record<string, unknown>; dependencies: Record<string, string> };

        expect(Object.keys(packageJson.exports)).toEqual(
            expect.arrayContaining(['./storage', './query', './support'])
        );
        expect(Object.keys(packageJson.dependencies)).not.toEqual(
            expect.arrayContaining([
                '@axrone/ecs-storage',
                '@axrone/ecs-query',
                '@axrone/ecs-world-support',
            ])
        );
    });
});
