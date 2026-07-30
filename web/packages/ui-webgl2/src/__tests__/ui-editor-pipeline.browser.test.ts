import { describe, expect, it } from 'vitest';
import { UIRuntime, deserializeUIAsset } from '@axrone/ui';
import { resolveCanvasScale } from '@axrone/ui/layout';
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
});
