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
});
