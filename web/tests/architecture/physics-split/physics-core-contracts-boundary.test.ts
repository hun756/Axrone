import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const testDir = path.dirname(fileURLToPath(import.meta.url));
const packagesDir = path.resolve(testDir, '../../../packages');
const physicsCoreSrcDir = path.resolve(packagesDir, 'physics-core/src');

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

/**
 * Class names that belong in simulation runtime packages (physics-2d, physics-3d, raycast),
 * NOT in the contracts-only physics-core package.
 *
 * physics-core may contain: type/interface/const-enum declarations, frozen constant objects
 * (e.g. PhysicsConstants), pure utility functions (e.g. makeCollisionPairKey), and branded
 * type aliases. It must NOT contain simulation world, body manager, solver, broadphase,
 * contact manager, or any other runtime simulation class.
 */
const forbiddenRuntimeClassNames = [
    'PhysicsWorld2D',
    'PhysicsWorld3D',
    'BodyManager2D',
    'BodyManager3D',
    'ConstraintSolver2D',
    'ConstraintSolver3D',
    'ContactManager2D',
    'ContactManager3D',
    'IslandSolver',
    'Broadphase',
    'DynamicAABBTree',
    'ShapeManager',
    'JointSolver',
];

const disallowedRuntimePackageImportPattern =
    /(?:from ['"]|import\(['"])(?:@axrone\/physics-2d|@axrone\/physics-3d|@axrone\/raycast)['"]/g;

describe('physics-core contracts-only boundary', () => {
    it('does not declare simulation runtime classes', () => {
        const sourceFiles = collectTypeScriptFiles(physicsCoreSrcDir);
        const violations: string[] = [];

        for (const filePath of sourceFiles) {
            const content = fs.readFileSync(filePath, 'utf8');

            for (const className of forbiddenRuntimeClassNames) {
                const classPattern = new RegExp(`\\bclass\\s+${className}\\b`);
                if (classPattern.test(content)) {
                    violations.push(
                        `${path.relative(packagesDir, filePath).replace(/\\/g, '/')}: declares forbidden runtime class ${className}`
                    );
                }
            }
        }

        expect(violations).toEqual([]);
    });

    it('does not import from simulation runtime packages', () => {
        const sourceFiles = collectTypeScriptFiles(physicsCoreSrcDir);
        const violations: string[] = [];

        for (const filePath of sourceFiles) {
            const content = fs.readFileSync(filePath, 'utf8');
            const hasRuntimeImport = disallowedRuntimePackageImportPattern.test(content);
            disallowedRuntimePackageImportPattern.lastIndex = 0;

            if (hasRuntimeImport) {
                violations.push(path.relative(packagesDir, filePath).replace(/\\/g, '/'));
            }
        }

        expect(violations).toEqual([]);
    });

    it('keeps source tree flat without simulation subdirectories', () => {
        const forbiddenSubdirs = [
            'core', 'world', 'solver', 'body-manager', 'contact',
            'broadphase', 'island', 'simulation', 'components',
        ];

        const topLevelEntries = fs.readdirSync(physicsCoreSrcDir, { withFileTypes: true });
        const actualSubdirs = topLevelEntries
            .filter((entry) => entry.isDirectory() && entry.name !== '__tests__')
            .map((entry) => entry.name);

        const violations = actualSubdirs.filter((dir) =>
            forbiddenSubdirs.some((forbidden) => dir === forbidden || dir.startsWith(forbidden))
        );

        expect(violations).toEqual([]);
    });
});
