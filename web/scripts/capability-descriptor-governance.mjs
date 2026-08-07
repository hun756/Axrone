/**
 * Capability-descriptor export surface governance.
 *
 * Verifies that every package exposing a `./capabilities` subpath:
 *  1. Has a matching `src/capabilities.ts` source file.
 *  2. The source file contains a frozen descriptor (`Object.freeze`).
 *  3. The `./capabilities` export entry in package.json points to existing
 *     dist files (`dist/capabilities.d.ts`, `dist/capabilities.mjs`, `dist/capabilities.js`).
 *  4. The package also exposes `./capabilities` via a rollup input entry
 *     (checked through rollup.config.mjs containing `capabilities`).
 */

import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const workspaceDir = path.resolve(scriptDir, '..');
const packagesDir = path.resolve(workspaceDir, 'packages');

const failures = [];
const reportRows = [];

for (const entry of fs.readdirSync(packagesDir, { withFileTypes: true })) {
    if (!entry.isDirectory()) continue;

    const packageDir = entry.name;
    const packageRoot = path.resolve(packagesDir, packageDir);
    const packageJsonPath = path.resolve(packageRoot, 'package.json');
    if (!fs.existsSync(packageJsonPath)) continue;

    const packageJson = JSON.parse(fs.readFileSync(packageJsonPath, 'utf8'));
    const packageName = packageJson.name;
    const exportsField = packageJson.exports;

    if (!exportsField || typeof exportsField !== 'object') continue;
    if (!('./capabilities' in exportsField)) continue;

    // ── 1. Source file must exist ──────────────────────────────────────
    const sourceFile = path.resolve(packageRoot, 'src', 'capabilities.ts');
    if (!fs.existsSync(sourceFile)) {
        failures.push(
            `${packageName}: package.json declares "./capabilities" export but src/capabilities.ts is missing.`,
        );
        continue;
    }

    // ── 2. Source must contain Object.freeze (frozen descriptor) ───────
    const sourceContent = fs.readFileSync(sourceFile, 'utf8');
    if (!sourceContent.includes('Object.freeze')) {
        failures.push(
            `${packageName}: src/capabilities.ts does not use Object.freeze — capability descriptors must be immutable.`,
        );
    }

    // ── 3. Dist files must exist (only if dist/ is present) ───────────
    const distDir = path.resolve(packageRoot, 'dist');
    if (fs.existsSync(distDir)) {
        const capabilitiesExport = exportsField['./capabilities'];
        const targets = {};

        if (typeof capabilitiesExport === 'string') {
            targets['default'] = capabilitiesExport;
        } else if (capabilitiesExport && typeof capabilitiesExport === 'object') {
            for (const [condition, target] of Object.entries(capabilitiesExport)) {
                if (typeof target === 'string') {
                    targets[condition] = target;
                }
            }
        }

        for (const [condition, target] of Object.entries(targets)) {
            const absoluteTarget = path.resolve(packageRoot, target);
            if (!fs.existsSync(absoluteTarget)) {
                failures.push(
                    `${packageName}: "./capabilities" export condition "${condition}" points to missing file: ${target}`,
                );
            }
        }
    }

    // ── 4. Rollup config must include capabilities entry ──────────────
    const rollupConfigPath = path.resolve(packageRoot, 'rollup.config.mjs');
    if (fs.existsSync(rollupConfigPath)) {
        const rollupContent = fs.readFileSync(rollupConfigPath, 'utf8');
        if (!rollupContent.includes('capabilities')) {
            failures.push(
                `${packageName}: rollup.config.mjs does not include a capabilities entry — add a createPackageConfig block for src/capabilities.ts.`,
            );
        }
    }

    reportRows.push(packageName);
}

// ── Report ─────────────────────────────────────────────────────────────
console.log('Capability-descriptor export surface governance');
console.log(`Packages with "./capabilities" subpath: ${reportRows.length}`);
for (const row of reportRows) {
    console.log(`  ✓ ${row}`);
}

if (failures.length > 0) {
    console.error(`\nCapability-descriptor governance violations (${failures.length}):`);
    for (const failure of failures) {
        console.error(`  ✗ ${failure}`);
    }
    process.exit(1);
}

console.log('\nCapability-descriptor governance satisfied.');
