import { describe, expect, it, vi } from 'vitest';
import { InvalidUIAssetError, UIRuntime } from '../index';
import type { UIAsset } from '../types/ui-asset';
import type { WidgetSnapshot } from '../types/render-frame';

const BUTTON_KEY = 'play-button';

const createButtonSnapshot = (overrides: Partial<WidgetSnapshot> = {}): WidgetSnapshot =>
    ({
        role: 'button',
        key: BUTTON_KEY,
        enabled: true,
        interactive: true,
        layout: { width: 400, height: 1080 },
        style: { background: '#3366ffff' },
        children: [],
        ...overrides,
    }) as WidgetSnapshot;

const createAsset = (overrides: Partial<UIAsset> = {}): UIAsset => ({
    id: 'ui.test-screen',
    name: 'Test Screen',
    version: 1,
    canvas: {
        referenceWidth: 1920,
        referenceHeight: 1080,
        scaleMode: 'match-width',
        matchBias: 0,
    },
    root: {
        role: 'root',
        enabled: true,
        interactive: false,
        layout: { display: 'overlay', width: '100%', height: '100%' },
        children: [createButtonSnapshot()],
    } as WidgetSnapshot,
    ...overrides,
});

describe('@axrone/ui asset runtime integration', () => {
    describe('commitToViewport', () => {
        it('keeps commands at reference resolution and carries the canvas scale in the transform', () => {
            const runtime = new UIRuntime();
            runtime.loadFromAsset(createAsset());

            // match-width: 1920x1080 ref -> 960x1080 actual => scale 0.5, offsetY 270
            const frame = runtime.commitToViewport(960, 1080);

            expect(frame.viewportWidth).toBe(960);
            expect(frame.viewportHeight).toBe(1080);

            const quad = frame.commands.find((command) => command.kind === 'quad');
            expect(quad).toBeDefined();
            if (quad && quad.kind === 'quad') {
                // Geometry stays in reference space; the renderer applies the transform once.
                expect(quad.x).toBeCloseTo(0);
                expect(quad.y).toBeCloseTo(0);
                expect(quad.width).toBeCloseTo(400);
                expect(quad.height).toBeCloseTo(1080);
                expect(quad.transform).toBeDefined();
                const [a, b, c, d, e, f] = quad.transform!;
                expect(a).toBeCloseTo(0.5);
                expect(b).toBe(0);
                expect(c).toBe(0);
                expect(d).toBeCloseTo(0.5);
                expect(e).toBeCloseTo(0);
                expect(f).toBeCloseTo(270);
            }
        });
    });

    describe('dispatchViewportInput', () => {
        it('maps viewport pointer coordinates into the reference canvas for hit-testing', () => {
            const runtime = new UIRuntime();
            runtime.loadFromAsset(
                createAsset({ bindings: { play: BUTTON_KEY } })
            );

            const button = runtime.getBoundWidget('play');
            expect(button).not.toBeNull();

            const pointerDown = vi.fn(() => true);
            runtime.updateWidget(button!, { handlers: { pointerDown } });
            runtime.commitToViewport(960, 1080);

            // Viewport (100, 500) -> canvas (200, 460): inside the 400x1080 button
            expect(
                runtime.dispatchViewportInput({ type: 'pointer', phase: 'down', x: 100, y: 500 })
            ).toBe(true);
            expect(pointerDown).toHaveBeenCalledTimes(1);

            // Viewport (500, 500) -> canvas (1000, 460): inside canvas, outside button
            runtime.dispatchViewportInput({ type: 'pointer', phase: 'down', x: 500, y: 500 });
            expect(pointerDown).toHaveBeenCalledTimes(1);
        });

        it('does not hit widgets from clicks inside the letterbox band', () => {
            const runtime = new UIRuntime();
            runtime.loadFromAsset(
                createAsset({ bindings: { play: BUTTON_KEY } })
            );

            const pointerDown = vi.fn(() => true);
            runtime.updateWidget(runtime.getBoundWidget('play')!, { handlers: { pointerDown } });
            runtime.commitToViewport(960, 1080);

            // Content band spans y in [270, 810]; y=100 is letterbox above it.
            runtime.dispatchViewportInput({ type: 'pointer', phase: 'down', x: 100, y: 100 });
            expect(pointerDown).not.toHaveBeenCalled();
        });

        it('passes pointer input through unchanged when no asset is loaded', () => {
            const runtime = new UIRuntime({ width: 400, height: 200 });
            const pointerDown = vi.fn(() => true);
            const button = runtime.createWidget({
                interactive: true,
                layout: { width: 100, height: 100 },
                handlers: { pointerDown },
            });
            runtime.appendChild(runtime.root, button);
            runtime.commit();

            expect(
                runtime.dispatchViewportInput({ type: 'pointer', phase: 'down', x: 50, y: 50 })
            ).toBe(true);
            expect(pointerDown).toHaveBeenCalledTimes(1);
        });
    });

    describe('bindings', () => {
        it('resolves bindings by widget key and exposes the binding table', () => {
            const runtime = new UIRuntime();
            runtime.loadFromAsset(
                createAsset({ bindings: { play: BUTTON_KEY } })
            );

            const table = runtime.getBindingTable();
            expect(table.size).toBe(1);
            expect(runtime.getBoundWidget('play')).toBe(table.get('play'));
            expect(runtime.getBoundWidget('unknown')).toBeNull();
        });

        it('throws InvalidUIAssetError for bindings targeting missing keys', () => {
            const runtime = new UIRuntime();
            expect(() =>
                runtime.loadFromAsset(createAsset({ bindings: { play: 'no-such-key' } }))
            ).toThrowError(InvalidUIAssetError);
        });

        it('throws InvalidUIAssetError for bindings targeting ambiguous keys', () => {
            const runtime = new UIRuntime();
            const asset = createAsset({
                bindings: { play: BUTTON_KEY },
                root: {
                    role: 'root',
                    enabled: true,
                    interactive: false,
                    layout: { display: 'overlay', width: '100%', height: '100%' },
                    children: [createButtonSnapshot(), createButtonSnapshot()],
                } as WidgetSnapshot,
            });
            expect(() => runtime.loadFromAsset(asset)).toThrowError(InvalidUIAssetError);
        });

        it('clears the binding table when the tree is cleared', () => {
            const runtime = new UIRuntime();
            runtime.loadFromAsset(
                createAsset({ bindings: { play: BUTTON_KEY } })
            );
            expect(runtime.getBindingTable().size).toBe(1);

            runtime.clear();
            expect(runtime.getBindingTable().size).toBe(0);
            expect(runtime.getBoundWidget('play')).toBeNull();
        });
    });
});
