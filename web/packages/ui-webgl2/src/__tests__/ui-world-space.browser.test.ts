import { describe, expect, it } from 'vitest';
import { UIRuntime, deserializeUIAsset } from '@axrone/ui';
import { WebGL2UIRenderer } from '../index';
import { createUIWorldSurface } from '../world-surface';
import { createUIWorldQuadRenderer } from '../world-quad';

/**
 * End-to-end world-space UI pipeline on a real WebGL2 surface:
 * UI asset -> offscreen framebuffer -> camera-projected 3D quad -> pixels.
 *
 * Guards the contract world-space UI depends on: the renderer can target an FBO,
 * the surface texture carries the UI, and the quad pass samples it without
 * leaking GL state into the surrounding scene pass.
 */
const createFullBleedAssetJson = (): string =>
    JSON.stringify({
        id: 'ui.world-space',
        name: 'world-space',
        version: 1,
        canvas: {
            referenceWidth: 512,
            referenceHeight: 256,
            scaleMode: 'fixed',
            matchBias: 0.5,
        },
        bindings: { root: 'root', panel: 'panel' },
        root: {
            role: 'root',
            key: 'root',
            enabled: true,
            interactive: false,
            layout: { display: 'overlay', width: '100%', height: '100%' },
            children: [
                {
                    role: 'panel',
                    key: 'panel',
                    enabled: true,
                    interactive: false,
                    // Fills the whole canvas so the quad is uniformly covered.
                    layout: { width: '100%', height: '100%' },
                    style: { background: '#ff0000ff' },
                    children: [],
                },
            ],
        },
    });

