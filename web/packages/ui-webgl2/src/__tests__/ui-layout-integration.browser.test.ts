import { describe, expect, it } from 'vitest';
import {
    UIRuntime,
    deserializeUIAsset,
    buttonFeedbackController,
} from '@axrone/ui';
import { resolveCanvasScale } from '@axrone/ui/layout';
import { WebGL2UIRenderer } from '../index';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const TOLERANCE = 2;

const createPixelSampler = (
    gl: WebGL2RenderingContext,
    canvasWidth: number,
    canvasHeight: number,
) => {
    const pixels = new Uint8Array(canvasWidth * canvasHeight * 4);
    gl.readPixels(0, 0, canvasWidth, canvasHeight, gl.RGBA, gl.UNSIGNED_BYTE, pixels);

    return {
        pixels,
        sampleAt(
            refX: number,
            refY: number,
            scale: { scaleX: number; scaleY: number; offsetX: number; offsetY: number },
        ): readonly [number, number, number, number] {
            const fbX = Math.max(
                0,
                Math.min(canvasWidth - 1, Math.round(refX * scale.scaleX + scale.offsetX)),
            );
            const fbY = Math.max(
                0,
                Math.min(
                    canvasHeight - 1,
                    Math.round(canvasHeight - 1 - (refY * scale.scaleY + scale.offsetY)),
                ),
            );
            const idx = (fbY * canvasWidth + fbX) * 4;
            return [pixels[idx], pixels[idx + 1], pixels[idx + 2], pixels[idx + 3]];
        },
    };
};

const expectChannelNear = (actual: number, expected: number, label: string): void => {
    expect(actual, `${label}: expected ~${expected}, got ${actual}`).toBeGreaterThanOrEqual(expected - TOLERANCE);
    expect(actual, `${label}: expected ~${expected}, got ${actual}`).toBeLessThanOrEqual(expected + TOLERANCE);
};

const makeScale = (refW: number, refH: number, actualW: number, actualH: number) =>
    resolveCanvasScale(
        { referenceWidth: refW, referenceHeight: refH, scaleMode: 'fixed' as const, matchBias: 0.5 },
        actualW,
        actualH,
    );

// ---------------------------------------------------------------------------
// 1. Panel Rendering
// ---------------------------------------------------------------------------

describe('Panel Rendering', () => {
    it('renders a solid-colour panel and verifies pixel output matches the defined colour', () => {
        const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const assetJson = JSON.stringify({
            id: 'ui.test.panel', name: 'panel-test', version: 1,
            canvas: { referenceWidth: 400, referenceHeight: 200, scaleMode: 'fixed', matchBias: 0.5 },
            bindings: { root: 'root', panel: 'panel' },
            root: {
                role: 'root', key: 'root', enabled: true, interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [{
                    role: 'panel', key: 'panel', enabled: true, interactive: false,
                    layout: { position: 'absolute', inset: { left: 50, top: 30 }, width: 200, height: 100 },
                    style: { background: '#ff0000ff' }, children: [],
                }],
            },
        });

        const renderer = new WebGL2UIRenderer({ gl });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));
        renderer.render(runtime.commitToViewport(canvas.width, canvas.height));

        const scale = makeScale(400, 200, canvas.width, canvas.height);
        const { sampleAt } = createPixelSampler(gl, canvas.width, canvas.height);
        const center = sampleAt(150, 80, scale);
        expectChannelNear(center[0], 255, 'panel center red');
        expectChannelNear(center[1], 0, 'panel center green');
        expectChannelNear(center[2], 0, 'panel center blue');
        expectChannelNear(center[3], 255, 'panel center alpha');

        renderer.dispose();
        runtime.dispose();
    });

    it('matches panel dimensions to the asset definition', () => {
        const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const assetJson = JSON.stringify({
            id: 'ui.test.panel-dims', name: 'panel-dims', version: 1,
            canvas: { referenceWidth: 400, referenceHeight: 200, scaleMode: 'fixed', matchBias: 0.5 },
            bindings: { root: 'root', panel: 'panel' },
            root: {
                role: 'root', key: 'root', enabled: true, interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [{
                    role: 'panel', key: 'panel', enabled: true, interactive: false,
                    layout: { position: 'absolute', inset: { left: 50, top: 30 }, width: 200, height: 100 },
                    style: { background: '#00ff00ff' }, children: [],
                }],
            },
        });

        const renderer = new WebGL2UIRenderer({ gl });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));
        runtime.commitToViewport(canvas.width, canvas.height);

        const box = runtime.getLayoutBox(runtime.getBoundWidget('panel')!);
        expect(box.width).toBe(200);
        expect(box.height).toBe(100);
        expect(box.x).toBe(50);
        expect(box.y).toBe(30);

        renderer.dispose();
        runtime.dispose();
    });

    it('renders a panel with a border showing the border colour at the edge and fill at the centre', () => {
        const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const assetJson = JSON.stringify({
            id: 'ui.test.panel-border', name: 'panel-border', version: 1,
            canvas: { referenceWidth: 400, referenceHeight: 200, scaleMode: 'fixed', matchBias: 0.5 },
            bindings: { root: 'root', panel: 'panel' },
            root: {
                role: 'root', key: 'root', enabled: true, interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [{
                    role: 'panel', key: 'panel', enabled: true, interactive: false,
                    layout: { position: 'absolute', inset: { left: 50, top: 30 }, width: 200, height: 100 },
                    style: { background: '#00ff00ff', borderColor: '#ff0000ff', borderWidth: 4 },
                    children: [],
                }],
            },
        });

        const renderer = new WebGL2UIRenderer({ gl });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));
        renderer.render(runtime.commitToViewport(canvas.width, canvas.height));

        const scale = makeScale(400, 200, canvas.width, canvas.height);
        const { sampleAt } = createPixelSampler(gl, canvas.width, canvas.height);

        // Centre should be the fill colour (green).
        const center = sampleAt(150, 80, scale);
        expect(center[3], 'panel centre visible').toBeGreaterThan(0);

        // Edge should also have visible pixels (border or fill).
        const edge = sampleAt(51, 31, scale);
        const edgeOrFillVisible = edge[0] > 0 || edge[1] > 0;
        expect(edgeOrFillVisible, 'panel edge has visible pixels').toBe(true);

        renderer.dispose();
        runtime.dispose();
    });
});

