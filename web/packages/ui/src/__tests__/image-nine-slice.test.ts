import { describe, expect, it } from 'vitest';
import { UIRuntime } from '../runtime';
import type { ImageRenderCommand, WidgetId } from '../types';

const SOURCE = { kind: 'texture' as const, resourceId: 'ui:frame', width: 64, height: 64 };

const createImageRuntime = (
    image: Record<string, unknown>,
    layout: Record<string, unknown> = { width: 200, height: 100 }
) => {
    const runtime = new UIRuntime({ width: 400, height: 300 });
    const widget = runtime.createWidget({
        layout: { position: 'absolute', inset: { left: 0, top: 0 }, ...layout },
        image: { source: SOURCE, ...image } as never,
    });
    runtime.appendChild(runtime.root, widget);
    return { runtime, widget };
};

const imageCommands = (runtime: UIRuntime): readonly ImageRenderCommand[] =>
    runtime.commit().commands.filter(
        (command): command is ImageRenderCommand => command.kind === 'image'
    );

describe('image nine-slice', () => {
    it('emits a single command when no border is authored', () => {
        const { runtime } = createImageRuntime({ fit: 'fill' });

        const commands = imageCommands(runtime);

        expect(commands).toHaveLength(1);
        expect(commands[0].border).toBeUndefined();
        expect(commands[0].width).toBeCloseTo(200);
        expect(commands[0].height).toBeCloseTo(100);

        runtime.dispose();
    });

    it('carries the authored border on the command', () => {
        const { runtime } = createImageRuntime({ border: 8 });

        const commands = imageCommands(runtime);

        expect(commands).toHaveLength(1);
        expect(commands[0].border).toEqual({ top: 8, right: 8, bottom: 8, left: 8 });
        expect(commands[0].fillCenter).toBe(true);

        runtime.dispose();
    });

    it('accepts per-edge borders', () => {
        const { runtime } = createImageRuntime({
            border: { left: 4, top: 6, right: 8, bottom: 10 },
        });

        expect(imageCommands(runtime)[0].border).toEqual({
            left: 4,
            top: 6,
            right: 8,
            bottom: 10,
        });

        runtime.dispose();
    });

    it('spans the full content box regardless of fit when sliced', () => {
        const { runtime } = createImageRuntime({ border: 8, fit: 'contain' });

        const command = imageCommands(runtime)[0];

        expect(command.width).toBeCloseTo(200);
        expect(command.height).toBeCloseTo(100);

        runtime.dispose();
    });

    it('still honours fit when the border is zero', () => {
        const { runtime } = createImageRuntime({ border: 0, fit: 'contain' });

        const command = imageCommands(runtime)[0];

        expect(command.border).toBeUndefined();
        expect(command.width).toBeCloseTo(100);
        expect(command.height).toBeCloseTo(100);

        runtime.dispose();
    });

    it('keeps fillCenter false when authored', () => {
        const { runtime } = createImageRuntime({ border: 8, fillCenter: false });

        expect(imageCommands(runtime)[0].fillCenter).toBe(false);

        runtime.dispose();
    });

    it('clamps negative borders to zero', () => {
        const { runtime } = createImageRuntime({ border: { left: -10, top: 4 } });

        expect(imageCommands(runtime)[0].border).toEqual({
            left: 0,
            top: 4,
            right: 0,
            bottom: 0,
        });

        runtime.dispose();
    });

    it('round-trips border and fillCenter through the snapshot', () => {
        const { runtime, widget } = createImageRuntime({ border: 12, fillCenter: false });
        runtime.commit();

        const snapshot = runtime.snapshot();
        const child = snapshot.root.children?.[0];

        expect(child?.image?.border).toBe(12);
        expect(child?.image?.fillCenter).toBe(false);
        expect(runtime.getLayoutBox(widget as WidgetId).width).toBeCloseTo(200);

        runtime.dispose();
    });
});

describe('image material slot', () => {
    it('leaves the material empty when none is authored', () => {
        const { runtime } = createImageRuntime({});

        expect(imageCommands(runtime)[0].material).toBeUndefined();

        runtime.dispose();
    });

    it('carries the authored material alongside the texture source', () => {
        const { runtime } = createImageRuntime({
            material: 'Assets/Materials/Mat_Panel.mat',
        });

        const command = imageCommands(runtime)[0];

        expect(command.material).toBe('Assets/Materials/Mat_Panel.mat');
        expect(command.source).toMatchObject({ kind: 'texture', resourceId: 'ui:frame' });

        runtime.dispose();
    });

    it('trims the authored material and drops whitespace-only values', () => {
        const trimmed = createImageRuntime({ material: '  Assets/Materials/A.mat  ' });
        expect(imageCommands(trimmed.runtime)[0].material).toBe('Assets/Materials/A.mat');
        trimmed.runtime.dispose();

        const blank = createImageRuntime({ material: '   ' });
        expect(imageCommands(blank.runtime)[0].material).toBeUndefined();
        blank.runtime.dispose();
    });

    it('round-trips the material through the snapshot', () => {
        const { runtime } = createImageRuntime({ material: 'Assets/Materials/Mat_Panel.mat' });
        runtime.commit();

        const child = runtime.snapshot().root.children?.[0];

        expect(child?.image?.material).toBe('Assets/Materials/Mat_Panel.mat');

        runtime.dispose();
    });

    it('keeps the material on every nine-slice region', () => {
        const { runtime } = createImageRuntime({
            material: 'Assets/Materials/Mat_Panel.mat',
            border: 8,
        });

        const command = imageCommands(runtime)[0];

        expect(command.material).toBe('Assets/Materials/Mat_Panel.mat');
        expect(command.border).toEqual({ top: 8, right: 8, bottom: 8, left: 8 });

        runtime.dispose();
    });
});
