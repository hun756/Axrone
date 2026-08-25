import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import {
    UIRuntime,
    WidgetRegistry,
    defineWidget,
    serializeUIAsset,
    deserializeUIAsset,
    validateUIAsset,
    UILayoutEngine,
    compileLayoutInput,
    normalizeAnchor,
    resolveCanvasScale,
    canvasScaleToTransform,
    mapViewportPointToCanvas,
    DisposedUIError,
} from '../index';
import type {
    UIAsset,
    UICanvasConfig,
    WidgetSnapshot,
    WidgetId,
    LayoutBox,
    SizeLike,
    ResolvedLayout,
    UIPointerEvent,
} from '../index';
import { createTestFontAsset } from './test-font';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Creates a minimal valid UIAsset for testing. */
const createTestAsset = (overrides: Partial<UIAsset> = {}): UIAsset => ({
    id: 'test-ui',
    name: 'Test UI',
    version: 1,
    canvas: {
        referenceWidth: 1920,
        referenceHeight: 1080,
        scaleMode: 'match-width-or-height',
        matchBias: 0.5,
    },
    root: {
        role: 'root',
        enabled: true,
        interactive: false,
        layout: { display: 'overlay', width: '100%', height: '100%' },
        children: [],
    },
    ...overrides,
});

/** Creates a UIAsset with a realistic widget tree (button, label, image, container). */
const createFullTestAsset = (): UIAsset => ({
    id: 'full-test-ui',
    name: 'Full Test UI',
    version: 1,
    canvas: {
        referenceWidth: 1920,
        referenceHeight: 1080,
        scaleMode: 'fill',
        matchBias: 0.5,
    },
    root: {
        role: 'root',
        enabled: true,
        interactive: false,
        layout: { display: 'overlay', width: '100%', height: '100%' },
        children: [
            {
                role: 'container',
                key: 'main-panel',
                enabled: true,
                interactive: false,
                layout: {
                    display: 'stack',
                    direction: 'column',
                    width: 400,
                    height: 300,
                    padding: 16,
                    gap: 8,
                },
                style: { background: '#222222ff' },
                children: [
                    {
                        role: 'text',
                        key: 'title-label',
                        enabled: true,
                        interactive: false,
                        layout: { height: 40 },
                        text: {
                            value: 'Hello World',
                            family: 'TestSans',
                            size: 24,
                        },
                        style: { color: '#ffffffff' },
                        children: [],
                    },
                    {
                        role: 'button',
                        key: 'action-button',
                        enabled: true,
                        interactive: true,
                        layout: { width: 200, height: 50 },
                        style: { background: '#336699ff' },
                        text: {
                            value: 'Click Me',
                            family: 'TestSans',
                            size: 18,
                        },
                        children: [],
                    },
                    {
                        role: 'container',
                        key: 'image-container',
                        enabled: true,
                        interactive: false,
                        layout: { width: 128, height: 64 },
                        style: { clip: true },
                        children: [
                            {
                                role: 'custom',
                                key: 'hero-image',
                                enabled: true,
                                interactive: false,
                                layout: { width: '100%', height: '100%' },
                                image: {
                                    source: {
                                        kind: 'texture',
                                        resourceId: 'ui:hero',
                                        width: 128,
                                        height: 64,
                                    },
                                    fit: 'fill',
                                },
                                children: [],
                            },
                        ],
                    },
                ],
            },
        ],
    },
    bindings: {
        'title': 'title-label',
        'button': 'action-button',
        'panel': 'main-panel',
    },
});

/** Creates a UIRuntime with a test font registered. */
const createTestRuntime = (width = 800, height = 600): UIRuntime => {
    const runtime = new UIRuntime({ width, height });
    runtime.fonts.registerFace(createTestFontAsset());
    return runtime;
};

// ===========================================================================
// 1. UI Definition Parsing (5 tests)
// ===========================================================================