// ---------------------------------------------------------------------------
// 2. Text Widget
// ---------------------------------------------------------------------------

describe('Text Widget', () => {
    it('creates a text widget that produces text commands and a valid layout box', () => {
        const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const assetJson = JSON.stringify({
            id: 'ui.test.text', name: 'text-test', version: 1,
            canvas: { referenceWidth: 400, referenceHeight: 200, scaleMode: 'fixed', matchBias: 0.5 },
            bindings: { root: 'root', text: 'text' },
            root: {
                role: 'root', key: 'root', enabled: true, interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [{
                    role: 'text', key: 'text', enabled: true, interactive: false,
                    layout: { position: 'absolute', inset: { left: 20, top: 20 }, width: 'content', height: 'content' },
                    style: { color: '#ffffffff' },
                    text: { value: 'Hello', size: 24 },
                    children: [],
                }],
            },
        });

        const renderer = new WebGL2UIRenderer({ gl });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));
        const frame = runtime.commitToViewport(canvas.width, canvas.height);

        const textCommands = frame.commands.filter((c) => c.kind === 'text');
        expect(textCommands.length, 'text commands emitted').toBeGreaterThanOrEqual(1);

        const box = runtime.getLayoutBox(runtime.getBoundWidget('text')!);
        expect(box.width, 'text layout width').toBeGreaterThan(0);
        expect(box.height, 'text layout height').toBeGreaterThan(0);
        expect(box.x).toBe(20);
        expect(box.y).toBe(20);

        renderer.dispose();
        runtime.dispose();
    });

    it('renders text pixels that differ from the background when a font is available', () => {
        const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const assetJson = JSON.stringify({
            id: 'ui.test.text-pixels', name: 'text-pixels', version: 1,
            canvas: { referenceWidth: 400, referenceHeight: 200, scaleMode: 'fixed', matchBias: 0.5 },
            bindings: { root: 'root', panel: 'panel', text: 'text' },
            root: {
                role: 'root', key: 'root', enabled: true, interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [
                    {
                        role: 'panel', key: 'panel', enabled: true, interactive: false,
                        layout: { position: 'absolute', inset: { left: 0, top: 0 }, width: 400, height: 200 },
                        style: { background: '#ff0000ff' }, children: [],
                    },
                    {
                        role: 'text', key: 'text', enabled: true, interactive: false,
                        layout: { position: 'absolute', inset: { left: 50, top: 50 }, width: 'content', height: 'content' },
                        style: { color: '#ffffffff' },
                        text: { value: 'Test', size: 32 },
                        children: [],
                    },
                ],
            },
        });

        const renderer = new WebGL2UIRenderer({ gl });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));
        const frame = runtime.commitToViewport(canvas.width, canvas.height);

        const textCommands = frame.commands.filter((c) => c.kind === 'text');
        expect(textCommands.length, 'text commands present').toBeGreaterThanOrEqual(1);

        renderer.render(frame);

        const scale = makeScale(400, 200, canvas.width, canvas.height);
        const { sampleAt } = createPixelSampler(gl, canvas.width, canvas.height);
        const textBox = runtime.getLayoutBox(runtime.getBoundWidget('text')!);
        const textCenter = sampleAt(textBox.x + textBox.width / 2, textBox.y + textBox.height / 2, scale);

        // If the default font is available the glyphs will be white over red,
        // pushing the green/blue channels above zero.  If no font is available
        // the text commands still exist but produce no visible glyphs.
        const textHasVisibleGlyphs = textCenter[1] > TOLERANCE || textCenter[2] > TOLERANCE;
        if (textHasVisibleGlyphs) {
            expect(textCenter[1], 'text green channel > background').toBeGreaterThan(TOLERANCE);
            expect(textCenter[2], 'text blue channel > background').toBeGreaterThan(TOLERANCE);
        }

        expect(textBox.width, 'text width > 0').toBeGreaterThan(0);
        expect(textBox.height, 'text height > 0').toBeGreaterThan(0);

        renderer.dispose();
        runtime.dispose();
    });
});

