import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { createHash } from 'node:crypto';
import { brotliCompressSync, gzipSync } from 'node:zlib';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { build } from 'vite';
import { createWorkspacePackageAliasEntries } from '../build/workspace-package-aliases.mjs';
import {
    PROFILE_IDS,
    PROFILE_MODULE_CATALOG,
    TEMPLATE_DEFINITIONS,
} from '../export-templates/module-catalog.mjs';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const workspaceDir = path.resolve(scriptDir, '..');
const templatesRootDir = path.resolve(workspaceDir, 'export-templates');
const distRootDir = path.resolve(workspaceDir, 'dist', 'export-templates');
const generatedDir = path.resolve(templatesRootDir, '.generated');

const fail = (message) => {
    console.error(message);
    process.exitCode = 1;
};

const toIdentifier = (specifier) =>
    `mod_${specifier.replace(/^@/, '').replace(/[^a-zA-Z0-9]+/g, '_')}`;

const resolveEngineVersion = () => {
    const packageJsonPath = path.resolve(workspaceDir, 'packages', 'scene-3d', 'package.json');
    if (!fs.existsSync(packageJsonPath)) {
        return '0.0.0';
    }

    const packageJson = JSON.parse(fs.readFileSync(packageJsonPath, 'utf8'));
    return typeof packageJson.version === 'string' ? packageJson.version : '0.0.0';
};

const generateEntrySource = (definition) => {
    const modules = PROFILE_MODULE_CATALOG[definition.profile];
    const lines = [];

    lines.push(
        `import { createAxroneBoot, type AxroneBootOptions, type AxroneRuntimeHandle } from '../boot-factory';`
    );
    lines.push(
        `import { ${definition.sceneFacadeExport} as SceneFacade } from '${definition.sceneFacadeModule}';`
    );

    const componentImports = [];
    for (const componentSet of definition.engineComponents) {
        lines.push(`import { ${componentSet.exports.join(', ')} } from '${componentSet.module}';`);
        componentImports.push(...componentSet.exports);
    }

    for (const specifier of modules) {
        lines.push(`import * as ${toIdentifier(specifier)} from '${specifier}';`);
    }

    lines.push('');
    lines.push('declare const __AXRONE_TEMPLATE_VERSION__: string;');
    lines.push('declare const __AXRONE_TEMPLATE_PROFILE_ID__: string;');
    lines.push('');
    lines.push('export type AxroneRuntimeGlobal = {');
    lines.push('    readonly version: string;');
    lines.push('    readonly profileId: string;');
    lines.push('    readonly modules: Readonly<Record<string, unknown>>;');
    lines.push('    readonly boot: (options?: AxroneBootOptions) => Promise<AxroneRuntimeHandle>;');
    lines.push('};');
    lines.push('');
    lines.push('const modules: Readonly<Record<string, unknown>> = Object.freeze({');
    for (const specifier of modules) {
        lines.push(`    ${JSON.stringify(specifier)}: ${toIdentifier(specifier)},`);
    }
    lines.push('});');
    lines.push('');
    lines.push('const engineComponentTypes = Object.freeze([');
    for (const name of componentImports) {
        lines.push(`    ${name},`);
    }
    lines.push(']);');
    lines.push('');
    lines.push('const runtime: AxroneRuntimeGlobal = Object.freeze({');
    lines.push('    version: __AXRONE_TEMPLATE_VERSION__,');
    lines.push('    profileId: __AXRONE_TEMPLATE_PROFILE_ID__,');
    lines.push('    modules,');
    lines.push(
        '    boot: createAxroneBoot({ createScene: (options) => new SceneFacade(options), engineComponentTypes }),'
    );
    lines.push('});');
    lines.push('');
    lines.push("Reflect.set(globalThis, '__AXRONE_RUNTIME__', runtime);");
    lines.push('');
    lines.push('export default runtime;');
    lines.push('');

    return lines.join('\n');
};

const writeGeneratedEntry = (definition) => {
    fs.mkdirSync(generatedDir, { recursive: true });
    const entryPath = path.resolve(generatedDir, `${definition.id}-entry.ts`);
    fs.writeFileSync(entryPath, generateEntrySource(definition), 'utf8');
    return entryPath;
};

/**
 * Patch Emscripten WASM module guards that check `.startsWith("file://")` on a
 * relative URL (which never matches). Replace with `location.protocol` checks so
 * the runtime correctly skips `fetch()` on `file://` pages where WASM bytes are
 * already embedded in the bundle.
 */
const patchEmscriptenFileGuards = (filePath) => {
    let source = fs.readFileSync(filePath, 'utf8');
    const original = source;

    // Fix 1: Direct fetch() guard — check page protocol instead of WASM URL
    source = source.replace(
        /typeof fetch=="function"&&!(\w+)\.startsWith\("file:\/\/"\)/g,
        'typeof fetch=="function"&&location.protocol!="file:"'
    );

    // Fix 2: instantiateStreaming guard — same protocol check
    source = source.replace(
        /(\w+)\.startsWith\("file:\/\/"\)\|\|(\w+)\|\|typeof fetch!="function"/g,
        'location.protocol==="file:"||$2||typeof fetch!="function"'
    );

    if (source !== original) {
        fs.writeFileSync(filePath, source, 'utf8');
        console.log('  Patched Emscripten file:// fetch guards.');
    }
};

