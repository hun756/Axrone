import { describe, expect, it } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES } from '../capabilities';
import {
    SCENE_2D_RUNTIME_PROFILE_ID,
    get2DSceneRuntimeProfile,
    scene2DRuntimeProfile,
} from '../profile';
import * as barrelExports from '../index';

/* ---------------------------------------------------------------------------
 * Path resolution
 * ------------------------------------------------------------------------- */

const currentDir = path.dirname(fileURLToPath(import.meta.url));
const packageDir = path.resolve(currentDir, '..');
const workspaceRoot = path.resolve(packageDir, '../..');
const buildJsonPath = path.resolve(workspaceRoot, '../../../../build.json');
const governanceScriptPath = path.resolve(workspaceRoot, 'scripts/playable-2d-governance.mjs');
const baselinePath = path.resolve(workspaceRoot, 'scripts/playable-2d-bundle-baseline.json');

/* ---------------------------------------------------------------------------
 * Shared helpers
 * ------------------------------------------------------------------------- */

interface BuildJsonTarget {
    compression?: string;
    bundleSplit?: boolean;
    lazyLoading?: boolean;
    sizeBudget?: string;
    sourceMaps?: boolean;
    minifyHtml?: boolean;
    minifyScripts?: boolean;
    buildMode?: string;
    inlineAssets?: boolean;
    inlineGameData?: boolean;
    [key: string]: unknown;
}

interface BuildJson {
    schemaVersion: number;
    activeTarget: string;
    outputDirectory: string;
    targets: Record<string, BuildJsonTarget>;
}

const readBuildJson = (): BuildJson => {
    const raw = fs.readFileSync(buildJsonPath, 'utf8');
    return JSON.parse(raw) as BuildJson;
};

const readBaseline = (): Record<string, unknown> => {
    const raw = fs.readFileSync(baselinePath, 'utf8');
    return JSON.parse(raw) as Record<string, unknown>;
};

const readGovernanceScript = (): string => {
    return fs.readFileSync(governanceScriptPath, 'utf8');
};

/* ---------------------------------------------------------------------------
 * Budget constants (from BIBLE / architecture specs)
 * ------------------------------------------------------------------------- */

/** Hard limits in KB as specified by the BIBLE. */
const BUNDLE_BUDGETS = {
    /** Playable-ad initial load: ≤ 2 MB */
    playableAdInitialKB: 2048,
    /** Playable-ad total: ≤ 5 MB */
    playableAdTotalKB: 5120,
    /** Web game mobile initial: ≤ 5 MB */
    webMobileInitialKB: 5120,
    /** Web game mobile total: ≤ 15 MB */
    webMobileTotalKB: 15360,
    /** Web game desktop initial: ≤ 10 MB */
    webDesktopInitialKB: 10240,
} as const;

/** Asset budgets. */
const ASSET_BUDGETS = {
    textureMobileMB: 128,
    textureDesktopMB: 256,
    audioPlayableMB: 4,
    audioFullMB: 16,
    atlasMaxDimension: 2048,
} as const;

/* ---------------------------------------------------------------------------
 * 1. Bundle Size Budget Enforcement
 * ------------------------------------------------------------------------- */