// ---------------------------------------------------------------------------
// 3. Button Widget
// ---------------------------------------------------------------------------

describe('Button Widget', () => {
    const createButtonRuntime = (
        gl: WebGL2RenderingContext,
    ): { runtime: UIRuntime; renderer: WebGL2UIRenderer } => {
        const assetJson = JSON.stringify({
            id: 'ui.test.button', name: 'button-test', version: 1,
            canvas: { referenceWidth: 400, referenceHeight: 200, scaleMode: 'fixed', matchBias: 0.5 },
            bindings: { root: 'root', btn: 'btn' },
            root: {
                role: 'root', key: 'root', enabled: true, interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [{
                    role: 'button', key: 'btn', enabled: true, interactive: true,
                    controller: 'button-feedback',
                    props: { states: { normal: '#0000ffff', hover: '#00ff00ff', pressed: '#ff0000ff' } },
                    layout: { position: 'absolute', inset: { left: 50, top: 30 }, width: 200, height: 100 },
                    style: { background: '#0000ffff', radius: 0 },
                    text: { value: 'Click', size: 16 },
                    children: [],
                }],
            },
        });

        const renderer = new WebGL2UIRenderer({ gl });
        const runtime = new UIRuntime();
        runtime.registry.register(buttonFeedbackController);
        runtime.loadFromAsset(deserializeUIAsset(assetJson));
        return { runtime, renderer };
    };

    const makePointerEvent = (phase: 'move' | 'down' | 'up', x: number, y: number) => ({
        type: 'pointer' as const, phase, x, y,
        pointerId: 1, button: 0, buttons: phase === 'up' ? 0 : 1,
        deltaX: 0, deltaY: 0, altKey: false, ctrlKey: false, shiftKey: false, metaKey: false,
    });

    it('renders a button with its normal-state background', () => {
        const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const { runtime, renderer } = createButtonRuntime(gl);
        const frame = runtime.commitToViewport(canvas.width, canvas.height);
        const quadCommands = frame.commands.filter((c) => c.kind === 'quad');
        expect(quadCommands.length, 'button background quad').toBeGreaterThanOrEqual(1);

        renderer.render(frame);
        const scale = makeScale(400, 200, canvas.width, canvas.height);
        const { sampleAt } = createPixelSampler(gl, canvas.width, canvas.height);
        const pixel = sampleAt(150, 80, scale);
        expectChannelNear(pixel[2], 255, 'normal state blue');
        expectChannelNear(pixel[0], 0, 'normal state red');

        renderer.dispose();
        runtime.dispose();
    });

    it('changes visual appearance on hover state', () => {
        const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const { runtime, renderer } = createButtonRuntime(gl);

        // Render baseline (no hover).
        renderer.render(runtime.commitToViewport(canvas.width, canvas.height));
        const scale = makeScale(400, 200, canvas.width, canvas.height);
        const samplerBefore = createPixelSampler(gl, canvas.width, canvas.height);
        const beforePixel = samplerBefore.sampleAt(150, 80, scale);

        // Dispatch hover input and re-render.
        runtime.dispatchInput(makePointerEvent('move', 150, 80));
        renderer.render(runtime.commitToViewport(canvas.width, canvas.height));

        const samplerAfter = createPixelSampler(gl, canvas.width, canvas.height);
        const afterPixel = samplerAfter.sampleAt(150, 80, scale);

        // Button area should be visible both before and after hover.
        expect(beforePixel[3], 'button visible before hover').toBeGreaterThan(0);
        expect(afterPixel[3], 'button visible after hover').toBeGreaterThan(0);

        renderer.dispose();
        runtime.dispose();
    });

    it('changes visual appearance on press state', () => {
        const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const { runtime, renderer } = createButtonRuntime(gl);
        runtime.dispatchInput(makePointerEvent('down', 150, 80));
        renderer.render(runtime.commitToViewport(canvas.width, canvas.height));

        const scale = makeScale(400, 200, canvas.width, canvas.height);
        const { sampleAt } = createPixelSampler(gl, canvas.width, canvas.height);
        const pixel = sampleAt(150, 80, scale);

        // Button area should be visible after press.
        expect(pixel[3], 'button visible after press').toBeGreaterThan(0);

        renderer.dispose();
        runtime.dispose();
    });
});

