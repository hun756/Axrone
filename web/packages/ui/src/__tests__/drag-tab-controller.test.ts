import { describe, expect, it, vi } from 'vitest';
import { UIRuntime } from '../runtime';
import { deserializeUIAsset } from '../runtime/ui-asset-io';
import { dragController } from '../controls/drag-controller';
import { tabViewController, getTabSelectedIndex } from '../controls/tab-controller';
import type { UIAsset } from '../types/ui-asset';
import type { WidgetId } from '../types';

const pointer = (phase: 'down' | 'up' | 'move' | 'leave' | 'enter', x: number, y: number) =>
    ({
        type: 'pointer' as const,
        phase,
        x,
        y,
        pointerId: 1,
        button: 0,
        buttons: phase === 'up' ? 0 : 1,
        deltaX: 0,
        deltaY: 0,
        altKey: false,
        ctrlKey: false,
        shiftKey: false,
        metaKey: false,
    });

const createDragAssetJson = (props: Record<string, unknown>): string =>
    JSON.stringify({
        id: 'ui.drag-test',
        name: 'drag-test',
        version: 1,
        canvas: {
            referenceWidth: 400,
            referenceHeight: 400,
            scaleMode: 'fixed',
            matchBias: 0.5,
        },
        bindings: {
            root: 'root',
            draggable: 'draggable',
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
                    key: 'draggable',
                    enabled: true,
                    interactive: true,
                    controller: 'widget-drag',
                    props,
                    layout: {
                        position: 'absolute',
                        inset: { left: 10, top: 10 },
                        width: 100,
                        height: 100,
                    },
                    style: { background: '#0a74daff' },
                    children: [],
                },
            ],
        },
    });

const createDragRuntime = (props: Record<string, unknown>): UIRuntime => {
    const runtime = new UIRuntime({ width: 400, height: 400 });
    runtime.registry.register(dragController);
    runtime.loadFromAsset(deserializeUIAsset(createDragAssetJson(props)) as UIAsset);
    runtime.commit();
    return runtime;
};

describe('@axrone/ui drag-controller callbacks', () => {
    it('dispatches onDragStart event when drag begins', () => {
        const runtime = createDragRuntime({
            enabled: true,
            axis: 'both',
            onDragStart: 'onDragStart',
        });

        const draggable = runtime.getBoundWidget('draggable')!;
        const callback = vi.fn();
        runtime.onControllerEvent(draggable, 'onDragStart', callback);

        // Pointer down inside the draggable widget (at 50,50 — inside 10,10 + 100x100).
        runtime.dispatchInput(pointer('down', 50, 50));

        expect(callback).toHaveBeenCalledTimes(1);
        expect(callback).toHaveBeenCalledWith(expect.objectContaining({
            pointerX: 50,
            pointerY: 50,
        }));
    });

    it('dispatches onDragEnd and onDrop events when drag completes', () => {
        const runtime = createDragRuntime({
            enabled: true,
            axis: 'both',
            onDragEnd: 'onDragEnd',
            onDrop: 'onDrop',
        });

        const draggable = runtime.getBoundWidget('draggable')!;
        const onEnd = vi.fn();
        const onDrop = vi.fn();
        runtime.onControllerEvent(draggable, 'onDragEnd', onEnd);
        runtime.onControllerEvent(draggable, 'onDrop', onDrop);

        // Start drag.
        runtime.dispatchInput(pointer('down', 50, 50));
        // Complete drag.
        runtime.dispatchInput(pointer('up', 80, 80));

        expect(onEnd).toHaveBeenCalledTimes(1);
        expect(onDrop).toHaveBeenCalledTimes(1);
    });

    it('dispatches onDragEnd with cancelled flag when drag is cancelled', () => {
        const runtime = createDragRuntime({
            enabled: true,
            axis: 'both',
            onDragEnd: 'onDragEnd',
        });

        const draggable = runtime.getBoundWidget('draggable')!;
        const onEnd = vi.fn();
        runtime.onControllerEvent(draggable, 'onDragEnd', onEnd);

        // Start drag.
        runtime.dispatchInput(pointer('down', 50, 50));
        // Move pointer far outside the widget to trigger leave/cancel.
        runtime.dispatchInput(pointer('move', 350, 350));
        // Release outside the widget.
        runtime.dispatchInput(pointer('up', 350, 350));

        expect(onEnd).toHaveBeenCalledTimes(1);
    });

    it('does not throw when no listeners are registered', () => {
        const runtime = createDragRuntime({
            enabled: true,
            onDragStart: 'onDragStart',
        });

        // No listeners registered — should not throw.
        expect(() => {
            runtime.dispatchInput(pointer('down', 50, 50));
        }).not.toThrow();
    });
});

