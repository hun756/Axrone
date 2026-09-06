import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const testDir = path.dirname(fileURLToPath(import.meta.url));
const packagesDir = path.resolve(testDir, '../../../packages');
const physics2dWorldFile = path.resolve(packagesDir, 'physics-2d/src/core/physics-world.ts');
const physics3dWorldFile = path.resolve(packagesDir, 'physics-3d/src/core/physics-world-3d.ts');

/**
 * Fields in getStatistics() that MUST be wired to real simulation data,
 * never hardcoded to literal 0.
 *
 * These fields were previously returned as hardcoded 0 (sahteydi):
 * - 2D: islandCount, treeHeight, treeBalance, treeQuality, collisionTime,
 *        solveTime, broadphaseTime, narrowphaseTime
 * - 3D: treeHeight, treeBalance, treeQuality
 *
 * Timing fields use `this._profiler?.xxxTime ?? 0` which is legitimate:
 * the ?? 0 fallback only fires when no profiler is attached (zero overhead).
 * The pattern we forbid is a bare literal `: 0` assignment for tree/island fields.
 */
const truthfulnessFields = [
    'treeHeight',
    'treeBalance',
    'treeQuality',
    'islandCount',
] as const;

const extractGetStatisticsBody = (source: string): string | null => {
    const methodStart = source.indexOf('getStatistics()');
    if (methodStart === -1) return null;

    const braceStart = source.indexOf('{', methodStart);
    if (braceStart === -1) return null;

    let depth = 0;
    let end = -1;
    for (let i = braceStart; i < source.length; i++) {
        if (source[i] === '{') depth++;
        if (source[i] === '}') {
            depth--;
            if (depth === 0) {
                end = i;
                break;
            }
        }
    }
    if (end === -1) return null;

    return source.slice(braceStart, end + 1);
};

/**
 * Detects hardcoded literal zero assignments for truthfulness fields.
 *
 * Catches patterns like:
 *   treeHeight: 0,
 *   treeHeight: 0 as number,
 *   treeHeight:0,
 *   "treeHeight": 0,
 *
 * Does NOT catch (intentionally):
 *   treeHeight: this._broadphase.getHeight(),     ← real wiring
 *   collisionTime: this._profiler?.collisionTime ?? 0,  ← legitimate fallback
 *   treeHeight: someVar,                           ← variable (could be 0 but not literal)
 */
const findHardcodedZeroFields = (methodBody: string): string[] => {
    const violations: string[] = [];

    for (const field of truthfulnessFields) {
        // Match: fieldName followed by optional whitespace, colon, optional whitespace,
        // then literal 0 not followed by another digit or dot (to avoid matching 0.5 etc.)
        const pattern = new RegExp(
            `\\b${field}\\s*:\\s*0(?!\\d|\\.|[0-9a-zA-Z_])`,
        );
        if (pattern.test(methodBody)) {
            violations.push(field);
        }
    }

    return violations;
};

describe('physics world statistics truthfulness', () => {
    it('physics-2d getStatistics() does not hardcode tree/island fields to literal 0', () => {
        expect(fs.existsSync(physics2dWorldFile)).toBe(true);
        const source = fs.readFileSync(physics2dWorldFile, 'utf8');
        const methodBody = extractGetStatisticsBody(source);
        expect(methodBody).not.toBeNull();

        const violations = findHardcodedZeroFields(methodBody!);
        expect(violations).toEqual([]);
    });

    it('physics-3d getStatistics() does not hardcode tree/island fields to literal 0', () => {
        expect(fs.existsSync(physics3dWorldFile)).toBe(true);
        const source = fs.readFileSync(physics3dWorldFile, 'utf8');
        const methodBody = extractGetStatisticsBody(source);
        expect(methodBody).not.toBeNull();

        const violations = findHardcodedZeroFields(methodBody!);
        expect(violations).toEqual([]);
    });

    it('physics-2d getStatistics() wires treeHeight to broadphase method call', () => {
        const source = fs.readFileSync(physics2dWorldFile, 'utf8');
        const methodBody = extractGetStatisticsBody(source);
        expect(methodBody).not.toBeNull();

        // treeHeight must reference a broadphase call (getHeight or similar)
        expect(methodBody).toMatch(/treeHeight\s*:\s*this\._broadphase\.getHeight\(\)/);
    });

    it('physics-3d getStatistics() wires treeHeight to broadphase method call', () => {
        const source = fs.readFileSync(physics3dWorldFile, 'utf8');
        const methodBody = extractGetStatisticsBody(source);
        expect(methodBody).not.toBeNull();

        // treeHeight must reference a tree method call (getHeight)
        expect(methodBody).toMatch(/treeHeight\s*:\s*tree\.getHeight\(\)/);
    });
});
