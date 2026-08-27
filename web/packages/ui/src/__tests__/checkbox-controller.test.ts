import { describe, expect, it, vi } from 'vitest';
import { UIRuntime } from '../runtime';
import { deserializeUIAsset } from '../runtime/ui-asset-io';
import {
	CHECKBOX_TOGGLE_CONTROLLER_TYPE,
	checkboxToggleController,
	getCheckboxChecked,
} from '../controls/checkbox-controller';
import type { WidgetId } from '../types';

const createCheckboxAssetJson = (
	props: Record<string, unknown>,
	boxImage?: Record<string, unknown>,
	markImage?: Record<string, unknown>,
): string =>
	JSON.stringify({
		id: 'ui.checkbox-test',
		name: 'checkbox-test',
		version: 1,
		canvas: {
			referenceWidth: 400,
			referenceHeight: 200,
			scaleMode: 'fixed',
			matchBias: 0.5,
		},
		bindings: {
			root: 'root',
			checkbox: 'checkbox',
			'checkbox-box': 'checkbox-box',
			'checkbox-mark': 'checkbox-mark',
			'checkbox-label': 'checkbox-label',
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
					key: 'checkbox',
					enabled: true,
					interactive: true,
					controller: CHECKBOX_TOGGLE_CONTROLLER_TYPE,
					props,
					layout: {
						display: 'overlay',
						position: 'absolute',
						inset: { left: 20, top: 20 },
						width: 160,
						height: 24,
					},
					children: [
						{
							role: 'custom:checkbox-box',
							key: 'checkbox-box',
							enabled: true,
							interactive: false,
							image: boxImage ?? null,
							layout: {
								position: 'absolute',
								inset: { left: 0, top: 0 },
								width: 20,
								height: 20,
							},
							style: { background: '#334155ff', radius: 4 },
							children: [
								{
									role: 'custom:checkbox-mark',
									key: 'checkbox-mark',
									enabled: false,
									interactive: false,
									image: markImage ?? null,
									layout: {
										position: 'absolute',
										anchor: { x: 0.5, y: 0.5, pivotX: 0.5, pivotY: 0.5 },
										width: 12,
										height: 12,
									},
									style: { background: '#00000000' },
									children: [],
								},
							],
						},
						{
							role: 'text',
							key: 'checkbox-label',
							enabled: true,
							interactive: false,
							layout: {
								position: 'absolute',
								inset: { left: 28, top: 2 },
								width: 120,
								height: 20,
							},
							text: { value: 'Option', size: 12 },
							children: [],
						},
					],
				},
			],
		},
	});

const createRuntime = (
	props: Record<string, unknown>,
	boxImage?: Record<string, unknown>,
	markImage?: Record<string, unknown>,
): UIRuntime => {
	const runtime = new UIRuntime();
	runtime.registry.register(checkboxToggleController);
	runtime.loadFromAsset(
		deserializeUIAsset(createCheckboxAssetJson(props, boxImage, markImage)),
	);
	runtime.commit();
	return runtime;
};

const checkboxWidget = (runtime: UIRuntime): WidgetId =>
	runtime.getBoundWidget('checkbox')!;
const boxWidget = (runtime: UIRuntime): WidgetId =>
	runtime.getBoundWidget('checkbox-box')!;
const markWidget = (runtime: UIRuntime): WidgetId =>
	runtime.getBoundWidget('checkbox-mark')!;

const pointer = (phase: 'down' | 'up' | 'move', x: number, y = 30) =>
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

const clickCheckbox = (runtime: UIRuntime) => {
	runtime.dispatchInput(pointer('down', 30));
	runtime.dispatchInput(pointer('up', 30));
};

const hoverCheckbox = (runtime: UIRuntime) => {
	runtime.dispatchInput(pointer('move', 30));
};

const leaveCheckbox = (runtime: UIRuntime) => {
	runtime.dispatchInput(pointer('move', 0, 190));
};

describe('checkbox-toggle controller — backward compatibility (color mode)', () => {
	it('applies per-state background on box in color mode (default)', () => {
		const runtime = createRuntime({
			boxKey: 'checkbox-box',
			markKey: 'checkbox-mark',
			labelKey: 'checkbox-label',
			states: {
				normal: '#334155ff',
				hover: '#475569ff',
				checked: '#0a74daff',
			},
		});

		const snapshot = runtime.snapshot();
		const boxNode = snapshot.root.children[0].children[0];
		expect(boxNode.style.background).toBe('#334155ff');

		hoverCheckbox(runtime);
		runtime.commit();
		const hoverSnapshot = runtime.snapshot();
		const hoverBox = hoverSnapshot.root.children[0].children[0];
		expect(hoverBox.style.background).toBe('#475569ff');

		clickCheckbox(runtime);
		runtime.commit();
		const checkedSnapshot = runtime.snapshot();
		const checkedBox = checkedSnapshot.root.children[0].children[0];
		expect(checkedBox.style.background).toBe('#0a74daff');
	});

	it('shows and hides the mark on toggle (color mode)', () => {
		const runtime = createRuntime({
			boxKey: 'checkbox-box',
			markKey: 'checkbox-mark',
			labelKey: 'checkbox-label',
			markColor: '#ffffffff',
		});

		expect(getCheckboxChecked(runtime, checkboxWidget(runtime))).toBe(false);

		const beforeSnapshot = runtime.snapshot();
		const markBefore = beforeSnapshot.root.children[0].children[0].children[0];
		expect(markBefore.enabled).toBe(false);

		clickCheckbox(runtime);
		runtime.commit();
		expect(getCheckboxChecked(runtime, checkboxWidget(runtime))).toBe(true);

		const afterSnapshot = runtime.snapshot();
		const markAfter = afterSnapshot.root.children[0].children[0].children[0];
		expect(markAfter.enabled).toBe(true);
	});
});

