/**
 * Fast single-process Rollup builder for all `@axrone/*` workspace packages.
 *
 * Replaces `lerna run build` process-spawn overhead (45x yarn+node processes)
 * and avoids Windows Lerna/Nx `--stream` pipe deadlocks on leaf packages.
 *
 * Prerequisites: run `yarn build:dts` first so `.dts-build/` is populated
 * (falls back to `.ts` sources automatically when absent).
 *
 * Usage: node ./scripts/build-all.mjs [--concurrency=8]
 */
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL, fileURLToPath } from 'node:url';
import { rollup } from 'rollup';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const workspaceDir = path.resolve(scriptDir, '..');
const packagesDir = path.join(workspaceDir, 'packages');

const concurrencyArg = process.argv.find((arg) => arg.startsWith('--concurrency='));
const concurrency = Math.max(1, Number(concurrencyArg?.split('=')[1] ?? 8) || 8);

const loadPackageList = () => {
    const dirs = fs
        .readdirSync(packagesDir, { withFileTypes: true })
        .filter((entry) => entry.isDirectory())
        .map((entry) => entry.name)
        .sort();

    const packages = [];
    for (const dirName of dirs) {
        const pkgDir = path.join(packagesDir, dirName);
        const pkgJsonPath = path.join(pkgDir, 'package.json');
        const rollupConfigPath = path.join(pkgDir, 'rollup.config.mjs');
        if (!fs.existsSync(pkgJsonPath) || !fs.existsSync(rollupConfigPath)) {
            continue;
        }
        const pkgJson = JSON.parse(fs.readFileSync(pkgJsonPath, 'utf8'));
        const deps = new Set([
            ...Object.keys(pkgJson.dependencies ?? {}),
            ...Object.keys(pkgJson.peerDependencies ?? {}),
        ]);
        packages.push({
            dirName,
            name: pkgJson.name ?? `@axrone/${dirName}`,
            pkgDir,
            rollupConfigPath,
            deps,
        });
    }
    return packages;
};

const topoSort = (packages) => {
    const byName = new Map(packages.map((p) => [p.name, p]));
    const inDegree = new Map(packages.map((p) => [p.name, 0]));
    const dependents = new Map(packages.map((p) => [p.name, []]));

    for (const pkg of packages) {
        for (const dep of pkg.deps) {
            if (byName.has(dep)) {
                inDegree.set(pkg.name, inDegree.get(pkg.name) + 1);
                dependents.get(dep).push(pkg.name);
            }
        }
    }

    const ready = packages.filter((p) => inDegree.get(p.name) === 0).map((p) => p.name);
    return { byName, inDegree, dependents, ready };
};

const buildOnePackage = async (pkg) => {
    const start = performance.now();
    const configModule = await import(pathToFileURL(pkg.rollupConfigPath).href);
    const configs = Array.isArray(configModule.default) ? configModule.default : [configModule.default];

    for (const config of configs) {
        const bundle = await rollup({
            input: config.input,
            external: config.external,
            plugins: config.plugins,
            onwarn(warning, warn) {
                if (warning.code === 'EMPTY_BUNDLE') return;
                warn(warning);
            },
        });
        try {
            const outputs = Array.isArray(config.output) ? config.output : [config.output];
            for (const out of outputs) {
                await bundle.write(out);
            }
        } finally {
            await bundle.close();
        }
    }

    const elapsedMs = Math.round(performance.now() - start);
    console.log(`[build] ${pkg.name} (${elapsedMs}ms)`);
};

const main = async () => {
    const totalStart = performance.now();
    const packages = loadPackageList();
    const { byName, inDegree, dependents, ready } = topoSort(packages);

    let completed = 0;
    let failed = null;
    const inFlight = new Set();

    const scheduleNext = () => {
        while (!failed && inFlight.size < concurrency && ready.length > 0) {
            const name = ready.shift();
            const pkg = byName.get(name);
            const task = buildOnePackage(pkg)
                .then(() => {
                    completed++;
                    for (const depName of dependents.get(name)) {
                        const nextDeg = inDegree.get(depName) - 1;
                        inDegree.set(depName, nextDeg);
                        if (nextDeg === 0) {
                            ready.push(depName);
                        }
                    }
                })
                .catch((err) => {
                    failed = { name, err };
                })
                .finally(() => {
                    inFlight.delete(task);
                });
            inFlight.add(task);
        }
    };

    scheduleNext();
    while (inFlight.size > 0) {
        await Promise.race(inFlight);
        scheduleNext();
    }

    if (failed) {
        console.error(`[build] FAILED in ${failed.name}:`, failed.err);
        process.exitCode = 1;
        return;
    }

    const totalSec = ((performance.now() - totalStart) / 1000).toFixed(2);
    console.log(`[build] Built ${completed}/${packages.length} packages in ${totalSec}s.`);
};

main();
