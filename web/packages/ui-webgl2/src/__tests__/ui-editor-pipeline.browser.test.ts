import { describe, expect, it } from 'vitest';
import { UIRuntime, checkboxToggleController, deserializeUIAsset } from '@axrone/ui';
import { resolveCanvasScale } from '@axrone/ui/layout';
import type { StrokeRenderCommand } from '@axrone/ui/types';
import { WebGL2UIRenderer } from '../index';

/**
 * Replicates the exact document shape the Editor's UI workspace produces
 * (ui-document-controller toSnapshot output) and pushes it through the same
 * runtime + renderer pipeline as UICanvasPreview.svelte. Guards against the
 * "nothing renders" regression: widgets authored in the UI Editor must emit
 * visible quad/text commands on a real WebGL2 surface.
 */
const createEditorShapedAssetJson = (): string =>
    JSON.stringify({
        id: 'ui.editor-shaped',
        name: 'editor-shaped',
        version: 1,
        canvas: {
            referenceWidth: 1920,
            referenceHeight: 1080,
            scaleMode: 'match-width-or-height',
            matchBias: 0.5,
        },
        bindings: {
            root: 'root',
            'widget-1': 'widget-1',
            'widget-2': 'widget-2',
            'widget-3': 'widget-3',
        },
        root: {
            role: 'root',
            key: 'root',
            enabled: true,
            interactive: false,
            layout: { display: 'overlay', width: '100%', height: '100%' },
            children: [
                {
                    role: 'panel',
                    key: 'widget-1',
                    enabled: true,
                    interactive: false,
                    layout: { width: 320, height: 200, direction: 'column', gap: 10, padding: 14 },
                    style: {
                        background: '#0f172acc',
                        borderColor: '#334155ff',
                        borderWidth: 1,
                        radius: 14,
                    },
                    children: [],
                },
                {
                    role: 'text',
                    key: 'widget-2',
                    enabled: true,
                    interactive: false,
                    layout: { width: 'content', height: 'content' },
                    style: { color: '#e2e8f0ff' },
                    text: { value: 'Text', size: 18 },
                    children: [],
                },
                {
                    role: 'button',
                    key: 'widget-3',
                    enabled: true,
                    interactive: true,
                    layout: {
                        width: 'content',
                        height: 'content',
                        padding: 12,
                        direction: 'column',
                        alignItems: 'center',
                        justifyContent: 'center',
                    },
                    style: { background: '#0a74daff', radius: 8, color: '#ffffffff' },
                    text: { value: 'Button', size: 16 },
                    children: [],
                },
            ],
        },
    });