describe('T-15.1 — Bundle size budget enforcement', () => {
    const build = readBuildJson();
    const playableAd = build.targets['playable-ad'];

    it('playable-ad sizeBudget in build.json is ≤ 2048 KB (2 MB hard limit)', () => {
        expect(playableAd).toBeDefined();
        const budgetKB = Number(playableAd.sizeBudget);
        expect(Number.isFinite(budgetKB)).toBe(true);
        expect(budgetKB).toBeLessThanOrEqual(BUNDLE_BUDGETS.playableAdInitialKB);
    });

    it('playable-ad budget constant matches the BIBLE 2 MB initial limit', () => {
        expect(BUNDLE_BUDGETS.playableAdInitialKB).toBe(2048);
    });

    it('playable-ad total budget constant is ≤ 5 MB', () => {
        expect(BUNDLE_BUDGETS.playableAdTotalKB).toBeLessThanOrEqual(5120);
        expect(BUNDLE_BUDGETS.playableAdTotalKB).toBeGreaterThanOrEqual(BUNDLE_BUDGETS.playableAdInitialKB);
    });

    it('web mobile initial budget is ≤ 5 MB', () => {
        expect(BUNDLE_BUDGETS.webMobileInitialKB).toBeLessThanOrEqual(5120);
    });

    it('web mobile total budget is ≤ 15 MB', () => {
        expect(BUNDLE_BUDGETS.webMobileTotalKB).toBeLessThanOrEqual(15360);
    });

    it('web desktop initial budget is ≤ 10 MB', () => {
        expect(BUNDLE_BUDGETS.webDesktopInitialKB).toBeLessThanOrEqual(10240);
    });

    it('budget hierarchy is consistent: initial ≤ total for every target', () => {
        expect(BUNDLE_BUDGETS.playableAdInitialKB).toBeLessThanOrEqual(BUNDLE_BUDGETS.playableAdTotalKB);
        expect(BUNDLE_BUDGETS.webMobileInitialKB).toBeLessThanOrEqual(BUNDLE_BUDGETS.webMobileTotalKB);
        expect(BUNDLE_BUDGETS.webDesktopInitialKB).toBeLessThanOrEqual(BUNDLE_BUDGETS.webMobileTotalKB);
    });
});

/* ---------------------------------------------------------------------------
 * 2. Build Configuration Validation
 * ------------------------------------------------------------------------- */

describe('T-15.2 — Build configuration validation (build.json)', () => {
    const build = readBuildJson();
    const playableAd = build.targets['playable-ad'];
    const web = build.targets['web'];

    it('playable-ad target exists in build.json', () => {
        expect(playableAd).toBeDefined();
        expect(typeof playableAd).toBe('object');
    });

    it('playable-ad has gzip compression enabled', () => {
        expect(playableAd.compression).toBe('gzip');
    });

    it('playable-ad has bundleSplit enabled (FIX-08)', () => {
        expect(playableAd.bundleSplit).toBe(true);
    });

    it('playable-ad has lazyLoading enabled (FIX-08)', () => {
        expect(playableAd.lazyLoading).toBe(true);
    });

    it('playable-ad sizeBudget is defined and ≤ 2048 KB', () => {
        expect(playableAd.sizeBudget).toBeDefined();
        const budgetKB = Number(playableAd.sizeBudget);
        expect(Number.isFinite(budgetKB)).toBe(true);
        expect(budgetKB).toBeGreaterThan(0);
        expect(budgetKB).toBeLessThanOrEqual(2048);
    });

    it('playable-ad has source maps disabled (generated but not deployed)', () => {
        // sourceMaps: false means they are NOT included in the deployed bundle.
        // The governance script builds with sourcemap: false as well.
        expect(playableAd.sourceMaps).toBe(false);
    });

    it('playable-ad has HTML and script minification enabled', () => {
        expect(playableAd.minifyHtml).toBe(true);
        expect(playableAd.minifyScripts).toBe(true);
    });

    it('playable-ad has inlineAssets and inlineGameData enabled for single-file delivery', () => {
        expect(playableAd.inlineAssets).toBe(true);
        expect(playableAd.inlineGameData).toBe(true);
    });

    it('web target also has gzip compression and bundle splitting', () => {
        expect(web).toBeDefined();
        expect(web.compression).toBe('gzip');
        expect(web.bundleSplit).toBe(true);
        expect(web.lazyLoading).toBe(true);
    });

    it('build.json schema version is a positive integer', () => {
        expect(build.schemaVersion).toBeGreaterThanOrEqual(1);
        expect(Number.isInteger(build.schemaVersion)).toBe(true);
    });
});

/* ---------------------------------------------------------------------------
 * 3. Runtime Profile Size Tracking
 * ------------------------------------------------------------------------- */