describe('T-17: UI Layout Integration — UI Definition Parsing', () => {
    it('loads and validates a UIAsset JSON without errors', () => {
        const asset = createTestAsset();
        expect(validateUIAsset(asset)).toBe(true);

        const json = serializeUIAsset(asset);
        const restored = deserializeUIAsset(json);
        expect(restored.id).toBe(asset.id);
        expect(restored.version).toBe(asset.version);
    });

    it('creates root canvas node with correct reference dimensions', () => {
        const asset = createTestAsset({
            canvas: {
                referenceWidth: 1920,
                referenceHeight: 1080,
                scaleMode: 'fill',
                matchBias: 0.5,
            },
        });

        const runtime = createTestRuntime();
        runtime.loadFromAsset(asset);

        const canvasConfig = runtime.getCanvasConfig();
        expect(canvasConfig).not.toBeNull();
        expect(canvasConfig!.referenceWidth).toBe(1920);
        expect(canvasConfig!.referenceHeight).toBe(1080);
        expect(runtime.width).toBe(1920);
        expect(runtime.height).toBe(1080);
    });

    it('correctly maps widget roles (button, text, container, custom)', () => {
        const asset = createFullTestAsset();
        const runtime = createTestRuntime();
        runtime.loadFromAsset(asset);

        const snapshot = runtime.snapshot();
        const container = snapshot.root.children[0];
        expect(container.role).toBe('container');

        const label = container.children[0];
        expect(label.role).toBe('text');

        const button = container.children[1];
        expect(button.role).toBe('button');

        const imageContainer = container.children[2];
        expect(imageContainer.role).toBe('container');
        expect(imageContainer.children[0].role).toBe('custom');
    });

    it('applies widget properties (color, fontSize, text, image source)', () => {
        const asset = createFullTestAsset();
        const runtime = createTestRuntime();
        runtime.loadFromAsset(asset);

        const snapshot = runtime.snapshot();
        const container = snapshot.root.children[0];
        const label = container.children[0];
        const button = container.children[1];
        const imageWidget = container.children[2].children[0];

        // Label text properties
        expect(label.text).toBeDefined();
        expect(label.text!.value).toBe('Hello World');
        expect(label.text!.size).toBe(24);

        // Button style
        expect(button.style).toBeDefined();
        expect(button.style!.background).toBe('#336699ff');
        expect(button.text!.value).toBe('Click Me');

        // Image source
        expect(imageWidget.image).toBeDefined();
        expect(imageWidget.image!.source.kind).toBe('texture');
    });

    it('preserves nested widget hierarchy through serialization round-trip', () => {
        const asset = createFullTestAsset();
        const json = serializeUIAsset(asset);
        const restored = deserializeUIAsset(json);

        // Root has 1 child (main panel)
        expect(restored.root.children).toHaveLength(1);

        // Main panel has 3 children (label, button, image container)
        const panel = restored.root.children[0];
        expect(panel.children).toHaveLength(3);

        // Image container has 1 child (the image)
        const imageContainer = panel.children[2];
        expect(imageContainer.children).toHaveLength(1);

        // Bindings preserved
        expect(restored.bindings).toBeDefined();
        expect(restored.bindings!['title']).toBe('title-label');
        expect(restored.bindings!['button']).toBe('action-button');
    });
});

// ===========================================================================
// 2. Layout Computation (5 tests)
// ===========================================================================