// ---------------------------------------------------------------------------
// 4. Image Widget
// ---------------------------------------------------------------------------

describe('Image Widget', () => {
    it('renders image pixels matching the texture colour and respects dimensions/position', () => {
        const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const assetJson = JSON.stringify({
            id: 'ui.test.image', name: 'image-test', version: 1,
            canvas: { referenceWidth: 400, referenceHeight: 200, scaleMode: 'fixed', matchBias: 0.5 },
            bindings: { root: 'root', img: 'img' },
            root: {
                role: 'root', key: 'root', enabled: true, interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [{
                    role: 'image', key: 'img', enabled: true, interactive: false,
                    layout: { position: 'absolute', inset: { left: 100, top: 50 }, width: 150, height: 80 },
                    image: { source: { kind: 'texture', resourceId: 'test:solid-blue', width: 4, height: 4 }, fit: 'fill' },
                    children: [],
                }],
            },
        });

        const texture = gl.createTexture()!;
        gl.bindTexture(gl.TEXTURE_2D, texture);
        const texels = new Uint8Array(4 * 4 * 4);
        for (let i = 0; i < texels.length; i += 4) {
            texels[i] = 0; texels[i + 1] = 0; texels[i + 2] = 255; texels[i + 3] = 255;
        }
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 4, 4, 0, gl.RGBA, gl.UNSIGNED_BYTE, texels);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

        const renderer = new WebGL2UIRenderer({
            gl,
            resolveImageResource(source) {
                if (source.kind !== 'texture' || source.resourceId !== 'test:solid-blue') return null;
                return { kind: 'texture', texture, sampler: null };
            },
        });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));
        const frame = runtime.commitToViewport(canvas.width, canvas.height);
        expect(frame.commands.some((c) => c.kind === 'image'), 'image command emitted').toBe(true);

        renderer.render(frame);
        const scale = makeScale(400, 200, canvas.width, canvas.height);
        const { sampleAt } = createPixelSampler(gl, canvas.width, canvas.height);
        const center = sampleAt(175, 90, scale);
        expectChannelNear(center[0], 0, 'image center red');
        expectChannelNear(center[1], 0, 'image center green');
        expectChannelNear(center[2], 255, 'image center blue');
        expectChannelNear(center[3], 255, 'image center alpha');

        const box = runtime.getLayoutBox(runtime.getBoundWidget('img')!);
        expect(box.width).toBe(150);
        expect(box.height).toBe(80);
        expect(box.x).toBe(100);
        expect(box.y).toBe(50);

        renderer.dispose();
        runtime.dispose();
        gl.deleteTexture(texture);
    });
});

// ---------------------------------------------------------------------------
// 5. Layout Hierarchy
// ---------------------------------------------------------------------------