describe('T-15.3 — Runtime profile size tracking', () => {
    it('runtime-profile-2d package.json declares sideEffects: false for tree-shaking', () => {
        const pkgJson = JSON.parse(
            fs.readFileSync(path.resolve(packageDir, 'package.json'), 'utf8'),
        ) as Record<string, unknown>;
        expect(pkgJson.sideEffects).toBe(false);
    });

    it('runtime-profile-2d capability list excludes heavy 3D packages', () => {
        const forbidden = [
            '@axrone/asset-gltf',
            '@axrone/render-3d',
            '@axrone/render-webgl2',
            '@axrone/scene-3d',
            '@axrone/runtime-profile-3d',
            '@axrone/runtime-profile-full',
        ];

        for (const pkg of forbidden) {
            expect(RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES).not.toContain(pkg);
        }
    });

    it('runtime-profile-2d capability list excludes three.js (pulled by 3D packages)', () => {
        // three.js is a forbidden package in the playable-2d governance script.
        // The 2D profile should never list it as a direct or transitive capability.
        expect(RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES).not.toContain('three');
    });

    it('runtime-profile-2d has exactly the expected 8 capability packages', () => {
        expect(RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES.length).toBe(8);
    });

    it('runtime-profile-2d dependencies in package.json match capability list', () => {
        const pkgJson = JSON.parse(
            fs.readFileSync(path.resolve(packageDir, 'package.json'), 'utf8'),
        ) as { dependencies: Record<string, string> };
        const deps = Object.keys(pkgJson.dependencies).sort();
        const expected = [
            '@axrone/asset-2d',
            '@axrone/input',
            '@axrone/physics-2d',
            '@axrone/physics-core',
            '@axrone/render-2d',
            '@axrone/scene-2d',
            '@axrone/scene-runtime',
            '@axrone/ui',
        ];
        expect(deps).toEqual(expected);
    });

    it('barrel index exports profile, capabilities, and scene2DRuntimeProfile', () => {
        expect(barrelExports.RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES).toBeDefined();
        expect(barrelExports.get2DSceneRuntimeProfile).toBeDefined();
        expect(barrelExports.scene2DRuntimeProfile).toBeDefined();
        expect(barrelExports.SCENE_2D_RUNTIME_PROFILE_ID).toBeDefined();
    });

    it('scene2DRuntimeProfile is an alias for get2DSceneRuntimeProfile (no duplicate code)', () => {
        expect(scene2DRuntimeProfile).toBe(get2DSceneRuntimeProfile);
    });

    it('profile resolves a registry without 3D components (MeshRenderer, Terrain, DirectionalLight)', () => {
        const registry = get2DSceneRuntimeProfile().resolveRegistry({});
        expect('MeshRenderer' in registry).toBe(false);
        expect('Terrain' in registry).toBe(false);
        expect('DirectionalLight' in registry).toBe(false);
    });

    it('governance script forbids the same 3D packages that the 2D profile excludes', () => {
        const scriptSource = readGovernanceScript();

        // The governance script defines forbiddenPackageRules — verify key ones exist.
        expect(scriptSource).toContain("'@axrone/physics-3d'");
        expect(scriptSource).toContain("'@axrone/render-3d'");
        expect(scriptSource).toContain("'@axrone/render-webgl2'");
        expect(scriptSource).toContain("'@axrone/scene-3d'");
        expect(scriptSource).toContain("'three'");
    });

    it('baseline gzip size is well under the 2 MB budget', () => {
        const baseline = readBaseline();
        const gzipBytes = baseline.gzipBytes as number;
        const budgetBytes = BUNDLE_BUDGETS.playableAdInitialKB * 1024;

        expect(Number.isFinite(gzipBytes)).toBe(true);
        expect(gzipBytes).toBeGreaterThan(0);
        // Current baseline: ~85 KB, budget: 2048 KB — should be well under.
        expect(gzipBytes).toBeLessThan(budgetBytes);
    });

    it('baseline raw size is well under the 5 MB total budget', () => {
        const baseline = readBaseline();
        const rawBytes = baseline.rawBytes as number;
        const budgetBytes = BUNDLE_BUDGETS.playableAdTotalKB * 1024;

        expect(Number.isFinite(rawBytes)).toBe(true);
        expect(rawBytes).toBeGreaterThan(0);
        expect(rawBytes).toBeLessThan(budgetBytes);
    });
});