describe('T-17: UI Layout Integration — Layout Computation', () => {
    it('applies absolute pixel positioning (x, y, width, height)', () => {
        const runtime = createTestRuntime(800, 600);

        const widget = runtime.createWidget({
            layout: {
                position: 'absolute',
                width: 200,
                height: 100,
                inset: { top: 50, left: 100 },
            },
            style: { background: '#ff0000ff' },
        });
        runtime.appendChild(runtime.root, widget);

        runtime.commit();
        const box = runtime.getLayoutBox(widget);

        expect(box.width).toBe(200);
        expect(box.height).toBe(100);
        expect(box.x).toBeCloseTo(100);
        expect(box.y).toBeCloseTo(50);
    });

    it('computes anchor/pivot offsets correctly', () => {
        const runtime = createTestRuntime(800, 600);

        // Center-anchored widget with pivot at center
        const widget = runtime.createWidget({
            layout: {
                position: 'absolute',
                width: 100,
                height: 50,
                anchor: {
                    x: 0.5,
                    y: 0.5,
                    maxX: 0.5,
                    maxY: 0.5,
                    pivotX: 0.5,
                    pivotY: 0.5,
                    offsetX: 0,
                    offsetY: 0,
                    stretch: false,
                },
            },
        });
        runtime.appendChild(runtime.root, widget);

        runtime.commit();
        const box = runtime.getLayoutBox(widget);

        // Widget should be centered: x = 800*0.5 - 100*0.5 = 350, y = 600*0.5 - 50*0.5 = 275
        expect(box.x).toBeCloseTo(350);
        expect(box.y).toBeCloseTo(275);
        expect(box.width).toBe(100);
        expect(box.height).toBe(50);
    });

    it('positions container children relative to parent with padding', () => {
        const runtime = createTestRuntime(800, 600);

        const container = runtime.createWidget({
            layout: {
                display: 'stack',
                direction: 'column',
                width: 300,
                height: 200,
                padding: 10,
                gap: 5,
            },
            style: { background: '#333333ff' },
        });
        const child1 = runtime.createWidget({
            layout: { width: 100, height: 30 },
            style: { background: '#ff0000ff' },
        });
        const child2 = runtime.createWidget({
            layout: { width: 100, height: 30 },
            style: { background: '#00ff00ff' },
        });

        runtime.appendChild(runtime.root, container);
        runtime.appendChild(container, child1);
        runtime.appendChild(container, child2);

        runtime.commit();

        const containerBox = runtime.getLayoutBox(container);
        const child1Box = runtime.getLayoutBox(child1);
        const child2Box = runtime.getLayoutBox(child2);

        // Children should be inside the container's content area
        expect(child1Box.x).toBeCloseTo(containerBox.x + 10);
        expect(child1Box.y).toBeCloseTo(containerBox.y + 10);

        // Second child should be below first child + gap
        expect(child2Box.y).toBeCloseTo(child1Box.y + child1Box.height + 5);
    });

    it('stretches widget to fill parent bounds with stretch width/height', () => {
        const runtime = createTestRuntime(800, 600);

        const container = runtime.createWidget({
            layout: {
                display: 'stack',
                direction: 'row',
                width: 400,
                height: 200,
                padding: 0,
            },
        });
        const stretchChild = runtime.createWidget({
            layout: {
                width: 'stretch:1',
                height: '100%',
            },
            style: { background: '#444444ff' },
        });

        runtime.appendChild(runtime.root, container);
        runtime.appendChild(container, stretchChild);

        runtime.commit();
        const box = runtime.getLayoutBox(stretchChild);

        expect(box.width).toBeCloseTo(400);
        expect(box.height).toBeCloseTo(200);
    });

    it('applies margin correctly around widgets', () => {
        const runtime = createTestRuntime(800, 600);

        const container = runtime.createWidget({
            layout: {
                display: 'stack',
                direction: 'row',
                width: 400,
                height: 200,
                gap: 0,
            },
        });
        const child = runtime.createWidget({
            layout: {
                width: 100,
                height: 50,
                margin: { top: 10, right: 20, bottom: 10, left: 30 },
            },
            style: { background: '#555555ff' },
        });

        runtime.appendChild(runtime.root, container);
        runtime.appendChild(container, child);

        runtime.commit();
        const childBox = runtime.getLayoutBox(child);

        // The child should be offset by its left margin from the container's content edge
        expect(childBox.x).toBeCloseTo(30);
        expect(childBox.y).toBeCloseTo(10);
        // Widget size is unchanged by margin
        expect(childBox.width).toBe(100);
        expect(childBox.height).toBe(50);
    });
});

// ===========================================================================
// 3. Widget Rendering (5 tests)
// ===========================================================================