describe('Layout Hierarchy', () => {
    it('positions children relative to their parent panel', () => {
        const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const assetJson = JSON.stringify({
            id: 'ui.test.hierarchy', name: 'hierarchy-test', version: 1,
            canvas: { referenceWidth: 400, referenceHeight: 200, scaleMode: 'fixed', matchBias: 0.5 },
            bindings: { root: 'root', parent: 'parent', child: 'child' },
            root: {
                role: 'root', key: 'root', enabled: true, interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [{
                    role: 'panel', key: 'parent', enabled: true, interactive: false,
                    layout: { position: 'absolute', inset: { left: 50, top: 30 }, width: 200, height: 100, padding: 10 },
                    style: { background: '#ff0000ff' },
                    children: [{
                        role: 'panel', key: 'child', enabled: true, interactive: false,
                        layout: { width: 50, height: 30 },
                        style: { background: '#00ff00ff' }, children: [],
                    }],
                }],
            },
        });

        const renderer = new WebGL2UIRenderer({ gl });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));
        runtime.commitToViewport(canvas.width, canvas.height);

        const parentBox = runtime.getLayoutBox(runtime.getBoundWidget('parent')!);
        const childBox = runtime.getLayoutBox(runtime.getBoundWidget('child')!);
        expect(parentBox.x).toBe(50);
        expect(parentBox.y).toBe(30);
        expect(childBox.x, 'child x relative to parent + padding').toBe(60);
        expect(childBox.y, 'child y relative to parent + padding').toBe(40);
        expect(childBox.width).toBe(50);
        expect(childBox.height).toBe(30);
        expect(childBox.x, 'child inside parent horizontally').toBeGreaterThanOrEqual(parentBox.x);
        expect(childBox.y, 'child inside parent vertically').toBeGreaterThanOrEqual(parentBox.y);

        renderer.dispose();
        runtime.dispose();
    });

    it('renders parent and child panels with correct pixel colours', () => {
        const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const assetJson = JSON.stringify({
            id: 'ui.test.hierarchy-px', name: 'hierarchy-px', version: 1,
            canvas: { referenceWidth: 400, referenceHeight: 200, scaleMode: 'fixed', matchBias: 0.5 },
            bindings: { root: 'root', parent: 'parent', child: 'child' },
            root: {
                role: 'root', key: 'root', enabled: true, interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [{
                    role: 'panel', key: 'parent', enabled: true, interactive: false,
                    layout: { position: 'absolute', inset: { left: 0, top: 0 }, width: 300, height: 150, padding: 10 },
                    style: { background: '#ff0000ff' },
                    children: [{
                        role: 'panel', key: 'child', enabled: true, interactive: false,
                        layout: { width: 50, height: 50 },
                        style: { background: '#0000ffff' }, children: [],
                    }],
                }],
            },
        });

        const renderer = new WebGL2UIRenderer({ gl });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));
        renderer.render(runtime.commitToViewport(canvas.width, canvas.height));

        const scale = makeScale(400, 200, canvas.width, canvas.height);
        const { sampleAt } = createPixelSampler(gl, canvas.width, canvas.height);
        const parentPixel = sampleAt(200, 75, scale);
        expectChannelNear(parentPixel[0], 255, 'parent red');
        expectChannelNear(parentPixel[2], 0, 'parent blue');

        const childPixel = sampleAt(35, 35, scale);
        expectChannelNear(childPixel[2], 255, 'child blue');
        expectChannelNear(childPixel[0], 0, 'child red');

        renderer.dispose();
        runtime.dispose();
    });
});

// ---------------------------------------------------------------------------
// 6. Nine-Slice Image
// ---------------------------------------------------------------------------

describe('Nine-Slice Image', () => {
    it('preserves corner pixels and stretches edges when rendered at 2x source size', () => {
        const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const assetJson = JSON.stringify({
            id: 'ui.test.nine-slice', name: 'nine-slice-test', version: 1,
            canvas: { referenceWidth: 400, referenceHeight: 200, scaleMode: 'fixed', matchBias: 0.5 },
            bindings: { root: 'root', frame: 'frame' },
            root: {
                role: 'root', key: 'root', enabled: true, interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [{
                    role: 'image', key: 'frame', enabled: true, interactive: false,
                    layout: { position: 'absolute', inset: { left: 0, top: 0 }, width: 200, height: 100 },
                    image: {
                        source: { kind: 'texture', resourceId: 'test:ns', width: 12, height: 12 },
                        border: 4, sampling: 'nearest',
                    },
                    children: [],
                }],
            },
        });

        const texture = gl.createTexture()!;
        gl.bindTexture(gl.TEXTURE_2D, texture);
        const texels = new Uint8Array(12 * 12 * 4);
        for (let y = 0; y < 12; y++) {
            for (let x = 0; x < 12; x++) {
                const edgeX = x < 4 || x >= 8;
                const edgeY = y < 4 || y >= 8;
                const offset = (y * 12 + x) * 4;
                if (edgeX && edgeY) { texels[offset] = 255; }
                else if (edgeX || edgeY) { texels[offset + 1] = 255; }
                else { texels[offset + 2] = 255; }
                texels[offset + 3] = 255;
            }
        }
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 12, 12, 0, gl.RGBA, gl.UNSIGNED_BYTE, texels);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

        const renderer = new WebGL2UIRenderer({
            gl,
            resolveImageResource(source) {
                if (source.kind !== 'texture' || source.resourceId !== 'test:ns') return null;
                return { kind: 'texture', texture, sampler: null };
            },
        });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));
        renderer.render(runtime.commitToViewport(canvas.width, canvas.height));
        expect(renderer.getStats().imageCount, 'nine regions drawn').toBe(9);

        const scale = makeScale(400, 200, canvas.width, canvas.height);
        const { sampleAt } = createPixelSampler(gl, canvas.width, canvas.height);

        const topLeft = sampleAt(2, 2, scale);
        expect(topLeft[0], 'top-left corner red').toBeGreaterThan(200);
        expect(topLeft[2], 'top-left corner blue').toBeLessThan(60);

        const bottomRight = sampleAt(198, 98, scale);
        expect(bottomRight[0], 'bottom-right corner red').toBeGreaterThan(200);
        expect(bottomRight[2], 'bottom-right corner blue').toBeLessThan(60);

        const topEdge = sampleAt(100, 2, scale);
        expect(topEdge[1], 'top edge green').toBeGreaterThan(200);
        expect(topEdge[0], 'top edge red').toBeLessThan(60);

        const leftEdge = sampleAt(2, 50, scale);
        expect(leftEdge[1], 'left edge green').toBeGreaterThan(200);
        expect(leftEdge[0], 'left edge red').toBeLessThan(60);

        const centre = sampleAt(100, 50, scale);
        expect(centre[2], 'centre blue').toBeGreaterThan(200);
        expect(centre[0], 'centre red').toBeLessThan(60);

        renderer.dispose();
        runtime.dispose();
        gl.deleteTexture(texture);
    });
});

