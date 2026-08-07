import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { listModuleSpecifiers } from '../_helpers/import-specifiers';

const testDir = path.dirname(fileURLToPath(import.meta.url));
const workspaceDir = path.resolve(testDir, '../../..');
const entryPath = path.resolve(workspaceDir, 'examples', 'playable-2d', 'main.ts');

describe('playable 2d entry boundary', () => {
    it('keeps the reference entry pinned to the 2d runtime profile surface', () => {
        expect(listModuleSpecifiers(entryPath)).toEqual(['@axrone/runtime-profile-2d/profile']);
    });
});