const buildTemplate = async (definition, engineVersion) => {
    const modules = PROFILE_MODULE_CATALOG[definition.profile];
    const entryPath = writeGeneratedEntry(definition);
    const outDir = path.resolve(distRootDir, definition.id);
    fs.rmSync(outDir, { recursive: true, force: true });
    fs.mkdirSync(outDir, { recursive: true });

    const outputFileName = 'axrone-runtime.js';

    await build({
        configFile: false,
        logLevel: 'error',
        publicDir: false,
        define: {
            __AXRONE_TEMPLATE_VERSION__: JSON.stringify(engineVersion),
            __AXRONE_TEMPLATE_PROFILE_ID__: JSON.stringify(PROFILE_IDS[definition.profile]),
            'process.env.NODE_ENV': JSON.stringify('production'),
        },
        resolve: {
            alias: createWorkspacePackageAliasEntries(workspaceDir),
        },
        root: templatesRootDir,
        build: {
            emptyOutDir: false,
            minify: false,
            outDir,
            sourcemap: false,
            target: 'es2020',
            write: true,
            lib: {
                entry: entryPath,
                name: 'AxroneRuntime',
                formats: ['iife'],
                fileName: () => outputFileName,
            },
        },
    });

    const outputPath = path.resolve(outDir, outputFileName);
    if (!fs.existsSync(outputPath)) {
        throw new Error(`Export template '${definition.id}' did not emit '${outputFileName}'.`);
    }

    patchEmscriptenFileGuards(outputPath);

    const payload = fs.readFileSync(outputPath);
    const manifest = {
        templateId: definition.id,
        profile: definition.profile,
        profileId: PROFILE_IDS[definition.profile],
        engineVersion,
        runtimeFileName: outputFileName,
        globalName: 'AxroneRuntime',
        modules: [...modules].sort(),
        rawBytes: payload.byteLength,
        gzipBytes: gzipSync(payload).byteLength,
        brotliBytes: brotliCompressSync(payload).byteLength,
        sha256: createHash('sha256').update(payload).digest('hex'),
        generatedAt: new Date().toISOString(),
    };

    fs.writeFileSync(
        path.resolve(outDir, 'template.json'),
        `${JSON.stringify(manifest, null, 4)}\n`,
        'utf8'
    );

    return manifest;
};

const runSmoke = (manifest) => {
    const outputPath = path.resolve(distRootDir, manifest.templateId, manifest.runtimeFileName);
    const source = fs.readFileSync(outputPath, 'utf8');

    if (!source.includes('__AXRONE_RUNTIME__')) {
        throw new Error(
            `Export template '${manifest.templateId}' does not publish the __AXRONE_RUNTIME__ global.`
        );
    }

    if (source.includes('__AXRONE_TEMPLATE_VERSION__')) {
        throw new Error(
            `Export template '${manifest.templateId}' left the version placeholder unresolved.`
        );
    }

    if (!Array.isArray(manifest.modules) || manifest.modules.length === 0) {
        throw new Error(`Export template '${manifest.templateId}' manifest has no module list.`);
    }
};

const runSizeOrderingCheck = (manifestsById) => {
    const web2d = manifestsById['web-2d'];
    const webFull = manifestsById['web-full'];
    if (web2d && webFull && web2d.rawBytes >= webFull.rawBytes) {
        throw new Error(
            `Expected web-2d (${web2d.rawBytes} bytes) to be smaller than web-full (${webFull.rawBytes} bytes).`
        );
    }
};

const { values: cli } = parseArgs({
    options: {
        template: { type: 'string' },
    },
    allowPositionals: false,
    strict: true,
});

const selectedDefinitions = cli.template
    ? TEMPLATE_DEFINITIONS.filter((definition) => definition.id === cli.template)
    : TEMPLATE_DEFINITIONS;

if (selectedDefinitions.length === 0) {
    fail(`No export template matches '${String(cli.template)}'.`);
} else {
    const engineVersion = resolveEngineVersion();
    const manifests = [];

    for (const definition of selectedDefinitions) {
        try {
            const manifest = await buildTemplate(definition, engineVersion);
            runSmoke(manifest);
            manifests.push(manifest);
            console.log(
                `Export template ${manifest.templateId} built: ${manifest.modules.length} modules, ${manifest.rawBytes} bytes raw, ${manifest.gzipBytes} bytes gzip.`
            );
        } catch (error) {
            fail(
                `Export template '${definition.id}' build failed: ${error instanceof Error ? error.message : String(error)}`
            );
        }
    }

    try {
        runSizeOrderingCheck(Object.fromEntries(manifests.map((manifest) => [manifest.templateId, manifest])));
    } catch (error) {
        fail(error instanceof Error ? error.message : String(error));
    }

    fs.rmSync(generatedDir, { recursive: true, force: true });
}
