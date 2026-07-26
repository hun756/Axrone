import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { brotliCompressSync, gzipSync } from 'node:zlib';
import { parseArgs } from 'node:util';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { build } from 'vite';
import { createWorkspacePackageAliasEntries } from '../build/workspace-package-aliases.mjs';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const workspaceDir = path.resolve(scriptDir, '..');
const entryPath = path.resolve(workspaceDir, 'examples', 'playable-2d', 'main.ts');
const defaultReportPath = path.resolve(
    workspaceDir,
    '.tmp',
    'governance',
    'playable-2d-bundle-report.json',
);
const defaultSmokeOutDir = path.resolve(workspaceDir, '.tmp', 'governance', 'playable-2d-smoke');
const baselinePath = path.resolve(scriptDir, 'playable-2d-bundle-baseline.json');
const globalMarkerKey = '__AXRONE_PLAYABLE_2D_REFERENCE__';

const forbiddenPackageRules = [
    { packageName: '@axrone/asset-core', needle: '/packages/asset-core/' },
    { packageName: '@axrone/asset-gltf', needle: '/packages/asset-gltf/' },
    { packageName: '@axrone/physics-3d', needle: '/packages/physics-3d/' },
    { packageName: '@axrone/render-3d', needle: '/packages/render-3d/' },
    { packageName: '@axrone/render-webgl2', needle: '/packages/render-webgl2/' },
    { packageName: '@axrone/runtime-profile-3d', needle: '/packages/runtime-profile-3d/' },
    { packageName: '@axrone/runtime-profile-full', needle: '/packages/runtime-profile-full/' },
    { packageName: '@axrone/scene-3d', needle: '/packages/scene-3d/' },
    { packageName: 'three', needle: '/node_modules/three/' },
];

const normalizePath = (value) => value.replace(/\\/g, '/');

const fail = (message) => {
    throw new Error(message);
};

const toBuffer = (value) => {
    if (typeof value === 'string') {
        return Buffer.from(value);
    }

    if (value instanceof Uint8Array) {
        return Buffer.from(value);
    }

    return Buffer.from(JSON.stringify(value));
};

const ensureParentDir = (filePath) => {
    fs.mkdirSync(path.dirname(filePath), { recursive: true });
};

const collectBundleOutput = async ({ write, outDir }) => {
    const buildResult = await build({
        configFile: false,
        logLevel: 'error',
        publicDir: false,
        resolve: {
            alias: createWorkspacePackageAliasEntries(workspaceDir),
        },
        root: path.resolve(workspaceDir, 'examples'),
        build: {
            emptyOutDir: write,
            minify: 'esbuild',
            outDir,
            rollupOptions: {
                input: {
                    'playable-2d': entryPath,
                },
            },
            sourcemap: false,
            target: 'esnext',
            write,
        },
    });

    const outputs = [];
    for (const result of Array.isArray(buildResult) ? buildResult : [buildResult]) {
        if (result && typeof result === 'object' && 'output' in result && Array.isArray(result.output)) {
            outputs.push(...result.output);
        }
    }

    return outputs;
};

const createBundleReport = (outputs) => {
    const chunks = outputs.filter((output) => output.type === 'chunk');
    const assets = outputs.filter((output) => output.type === 'asset');
    const countedAssets = assets.filter(
        (asset) => !asset.fileName.endsWith('.html') && !asset.fileName.endsWith('.map'),
    );
    const moduleIds = new Set();
    const payloadBuffers = [];

    for (const chunk of chunks) {
        payloadBuffers.push(Buffer.from(chunk.code));

        for (const moduleId of Object.keys(chunk.modules)) {
            moduleIds.add(normalizePath(moduleId));
        }
    }

    for (const asset of countedAssets) {
        payloadBuffers.push(toBuffer(asset.source));
    }

    const payload = Buffer.concat(payloadBuffers);
    const entryChunk =
        chunks.find((chunk) => normalizePath(chunk.facadeModuleId ?? '') === normalizePath(entryPath)) ??
        chunks.find((chunk) => chunk.isEntry) ??
        null;

    const forbiddenPackages = [];
    for (const rule of forbiddenPackageRules) {
        const hit = [...moduleIds].find((moduleId) => moduleId.includes(rule.needle));
        if (hit) {
            forbiddenPackages.push({ packageName: rule.packageName, moduleId: hit });
        }
    }

    return {
        assetCount: countedAssets.length,
        brotliBytes: brotliCompressSync(payload).byteLength,
        chunkCount: chunks.length,
        entryChunkFile: entryChunk?.fileName ?? null,
        forbiddenPackages,
        generatedAt: new Date().toISOString(),
        gzipBytes: gzipSync(payload).byteLength,
        moduleCount: moduleIds.size,
        modules: [...moduleIds].sort((left, right) => left.localeCompare(right)),
        rawBytes: payload.byteLength,
    };
};

const writeReport = (reportPath, report) => {
    ensureParentDir(reportPath);
    fs.writeFileSync(reportPath, JSON.stringify(report, null, 4) + '\n');
};

const readBaseline = () => {
    if (!fs.existsSync(baselinePath)) {
        fail(
            `Playable 2D baseline not found at ${baselinePath}. Run yarn test:playable-2d-size-governance -- --refresh-baseline once to seed it.`,
        );
    }

    return JSON.parse(fs.readFileSync(baselinePath, 'utf8'));
};