describe('checkbox-toggle controller — tint mode', () => {
	it('patches box image.tint per state', () => {
		const runtime = createRuntime(
			{
				boxKey: 'checkbox-box',
				markKey: 'checkbox-mark',
				labelKey: 'checkbox-label',
				transition: 'tint',
				boxTints: {
					normal: '#334155ff',
					hover: '#475569ff',
					checked: '#0a74daff',
				},
			},
			{
				source: { kind: 'texture', resourceId: 'box.png', width: 20, height: 20 },
				tint: '#334155ff',
			},
		);

		expect(runtime.getWidgetImageInput(boxWidget(runtime))?.tint).toBe('#334155ff');

		hoverCheckbox(runtime);
		runtime.commit();
		expect(runtime.getWidgetImageInput(boxWidget(runtime))?.tint).toBe('#475569ff');

		clickCheckbox(runtime);
		runtime.commit();
		expect(runtime.getWidgetImageInput(boxWidget(runtime))?.tint).toBe('#0a74daff');
	});

	it('patches mark image.tint when checked, hides when unchecked', () => {
		const runtime = createRuntime(
			{
				boxKey: 'checkbox-box',
				markKey: 'checkbox-mark',
				labelKey: 'checkbox-label',
				transition: 'tint',
				markTints: {
					normal: '#ffffffff',
					checked: '#ff0000ff',
				},
			},
			{ source: { kind: 'texture', resourceId: 'box.png', width: 20, height: 20 } },
			{ source: { kind: 'texture', resourceId: 'mark.png', width: 12, height: 12 }, tint: '#ffffffff' },
		);

		const snapshot = runtime.snapshot();
		const markNode = snapshot.root.children[0].children[0].children[0];
		expect(markNode.enabled).toBe(false);

		clickCheckbox(runtime);
		runtime.commit();

		const checkedSnapshot = runtime.snapshot();
		const checkedMark = checkedSnapshot.root.children[0].children[0].children[0];
		expect(checkedMark.enabled).toBe(true);
		expect(runtime.getWidgetImageInput(markWidget(runtime))?.tint).toBe('#ff0000ff');
	});

	it('preserves existing image properties when patching tint', () => {
		const runtime = createRuntime(
			{
				boxKey: 'checkbox-box',
				markKey: 'checkbox-mark',
				labelKey: 'checkbox-label',
				transition: 'tint',
				boxTints: { hover: '#ff0000ff' },
			},
			{
				source: { kind: 'texture', resourceId: 'box.png', width: 20, height: 20 },
				fit: 'fill',
				sampling: 'nearest',
				border: 4,
				tint: '#334155ff',
			},
		);

		hoverCheckbox(runtime);
		runtime.commit();

		const image = runtime.getWidgetImageInput(boxWidget(runtime));
		expect(image?.tint).toBe('#ff0000ff');
		expect((image?.source as { resourceId?: string })?.resourceId).toBe('box.png');
		expect(image?.fit).toBe('fill');
		expect(image?.sampling).toBe('nearest');
	});
});