describe('UI Editor preview pipeline (browser)', () => {
    it('renders editor-authored panel, text and button widgets to real pixels', () => {
        const canvas = (window as any).createTestCanvas(800, 450) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true,
            antialias: true,
            premultipliedAlpha: true,
            preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const renderer = new WebGL2UIRenderer({ gl });
        const runtime = new UIRuntime();

        const asset = deserializeUIAsset(createEditorShapedAssetJson());
        runtime.loadFromAsset(asset);

        // Binding table must resolve every authored key (click-select contract).
        expect(runtime.getBoundWidget('widget-1')).not.toBeNull();
        expect(runtime.getBoundWidget('widget-2')).not.toBeNull();
        expect(runtime.getBoundWidget('widget-3')).not.toBeNull();

        const frame = runtime.commitToViewport(canvas.width, canvas.height);

        const quadCommands = frame.commands.filter((command) => command.kind === 'quad');
        const textCommands = frame.commands.filter((command) => command.kind === 'text');
        expect(quadCommands.length).toBeGreaterThanOrEqual(2); // panel + button backgrounds
        expect(textCommands.length).toBeGreaterThanOrEqual(2); // 'Text' + 'Button' labels

        // Every widget must have a non-degenerate layout box.
        for (const key of ['widget-1', 'widget-2', 'widget-3']) {
            const box = runtime.getLayoutBox(runtime.getBoundWidget(key)!);
            expect(box.width, `${key} width`).toBeGreaterThan(0);
            expect(box.height, `${key} height`).toBeGreaterThan(0);
        }

        renderer.render(frame);

        // Read back pixels and sample the CENTER of the panel's actual layout
        // box (projected through the same canvas scaler the editor uses).
        const scale = resolveCanvasScale(asset.canvas, canvas.width, canvas.height);
        const panelBox = runtime.getLayoutBox(runtime.getBoundWidget('widget-1')!);
        const panelCenterX = (panelBox.x + panelBox.width / 2) * scale.scaleX + scale.offsetX;
        const panelCenterY = (panelBox.y + panelBox.height / 2) * scale.scaleY + scale.offsetY;

        const pixels = new Uint8Array(canvas.width * canvas.height * 4);
        gl.readPixels(0, 0, canvas.width, canvas.height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);

        const sampleAlpha = (fbX: number, fbY: number): number => {
            // Framebuffer origin is bottom-left; layout origin is top-left.
            const x = Math.max(0, Math.min(canvas.width - 1, Math.round(fbX)));
            const y = Math.max(0, Math.min(canvas.height - 1, Math.round(canvas.height - 1 - fbY)));
            return pixels[(y * canvas.width + x) * 4 + 3];
        };

        expect(sampleAlpha(panelCenterX, panelCenterY), 'panel center alpha').toBeGreaterThan(0);
        // A corner far away from any authored widget must stay transparent.
        expect(sampleAlpha(canvas.width - 2, canvas.height - 2), 'empty corner alpha').toBe(0);

        renderer.dispose();
        runtime.dispose();
    });

    it('renders the exact single-button asset a user authors in the UI Editor', () => {
        // Mirrors Assets/UI-test.ui.json: overlay root with ONE content-sized button.
        const assetJson = JSON.stringify({
            id: 'ui.UI/test',
            name: 'UI/test',
            version: 1,
            canvas: {
                referenceWidth: 1920,
                referenceHeight: 1080,
                scaleMode: 'match-width-or-height',
                matchBias: 0.45,
            },
            bindings: { root: 'root', 'widget-4': 'widget-4' },
            root: {
                role: 'root',
                key: 'root',
                enabled: true,
                interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [
                    {
                        role: 'button',
                        key: 'widget-4',
                        enabled: true,
                        interactive: true,
                        layout: {
                            width: 'content',
                            height: 'content',
                            padding: 12,
                            direction: 'column',
                            alignItems: 'center',
                            justifyContent: 'center',
                        },
                        style: { background: '#0a74daff', radius: 8, color: '#ffffffff' },
                        text: { value: 'Button', size: 16 },
                        children: [],
                    },
                ],
            },
        });

        const canvas = (window as any).createTestCanvas(800, 450) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true,
            antialias: true,
            premultipliedAlpha: true,
            preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const renderer = new WebGL2UIRenderer({ gl });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));

        const button = runtime.getBoundWidget('widget-4');
        expect(button).not.toBeNull();

        const frame = runtime.commitToViewport(canvas.width, canvas.height);
        renderer.render(frame);

        const box = runtime.getLayoutBox(button!);
        expect(box.width, 'button layout width').toBeGreaterThan(0);
        expect(box.height, 'button layout height').toBeGreaterThan(0);

        const scale = resolveCanvasScale(
            { referenceWidth: 1920, referenceHeight: 1080, scaleMode: 'match-width-or-height', matchBias: 0.45 },
            canvas.width,
            canvas.height,
        );
        const cx = (box.x + box.width / 2) * scale.scaleX + scale.offsetX;
        const cy = (box.y + box.height / 2) * scale.scaleY + scale.offsetY;

        const pixels = new Uint8Array(canvas.width * canvas.height * 4);
        gl.readPixels(0, 0, canvas.width, canvas.height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
        const idx =
            (Math.max(0, Math.min(canvas.height - 1, Math.round(canvas.height - 1 - cy))) * canvas.width +
                Math.max(0, Math.min(canvas.width - 1, Math.round(cx)))) *
            4;

        expect(box.width * scale.scaleX).toBeGreaterThan(4); // sanity: big enough to see
        expect(pixels[idx + 3], 'button center alpha').toBeGreaterThan(0);

        renderer.dispose();
        runtime.dispose();
    });

    it('renders an image widget through the project texture resolver to real pixels', () => {
        // Mirrors the editor's image preset output: an absolute-positioned image
        // widget whose source points at a project texture resource id.
        const assetJson = JSON.stringify({
            id: 'ui.image-widget',
            name: 'image-widget',
            version: 1,
            canvas: {
                referenceWidth: 1920,
                referenceHeight: 1080,
                scaleMode: 'match-width-or-height',
                matchBias: 0.5,
            },
            bindings: { root: 'root', 'widget-img': 'widget-img' },
            root: {
                role: 'root',
                key: 'root',
                enabled: true,
                interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [
                    {
                        role: 'image',
                        key: 'widget-img',
                        enabled: true,
                        interactive: false,
                        layout: {
                            position: 'absolute',
                            inset: { left: 100, top: 80 },
                            width: 200,
                            height: 150,
                        },
                        image: {
                            source: { kind: 'texture', resourceId: 'test:image', width: 64, height: 64 },
                            fit: 'fill',
                        },
                        children: [],
                    },
                ],
            },
        });

        const canvas = (window as any).createTestCanvas(800, 450) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true,
            antialias: true,
            premultipliedAlpha: true,
            preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        // Opaque red 4x4 texture standing in for a decoded project image file,
        // uploaded exactly like UICanvasPreview's loadTexture does.
        const texture = gl.createTexture()!;
        gl.bindTexture(gl.TEXTURE_2D, texture);
        const texels = new Uint8Array(4 * 4 * 4);
        for (let i = 0; i < texels.length; i += 4) {
            texels[i] = 255;
            texels[i + 1] = 0;
            texels[i + 2] = 0;
            texels[i + 3] = 255;
        }
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 4, 4, 0, gl.RGBA, gl.UNSIGNED_BYTE, texels);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

        let resolverCalls = 0;
        const renderer = new WebGL2UIRenderer({
            gl,
            resolveImageResource(source) {
                resolverCalls += 1;
                if (source.kind !== 'texture' || source.resourceId !== 'test:image') {
                    return null;
                }
                return { kind: 'texture', texture, sampler: null };
            },
        });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));

        const imageWidget = runtime.getBoundWidget('widget-img');
        expect(imageWidget).not.toBeNull();

        const frame = runtime.commitToViewport(canvas.width, canvas.height);
        expect(frame.commands.some((command) => command.kind === 'image')).toBe(true);

        renderer.render(frame);

        // The resolver must have been consulted and the textured quad drawn.
        expect(resolverCalls).toBeGreaterThan(0);
        expect(renderer.getStats().imageCount).toBeGreaterThanOrEqual(1);

        const box = runtime.getLayoutBox(imageWidget!);
        const scale = resolveCanvasScale(
            { referenceWidth: 1920, referenceHeight: 1080, scaleMode: 'match-width-or-height', matchBias: 0.5 },
            canvas.width,
            canvas.height,
        );
        const cx = (box.x + box.width / 2) * scale.scaleX + scale.offsetX;
        const cy = (box.y + box.height / 2) * scale.scaleY + scale.offsetY;

        const pixels = new Uint8Array(canvas.width * canvas.height * 4);
        gl.readPixels(0, 0, canvas.width, canvas.height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
        const idx =
            (Math.max(0, Math.min(canvas.height - 1, Math.round(canvas.height - 1 - cy))) * canvas.width +
                Math.max(0, Math.min(canvas.width - 1, Math.round(cx)))) *
            4;

        expect(box.width * scale.scaleX).toBeGreaterThan(8); // sanity: big enough to see
        expect(pixels[idx + 3], 'image center alpha').toBeGreaterThan(0);
        expect(pixels[idx], 'image center red channel').toBeGreaterThan(200);
        expect(pixels[idx + 2], 'image center blue channel').toBeLessThan(60);

        renderer.dispose();
        runtime.dispose();
        gl.deleteTexture(texture);
    });

    it('draws a nine-slice image so corners keep their source pixels while the centre stretches', () => {
        const canvas = (window as any).createTestCanvas(800, 450) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true,
            antialias: false,
            premultipliedAlpha: true,
            preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const assetJson = JSON.stringify({
            id: 'ui.nine-slice',
            name: 'nine-slice',
            version: 1,
            canvas: {
                referenceWidth: 400,
                referenceHeight: 200,
                scaleMode: 'fixed',
                matchBias: 0.5,
            },
            bindings: { root: 'root', frame: 'frame' },
            root: {
                role: 'root',
                key: 'root',
                enabled: true,
                interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [
                    {
                        role: 'image',
                        key: 'frame',
                        enabled: true,
                        interactive: false,
                        layout: {
                            position: 'absolute',
                            inset: { left: 0, top: 0 },
                            width: 300,
                            height: 150,
                        },
                        image: {
                            source: {
                                kind: 'texture',
                                resourceId: 'test:frame',
                                width: 12,
                                height: 12,
                            },
                            border: 4,
                            sampling: 'nearest',
                        },
                        children: [],
                    },
                ],
            },
        });

        const texture = gl.createTexture()!;
        gl.bindTexture(gl.TEXTURE_2D, texture);
        const texels = new Uint8Array(12 * 12 * 4);
        for (let y = 0; y < 12; y += 1) {
            for (let x = 0; x < 12; x += 1) {
                const edgeX = x < 4 || x >= 8;
                const edgeY = y < 4 || y >= 8;
                const offset = (y * 12 + x) * 4;
                if (edgeX && edgeY) {
                    texels[offset] = 255;
                } else if (edgeX || edgeY) {
                    texels[offset + 1] = 255;
                } else {
                    texels[offset + 2] = 255;
                }
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
                if (source.kind !== 'texture' || source.resourceId !== 'test:frame') {
                    return null;
                }
                return { kind: 'texture', texture, sampler: null };
            },
        });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));
        const frame = runtime.commitToViewport(canvas.width, canvas.height);

        renderer.render(frame);

        expect(renderer.getStats().imageCount, 'nine regions drawn').toBe(9);

        const box = runtime.getLayoutBox(runtime.getBoundWidget('frame')!);
        const scale = resolveCanvasScale(
            {
                referenceWidth: 400,
                referenceHeight: 200,
                scaleMode: 'fixed',
                matchBias: 0.5,
            },
            canvas.width,
            canvas.height,
        );
        const pixels = new Uint8Array(canvas.width * canvas.height * 4);
        gl.readPixels(0, 0, canvas.width, canvas.height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);

        const sampleAt = (refX: number, refY: number): readonly number[] => {
            const cx = refX * scale.scaleX + scale.offsetX;
            const cy = refY * scale.scaleY + scale.offsetY;
            const px = Math.max(0, Math.min(canvas.width - 1, Math.round(cx)));
            const py = Math.max(0, Math.min(canvas.height - 1, Math.round(canvas.height - 1 - cy)));
            const index = (py * canvas.width + px) * 4;
            return [pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]];
        };

        const topLeft = sampleAt(box.x + 2, box.y + 2);
        expect(topLeft[0], 'top-left corner red').toBeGreaterThan(200);
        expect(topLeft[2], 'top-left corner blue').toBeLessThan(60);

        const bottomRight = sampleAt(box.x + box.width - 2, box.y + box.height - 2);
        expect(bottomRight[0], 'bottom-right corner red').toBeGreaterThan(200);
        expect(bottomRight[2], 'bottom-right corner blue').toBeLessThan(60);

        const topEdge = sampleAt(box.x + box.width / 2, box.y + 2);
        expect(topEdge[1], 'top edge green').toBeGreaterThan(200);
        expect(topEdge[0], 'top edge red').toBeLessThan(60);

        const leftEdge = sampleAt(box.x + 2, box.y + box.height / 2);
        expect(leftEdge[1], 'left edge green').toBeGreaterThan(200);
        expect(leftEdge[0], 'left edge red').toBeLessThan(60);

        const centre = sampleAt(box.x + box.width / 2, box.y + box.height / 2);
        expect(centre[2], 'centre blue').toBeGreaterThan(200);
        expect(centre[0], 'centre red').toBeLessThan(60);

        renderer.dispose();
        runtime.dispose();
        gl.deleteTexture(texture);
    });

    it('routes the authored material to the resolver and draws what it returns', () => {
        const canvas = (window as any).createTestCanvas(800, 450) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true,
            antialias: false,
            premultipliedAlpha: true,
            preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const assetJson = JSON.stringify({
            id: 'ui.material-slot',
            name: 'material-slot',
            version: 1,
            canvas: {
                referenceWidth: 400,
                referenceHeight: 200,
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
                        role: 'container',
                        key: 'panel',
                        enabled: true,
                        interactive: false,
                        layout: {
                            position: 'absolute',
                            inset: { left: 0, top: 0 },
                            width: 200,
                            height: 100,
                        },
                        image: {
                            source: {
                                kind: 'texture',
                                resourceId: '',
                                width: 1,
                                height: 1,
                            },
                            material: 'Assets/Materials/Mat_Panel.mat',
                            sampling: 'nearest',
                        },
                        children: [],
                    },
                ],
            },
        });

        const texture = gl.createTexture()!;
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.texImage2D(
            gl.TEXTURE_2D,
            0,
            gl.RGBA,
            1,
            1,
            0,
            gl.RGBA,
            gl.UNSIGNED_BYTE,
            new Uint8Array([0, 255, 0, 255]),
        );
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);

        const seenMaterials: (string | undefined)[] = [];
        const renderer = new WebGL2UIRenderer({
            gl,
            resolveImageResource(source, context) {
                seenMaterials.push(context.command.material);
                if (context.command.material !== 'Assets/Materials/Mat_Panel.mat') {
                    return null;
                }
                return { kind: 'texture', texture, sampler: null };
            },
        });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));
        const frame = runtime.commitToViewport(canvas.width, canvas.height);

        const imageCommand = frame.commands.find((command) => command.kind === 'image');
        expect(imageCommand, 'image command emitted for a material-only image').toBeDefined();

        renderer.render(frame);

        expect(seenMaterials, 'resolver received the authored material').toContain(
            'Assets/Materials/Mat_Panel.mat',
        );

        const box = runtime.getLayoutBox(runtime.getBoundWidget('panel')!);
        const scale = resolveCanvasScale(
            {
                referenceWidth: 400,
                referenceHeight: 200,
                scaleMode: 'fixed',
                matchBias: 0.5,
            },
            canvas.width,
            canvas.height,
        );
        const pixels = new Uint8Array(canvas.width * canvas.height * 4);
        gl.readPixels(0, 0, canvas.width, canvas.height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
        const cx = (box.x + box.width / 2) * scale.scaleX + scale.offsetX;
        const cy = (box.y + box.height / 2) * scale.scaleY + scale.offsetY;
        const index =
            (Math.max(0, Math.min(canvas.height - 1, Math.round(canvas.height - 1 - cy))) *
                canvas.width +
                Math.max(0, Math.min(canvas.width - 1, Math.round(cx)))) *
            4;

        expect(pixels[index + 1], 'material texture green channel').toBeGreaterThan(200);
        expect(pixels[index], 'material texture red channel').toBeLessThan(60);

        renderer.dispose();
        runtime.dispose();
        gl.deleteTexture(texture);
    });

    it('skips the centre region when fillCenter is disabled', () => {
        const canvas = (window as any).createTestCanvas(800, 450) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true,
            antialias: false,
            premultipliedAlpha: true,
            preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;

        const assetJson = JSON.stringify({
            id: 'ui.nine-slice-hollow',
            name: 'nine-slice-hollow',
            version: 1,
            canvas: {
                referenceWidth: 400,
                referenceHeight: 200,
                scaleMode: 'fixed',
                matchBias: 0.5,
            },
            bindings: { root: 'root', frame: 'frame' },
            root: {
                role: 'root',
                key: 'root',
                enabled: true,
                interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [
                    {
                        role: 'image',
                        key: 'frame',
                        enabled: true,
                        interactive: false,
                        layout: {
                            position: 'absolute',
                            inset: { left: 0, top: 0 },
                            width: 300,
                            height: 150,
                        },
                        image: {
                            source: {
                                kind: 'texture',
                                resourceId: 'test:frame',
                                width: 3,
                                height: 3,
                            },
                            border: 1,
                            fillCenter: false,
                            sampling: 'nearest',
                        },
                        children: [],
                    },
                ],
            },
        });

        const texture = gl.createTexture()!;
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.texImage2D(
            gl.TEXTURE_2D,
            0,
            gl.RGBA,
            1,
            1,
            0,
            gl.RGBA,
            gl.UNSIGNED_BYTE,
            new Uint8Array([255, 255, 255, 255]),
        );
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);

        const renderer = new WebGL2UIRenderer({
            gl,
            resolveImageResource: () => ({ kind: 'texture', texture, sampler: null }),
        });
        const runtime = new UIRuntime();
        runtime.loadFromAsset(deserializeUIAsset(assetJson));

        renderer.render(runtime.commitToViewport(canvas.width, canvas.height));

        expect(renderer.getStats().imageCount, 'eight regions drawn').toBe(8);

        renderer.dispose();
        runtime.dispose();
        gl.deleteTexture(texture);
    });

    /**
     * Checkbox document as the Editor's `checkbox` preset authors it: a flow row
     * with `alignItems: center`, a box child that owns an anchored mark child, and
     * a text label. `corrupted` reproduces the shape found in real project files
     * (Assets/UI-test.ui.json) where a multi-select drag baked root-space pixel
     * insets into every composite child, so the children are absolute instead of
     * flowing. Both shapes must render a visible tick.
     */
    const createCheckboxAssetJson = (corrupted: boolean): string => {
        const rootInset = corrupted ? { left: 40, top: 40 } : { left: 40, top: 40 };
        const boxLayout = corrupted
            ? {
                position: 'absolute',
                inset: { left: 40, top: 40 },
                width: 20,
                height: 20,
                display: 'overlay',
                shrink: 0,
            }
            : { width: 20, height: 20, display: 'overlay', shrink: 0 };
        const markLayout = corrupted
            ? {
                position: 'absolute',
                inset: { left: 43, top: 43 },
                width: 14,
                height: 14,
                anchor: { x: 0.5, y: 0.5, maxX: 0.5, maxY: 0.5, pivotX: 0.5, pivotY: 0.5 },
            }
            : {
                position: 'absolute',
                width: 14,
                height: 14,
                anchor: { x: 0.5, y: 0.5, maxX: 0.5, maxY: 0.5, pivotX: 0.5, pivotY: 0.5 },
            };
        const labelLayout = corrupted
            ? { position: 'absolute', inset: { left: 68, top: 40 }, width: 'content', height: 'content' }
            : { width: 'content', height: 'content' };
        return JSON.stringify({
            id: 'ui.checkbox-preset',
            name: 'checkbox-preset',
            version: 1,
            canvas: {
                referenceWidth: 400,
                referenceHeight: 200,
                scaleMode: 'match-width',
                matchBias: 0.5,
            },
            bindings: {
                root: 'root',
                'widget-1': 'widget-1',
                'widget-1-box': 'widget-1-box',
                'widget-1-mark': 'widget-1-mark',
                'widget-1-label': 'widget-1-label',
            },
            root: {
                role: 'root',
                key: 'root',
                enabled: true,
                interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [
                    {
                        role: 'custom:checkbox',
                        key: 'widget-1',
                        enabled: true,
                        interactive: true,
                        controller: 'checkbox-toggle',
                        props: {
                            isOn: true,
                            indeterminate: false,
                            markStyle: 'check',
                            boxSize: 20,
                            markSize: 14,
                            markWeight: 2,
                            markColor: '#ffffffff',
                            boxKey: 'widget-1-box',
                            markKey: 'widget-1-mark',
                            labelKey: 'widget-1-label',
                            states: {
                                normal: '#334155ff',
                                hover: '#475569ff',
                                checked: '#0a74daff',
                                disabled: '#1e293bff',
                            },
                            transition: 'color',
                            transitionDuration: 0,
                            labelPosition: 'right',
                            labelGap: 8,
                        },
                        layout: {
                            width: 'content',
                            height: 'content',
                            direction: 'row',
                            alignItems: 'center',
                            gap: 8,
                            position: 'absolute',
                            inset: rootInset,
                        },
                        children: [
                            {
                                role: 'custom:checkbox-box',
                                key: 'widget-1-box',
                                enabled: true,
                                interactive: false,
                                layout: boxLayout,
                                style: {
                                    background: '#334155ff',
                                    borderColor: '#475569ff',
                                    borderWidth: 1,
                                    radius: 4,
                                },
                                children: [
                                    {
                                        role: 'custom:checkbox-mark',
                                        key: 'widget-1-mark',
                                        enabled: false,
                                        interactive: false,
                                        layout: markLayout,
                                        style: { background: '#00000000', radius: 2 },
                                        children: [],
                                    },
                                ],
                            },
                            {
                                role: 'text',
                                key: 'widget-1-label',
                                enabled: true,
                                interactive: false,
                                layout: labelLayout,
                                style: { color: '#e2e8f0ff' },
                                text: { value: 'Checkbox', size: 16 },
                                children: [],
                            },
                        ],
                    },
                ],
            },
        });
    };

    type PixelProbe = {
        readonly canvas: HTMLCanvasElement;
        readonly scale: { scaleX: number; scaleY: number; offsetX: number; offsetY: number };
        /** Samples a reference-space point and returns premultiplied RGBA. */
        readonly sample: (refX: number, refY: number) => readonly [number, number, number, number];
    };

    const createPixelProbe = (
        gl: WebGL2RenderingContext,
        canvas: HTMLCanvasElement,
        asset: { canvas: Record<string, number> },
    ): PixelProbe => {
        const scale = resolveCanvasScale(asset.canvas as never, canvas.width, canvas.height);
        const pixels = new Uint8Array(canvas.width * canvas.height * 4);
        gl.readPixels(0, 0, canvas.width, canvas.height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
        const sample = (refX: number, refY: number): readonly [number, number, number, number] => {
            const devX = refX * scale.scaleX + scale.offsetX;
            const devY = refY * scale.scaleY + scale.offsetY;
            const x = Math.max(0, Math.min(canvas.width - 1, Math.round(devX)));
            // Framebuffer origin is bottom-left; layout origin is top-left.
            const y = Math.max(0, Math.min(canvas.height - 1, Math.round(canvas.height - 1 - devY)));
            const idx = (y * canvas.width + x) * 4;
            return [pixels[idx], pixels[idx + 1], pixels[idx + 2], pixels[idx + 3]];
        };
        return { canvas, scale, sample };
    };

    /** Midpoints of every emitted stroke segment, in reference space. */
    const strokeSegmentMidpoints = (
        command: StrokeRenderCommand,
    ): readonly { x: number; y: number }[] => {
        const midpoints: { x: number; y: number }[] = [];
        for (const stroke of command.strokes) {
            for (let index = 0; index < stroke.points.length - 1; index += 1) {
                const p0 = stroke.points[index];
                const p1 = stroke.points[index + 1];
                midpoints.push({
                    x: command.x + ((p0[0] + p1[0]) / 2) * command.width,
                    y: command.y + ((p0[1] + p1[1]) / 2) * command.height,
                });
            }
        }
        return midpoints;
    };

    const loadCheckboxRuntime = (corrupted: boolean) => {
        const runtime = new UIRuntime();
        runtime.registry.register(checkboxToggleController);
        const asset = deserializeUIAsset(createCheckboxAssetJson(corrupted));
        runtime.loadFromAsset(asset);
        return { runtime, asset };
    };

    it('keeps the checkbox box and label cross-axis aligned and gap-correct', () => {
        const { runtime } = loadCheckboxRuntime(false);
        // Layout is resolved lazily; the controller also applies boxSize/markSize on mount.
        runtime.commit();
        const boxRect = runtime.getLayoutBox(runtime.getBoundWidget('widget-1-box')!);
        const labelRect = runtime.getLayoutBox(runtime.getBoundWidget('widget-1-label')!);
        const markRect = runtime.getLayoutBox(runtime.getBoundWidget('widget-1-mark')!);

        expect(boxRect.width, 'box width follows boxSize').toBe(20);
        expect(markRect.width, 'mark width follows markSize').toBe(14);

        // alignItems: center must put both children on the same cross-axis centre.
        expect(
            Math.abs((boxRect.y + boxRect.height / 2) - (labelRect.y + labelRect.height / 2)),
            `box center y ${boxRect.y + boxRect.height / 2} vs label center y ${labelRect.y + labelRect.height / 2}`,
        ).toBeLessThan(0.5);
        // The authored labelGap must hold between the box and the label.
        expect(labelRect.x - (boxRect.x + boxRect.width), 'label gap').toBeCloseTo(8, 0);
        // The mark is centered inside the box by controller contract.
        expect(markRect.x, 'mark left').toBeGreaterThanOrEqual(boxRect.x - 0.001);
        expect(markRect.x + markRect.width, 'mark right').toBeLessThanOrEqual(boxRect.x + boxRect.width + 0.001);
        expect(markRect.y, 'mark top').toBeGreaterThanOrEqual(boxRect.y - 0.001);
        expect(markRect.y + markRect.height, 'mark bottom').toBeLessThanOrEqual(boxRect.y + boxRect.height + 0.001);

        runtime.dispose();
    });

    it('renders a checked editor-authored checkbox tick onto real pixels', () => {
        const canvas = (window as any).createTestCanvas(800, 400) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true,
            antialias: false,
            premultipliedAlpha: true,
            preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;
        const renderer = new WebGL2UIRenderer({ gl });
        const { runtime, asset } = loadCheckboxRuntime(false);

        // The canvas must be a scaled view of the reference resolution, otherwise
        // the test could not catch a stroke that ignores the camera transform.
        expect(
            resolveCanvasScale(asset.canvas, canvas.width, canvas.height).scaleX,
            'reference space is scaled onto the canvas',
        ).toBe(2);

        const frame = runtime.commitToViewport(canvas.width, canvas.height);
        const strokeCommands = frame.commands.filter(
            (command) => command.kind === 'stroke',
        ) as StrokeRenderCommand[];
        expect(strokeCommands.length, 'checked mark emits a stroke command').toBe(1);
        const strokeCommand = strokeCommands[0]!;
        expect(strokeCommand.transform, 'stroke command carries the canvas transform').toBeDefined();
        expect(strokeCommand.strokes[0]!.points.length, 'check polyline has three points').toBe(3);

        renderer.render(frame);

        const probe = createPixelProbe(gl, canvas, asset);
        const midpoints = strokeSegmentMidpoints(strokeCommand);
        expect(midpoints.length).toBe(2);
        for (const point of midpoints) {
            const [r, g, b, a] = probe.sample(point.x, point.y);
            expect(
                a,
                `tick pixel at (${point.x.toFixed(2)}, ${point.y.toFixed(2)}) = rgba(${r}, ${g}, ${b}, ${a})`,
            ).toBeGreaterThan(160);
            expect(r, 'tick red channel (markColor #ffffff)').toBeGreaterThan(140);
            expect(g, 'tick green channel (markColor #ffffff)').toBeGreaterThan(140);
            expect(b, 'tick blue channel (markColor #ffffff)').toBeGreaterThan(140);
        }

        // Inside the box but clear of the polyline must stay the checked blue.
        const boxRect = runtime.getLayoutBox(runtime.getBoundWidget('widget-1-box')!);
        const [fr, , fb] = probe.sample(boxRect.x + 2.5, boxRect.y + boxRect.height / 2);
        expect(fb, 'box fill blue channel').toBeGreaterThan(fr);

        renderer.dispose();
        runtime.dispose();
    });

    it('renders the checkbox tick inside the box for legacy documents with baked child insets', () => {
        const canvas = (window as any).createTestCanvas(800, 400) as HTMLCanvasElement;
        const gl = (window as any).createWebGLContext(canvas, {
            alpha: true,
            antialias: false,
            premultipliedAlpha: true,
            preserveDrawingBuffer: true,
        }) as WebGL2RenderingContext;
        const renderer = new WebGL2UIRenderer({ gl });
        const { runtime, asset } = loadCheckboxRuntime(true);

        const frame = runtime.commitToViewport(canvas.width, canvas.height);
        const boxRect = runtime.getLayoutBox(runtime.getBoundWidget('widget-1-box')!);
        const markRect = runtime.getLayoutBox(runtime.getBoundWidget('widget-1-mark')!);
        // The mark must not be flung out of the box by the stale baked insets.
        expect(markRect.x, 'legacy mark left stays in the box').toBeGreaterThanOrEqual(boxRect.x - 0.001);
        expect(markRect.x + markRect.width, 'legacy mark right stays in the box').toBeLessThanOrEqual(boxRect.x + boxRect.width + 0.001);
        expect(markRect.y, 'legacy mark top stays in the box').toBeGreaterThanOrEqual(boxRect.y - 0.001);
        expect(markRect.y + markRect.height, 'legacy mark bottom stays in the box').toBeLessThanOrEqual(boxRect.y + boxRect.height + 0.001);

        const strokeCommands = frame.commands.filter(
            (command) => command.kind === 'stroke',
        ) as StrokeRenderCommand[];
        expect(strokeCommands.length, 'legacy checked mark emits a stroke command').toBe(1);

        renderer.render(frame);
        const probe = createPixelProbe(gl, canvas, asset);
        for (const point of strokeSegmentMidpoints(strokeCommands[0]!)) {
            expect(probe.sample(point.x, point.y)[3], 'legacy tick pixel alpha').toBeGreaterThan(160);
        }

        renderer.dispose();
        runtime.dispose();
    });

    it('renders the content-sized editor preset tick inside its box through the editor camera path', () => {
		// Mirrors the Editor's authored document: a content-sized checkbox row stack
		// nested in the same container chain the presets emit, rendered with the
		// Editor's own frame composition (commit + manual camera transform) instead
		// of resolveCanvasScale, because that is the path the UI workspace takes.
		const canvas = (window as any).createTestCanvas(800, 400) as HTMLCanvasElement;
		const gl = (window as any).createWebGLContext(canvas, {
			alpha: true,
			antialias: false,
			premultipliedAlpha: true,
			preserveDrawingBuffer: true,
		}) as WebGL2RenderingContext;
		const renderer = new WebGL2UIRenderer({ gl });

		const asset = deserializeUIAsset(
			JSON.stringify({
				id: 'ui.preset-nested',
				name: 'preset-nested',
				version: 1,
				canvas: {
					referenceWidth: 1920,
					referenceHeight: 1080,
					scaleMode: 'match-width-or-height',
					matchBias: 0.5,
				},
				bindings: {
					root: 'root',
					'cnt-1': 'cnt-1',
					'cnt-2': 'cnt-2',
					'chk-4': 'chk-4',
					'chk-4-box': 'chk-4-box',
					'chk-4-mark': 'chk-4-mark',
					'chk-4-label': 'chk-4-label',
				},
				root: {
					role: 'root',
					key: 'root',
					enabled: true,
					interactive: false,
					layout: { display: 'overlay', width: '100%', height: '100%' },
					children: [
						{
							role: 'container',
							key: 'cnt-1',
							enabled: true,
							interactive: false,
							layout: {
								width: 672,
								height: 'content',
								direction: 'row',
								gap: 8,
								padding: 8,
								justifyContent: 'space-evenly',
								alignItems: 'start',
								flexWrap: 'wrap',
							},
							children: [
								{
									role: 'container',
									key: 'cnt-2',
									enabled: true,
									interactive: false,
									layout: { width: 240, height: 'content', direction: 'column', gap: 8, padding: 8 },
									children: [
										{
											role: 'custom:checkbox',
											key: 'chk-4',
											enabled: true,
											interactive: true,
											controller: 'checkbox-toggle',
											props: {
												isOn: true,
												indeterminate: false,
												markStyle: 'check',
												boxSize: 20,
												markSize: 14,
												markWeight: 2,
												markColor: '#ffffffff',
												boxKey: 'chk-4-box',
												markKey: 'chk-4-mark',
												labelKey: 'chk-4-label',
												states: {
													normal: '#334155ff',
													hover: '#475569ff',
													checked: '#0a74daff',
													disabled: '#1e293bff',
												},
												transition: 'color',
												transitionDuration: 0.15,
												zoomScale: 0.9,
												labelPosition: 'right',
												labelGap: 8,
											},
											layout: {
												display: 'stack',
												width: 'content',
												height: 'content',
												direction: 'row',
												alignItems: 'center',
												gap: 8,
											},
											children: [
												{
													role: 'custom:checkbox-box',
													key: 'chk-4-box',
													enabled: true,
													interactive: false,
													layout: { width: 20, height: 20, display: 'overlay', shrink: 0 },
													style: {
														background: '#334155ff',
														borderColor: '#475569ff',
														borderWidth: 1,
														radius: 4,
													},
													children: [
														{
															role: 'custom:checkbox-mark',
															key: 'chk-4-mark',
															enabled: true,
															interactive: false,
															layout: {
																position: 'absolute',
																width: 14,
																height: 14,
																anchor: {
																	x: 0.5,
																	y: 0.5,
																	maxX: 0.5,
																	maxY: 0.5,
																	pivotX: 0.5,
																	pivotY: 0.5,
																},
															},
															style: { background: '#00000000', radius: 2 },
															children: [],
														},
													],
												},
												{
													role: 'text',
													key: 'chk-4-label',
													enabled: true,
													interactive: false,
													layout: { width: 'content', height: 'content' },
													style: { color: '#e2e8f0ff' },
													text: {
														value: 'Checkbox',
														size: 16,
														family: 'Inter',
														lineHeight: 20.8,
													},
													children: [],
												},
											],
										},
									],
								},
							],
						},
					],
				},
			}),
		);

		const runtime = new UIRuntime();
		runtime.registry.register(checkboxToggleController);
		runtime.loadFromAsset(asset);

		// Exactly the Editor's renderPreview composition: commit at reference
		// resolution, then attach one camera transform to every command.
		const frame = runtime.commit();
		const dpr = window.devicePixelRatio || 1;
		const scale = 1 * dpr;
		const cameraTransform = [scale, 0, 0, scale, 0, 0] as const;
		renderer.render({
			viewportWidth: canvas.width,
			viewportHeight: canvas.height,
			metrics: frame.metrics,
			commands: frame.commands.map((cmd) => ({ ...cmd, transform: cameraTransform }) as never),
		} as never);

		const boxRect = runtime.getLayoutBox(runtime.getBoundWidget('chk-4-box')!);
		const markRect = runtime.getLayoutBox(runtime.getBoundWidget('chk-4-mark')!);
		const strokeCommands = frame.commands.filter(
			(command) => command.kind === 'stroke',
		) as StrokeRenderCommand[];
		expect(strokeCommands.length, 'checked mark emits one stroke command').toBe(1);
		const strokeCommand = strokeCommands[0]!;

		// The tick must stay authored at mark size; anything larger is the
		// overscale bug and the strip escapes the box.
		expect(strokeCommand.width, 'stroke space is the mark rect, not an ancestor').toBeCloseTo(14, 2);
		expect(strokeCommand.height).toBeCloseTo(14, 2);
		expect(markRect.width).toBeCloseTo(14, 2);
		expect(strokeCommand.x).toBeGreaterThanOrEqual(boxRect.x - 0.01);
		expect(strokeCommand.y).toBeGreaterThanOrEqual(boxRect.y - 0.01);
		expect(strokeCommand.x + strokeCommand.width).toBeLessThanOrEqual(
			boxRect.x + boxRect.width + 0.01,
		);
		expect(strokeCommand.y + strokeCommand.height).toBeLessThanOrEqual(
			boxRect.y + boxRect.height + 0.01,
		);

		// Sample in the same space the transform was built in.
		const pixels = new Uint8Array(canvas.width * canvas.height * 4);
		gl.readPixels(0, 0, canvas.width, canvas.height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
		const alphaAt = (refX: number, refY: number): number => {
			const x = Math.max(0, Math.min(canvas.width - 1, Math.round(refX * scale)));
			const y = Math.max(0, Math.min(canvas.height - 1, Math.round(canvas.height - 1 - refY * scale)));
			return pixels[(y * canvas.width + x) * 4 + 3] ?? 0;
		};

		for (const point of strokeSegmentMidpoints(strokeCommand)) {
			expect(
				alphaAt(point.x, point.y),
				`tick pixel drawn at (${point.x.toFixed(2)}, ${point.y.toFixed(2)})`,
			).toBeGreaterThan(160);
		}
		// Nothing may be painted far outside the box: the giant-strip symptom.
		const reach = Math.max(boxRect.width, boxRect.height) * 4;
		for (const [dx, dy] of [
			[reach, 0],
			[-reach, 0],
			[0, -reach],
			[reach, -reach],
		] as const) {
			expect(
				alphaAt(boxRect.x + boxRect.width / 2 + dx, boxRect.y + boxRect.height / 2 + dy),
				`no tick ink ${dx},${dy} px outside the box`,
			).toBe(0);
		}

		renderer.dispose();
		runtime.dispose();
	});
});