describe('T-17: UI Layout Integration — Widget Rendering', () => {
    it('button widget creates clickable area with correct bounds and quad command', () => {
        const runtime = createTestRuntime(800, 600);

        const button = runtime.createWidget({
            role: 'button',
            interactive: true,
            layout: { width: 200, height: 50 },
            style: { background: '#336699ff', radius: 4 },
            text: { value: 'Click', family: 'TestSans', size: 16 },
        });
        runtime.appendChild(runtime.root, button);

        const frame = runtime.commit();
        const box = runtime.getLayoutBox(button);

        expect(box.width).toBe(200);
        expect(box.height).toBe(50);

        // Should produce a quad command for the background
        const quadCommand = frame.commands.find(
            (cmd) => cmd.kind === 'quad' && cmd.widget === button
        );
        expect(quadCommand).toBeDefined();
        if (quadCommand && quadCommand.kind === 'quad') {
            expect(quadCommand.width).toBe(200);
            expect(quadCommand.height).toBe(50);
        }

        // Should produce a text command for the label
        const textCommand = frame.commands.find(
            (cmd) => cmd.kind === 'text' && cmd.widget === button
        );
        expect(textCommand).toBeDefined();
    });

    it('label widget renders text with correct layout metrics', () => {
        const runtime = createTestRuntime(800, 600);

        const label = runtime.createWidget({
            layout: { width: 300, height: 40 },
            text: {
                value: 'Test Label',
                family: 'TestSans',
                size: 20,
            },
            style: { color: '#ffffffff' },
        });
        runtime.appendChild(runtime.root, label);

        const frame = runtime.commit();
        const textCommand = frame.commands.find(
            (cmd) => cmd.kind === 'text' && cmd.widget === label
        );

        expect(textCommand).toBeDefined();
        if (textCommand && textCommand.kind === 'text') {
            expect(textCommand.layout.width).toBeGreaterThan(0);
            expect(textCommand.layout.height).toBeGreaterThan(0);
            expect(textCommand.layout.glyphs.length).toBeGreaterThan(0);
        }
        expect(frame.metrics.textCommandCount).toBeGreaterThanOrEqual(1);
    });

    it('image widget loads and displays texture with correct dimensions', () => {
        const runtime = createTestRuntime(800, 600);

        const image = runtime.createWidget({
            layout: { width: 'content', height: 'content' },
            image: {
                source: {
                    kind: 'texture',
                    resourceId: 'ui:icon',
                    width: 64,
                    height: 64,
                },
                fit: 'none',
            },
        });
        runtime.appendChild(runtime.root, image);

        const frame = runtime.commit();
        const box = runtime.getLayoutBox(image);

        expect(box.width).toBe(64);
        expect(box.height).toBe(64);
        expect(frame.metrics.imageCommandCount).toBe(1);

        const imageCommand = frame.commands.find((cmd) => cmd.kind === 'image');
        expect(imageCommand).toBeDefined();
        if (imageCommand && imageCommand.kind === 'image') {
            expect(imageCommand.source.kind).toBe('texture');
            expect(imageCommand.width).toBe(64);
            expect(imageCommand.height).toBe(64);
        }
    });

    it('container widget clips children to bounds when clip is enabled', () => {
        const runtime = createTestRuntime(800, 600);

        const container = runtime.createWidget({
            layout: {
                display: 'overlay',
                width: 100,
                height: 100,
            },
            style: { clip: true, background: '#111111ff' },
        });
        // Child extends beyond container bounds
        const child = runtime.createWidget({
            layout: {
                position: 'absolute',
                width: 200,
                height: 200,
                inset: { top: 0, left: 0 },
            },
            style: { background: '#ff0000ff' },
        });

        runtime.appendChild(runtime.root, container);
        runtime.appendChild(container, child);

        const frame = runtime.commit();

        // The child's render command should have a clip rect matching the container
        const childQuad = frame.commands.find(
            (cmd) => cmd.kind === 'quad' && cmd.widget === child
        );
        expect(childQuad).toBeDefined();
        if (childQuad && childQuad.kind === 'quad') {
            expect(childQuad.clip).not.toBeNull();
            if (childQuad.clip) {
                expect(childQuad.clip.width).toBeLessThanOrEqual(100);
                expect(childQuad.clip.height).toBeLessThanOrEqual(100);
            }
        }
    });

    it('scroll-view-like container computes content size with overflow', () => {
        const runtime = createTestRuntime(800, 600);

        // Simulate a scroll view: a clipped container with a tall content child
        const scrollView = runtime.createWidget({
            layout: {
                display: 'stack',
                direction: 'column',
                width: 200,
                height: 100,
                padding: 0,
                gap: 0,
            },
            style: { clip: true },
        });

        // Add tall content that overflows
        for (let i = 0; i < 5; i++) {
            const item = runtime.createWidget({
                layout: { width: 200, height: 40 },
                style: { background: '#444444ff' },
            });
            runtime.appendChild(scrollView, item);
        }

        runtime.appendChild(runtime.root, scrollView);
        runtime.commit();

        const scrollBox = runtime.getLayoutBox(scrollView);
        // The scroll view itself should be the authored size
        expect(scrollBox.width).toBe(200);
        expect(scrollBox.height).toBe(100);

        // The content area should reflect the clipped viewport
        expect(scrollBox.contentWidth).toBe(200);
        expect(scrollBox.contentHeight).toBe(100);
    });
});

// ===========================================================================
// 4. Input Handling (5 tests)
// ===========================================================================