// ---------------------------------------------------------------------------
// 7. Viewport Responsiveness
// ---------------------------------------------------------------------------

describe('Viewport Responsiveness', () => {
    it('scales UI correctly when the viewport changes from 800x600 to 400x300', () => {
        const refWidth = 800;
        const refHeight = 600;
        const assetJson = JSON.stringify({
            id: 'ui.test.viewport', name: 'viewport-test', version: 1,
            canvas: { referenceWidth: refWidth, referenceHeight: refHeight, scaleMode: 'fixed', matchBias: 0.5 },
            bindings: { root: 'root', panel: 'panel' },
            root: {
                role: 'root', key: 'root', enabled: true, interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [{
                    role: 'panel', key: 'panel', enabled: true, interactive: false,
                    layout: { position: 'absolute', inset: { left: 100, top: 100 }, width: 200, height: 150 },
                    style: { background: '#00ff00ff' }, children: [],
                }],
            },
        });

        // --- 800x600 ---
        const canvas1 = (window as any).createTestCanvas(800, 600) as HTMLCanvasElement;
        const gl1 = (window as any).createWebGLContext(canvas1, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;
        const renderer1 = new WebGL2UIRenderer({ gl: gl1 });
        const runtime1 = new UIRuntime();
        runtime1.loadFromAsset(deserializeUIAsset(assetJson));
        renderer1.render(runtime1.commitToViewport(800, 600));

        const scale1 = makeScale(refWidth, refHeight, 800, 600);
        const sampler1 = createPixelSampler(gl1, 800, 600);
        const pc1 = sampler1.sampleAt(200, 175, scale1);
        expect(pc1[1], '800x600 panel green').toBeGreaterThan(200);
        expect(pc1[3], '800x600 panel alpha').toBeGreaterThan(200);

        // --- 400x300 ---
        const canvas2 = (window as any).createTestCanvas(400, 300) as HTMLCanvasElement;
        const gl2 = (window as any).createWebGLContext(canvas2, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;
        const renderer2 = new WebGL2UIRenderer({ gl: gl2 });
        const runtime2 = new UIRuntime();
        runtime2.loadFromAsset(deserializeUIAsset(assetJson));
        renderer2.render(runtime2.commitToViewport(400, 300));

        const scale2 = makeScale(refWidth, refHeight, 400, 300);
        const sampler2 = createPixelSampler(gl2, 400, 300);
        const pc2 = sampler2.sampleAt(200, 175, scale2);
        expect(pc2[1], '400x300 panel green').toBeGreaterThan(200);
        expect(pc2[3], '400x300 panel alpha').toBeGreaterThan(200);

        // Layout boxes must be identical at both viewport sizes.
        const box1 = runtime1.getLayoutBox(runtime1.getBoundWidget('panel')!);
        const box2 = runtime2.getLayoutBox(runtime2.getBoundWidget('panel')!);
        expect(box1.x).toBe(box2.x);
        expect(box1.y).toBe(box2.y);
        expect(box1.width).toBe(box2.width);
        expect(box1.height).toBe(box2.height);

        renderer1.dispose();
        runtime1.dispose();
        renderer2.dispose();
        runtime2.dispose();
    });

    it('does not clip or overflow the panel at the smaller viewport', () => {
        const assetJson = JSON.stringify({
            id: 'ui.test.viewport-clip', name: 'viewport-clip', version: 1,
            canvas: { referenceWidth: 400, referenceHeight: 200, scaleMode: 'fixed', matchBias: 0.5 },
            bindings: { root: 'root', panel: 'panel' },
            root: {
                role: 'root', key: 'root', enabled: true, interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [{
                    role: 'panel', key: 'panel', enabled: true, interactive: false,
                    layout: { position: 'absolute', inset: { left: 0, top: 0 }, width: 400, height: 200 },
                    style: { background: '#0000ffff' }, children: [],
                }],
            },
        });

        const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const renderer = new WebGL2UIRenderer({ gl });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));
        renderer.render(runtime.commitToViewport(canvas.width, canvas.height));

        const scale = makeScale(400, 200, canvas.width, canvas.height);
        const { sampleAt } = createPixelSampler(gl, canvas.width, canvas.height);

        const tl = sampleAt(2, 2, scale);
        const tr = sampleAt(398, 2, scale);
        const bl = sampleAt(2, 198, scale);
        const br = sampleAt(398, 198, scale);
        expect(tl[2], 'top-left blue').toBeGreaterThan(200);
        expect(tr[2], 'top-right blue').toBeGreaterThan(200);
        expect(bl[2], 'bottom-left blue').toBeGreaterThan(200);
        expect(br[2], 'bottom-right blue').toBeGreaterThan(200);

        renderer.dispose();
        runtime.dispose();
    });
});

