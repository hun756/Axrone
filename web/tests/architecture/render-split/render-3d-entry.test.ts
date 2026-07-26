import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import * as render3D from '@axrone/render-3d';
import * as render3DCapabilities from '@axrone/render-3d/capabilities';

const testDir = path.dirname(fileURLToPath(import.meta.url));
const render3dPackageDir = path.resolve(testDir, '../../../packages/render-3d');
const render3dSrcDir = path.resolve(render3dPackageDir, 'src');
const allowedSourceFiles = ['capabilities.ts', 'index.ts'] as const;

describe('render-3d entry', () => {
    it('exposes only capability metadata from the package root', () => {
        expect(render3D.RENDER_3D_CAPABILITY_ID).toBe('render/3d');
        expect(render3D.getRender3DCapability().packageName).toBe('@axrone/render-3d');
        expect(render3D.getRender3DCapability().ownerPackage).toBe('@axrone/render-core');

        // The descriptor package must not re-export rendering contracts;
        // those belong to @axrone/render-core and @axrone/render-webgl2.
        expect('RenderPipeline' in render3D).toBe(false);
        expect('createRenderPipeline' in render3D).toBe(false);
        expect('compileRenderShaderEffect' in render3D).toBe(false);
    });

    it('keeps the capabilities subpath aligned with the root export', () => {
        expect(render3DCapabilities.RENDER_3D_CAPABILITY_ID).toBe(
            render3D.RENDER_3D_CAPABILITY_ID
        );
        expect(render3DCapabilities.RENDER_3D_OWNER_PACKAGE).toBe('@axrone/render-core');
    });

    it('stays a pure capability descriptor with no runtime source files', () => {
        const sourceFiles = fs
            .readdirSync(render3dSrcDir, { withFileTypes: true })
            .filter((entry) => entry.isFile() && entry.name.endsWith('.ts'))
            .map((entry) => entry.name)
            .sort((left, right) => left.localeCompare(right));

        expect(sourceFiles).toEqual([...allowedSourceFiles]);
    });

    it('declares only the root and capabilities subpath exports', () => {
        const packageJson = JSON.parse(
            fs.readFileSync(path.resolve(render3dPackageDir, 'package.json'), 'utf8')
        ) as { exports: Record<string, unknown> };

        expect(Object.keys(packageJson.exports).sort()).toEqual(['.', './capabilities']);
    });
});