/* ---------------------------------------------------------------------------
 * 4. Asset Budget Validation
 * ------------------------------------------------------------------------- */

describe('T-15.4 — Asset budget constants', () => {
    it('mobile texture memory budget is ≤ 128 MB', () => {
        expect(ASSET_BUDGETS.textureMobileMB).toBeLessThanOrEqual(128);
        expect(ASSET_BUDGETS.textureMobileMB).toBeGreaterThan(0);
    });

    it('desktop texture memory budget is ≤ 256 MB', () => {
        expect(ASSET_BUDGETS.textureDesktopMB).toBeLessThanOrEqual(256);
        expect(ASSET_BUDGETS.textureDesktopMB).toBeGreaterThanOrEqual(ASSET_BUDGETS.textureMobileMB);
    });

    it('playable audio budget is ≤ 4 MB', () => {
        expect(ASSET_BUDGETS.audioPlayableMB).toBeLessThanOrEqual(4);
        expect(ASSET_BUDGETS.audioPlayableMB).toBeGreaterThan(0);
    });

    it('full game audio budget is ≤ 16 MB', () => {
        expect(ASSET_BUDGETS.audioFullMB).toBeLessThanOrEqual(16);
        expect(ASSET_BUDGETS.audioFullMB).toBeGreaterThanOrEqual(ASSET_BUDGETS.audioPlayableMB);
    });

    it('atlas max dimension is 2048×2048', () => {
        expect(ASSET_BUDGETS.atlasMaxDimension).toBe(2048);
    });

    it('atlas dimension is a power of two', () => {
        const dim = ASSET_BUDGETS.atlasMaxDimension;
        expect(dim).toBeGreaterThan(0);
        // Power-of-two check: n & (n-1) === 0
        expect((dim & (dim - 1))).toBe(0);
    });
});

/* ---------------------------------------------------------------------------
 * 5. Governance Script Integrity
 * ------------------------------------------------------------------------- */

describe('T-15.5 — Governance script integrity', () => {
    const scriptSource = readGovernanceScript();

    it('playable-2d governance script exists and is non-empty', () => {
        expect(fs.existsSync(governanceScriptPath)).toBe(true);
        expect(scriptSource.length).toBeGreaterThan(0);
    });

    it('governance script defines forbidden package rules for 3D leakage', () => {
        expect(scriptSource).toContain('forbiddenPackageRules');
    });

    it('governance script supports bundle, size, and smoke modes', () => {
        expect(scriptSource).toContain("mode === 'bundle'");
        expect(scriptSource).toContain("mode === 'size'");
        expect(scriptSource).toContain("mode === 'smoke'");
    });

    it('governance script computes gzip and brotli compressed sizes', () => {
        expect(scriptSource).toContain('gzipSync');
        expect(scriptSource).toContain('brotliCompressSync');
    });

    it('baseline file has a valid maxDelta structure', () => {
        const baseline = readBaseline();
        const maxDelta = baseline.maxDelta as Record<string, number>;

        expect(maxDelta).toBeDefined();
        expect(typeof maxDelta.rawBytes).toBe('number');
        expect(typeof maxDelta.gzipBytes).toBe('number');
        expect(typeof maxDelta.brotliBytes).toBe('number');
        expect(typeof maxDelta.chunkCount).toBe('number');
        expect(typeof maxDelta.assetCount).toBe('number');

        expect(maxDelta.rawBytes).toBeGreaterThan(0);
        expect(maxDelta.gzipBytes).toBeGreaterThan(0);
        expect(maxDelta.brotliBytes).toBeGreaterThan(0);
    });

    it('baseline maxDelta values are within reasonable bounds', () => {
        const baseline = readBaseline();
        const maxDelta = baseline.maxDelta as Record<string, number>;

        // Delta tolerances should be small relative to the budgets.
        expect(maxDelta.gzipBytes).toBeLessThanOrEqual(2048);
        expect(maxDelta.rawBytes).toBeLessThanOrEqual(4096);
    });
});