describe('checkbox-toggle controller — sprite mode', () => {
	it('swaps box image.source per state and restores on normal', () => {
		const runtime = createRuntime(
			{
				boxKey: 'checkbox-box',
				markKey: 'checkbox-mark',
				labelKey: 'checkbox-label',
				transition: 'sprite',
				boxSprites: {
					normal: { kind: 'texture', resourceId: 'box_normal.png', width: 20, height: 20 },
					hover: { kind: 'texture', resourceId: 'box_hover.png', width: 20, height: 20 },
					checked: { kind: 'texture', resourceId: 'box_checked.png', width: 20, height: 20 },
				},
			},
			{
				source: { kind: 'texture', resourceId: 'box_normal.png', width: 20, height: 20 },
			},
		);

		const sourceId = () =>
			(runtime.getWidgetImageInput(boxWidget(runtime))?.source as { resourceId?: string })?.resourceId;

		expect(sourceId()).toBe('box_normal.png');

		hoverCheckbox(runtime);
		runtime.commit();
		expect(sourceId()).toBe('box_hover.png');

		clickCheckbox(runtime);
		runtime.commit();
		expect(sourceId()).toBe('box_checked.png');
	});

	it('swaps mark image.source when checked, hides when unchecked', () => {
		const runtime = createRuntime(
			{
				boxKey: 'checkbox-box',
				markKey: 'checkbox-mark',
				labelKey: 'checkbox-label',
				transition: 'sprite',
				boxSprites: {
					normal: { kind: 'texture', resourceId: 'box.png', width: 20, height: 20 },
					checked: { kind: 'texture', resourceId: 'box_on.png', width: 20, height: 20 },
				},
				markSprites: {
					checked: { kind: 'texture', resourceId: 'check_mark.png', width: 12, height: 12 },
				},
			},
			{ source: { kind: 'texture', resourceId: 'box.png', width: 20, height: 20 } },
			{ source: { kind: 'texture', resourceId: 'check_mark.png', width: 12, height: 12 } },
		);

		const beforeSnapshot = runtime.snapshot();
		const markBefore = beforeSnapshot.root.children[0].children[0].children[0];
		expect(markBefore.enabled).toBe(false);

		clickCheckbox(runtime);
		runtime.commit();

		const afterSnapshot = runtime.snapshot();
		const markAfter = afterSnapshot.root.children[0].children[0].children[0];
		expect(markAfter.enabled).toBe(true);
		expect(
			(runtime.getWidgetImageInput(markWidget(runtime))?.source as { resourceId?: string })?.resourceId,
		).toBe('check_mark.png');
	});

	it('restores original source from mount when state has no sprite entry', () => {
		const runtime = createRuntime(
			{
				boxKey: 'checkbox-box',
				markKey: 'checkbox-mark',
				labelKey: 'checkbox-label',
				transition: 'sprite',
				boxSprites: {
					checked: { kind: 'texture', resourceId: 'box_checked.png', width: 20, height: 20 },
				},
			},
			{ source: { kind: 'texture', resourceId: 'box_default.png', width: 20, height: 20 } },
		);

		const sourceId = () =>
			(runtime.getWidgetImageInput(boxWidget(runtime))?.source as { resourceId?: string })?.resourceId;

		expect(sourceId()).toBe('box_default.png');

		clickCheckbox(runtime);
		runtime.commit();
		expect(sourceId()).toBe('box_checked.png');

		clickCheckbox(runtime);
		runtime.commit();
		expect(sourceId()).toBe('box_default.png');
	});

	it('preserves shared image properties when swapping source', () => {
		const runtime = createRuntime(
			{
				boxKey: 'checkbox-box',
				markKey: 'checkbox-mark',
				labelKey: 'checkbox-label',
				transition: 'sprite',
				boxSprites: {
					hover: { kind: 'texture', resourceId: 'box_hover.png', width: 20, height: 20 },
				},
			},
			{
				source: { kind: 'texture', resourceId: 'box_normal.png', width: 20, height: 20 },
				fit: 'fill',
				sampling: 'nearest',
				border: { left: 4, top: 4, right: 4, bottom: 4 },
			},
		);

		hoverCheckbox(runtime);
		runtime.commit();

		const image = runtime.getWidgetImageInput(boxWidget(runtime));
		expect((image?.source as { resourceId?: string })?.resourceId).toBe('box_hover.png');
		expect(image?.fit).toBe('fill');
		expect(image?.sampling).toBe('nearest');
	});
});

describe('checkbox-toggle controller — sentinel previousBoxColor', () => {
	it('applies initial color directly without animation on first mount', () => {
		// Even with a non-zero transitionDuration the first applyVisuals must
		// skip animation (previousBoxColor starts as '' sentinel).
		const runtime = createRuntime({
			boxKey: 'checkbox-box',
			markKey: 'checkbox-mark',
			labelKey: 'checkbox-label',
			states: { normal: '#334155ff', hover: '#475569ff', checked: '#0a74daff' },
			transitionDuration: 0.5,
		});

		const snapshot = runtime.snapshot();
		const boxNode = snapshot.root.children[0].children[0];
		// Color applied immediately — no animation flash.
		expect(boxNode.style.background).toBe('#334155ff');
	});

	it('reads widget actual current color for animation from after cancellation', () => {
		const runtime = createRuntime({
			boxKey: 'checkbox-box',
			markKey: 'checkbox-mark',
			labelKey: 'checkbox-label',
			states: { normal: '#334155ff', hover: '#475569ff', checked: '#0a74daff' },
			transitionDuration: 1,
		});

		// Spy on getWidgetStyleInput to confirm it is consulted when starting
		// a color animation (the fix for rapid-toggle jump).
		const spy = vi.spyOn(runtime, 'getWidgetStyleInput');

		// Trigger a state change that enters the animation path.
		hoverCheckbox(runtime);
		runtime.commit();

		expect(spy).toHaveBeenCalledWith(boxWidget(runtime));
		spy.mockRestore();
	});
});