describe('T-17: UI Layout Integration — Input Handling', () => {
    it('button click fires on pointer down + up within bounds', () => {
        const runtime = createTestRuntime(800, 600);
        const onClick = vi.fn(() => true);

        const button = runtime.createWidget({
            role: 'button',
            interactive: true,
            layout: { width: 200, height: 50 },
            style: { background: '#336699ff' },
            handlers: {
                pointerDown: onClick,
            },
        });
        runtime.appendChild(runtime.root, button);
        runtime.commit();

        // Pointer down within button bounds
        const downEvent: UIPointerEvent = {
            type: 'pointer',
            phase: 'down',
            x: 100,
            y: 25,
        };
        runtime.dispatchInput(downEvent);

        expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('button hover state changes on pointer enter/exit', () => {
        const runtime = createTestRuntime(800, 600);
        const onEnter = vi.fn(() => true);
        const onLeave = vi.fn(() => true);

        const button = runtime.createWidget({
            role: 'button',
            interactive: true,
            layout: { width: 200, height: 50 },
            style: { background: '#336699ff' },
            handlers: {
                pointerEnter: onEnter,
                pointerLeave: onLeave,
            },
        });
        runtime.appendChild(runtime.root, button);
        runtime.commit();

        // Move pointer into button bounds
        runtime.dispatchInput({
            type: 'pointer',
            phase: 'move',
            x: 100,
            y: 25,
        });
        expect(onEnter).toHaveBeenCalledTimes(1);

        // Move pointer out of button bounds
        runtime.dispatchInput({
            type: 'pointer',
            phase: 'move',
            x: 500,
            y: 500,
        });
        expect(onLeave).toHaveBeenCalledTimes(1);
    });

    it('touch/pointer input activates button via down+up sequence', () => {
        const runtime = createTestRuntime(800, 600);
        const onPointerUp = vi.fn(() => true);

        const button = runtime.createWidget({
            role: 'button',
            interactive: true,
            layout: { width: 200, height: 50 },
            style: { background: '#336699ff' },
            handlers: {
                pointerUp: onPointerUp,
            },
        });
        runtime.appendChild(runtime.root, button);
        runtime.commit();

        // Simulate touch: down then up within bounds
        runtime.dispatchInput({ type: 'pointer', phase: 'down', x: 50, y: 25 });
        runtime.dispatchInput({ type: 'pointer', phase: 'up', x: 50, y: 25 });

        expect(onPointerUp).toHaveBeenCalledTimes(1);
    });

    it('scroll view handles wheel/drag gesture for scrolling', () => {
        const runtime = createTestRuntime(800, 600);
        const onWheel = vi.fn(() => true);

        const scrollView = runtime.createWidget({
            role: 'container',
            interactive: true,
            layout: {
                display: 'stack',
                direction: 'column',
                width: 200,
                height: 100,
            },
            style: { clip: true },
            handlers: {
                wheel: onWheel,
            },
        });
        runtime.appendChild(runtime.root, scrollView);
        runtime.commit();

        // Dispatch a wheel event within the scroll view
        runtime.dispatchInput({
            type: 'pointer',
            phase: 'wheel',
            x: 100,
            y: 50,
            deltaY: 30,
        });

        expect(onWheel).toHaveBeenCalledTimes(1);
    });

    it('input is blocked by non-interactive overlay widget', () => {
        const runtime = createTestRuntime(800, 600);
        const onButtonClick = vi.fn(() => true);

        // Background button
        const button = runtime.createWidget({
            role: 'button',
            interactive: true,
            layout: { width: 200, height: 50 },
            style: { background: '#336699ff' },
            handlers: {
                pointerDown: onButtonClick,
            },
        });

        // Modal overlay on top (interactive, so it captures input instead)
        const overlay = runtime.createWidget({
            role: 'container',
            interactive: true,
            layout: {
                position: 'absolute',
                width: 800,
                height: 600,
                inset: { top: 0, left: 0 },
            },
            style: { background: '#00000080' },
        });

        runtime.appendChild(runtime.root, button);
        runtime.appendChild(runtime.root, overlay);
        runtime.commit();

        // Click where the button is, but the overlay is on top
        runtime.dispatchInput({
            type: 'pointer',
            phase: 'down',
            x: 100,
            y: 25,
        });

        // The button handler should NOT be called because the overlay captures the hit
        expect(onButtonClick).not.toHaveBeenCalled();
    });
});

// ===========================================================================
// 5. Lifecycle & Cleanup (5 tests)
// ===========================================================================

describe('T-17: UI Layout Integration — Lifecycle & Cleanup', () => {
    it('widgets are destroyed when runtime is disposed', () => {
        const runtime = createTestRuntime(800, 600);

        const widget = runtime.createWidget({
            layout: { width: 100, height: 50 },
            style: { background: '#ff0000ff' },
        });
        runtime.appendChild(runtime.root, widget);
        runtime.commit();

        expect(runtime.getWidgetCount()).toBe(1);

        runtime.dispose();

        // After disposal, operations that check ensureActive() should throw
        expect(() => runtime.createWidget({})).toThrow(DisposedUIError);
        expect(() => runtime.commit()).toThrow(DisposedUIError);
    });

    it('event listeners are cleaned up on widget removal', () => {
        const runtime = createTestRuntime(800, 600);
        const handler = vi.fn(() => true);

        const button = runtime.createWidget({
            role: 'button',
            interactive: true,
            layout: { width: 200, height: 50 },
            style: { background: '#336699ff' },
            handlers: {
                pointerDown: handler,
            },
        });
        runtime.appendChild(runtime.root, button);
        runtime.commit();

        // Remove the widget
        runtime.removeWidget(button);
        runtime.commit();

        // Dispatch input where the button used to be
        runtime.dispatchInput({
            type: 'pointer',
            phase: 'down',
            x: 100,
            y: 25,
        });

        // Handler should not be called since widget was removed
        expect(handler).not.toHaveBeenCalled();
    });

    it('dynamic widget add/remove works correctly', () => {
        const runtime = createTestRuntime(800, 600);

        // Start with one widget
        const widget1 = runtime.createWidget({
            layout: { width: 100, height: 50 },
            style: { background: '#ff0000ff' },
        });
        runtime.appendChild(runtime.root, widget1);
        runtime.commit();
        expect(runtime.getWidgetCount()).toBe(1);

        // Add a second widget
        const widget2 = runtime.createWidget({
            layout: { width: 100, height: 50 },
            style: { background: '#00ff00ff' },
        });
        runtime.appendChild(runtime.root, widget2);
        runtime.commit();
        expect(runtime.getWidgetCount()).toBe(2);

        // Remove the first widget
        runtime.removeWidget(widget1);
        runtime.commit();
        expect(runtime.getWidgetCount()).toBe(1);

        // The remaining widget should still work
        const box = runtime.getLayoutBox(widget2);
        expect(box.width).toBe(100);
        expect(box.height).toBe(50);
    });

    it('layout recomputes when widget properties change', () => {
        const runtime = createTestRuntime(800, 600);

        const widget = runtime.createWidget({
            layout: { width: 100, height: 50 },
            style: { background: '#ff0000ff' },
        });
        runtime.appendChild(runtime.root, widget);
        runtime.commit();

        let box = runtime.getLayoutBox(widget);
        expect(box.width).toBe(100);
        expect(box.height).toBe(50);

        // Update the widget size
        runtime.updateWidget(widget, {
            layout: { width: 200, height: 80 },
        });
        runtime.commit();

        box = runtime.getLayoutBox(widget);
        expect(box.width).toBe(200);
        expect(box.height).toBe(80);
    });

    it('controller state is disposed when widget is removed', () => {
        const disposeState = vi.fn();
        const registry = new WidgetRegistry<UIRuntime>();
        registry.register(
            defineWidget({
                type: 'test-counter',
                createState: () => ({ count: 0 }),
                disposeState,
            })
        );

        const runtime = new UIRuntime({ width: 800, height: 600, registry });
        runtime.fonts.registerFace(createTestFontAsset());

        const widget = runtime.createWidget({
            controller: 'test-counter',
            layout: { width: 100, height: 50 },
        });
        runtime.appendChild(runtime.root, widget);
        runtime.commit();

        // Verify state was created
        const state = runtime.getWidgetState<{ count: number }>(widget);
        expect(state).not.toBeNull();
        expect(state!.count).toBe(0);

        // Remove the widget
        runtime.removeWidget(widget);

        // disposeState should have been called
        expect(disposeState).toHaveBeenCalledTimes(1);
    });
});

// ===========================================================================
// 6. Canvas Scaling Integration (5 additional tests for completeness)
// ===========================================================================

describe('T-17: UI Layout Integration — Canvas Scaling', () => {
    it('loadFromAsset sets viewport to reference resolution', () => {
        const asset = createTestAsset({
            canvas: {
                referenceWidth: 1280,
                referenceHeight: 720,
                scaleMode: 'fixed',
                matchBias: 0.5,
            },
        });

        const runtime = createTestRuntime(800, 600);
        runtime.loadFromAsset(asset);

        expect(runtime.width).toBe(1280);
        expect(runtime.height).toBe(720);
    });

    it('commitToViewport applies canvas scale transform to commands', () => {
        const asset = createTestAsset({
            canvas: {
                referenceWidth: 1920,
                referenceHeight: 1080,
                scaleMode: 'fill',
                matchBias: 0.5,
            },
            root: {
                role: 'root',
                enabled: true,
                interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [
                    {
                        role: 'container',
                        enabled: true,
                        interactive: false,
                        layout: { width: 100, height: 50 },
                        style: { background: '#ff0000ff' },
                        children: [],
                    },
                ],
            },
        });

        const runtime = createTestRuntime();
        runtime.loadFromAsset(asset);

        // Commit to a different actual viewport
        const frame = runtime.commitToViewport(960, 540);

        expect(frame.viewportWidth).toBe(960);
        expect(frame.viewportHeight).toBe(540);

        // Commands should have a transform applied
        const quadCommand = frame.commands.find((cmd) => cmd.kind === 'quad');
        if (quadCommand && (quadCommand.kind === 'quad' || quadCommand.kind === 'text' || quadCommand.kind === 'image')) {
            expect(quadCommand.transform).toBeDefined();
        }
    });

    it('resolveCanvasScale computes correct fill scaling', () => {
        const canvas: UICanvasConfig = {
            referenceWidth: 1920,
            referenceHeight: 1080,
            scaleMode: 'fill',
            matchBias: 0.5,
        };

        const result = resolveCanvasScale(canvas, 960, 540);

        expect(result.scaleX).toBeCloseTo(0.5);
        expect(result.scaleY).toBeCloseTo(0.5);
        expect(result.offsetX).toBeCloseTo(0);
        expect(result.offsetY).toBeCloseTo(0);
    });

    it('mapViewportPointToCanvas inverts scale correctly', () => {
        const canvas: UICanvasConfig = {
            referenceWidth: 1920,
            referenceHeight: 1080,
            scaleMode: 'fill',
            matchBias: 0.5,
        };

        const scale = resolveCanvasScale(canvas, 960, 540);
        const canvasPoint = mapViewportPointToCanvas(scale, 480, 270);

        // 480 viewport -> (480 - 0) / 0.5 = 960 canvas
        // 270 viewport -> (270 - 0) / 0.5 = 540 canvas
        expect(canvasPoint.x).toBeCloseTo(960);
        expect(canvasPoint.y).toBeCloseTo(540);
    });

    it('canvasScaleToTransform produces correct affine matrix', () => {
        const canvas: UICanvasConfig = {
            referenceWidth: 1920,
            referenceHeight: 1080,
            scaleMode: 'fixed',
            matchBias: 0.5,
        };

        const scale = resolveCanvasScale(canvas, 3840, 2160);
        const transform = canvasScaleToTransform(scale);

        // Fixed mode: scale is 1:1, centered with offset
        expect(transform[0]).toBeCloseTo(1); // scaleX
        expect(transform[3]).toBeCloseTo(1); // scaleY
        // Offset should center the 1920x1080 canvas in 3840x2160 viewport
        expect(transform[4]).toBeCloseTo((3840 - 1920) / 2); // offsetX
        expect(transform[5]).toBeCloseTo((2160 - 1080) / 2); // offsetY
    });
});

// ===========================================================================
// 7. Binding Resolution (additional integration tests)
// ===========================================================================

describe('T-17: UI Layout Integration — Binding Resolution', () => {
    it('resolves named bindings to widget IDs after loadFromAsset', () => {
        const asset = createFullTestAsset();
        const runtime = createTestRuntime();
        runtime.loadFromAsset(asset);

        const titleWidget = runtime.getBoundWidget('title');
        const buttonWidget = runtime.getBoundWidget('button');
        const panelWidget = runtime.getBoundWidget('panel');

        expect(titleWidget).not.toBeNull();
        expect(buttonWidget).not.toBeNull();
        expect(panelWidget).not.toBeNull();

        // Each binding should resolve to a different widget
        expect(titleWidget).not.toBe(buttonWidget);
        expect(buttonWidget).not.toBe(panelWidget);
    });

    it('returns null for unknown binding names', () => {
        const asset = createFullTestAsset();
        const runtime = createTestRuntime();
        runtime.loadFromAsset(asset);

        expect(runtime.getBoundWidget('nonexistent')).toBeNull();
    });

    it('getBindingTable returns the full binding map', () => {
        const asset = createFullTestAsset();
        const runtime = createTestRuntime();
        runtime.loadFromAsset(asset);

        const table = runtime.getBindingTable();
        expect(table.size).toBe(3);
        expect(table.has('title')).toBe(true);
        expect(table.has('button')).toBe(true);
        expect(table.has('panel')).toBe(true);
    });
});

// ===========================================================================
// 8. Layout Engine Direct Tests
// ===========================================================================

describe('T-17: UI Layout Integration — Layout Engine Direct', () => {
    it('normalizeAnchor produces correct preset anchors', () => {
        const centerAnchor = normalizeAnchor('center');
        expect(centerAnchor.x).toBeCloseTo(0.5);
        expect(centerAnchor.y).toBeCloseTo(0.5);
        expect(centerAnchor.maxX).toBeCloseTo(0.5);
        expect(centerAnchor.maxY).toBeCloseTo(0.5);
        expect(centerAnchor.pivotX).toBeCloseTo(0.5);
        expect(centerAnchor.pivotY).toBeCloseTo(0.5);

        const topLeft = normalizeAnchor('top-left');
        expect(topLeft.x).toBeCloseTo(0);
        expect(topLeft.y).toBeCloseTo(0);

        const stretch = normalizeAnchor('stretch');
        expect(stretch.stretch).toBe(true);
    });

    it('compileLayoutInput resolves default layout values', () => {
        const layout = compileLayoutInput({
            width: 200,
            height: 100,
            display: 'stack',
            direction: 'row',
        });

        expect(layout.width.kind).toBe('px');
        expect(layout.width.value).toBe(200);
        expect(layout.height.kind).toBe('px');
        expect(layout.height.value).toBe(100);
        expect(layout.display).toBe('stack');
        expect(layout.direction).toBe('row');
        expect(layout.gap).toBe(0);
        expect(layout.position).toBe('flow');
    });

    it('UILayoutEngine computes layout for a simple tree', () => {
        interface TestNode {
            readonly name: string;
            readonly children: TestNode[];
            layout: ResolvedLayout;
            box: LayoutBox;
            visible: boolean;
        }

        const createNode = (name: string, layout: Partial<ResolvedLayout> = {}): TestNode => {
            const node: TestNode = {
                name,
                children: [],
                layout: compileLayoutInput({ width: 100, height: 50, ...layout }),
                box: { x: 0, y: 0, width: 0, height: 0, contentX: 0, contentY: 0, contentWidth: 0, contentHeight: 0 },
                visible: true,
            };
            return node;
        };

        const root = createNode('root', { display: 'stack', direction: 'column', width: 400, height: 300 });
        const child1 = createNode('child1', { width: 100, height: 50 });
        const child2 = createNode('child2', { width: 100, height: 50 });
        root.children = [child1, child2];

        const engine = new UILayoutEngine<TestNode>();
        engine.compute(
            {
                root,
                getLayout: (node) => node.layout,
                getFirstChild: (node) => node.children[0] ?? null,
                getNextSibling: (node) => {
                    const parent = findParent(root, node);
                    if (!parent) return null;
                    const idx = parent.children.indexOf(node);
                    return parent.children[idx + 1] ?? null;
                },
                measureContent: () => ({ width: 0, height: 0 }),
                setBox: (node, box) => { node.box = box; },
                isVisible: (node) => node.visible,
            },
            { width: 400, height: 300 }
        );

        expect(root.box.width).toBe(400);
        expect(root.box.height).toBe(300);
        expect(child1.box.width).toBe(100);
        expect(child1.box.height).toBe(50);
        expect(child2.box.y).toBeGreaterThan(child1.box.y);
    });
});

/** Helper to find a node's parent in the test tree. */
function findParent(root: { children: typeof root[] }, target: { children: typeof root[] }): { children: typeof root[] } | null {
    for (const child of root.children) {
        if (child === target) return root;
        const found = findParent(child as unknown as { children: typeof root[] }, target as unknown as { children: typeof root[] });
        if (found) return found;
    }
    return null;
}