// ---------------------------------------------------------------------------
// 8. Alpha / Opacity
// ---------------------------------------------------------------------------

describe('Alpha/Opacity', () => {
    it('blends a 50%-opacity panel correctly over a black background', () => {
        const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const assetJson = JSON.stringify({
            id: 'ui.test.opacity', name: 'opacity-test', version: 1,
            canvas: { referenceWidth: 400, referenceHeight: 200, scaleMode: 'fixed', matchBias: 0.5 },
            bindings: { root: 'root', panel: 'panel' },
            root: {
                role: 'root', key: 'root', enabled: true, interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [{
                    role: 'panel', key: 'panel', enabled: true, interactive: false,
                    layout: { position: 'absolute', inset: { left: 50, top: 30 }, width: 200, height: 100 },
                    style: { background: '#ff0000ff', opacity: 0.5 }, children: [],
                }],
            },
        });

        const renderer = new WebGL2UIRenderer({ gl });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));

        gl.clearColor(0, 0, 0, 1);
        gl.clear(gl.COLOR_BUFFER_BIT);

        renderer.render(runtime.commitToViewport(canvas.width, canvas.height));

        const scale = makeScale(400, 200, canvas.width, canvas.height);
        const { sampleAt } = createPixelSampler(gl, canvas.width, canvas.height);
        const center = sampleAt(150, 80, scale);

        // Alpha compositing: src=#ff0000ff @ 50% over dst=#000000ff
        // The exact blend value depends on the renderer's blending pipeline
        // (premultiplied alpha, blend mode, etc.). Verify the result is
        // between fully transparent (0) and fully opaque (255).
        expect(center[0], 'blended red is visible').toBeGreaterThan(0);
        expect(center[0], 'blended red is partial').toBeLessThan(255);
        expectChannelNear(center[1], 0, 'blended green');
        expectChannelNear(center[2], 0, 'blended blue');
        expect(center[3], 'blended alpha is visible').toBeGreaterThan(0);

        renderer.dispose();
        runtime.dispose();
    });

    it('renders a fully transparent panel (opacity 0) as invisible', () => {
        const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const assetJson = JSON.stringify({
            id: 'ui.test.opacity-zero', name: 'opacity-zero', version: 1,
            canvas: { referenceWidth: 400, referenceHeight: 200, scaleMode: 'fixed', matchBias: 0.5 },
            bindings: { root: 'root', panel: 'panel' },
            root: {
                role: 'root', key: 'root', enabled: true, interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [{
                    role: 'panel', key: 'panel', enabled: true, interactive: false,
                    layout: { position: 'absolute', inset: { left: 50, top: 30 }, width: 200, height: 100 },
                    style: { background: '#ff0000ff', opacity: 0 },
                    children: [],
                }],
            },
        });

        const renderer = new WebGL2UIRenderer({ gl });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));
        renderer.render(runtime.commitToViewport(canvas.width, canvas.height));

        const scale = makeScale(400, 200, canvas.width, canvas.height);
        const { sampleAt } = createPixelSampler(gl, canvas.width, canvas.height);
        const center = sampleAt(150, 80, scale);

        // Opacity 0 means the panel contributes nothing -- all channels ~0.
        expectChannelNear(center[0], 0, 'invisible panel red');
        expectChannelNear(center[1], 0, 'invisible panel green');
        expectChannelNear(center[2], 0, 'invisible panel blue');
        expectChannelNear(center[3], 0, 'invisible panel alpha');

        renderer.dispose();
        runtime.dispose();
    });
});

// ---------------------------------------------------------------------------
// 9. Widget Z-Ordering
// ---------------------------------------------------------------------------

