import fs from 'node:fs';
import path from 'node:path';
import { builtinModules } from 'node:module';
import { fileURLToPath } from 'node:url';
import commonjs from '@rollup/plugin-commonjs';
import resolve from '@rollup/plugin-node-resolve';
import esbuild from 'rollup-plugin-esbuild';
import dts from 'rollup-plugin-dts';
import peerDepsExternal from 'rollup-plugin-peer-deps-external';

const buildDir = path.dirname(fileURLToPath(import.meta.url));
const workspaceDir = path.resolve(buildDir, '..');
const defaultTsconfigPath = path.join(workspaceDir, 'tsconfig.build.json');

const builtinModuleIds = new Set([
    ...builtinModules,
    ...builtinModules.map((moduleName) => `node:${moduleName}`),
]);

const createExternalMatcher = (packageJson, additionalExternalIds) => {
    const packageIds = new Set([
        ...Object.keys(packageJson.dependencies ?? {}),
        ...Object.keys(packageJson.peerDependencies ?? {}),
        ...additionalExternalIds,
    ]);

    return (id) => {
        if (builtinModuleIds.has(id)) {
            return true;
        }

        for (const packageId of packageIds) {
            if (id === packageId || id.startsWith(`${packageId}/`)) {
                return true;
            }
        }

        return false;
    };
};

export const createMultiEntryConfig = ({
    packageDir,
    entries = { index: 'src/index.ts' },
    external = [],
}) => {
    const packageJsonPath = path.join(packageDir, 'package.json');
    const packageJson = JSON.parse(fs.readFileSync(packageJsonPath, 'utf8'));
    const packageTsconfigPath = fs.existsSync(path.join(packageDir, 'tsconfig.build.json'))
        ? path.join(packageDir, 'tsconfig.build.json')
        : defaultTsconfigPath;
    const distDir = path.join(packageDir, 'dist');
    const isExternal = createExternalMatcher(packageJson, external);

    const jsInput = {};
    for (const [name, relativePath] of Object.entries(entries)) {
        jsInput[name] = path.join(packageDir, relativePath);
    }

    return [
        {
            input: jsInput,
            external: isExternal,
            output: [
                {
                    dir: distDir,
                    format: 'cjs',
                    sourcemap: true,
                    entryFileNames: '[name].js',
                    chunkFileNames: 'chunks/[name]-[hash].js',
                    exports: 'named',
                },
                {
                    dir: distDir,
                    format: 'es',
                    sourcemap: true,
                    entryFileNames: '[name].mjs',
                    chunkFileNames: 'chunks/[name]-[hash].mjs',
                },
            ],
            plugins: [
                peerDepsExternal(),
                resolve({
                    extensions: ['.mjs', '.js', '.json', '.ts'],
                }),
                commonjs(),
                esbuild({
                    include: /\.ts$/,
                    target: 'es2020',
                    tsconfig: packageTsconfigPath,
                }),
            ],
        },
        ...Object.entries(entries).map(([name, relativePath]) => ({
            input: path.join(packageDir, relativePath),
            external: isExternal,
            output: {
                file: path.join(distDir, `${name}.d.ts`),
                format: 'es',
            },
            plugins: [
                dts({
                    tsconfig: packageTsconfigPath,
                }),
            ],
        })),
    ];
};