const writeBaseline = (report) => {
    const baseline = {
        assetCount: report.assetCount,
        brotliBytes: report.brotliBytes,
        chunkCount: report.chunkCount,
        entryChunkFile: report.entryChunkFile,
        gzipBytes: report.gzipBytes,
        maxDelta: {
            assetCount: 1,
            brotliBytes: 512,
            chunkCount: 1,
            gzipBytes: 512,
            rawBytes: 1024,
        },
        moduleCount: report.moduleCount,
        rawBytes: report.rawBytes,
    };

    fs.writeFileSync(baselinePath, JSON.stringify(baseline, null, 4) + '\n');
};

const runBundleGovernance = (report) => {
    if (report.forbiddenPackages.length > 0) {
        const failures = report.forbiddenPackages.map(
            (violation) => `${violation.packageName} leaked into ${violation.moduleId}.`,
        );
        fail(`Playable 2D bundle governance violations:\n- ${failures.join('\n- ')}`);
    }

    console.log('Playable 2D bundle governance report');
    console.log(`Entry chunk: ${report.entryChunkFile ?? 'missing'}`);
    console.log(`Raw bytes : ${report.rawBytes}`);
    console.log(`Gzip bytes: ${report.gzipBytes}`);
    console.log(`Brotli    : ${report.brotliBytes}`);
    console.log(`Chunks    : ${report.chunkCount}`);
    console.log(`Assets    : ${report.assetCount}`);
    console.log(`Modules   : ${report.moduleCount}`);
    console.log('Forbidden package scan passed.');
};

const runSizeGovernance = (report, refreshBaseline) => {
    if (refreshBaseline) {
        writeBaseline(report);
        console.log(`Playable 2D size baseline refreshed at ${baselinePath}.`);
        return;
    }

    const baseline = readBaseline();
    const failures = [];
    const assertBudget = (label, actual, expected, maxDelta) => {
        if (actual > expected + maxDelta) {
            failures.push(
                `${label} ${actual} exceeds baseline ${expected} plus delta ${maxDelta}.`,
            );
        }
    };

    assertBudget('rawBytes', report.rawBytes, baseline.rawBytes, baseline.maxDelta.rawBytes);
    assertBudget('gzipBytes', report.gzipBytes, baseline.gzipBytes, baseline.maxDelta.gzipBytes);
    assertBudget(
        'brotliBytes',
        report.brotliBytes,
        baseline.brotliBytes,
        baseline.maxDelta.brotliBytes,
    );
    assertBudget('chunkCount', report.chunkCount, baseline.chunkCount, baseline.maxDelta.chunkCount);
    assertBudget('assetCount', report.assetCount, baseline.assetCount, baseline.maxDelta.assetCount);

    if (failures.length > 0) {
        fail(`Playable 2D size governance violations:\n- ${failures.join('\n- ')}`);
    }

    console.log('Playable 2D size governance passed.');
    console.log(`Raw bytes : ${report.rawBytes}`);
    console.log(`Gzip bytes: ${report.gzipBytes}`);
    console.log(`Brotli    : ${report.brotliBytes}`);
};

const runSmoke = async (report) => {
    if (!report.entryChunkFile) {
        fail('Playable 2D smoke build did not emit an entry chunk.');
    }

    Reflect.deleteProperty(globalThis, globalMarkerKey);

    const builtEntryPath = path.resolve(defaultSmokeOutDir, report.entryChunkFile);
    await import(pathToFileURL(builtEntryPath).href + `?smoke=${Date.now()}`);

    const marker = Reflect.get(globalThis, globalMarkerKey);
    if (!marker || typeof marker !== 'object') {
        fail('Playable 2D smoke build did not publish the runtime profile marker.');
    }

    if (marker.profileId !== 'scene/2d-default') {
        fail(
            `Playable 2D smoke build published unexpected profile id ${String(marker.profileId)}.`,
        );
    }

    console.log('Playable 2D smoke passed.');
    console.log(`Profile id: ${marker.profileId}`);
};

const { values: cli } = parseArgs({
    options: {
        mode: { type: 'string' },
        'refresh-baseline': { type: 'boolean' },
        refreshBaseline: { type: 'boolean' },
        report: { type: 'string' },
    },
    allowPositionals: false,
    strict: true,
});

const mode = cli.mode ?? 'bundle';
const reportPath = path.resolve(workspaceDir, cli.report ?? defaultReportPath);
const write = mode === 'smoke';
const outDir = write ? defaultSmokeOutDir : path.resolve(workspaceDir, '.tmp', 'governance', 'playable-2d-bundle');

const outputs = await collectBundleOutput({ write, outDir });
const report = createBundleReport(outputs);
writeReport(reportPath, report);

if (mode === 'bundle') {
    runBundleGovernance(report);
} else if (mode === 'size') {
    runSizeGovernance(report, Boolean(cli.refreshBaseline || cli['refresh-baseline']));
} else if (mode === 'smoke') {
    await runSmoke(report);
} else {
    fail(`Unsupported mode ${mode}. Expected bundle, size, or smoke.`);
}