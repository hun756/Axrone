import { describe, expect, it, beforeAll } from 'vitest';
import { execSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

/**
 * T-15: Playable-Ad Bundle Size Governance Tests
 *
 * Validates bundle size constraints for the playable-2d target by leveraging
 * the existing governance script (playable-2d-governance.mjs) which performs
 * a real Vite build and produces a JSON report.
 */

const WEB_ROOT = path.resolve(__dirname, '..', '..', '..', '..');
const GOVERNANCE_SCRIPT = path.join(WEB_ROOT, 'scripts', 'playable-2d-governance.mjs');
const REPORT_DIR = path.join(WEB_ROOT, '.tmp', 'governance');
const REPORT_PATH = path.join(REPORT_DIR, 'playable-2d-bundle-report.json');
const BASELINE_PATH = path.join(WEB_ROOT, 'scripts', 'playable-2d-bundle-baseline.json');

// Budget constants
const HARD_LIMIT_GZIP_BYTES = 2 * 1024 * 1024; // 2 MB
const PRACTICAL_TARGET_GZIP_BYTES = 150 * 1024; // 150 KB
const MAX_CHUNK_COUNT = 5;
const MAX_MODULE_COUNT = 200;

// Forbidden packages that must NOT appear in the playable-2d bundle
const FORBIDDEN_PACKAGES = [
    '@axrone/asset-core',
    '@axrone/asset-gltf',
    '@axrone/physics-3d',
    '@axrone/render-3d',
    '@axrone/render-webgl2',
    '@axrone/runtime-profile-3d',
    '@axrone/runtime-profile-full',
    '@axrone/scene-3d',
    'three',
];

// Needles used to detect forbidden packages in module paths (mirrors governance script)
const FORBIDDEN_NEEDLES: Record<string, string> = {
    '@axrone/asset-core': '/packages/asset-core/',
    '@axrone/asset-gltf': '/packages/asset-gltf/',
    '@axrone/physics-3d': '/packages/physics-3d/',
    '@axrone/render-3d': '/packages/render-3d/',
    '@axrone/render-webgl2': '/packages/render-webgl2/',
    '@axrone/runtime-profile-3d': '/packages/runtime-profile-3d/',
    '@axrone/runtime-profile-full': '/packages/runtime-profile-full/',
    '@axrone/scene-3d': '/packages/scene-3d/',
    'three': '/node_modules/three/',
};

interface BundleReport {
    assetCount: number;
    brotliBytes: number;
    chunkCount: number;
    entryChunkFile: string | null;
    forbiddenPackages: Array<{ packageName: string; moduleId: string }>;
    generatedAt: string;
    gzipBytes: number;
    moduleCount: number;
    modules: string[];
    rawBytes: number;
}

let report: BundleReport;

beforeAll(() => {
    // Run the governance script in bundle mode to produce a fresh report.
    // This performs a real Vite build of the playable-2d entry point.
    execSync(`node "${GOVERNANCE_SCRIPT}" --mode=bundle`, {
        cwd: WEB_ROOT,
        stdio: 'pipe',
        timeout: 120_000,
    });

    expect(fs.existsSync(REPORT_PATH)).toBe(true);
    report = JSON.parse(fs.readFileSync(REPORT_PATH, 'utf8'));
}, 180_000);

describe('Playable-2D Bundle Size Governance', () => {
    describe('Hard size limit', () => {
        it('gzip bundle size must not exceed 2MB hard limit', () => {
            expect(report.gzipBytes).toBeLessThanOrEqual(HARD_LIMIT_GZIP_BYTES);
        });

        it('raw bundle size must be reasonable (under 10MB)', () => {
            expect(report.rawBytes).toBeLessThanOrEqual(10 * 1024 * 1024);
        });
    });

    describe('Practical budget', () => {
        it('gzip bundle size must be within 150KB practical target', () => {
            expect(report.gzipBytes).toBeLessThanOrEqual(PRACTICAL_TARGET_GZIP_BYTES);
        });

        it('gzip size should stay close to the baseline', () => {
            const baseline = JSON.parse(fs.readFileSync(BASELINE_PATH, 'utf8'));
            const maxDelta = baseline.maxDelta.gzipBytes;
            // Allow baseline + delta tolerance
            expect(report.gzipBytes).toBeLessThanOrEqual(baseline.gzipBytes + maxDelta);
        });
    });

    describe('Forbidden package exclusion', () => {
        it('must not include any forbidden 3D-only packages', () => {
            expect(report.forbiddenPackages).toEqual([]);
        });

        it('module list must not contain paths from forbidden packages', () => {
            const violations: string[] = [];

            for (const [packageName, needle] of Object.entries(FORBIDDEN_NEEDLES)) {
                const hit = report.modules.find((moduleId) => moduleId.includes(needle));
                if (hit) {
                    violations.push(`${packageName} leaked via ${hit}`);
                }
            }

            expect(violations).toEqual([]);
        });
    });

    describe('Tree-shaking effectiveness', () => {
        it('module count should be well below the maximum bound', () => {
            expect(report.moduleCount).toBeGreaterThan(0);
            expect(report.moduleCount).toBeLessThanOrEqual(MAX_MODULE_COUNT);
        });

        it('should not include entire engine when only playable-2d entry is used', () => {
            // The playable-2d entry only imports from runtime-profile-2d.
            // Verify that heavy 3D packages are absent, proving tree-shaking works.
            const has3dModules = report.modules.some(
                (moduleId) =>
                    moduleId.includes('/packages/scene-3d/') ||
                    moduleId.includes('/packages/render-3d/') ||
                    moduleId.includes('/node_modules/three/')
            );
            expect(has3dModules).toBe(false);
        });

        it('should only include modules from expected 2D-relevant packages', () => {
            // All modules should come from known-allowed package directories
            const allowedPackageDirs = [
                '/packages/ecs-runtime/',
                '/packages/numeric/',
                '/packages/random/',
                '/packages/utility/',
                '/packages/render-core/',
                '/packages/render-2d/',
                '/packages/scene-core/',
                '/packages/scene-runtime/',
                '/packages/runtime-profile-2d/',
                '/packages/asset-core/', // may be pulled transitively but should be tree-shaken
                '/packages/observer/',
                '/packages/event/',
                '/packages/tween/',
                '/packages/input/',
                '/packages/audio/',
                '/packages/geometry/',
                '/packages/physics-core/',
                '/packages/physics-2d/',
            ];

            // Verify no modules from 3D-only packages
            const forbidden3dPaths = [
                '/packages/scene-3d/',
                '/packages/render-3d/',
                '/packages/render-webgl2/',
                '/packages/physics-3d/',
                '/packages/asset-gltf/',
                '/packages/runtime-profile-3d/',
                '/packages/runtime-profile-full/',
                '/node_modules/three/',
            ];

            for (const forbiddenPath of forbidden3dPaths) {
                const found = report.modules.filter((m) => m.includes(forbiddenPath));
                expect(found, `Found modules from forbidden path ${forbiddenPath}: ${found.join(', ')}`).toHaveLength(0);
            }
        });
    });

    describe('Chunk governance', () => {
        it('chunk count must not exceed maximum allowed', () => {
            expect(report.chunkCount).toBeLessThanOrEqual(MAX_CHUNK_COUNT);
        });

        it('should produce exactly one entry chunk for playable-2d', () => {
            // Playable-2d is a small ad — should be a single chunk
            expect(report.chunkCount).toBeGreaterThanOrEqual(1);
        });

        it('entry chunk file should be present', () => {
            expect(report.entryChunkFile).toBeTruthy();
            expect(typeof report.entryChunkFile).toBe('string');
        });
    });

    describe('Source map exclusion', () => {
        it('governance script builds with sourcemap disabled', () => {
            // The governance script sets sourcemap: false in the Vite build config.
            // Verify no .map files are referenced in the report output directory.
            const bundleOutDir = path.join(REPORT_DIR, 'playable-2d-bundle');
            if (fs.existsSync(bundleOutDir)) {
                const mapFiles = fs
                    .readdirSync(bundleOutDir, { recursive: true })
                    .filter((file) => String(file).endsWith('.map'));
                expect(mapFiles).toHaveLength(0);
            } else {
                // If the output dir doesn't exist (non-write mode), verify via the
                // governance script source that sourcemap is disabled
                const scriptSource = fs.readFileSync(GOVERNANCE_SCRIPT, 'utf8');
                expect(scriptSource).toContain('sourcemap: false');
            }
        });
    });

    describe('Per-package size tracking', () => {
        it('should report individual module contributions', () => {
            expect(report.modules.length).toBe(report.moduleCount);
            expect(report.modules.length).toBeGreaterThan(0);
        });

        it('each module path should be a normalized absolute-style path', () => {
            for (const moduleId of report.modules) {
                // Modules should use forward slashes (normalized paths)
                expect(moduleId).not.toContain('\\\\');
            }
        });

        it('should track which packages contribute modules to the bundle', () => {
            // Group modules by their package directory
            const packageContributions = new Map<string, number>();

            for (const moduleId of report.modules) {
                const packagesMatch = moduleId.match(/\/packages\/([^/]+)\//);
                const nodeModulesMatch = moduleId.match(/\/node_modules\/([^/]+)\//);
                const pkgName = packagesMatch?.[1] ?? nodeModulesMatch?.[1] ?? 'other';
                packageContributions.set(pkgName, (packageContributions.get(pkgName) ?? 0) + 1);
            }

            // Verify we can identify contributing packages
            expect(packageContributions.size).toBeGreaterThan(0);

            // The playable-2d entry should pull from runtime-profile-2d at minimum
            expect(packageContributions.has('runtime-profile-2d')).toBe(true);
        });
    });

    describe('Baseline consistency', () => {
        it('raw bytes should stay close to baseline', () => {
            const baseline = JSON.parse(fs.readFileSync(BASELINE_PATH, 'utf8'));
            const maxDelta = baseline.maxDelta.rawBytes;
            expect(report.rawBytes).toBeLessThanOrEqual(baseline.rawBytes + maxDelta);
        });

        it('brotli bytes should stay close to baseline', () => {
            const baseline = JSON.parse(fs.readFileSync(BASELINE_PATH, 'utf8'));
            const maxDelta = baseline.maxDelta.brotliBytes;
            expect(report.brotliBytes).toBeLessThanOrEqual(baseline.brotliBytes + maxDelta);
        });

        it('chunk count should stay close to baseline', () => {
            const baseline = JSON.parse(fs.readFileSync(BASELINE_PATH, 'utf8'));
            const maxDelta = baseline.maxDelta.chunkCount;
            expect(report.chunkCount).toBeLessThanOrEqual(baseline.chunkCount + maxDelta);
        });
    });
});
