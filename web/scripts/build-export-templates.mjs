import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { createHash } from 'node:crypto';
import { brotliCompressSync, gzipSync } from 'node:zlib';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { build } from 'vite';
import { createWorkspacePackageAliasEntries } from '../build/workspace-package-aliases.mjs';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const workspaceDir = path.resolve(scriptDir, '..');
const templatesRootDir = path.resolve(workspaceDir, 'export-templates');
const distRootDir = path.resolve(workspaceDir, 'dist', 'export-templates');

const templates = [
    {
        id: 'web-full',
        profileId: 'scene/3d-full',
        entryRelativePath: 'web-full/runtime-entry.ts',
        globalName: 'AxroneRuntime',
        outputFileName: 'axrone-runtime.js',
    },
];

const fail = (message) => {
    console.error(message);
    process.exitCode = 1;
};

const resolveEngineVersion = () => {
    const packageJsonPath = path.resolve(workspaceDir, 'packages', 'scene-3d', 'package.json');
    if (!fs.existsSync(packageJsonPath)) {
        return '0.0.0';
    }

    const packageJson = JSON.parse(fs.readFileSync(packageJsonPath, 'utf8'));
    return typeof packageJson.version === 'string' ? packageJson.version : '0.0.0';
};

const buildTemplate = async (template, engineVersion) => {
    const entryPath = path.resolve(templatesRootDir, template.entryRelativePath);
    if (!fs.existsSync(entryPath)) {
        throw new Error(`Export template entry '${entryPath}' does not exist.`);
    }

    const outDir = path.resolve(distRootDir, template.id);
    fs.rmSync(outDir, { recursive: true, force: true });
    fs.mkdirSync(outDir, { recursive: true });

    await build({
        configFile: false,
        logLevel: 'error',
        publicDir: false,
        define: {
            __AXRONE_TEMPLATE_VERSION__: JSON.stringify(engineVersion),
            __AXRONE_TEMPLATE_PROFILE_ID__: JSON.stringify(template.profileId),
        },
        resolve: {
            alias: createWorkspacePackageAliasEntries(workspaceDir),
        },
        root: templatesRootDir,
        build: {
            emptyOutDir: false,
            minify: 'esbuild',
            outDir,
            sourcemap: false,
            target: 'es2020',
            write: true,
            lib: {
                entry: entryPath,
                name: template.globalName,
                formats: ['iife'],
                fileName: () => template.outputFileName,
            },
        },
    });

    const outputPath = path.resolve(outDir, template.outputFileName);
    if (!fs.existsSync(outputPath)) {
        throw new Error(`Export template '${template.id}' did not emit '${template.outputFileName}'.`);
    }

    const payload = fs.readFileSync(outputPath);
    const manifest = {
        templateId: template.id,
        profileId: template.profileId,
        engineVersion,
        runtimeFileName: template.outputFileName,
        globalName: template.globalName,
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
};

const { values: cli } = parseArgs({
    options: {
        template: { type: 'string' },
    },
    allowPositionals: false,
    strict: true,
});

const selectedTemplates = cli.template
    ? templates.filter((template) => template.id === cli.template)
    : templates;

if (selectedTemplates.length === 0) {
    fail(`No export template matches '${String(cli.template)}'.`);
} else {
    const engineVersion = resolveEngineVersion();

    for (const template of selectedTemplates) {
        try {
            const manifest = await buildTemplate(template, engineVersion);
            runSmoke(manifest);
            console.log(
                `Export template ${manifest.templateId} built: ${manifest.rawBytes} bytes raw, ${manifest.gzipBytes} bytes gzip.`
            );
        } catch (error) {
            fail(
                `Export template '${template.id}' build failed: ${error instanceof Error ? error.message : String(error)}`
            );
        }
    }
}