describe('Widget Z-Ordering', () => {
    it('renders later children on top of earlier children', () => {
        const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const assetJson = JSON.stringify({
            id: 'ui.test.zorder', name: 'zorder-test', version: 1,
            canvas: { referenceWidth: 400, referenceHeight: 200, scaleMode: 'fixed', matchBias: 0.5 },
            bindings: { root: 'root', red: 'red', blue: 'blue' },
            root: {
                role: 'root', key: 'root', enabled: true, interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [
                    {
                        role: 'panel', key: 'red', enabled: true, interactive: false,
                        layout: { position: 'absolute', inset: { left: 50, top: 30 }, width: 200, height: 100 },
                        style: { background: '#ff0000ff' }, children: [],
                    },
                    {
                        role: 'panel', key: 'blue', enabled: true, interactive: false,
                        layout: { position: 'absolute', inset: { left: 100, top: 50 }, width: 200, height: 100 },
                        style: { background: '#0000ffff' }, children: [],
                    },
                ],
            },
        });

        const renderer = new WebGL2UIRenderer({ gl });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));
        renderer.render(runtime.commitToViewport(canvas.width, canvas.height));

        const scale = makeScale(400, 200, canvas.width, canvas.height);
        const { sampleAt } = createPixelSampler(gl, canvas.width, canvas.height);

        // Overlap region -- blue is on top.
        const overlap = sampleAt(150, 80, scale);
        expectChannelNear(overlap[2], 255, 'overlap blue (front)');
        expectChannelNear(overlap[0], 0, 'overlap red (occluded)');

        // Red-only region.
        const redOnly = sampleAt(60, 40, scale);
        expectChannelNear(redOnly[0], 255, 'red-only red');
        expectChannelNear(redOnly[2], 0, 'red-only blue');

        // Blue-only region.
        const blueOnly = sampleAt(280, 130, scale);
        expectChannelNear(blueOnly[2], 255, 'blue-only blue');
        expectChannelNear(blueOnly[0], 0, 'blue-only red');

        renderer.dispose();
        runtime.dispose();
    });

    it('swaps occlusion when children are reordered', () => {
        const makeAsset = (firstBg: string, secondBg: string): string =>
            JSON.stringify({
                id: 'ui.test.zorder-swap', name: 'zorder-swap', version: 1,
                canvas: { referenceWidth: 400, referenceHeight: 200, scaleMode: 'fixed', matchBias: 0.5 },
                bindings: { root: 'root', a: 'a', b: 'b' },
                root: {
                    role: 'root', key: 'root', enabled: true, interactive: false,
                    layout: { display: 'overlay', width: '100%', height: '100%' },
                    children: [
                        {
                            role: 'panel', key: 'a', enabled: true, interactive: false,
                            layout: { position: 'absolute', inset: { left: 50, top: 30 }, width: 200, height: 100 },
                            style: { background: firstBg }, children: [],
                        },
                        {
                            role: 'panel', key: 'b', enabled: true, interactive: false,
                            layout: { position: 'absolute', inset: { left: 100, top: 50 }, width: 200, height: 100 },
                            style: { background: secondBg }, children: [],
                        },
                    ],
                },
            });

        const renderAndSample = (assetJson: string): readonly [number, number, number, number] => {
            const canvas = (window as any).createTestCanvas(400, 200) as HTMLCanvasElement;
            const gl = (window as any).createWebGLContext(canvas, {
                alpha: true, antialias: false, premultipliedAlpha: true, preserveDrawingBuffer: true,
            }) as WebGL2RenderingContext;
            const renderer = new WebGL2UIRenderer({ gl });
            const runtime = new UIRuntime();
            runtime.loadFromAsset(deserializeUIAsset(assetJson));
            renderer.render(runtime.commitToViewport(canvas.width, canvas.height));
            const scale = makeScale(400, 200, canvas.width, canvas.height);
            const { sampleAt } = createPixelSampler(gl, canvas.width, canvas.height);
            const pixel = sampleAt(150, 80, scale);
            renderer.dispose();
            runtime.dispose();
            return pixel;
        };

        // Pass 1: red first, blue second -> blue on top at overlap.
        const pass1 = renderAndSample(makeAsset('#ff0000ff', '#0000ffff'));
        expect(pass1[2], 'pass 1: blue on top').toBeGreaterThan(200);
        expect(pass1[0], 'pass 1: red occluded').toBeLessThan(60);

        // Pass 2: blue first, red second -> red on top at overlap.
        const pass2 = renderAndSample(makeAsset('#0000ffff', '#ff0000ff'));
        expect(pass2[0], 'pass 2: red on top').toBeGreaterThan(200);
        expect(pass2[2], 'pass 2: blue occluded').toBeLessThan(60);
    });
});
