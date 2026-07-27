import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import type { IHeightFieldShapeDef3D } from '@axrone/physics-core';
import {
    TerrainHeightmap,
    createTerrainHeightfieldSource,
} from '@axrone/terrain';

const testDir = path.dirname(fileURLToPath(import.meta.url));
const packagesDir = path.resolve(testDir, '../../../packages');
const terrainSrcDir = path.resolve(packagesDir, 'terrain/src');

const allowedPackageDependencies = new Set(['@axrone/numeric', '@axrone/random']);
const packageImportPattern =
    /(?:from ['"]|import\(['"]|(?:^|\s)import\s+['"]|require\(['"])(@axrone\/[a-z0-9-]+)(?:\/[^'"]*)?['"]/g;
const siblingSourceBypassPattern = /(?:from ['"]|import\(['"])(?:\.\.\/)+[a-z0-9-]+\/src(?:\/[^'"]*)?['"]/g;

const collectTypeScriptFiles = (dirPath: string): readonly string[] => {
    const files: string[] = [];

    for (const entry of fs.readdirSync(dirPath, { withFileTypes: true })) {
        const fullPath = path.resolve(dirPath, entry.name);
        if (entry.isDirectory()) {
            files.push(...collectTypeScriptFiles(fullPath));
            continue;
        }

        if (entry.isFile() && entry.name.endsWith('.ts')) {
            files.push(fullPath);
        }
    }

    return files;
};

describe('terrain entry', () => {
    it('surfaces the terrain authoring primitives from the package root', async () => {
        const terrain = await import('@axrone/terrain');

        expect(terrain.TerrainHeightmap).toBeDefined();
        expect(terrain.TerrainError).toBeDefined();
        expect(terrain.TerrainErrorCode).toBeDefined();
        expect(terrain.TERRAIN_RESOLUTIONS).toBeDefined();
        expect(terrain.DEFAULT_TERRAIN_NOISE_OPTIONS).toBeDefined();
        expect(terrain.isTerrainResolution).toBeDefined();
        expect(terrain.validateTerrainDescriptor).toBeDefined();
        expect(terrain.resolveTerrainNoiseOptions).toBeDefined();
        expect(terrain.generateNoiseHeightmap).toBeDefined();
        expect(terrain.decodeHeightmapFromImageData).toBeDefined();
        expect(terrain.buildTerrainMesh).toBeDefined();
        expect(terrain.createTerrainHeightfieldSource).toBeDefined();
    });

    it('keeps terrain sources on the approved dependency surface', () => {
        const violations: string[] = [];

        for (const filePath of collectTypeScriptFiles(terrainSrcDir)) {
            const content = fs.readFileSync(filePath, 'utf8');
            const relativePath = path.relative(packagesDir, filePath).replace(/\\/g, '/');

            for (const match of content.matchAll(packageImportPattern)) {
                const packageName = match[1]!;
                if (!allowedPackageDependencies.has(packageName)) {
                    violations.push(`${relativePath} -> ${packageName}`);
                }
            }

            if (siblingSourceBypassPattern.test(content)) {
                violations.push(`${relativePath} -> sibling package src bypass`);
            }
            siblingSourceBypassPattern.lastIndex = 0;
        }

        expect(violations.sort((left, right) => left.localeCompare(right))).toEqual([]);
    });

    it('declares only the approved runtime dependencies in package.json', () => {
        const packageJson = JSON.parse(
            fs.readFileSync(path.resolve(packagesDir, 'terrain/package.json'), 'utf8')
        ) as {
            dependencies?: Record<string, string>;
            peerDependencies?: Record<string, string>;
            optionalDependencies?: Record<string, string>;
        };

        expect(Object.keys(packageJson.dependencies ?? {}).sort()).toEqual(
            [...allowedPackageDependencies].sort()
        );
        expect(packageJson.peerDependencies).toBeUndefined();
        expect(packageJson.optionalDependencies).toBeUndefined();
    });

    it('keeps the heightfield source structurally compatible with the physics-3d contract', () => {
        const heightmap = TerrainHeightmap.createFlat(33, 0.5);
        const source = createTerrainHeightfieldSource(heightmap, {
            width: 32,
            length: 32,
            maxHeight: 10,
            resolution: 33,
        });

        // Compile-time drift guard: TerrainHeightfieldSource must stay
        // assignable to IHeightFieldShapeDef3D without importing physics-core
        // from the terrain package itself.
        const compatible: IHeightFieldShapeDef3D = source;

        expect(compatible.heights).toBeInstanceOf(Float32Array);
        expect(compatible.width).toBe(33);
        expect(compatible.depth).toBe(33);
        expect(compatible.scaleY).toBe(10);
    });
});