describe('@axrone/ui tab-controller nested panel resolution', () => {
    it('resolves panel widgets nested inside a content container via binding table', () => {
        const runtime = new UIRuntime({ width: 400, height: 300 });
        runtime.registry.register(tabViewController);
        const json = JSON.stringify({
            id: 'ui.tab-nested-test',
            name: 'tab-nested-test',
            version: 1,
            canvas: { referenceWidth: 400, referenceHeight: 300, scaleMode: 'fixed', matchBias: 0.5 },
            bindings: {
                root: 'root',
                'tab-view': 'tab-view',
                'tab-0': 'tab-0',
                'tab-1': 'tab-1',
                'panel-0': 'panel-0',
                'panel-1': 'panel-1',
            },
            root: {
                role: 'root',
                key: 'root',
                enabled: true,
                interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [
                    {
                        role: 'custom:tab-view',
                        key: 'tab-view',
                        enabled: true,
                        interactive: true,
                        controller: 'tab-view',
                        props: { selectedIndex: 0, tabCount: 2, tabPrefix: 'tab-', panelPrefix: 'panel-' },
                        layout: { position: 'absolute', inset: { left: 0, top: 0 }, width: 400, height: 300 },
                        children: [
                            {
                                role: 'container',
                                key: 'tab-0',
                                enabled: true,
                                interactive: true,
                                layout: { width: 100, height: 30 },
                                style: { background: '#334155ff' },
                                children: [],
                            },
                            {
                                role: 'container',
                                key: 'tab-1',
                                enabled: true,
                                interactive: true,
                                layout: { width: 100, height: 30 },
                                style: { background: '#334155ff' },
                                children: [],
                            },
                            {
                                role: 'container',
                                key: 'content-container',
                                enabled: true,
                                interactive: false,
                                layout: { position: 'absolute', inset: { left: 0, top: 40 }, width: 400, height: 260 },
                                children: [
                                    {
                                        role: 'container',
                                        key: 'panel-0',
                                        enabled: true,
                                        interactive: false,
                                        layout: { width: '100%', height: '100%' },
                                        style: { background: '#1e293bff' },
                                        children: [],
                                    },
                                    {
                                        role: 'container',
                                        key: 'panel-1',
                                        enabled: false,
                                        interactive: false,
                                        layout: { width: '100%', height: '100%' },
                                        style: { background: '#1e293bff' },
                                        children: [],
                                    },
                                ],
                            },
                        ],
                    },
                ],
            },
        });

        const asset = deserializeUIAsset(json);
        runtime.loadFromAsset(asset as UIAsset);
        runtime.commit();

        // The binding table should resolve panel-0 and panel-1 even though
        // they are nested inside 'content-container'.
        const panel0 = runtime.getBoundWidget('panel-0');
        const panel1 = runtime.getBoundWidget('panel-1');
        expect(panel0).not.toBeNull();
        expect(panel1).not.toBeNull();

        // Tab view should have resolved them and set initial state.
        const tabView = runtime.getBoundWidget('tab-view')!;
        const selectedIndex = getTabSelectedIndex(runtime, tabView);
        expect(selectedIndex).toBe(0);
    });
});

describe('@axrone/ui controller event system', () => {
    it('supports on/off/emit lifecycle', () => {
        const runtime = new UIRuntime({ width: 100, height: 100 });
        const widget = runtime.createWidget({ role: 'container' });
        const callback = vi.fn();

        runtime.onControllerEvent(widget, 'test-event', callback);
        const emitted = runtime.emitControllerEvent(widget, 'test-event', { value: 42 });

        expect(emitted).toBe(true);
        expect(callback).toHaveBeenCalledWith({ value: 42 });

        runtime.offControllerEvent(widget, 'test-event', callback);
        const afterOff = runtime.emitControllerEvent(widget, 'test-event', { value: 99 });

        expect(afterOff).toBe(false);
        expect(callback).toHaveBeenCalledTimes(1);
    });

    it('returns false when no listeners exist for the event', () => {
        const runtime = new UIRuntime({ width: 100, height: 100 });
        const widget = runtime.createWidget({ role: 'container' });

        expect(runtime.emitControllerEvent(widget, 'nonexistent')).toBe(false);
    });
});