/** Column-major identity matrix. */
const identityMatrix = (): Float32Array =>
    new Float32Array([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);

describe('World-space UI pipeline (browser)', () => {
    it('renders a UI asset into an offscreen surface and onto a 3D quad', () => {
        const canvas = (window as any).createTestCanvas(256, 256) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true,
            antialias: false,
            premultipliedAlpha: true,
            preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const surface = createUIWorldSurface(gl, 256, 128);
        expect(surface.width).toBe(256);
        expect(surface.height).toBe(128);
        expect(gl.checkFramebufferStatus(gl.FRAMEBUFFER)).not.toBe(0);

        const uiRenderer = new WebGL2UIRenderer({ gl });
        const quadRenderer = createUIWorldQuadRenderer(gl);
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(createFullBleedAssetJson()));

        // 1) UI into the offscreen colour attachment.
        const frame = runtime.commitToViewport(surface.width, surface.height);
        gl.bindFramebuffer(gl.FRAMEBUFFER, surface.framebuffer);
        gl.viewport(0, 0, surface.width, surface.height);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT);
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        uiRenderer.render(frame, { framebuffer: surface.framebuffer });

        // The offscreen texture must actually hold the panel colour.
        gl.bindFramebuffer(gl.FRAMEBUFFER, surface.framebuffer);
        const surfacePixels = new Uint8Array(4);
        gl.readPixels(
            Math.floor(surface.width / 2),
            Math.floor(surface.height / 2),
            1,
            1,
            gl.RGBA,
            gl.UNSIGNED_BYTE,
            surfacePixels
        );
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        expect(surfacePixels[3], 'offscreen alpha').toBeGreaterThan(0);
        expect(surfacePixels[0], 'offscreen red').toBeGreaterThan(200);

        // 2) Quad into the default framebuffer with an identity camera, sized to
        // fill clip space (2 x 2 spans -1..1 on both axes).
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        gl.viewport(0, 0, canvas.width, canvas.height);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT);
        quadRenderer.draw(surface.texture, {
            modelMatrix: identityMatrix(),
            viewProjection: identityMatrix(),
            width: 2,
            height: 2,
            depthTest: false,
        });

        const screenPixels = new Uint8Array(4);
        gl.readPixels(
            Math.floor(canvas.width / 2),
            Math.floor(canvas.height / 2),
            1,
            1,
            gl.RGBA,
            gl.UNSIGNED_BYTE,
            screenPixels
        );
        expect(screenPixels[3], 'quad center alpha').toBeGreaterThan(0);
        expect(screenPixels[0], 'quad center red').toBeGreaterThan(200);
        expect(screenPixels[2], 'quad center blue').toBeLessThan(60);

        quadRenderer.dispose();
        surface.dispose();
        uiRenderer.dispose();
        runtime.dispose();
    });

    it('leaves the surrounding GL state untouched after a quad draw', () => {
        const canvas = (window as any).createTestCanvas(128, 128) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const surface = createUIWorldSurface(gl, 64, 64);
        const quadRenderer = createUIWorldQuadRenderer(gl);

        // Establish a scene-like state the pass must restore.
        gl.enable(gl.DEPTH_TEST);
        gl.enable(gl.CULL_FACE);
        gl.disable(gl.BLEND);
        gl.depthMask(true);

        quadRenderer.draw(surface.texture, {
            modelMatrix: identityMatrix(),
            viewProjection: identityMatrix(),
            width: 1,
            height: 1,
        });

        expect(gl.isEnabled(gl.DEPTH_TEST), 'depth test restored').toBe(true);
        expect(gl.isEnabled(gl.CULL_FACE), 'cull face restored').toBe(true);
        expect(gl.isEnabled(gl.BLEND), 'blend restored').toBe(false);
        expect(gl.getParameter(gl.DEPTH_WRITEMASK), 'depth mask restored').toBe(true);
        expect(gl.getParameter(gl.CURRENT_PROGRAM), 'program restored').toBeNull();

        quadRenderer.dispose();
        surface.dispose();
    });

    it('resizes the surface without leaking the previous attachment', () => {
        const canvas = (window as any).createTestCanvas(64, 64) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {}) as WebGL2RenderingContext;

        const surface = createUIWorldSurface(gl, 32, 16);
        surface.resize(64, 48);

        expect(surface.width).toBe(64);
        expect(surface.height).toBe(48);
        expect(gl.isTexture(surface.texture)).toBe(true);

        surface.dispose();
        expect(gl.isTexture(surface.texture)).toBe(false);
    });

    it('lets nearer geometry occlude the UI when depth testing is on', () => {
        // Proves the "walk behind the machine and the UI disappears" behaviour:
        // a nearer opaque quad writing depth must hide a farther UI quad.
        const canvas = (window as any).createTestCanvas(128, 128) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            depth: true,
            preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const uiSurface = createUIWorldSurface(gl, 64, 64);
        const blockerSurface = createUIWorldSurface(gl, 4, 4);
        const quadRenderer = createUIWorldQuadRenderer(gl);

        // Fill the UI surface red and the blocker surface green.
        const fillSurface = (surface: typeof uiSurface, color: readonly number[]): void => {
            gl.bindFramebuffer(gl.FRAMEBUFFER, surface.framebuffer);
            gl.viewport(0, 0, surface.width, surface.height);
            gl.clearColor(color[0], color[1], color[2], color[3]);
            gl.clear(gl.COLOR_BUFFER_BIT);
            gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        };
        fillSurface(uiSurface, [1, 0, 0, 1]);
        fillSurface(blockerSurface, [0, 1, 0, 1]);

        gl.viewport(0, 0, canvas.width, canvas.height);
        gl.enable(gl.DEPTH_TEST);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

        // Blocker sits nearer the camera (clip z = -0.5) and writes depth.
        const blockerModel = new Float32Array([
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, -0.5, 1,
        ]);
        quadRenderer.draw(blockerSurface.texture, {
            modelMatrix: blockerModel,
            viewProjection: identityMatrix(),
            width: 2,
            height: 2,
            depthTest: true,
            depthWrite: true,
        });

        // UI sits farther away (clip z = +0.5) and must fail the depth test.
        const uiModel = new Float32Array([
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0.5, 1,
        ]);
        quadRenderer.draw(uiSurface.texture, {
            modelMatrix: uiModel,
            viewProjection: identityMatrix(),
            width: 2,
            height: 2,
            depthTest: true,
            depthWrite: false,
        });

        const pixels = new Uint8Array(4);
        gl.readPixels(
            Math.floor(canvas.width / 2),
            Math.floor(canvas.height / 2),
            1,
            1,
            gl.RGBA,
            gl.UNSIGNED_BYTE,
            pixels
        );

        // Green blocker survived, red UI was occluded.
        expect(pixels[1], 'blocker green channel').toBeGreaterThan(200);
        expect(pixels[0], 'occluded UI red channel').toBeLessThan(60);

        quadRenderer.dispose();
        uiSurface.dispose();
        blockerSurface.dispose();
    });

    it('draws the UI over nearer geometry when depth testing is off', () => {
        // The complement of the occlusion test: screen-space style always-on-top.
        const canvas = (window as any).createTestCanvas(128, 128) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            depth: true,
            preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const uiSurface = createUIWorldSurface(gl, 32, 32);
        const quadRenderer = createUIWorldQuadRenderer(gl);

        gl.bindFramebuffer(gl.FRAMEBUFFER, uiSurface.framebuffer);
        gl.viewport(0, 0, uiSurface.width, uiSurface.height);
        gl.clearColor(1, 0, 0, 1);
        gl.clear(gl.COLOR_BUFFER_BIT);
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);

        gl.viewport(0, 0, canvas.width, canvas.height);
        gl.enable(gl.DEPTH_TEST);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
        // Prime the depth buffer with a nearer surface.
        quadRenderer.draw(uiSurface.texture, {
            modelMatrix: new Float32Array([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, -0.5, 1]),
            viewProjection: identityMatrix(),
            width: 2,
            height: 2,
            depthTest: true,
            depthWrite: true,
        });

        quadRenderer.draw(uiSurface.texture, {
            modelMatrix: new Float32Array([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0.5, 1]),
            viewProjection: identityMatrix(),
            width: 2,
            height: 2,
            depthTest: false,
        });

        const pixels = new Uint8Array(4);
        gl.readPixels(64, 64, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
        expect(pixels[0], 'UI red channel drawn on top').toBeGreaterThan(200);

        quadRenderer.dispose();
        uiSurface.dispose();
    });
});
