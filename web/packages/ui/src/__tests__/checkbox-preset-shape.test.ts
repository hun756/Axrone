import { describe, expect, it } from 'vitest';
import { UIRuntime } from '../runtime';
import { deserializeUIAsset } from '../runtime/ui-asset-io';
import {
	CHECKBOX_TOGGLE_CONTROLLER_TYPE,
	checkboxToggleController,
} from '../controls/checkbox-controller';
import type { StrokeRenderCommand } from '../types';

/**
 * The Editor's checkbox preset authors a content-sized row stack whose mark is
 * centered inside the box purely by anchor. Existing controller tests use
 * fixed-size overlay fixtures, so this shape was never covered and both the
 * mark centering and the label's cross-axis alignment regressed silently.
 */
const createPresetShapedAsset = (textLineHeight: number): string =>
	JSON.stringify({
		id: 'ui.preset-shape',
		name: 'preset-shape',
		version: 1,
		canvas: {
			referenceWidth: 1920,
			referenceHeight: 1080,
			scaleMode: 'match-width-or-height',
			matchBias: 0.5,
		},
		bindings: {
			root: 'root',
			chk: 'chk',
			'chk-box': 'chk-box',
			'chk-mark': 'chk-mark',
			'chk-label': 'chk-label',
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
					key: 'chk',
					enabled: true,
					interactive: true,
					controller: CHECKBOX_TOGGLE_CONTROLLER_TYPE,
					props: {
						isOn: true,
						markStyle: 'check',
						boxSize: 20,
						markSize: 14,
						markWeight: 2,
						markColor: '#ffffffff',
						boxKey: 'chk-box',
						markKey: 'chk-mark',
						labelKey: 'chk-label',
						transition: 'color',
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
							key: 'chk-box',
							enabled: true,
							interactive: false,
							layout: { width: 20, height: 20, display: 'overlay', shrink: 0 },
							style: { background: '#334155ff', radius: 4 },
							children: [
								{
									role: 'custom:checkbox-mark',
									key: 'chk-mark',
									enabled: true,
									interactive: false,
									layout: {
										position: 'absolute',
										width: 14,
										height: 14,
										anchor: { x: 0.5, y: 0.5, maxX: 0.5, maxY: 0.5, pivotX: 0.5, pivotY: 0.5 },
									},
									style: { background: '#00000000', radius: 2 },
									children: [],
								},
							],
						},
						{
							role: 'text',
							key: 'chk-label',
							enabled: true,
							interactive: false,
							layout: { width: 'content', height: 'content' },
							style: { color: '#e2e8f0ff' },
							text: { value: 'Checkbox', size: 16, lineHeight: textLineHeight },
							children: [],
						},
					],
				},
			],
		},
	});

const commitPreset = (textLineHeight: number) => {
	const runtime = new UIRuntime();
	runtime.registry.register(checkboxToggleController);
	runtime.loadFromAsset(deserializeUIAsset(createPresetShapedAsset(textLineHeight)));
	const frame = runtime.commit();
	const rect = (key: string) => {
		const id = runtime.getBoundWidget(key);
		return id === null ? null : runtime.getLayoutBox(id);
	};
	const center = (box: { x: number; y: number; width: number; height: number } | null) =>
		box === null ? null : { x: box.x + box.width / 2, y: box.y + box.height / 2 };
	const strokes = frame.commands.filter(
		(command): command is StrokeRenderCommand => command.kind === 'stroke',
	);
	return { runtime, rect, center, strokes };
};

const CENTIPIXEL = 0.01;

describe('checkbox-toggle — editor preset shape (content-sized row stack)', () => {
	it('centers the mark inside the box instead of pinning it to the top-left corner', () => {
		const { runtime, rect, center } = commitPreset(20.8);
		const box = rect('chk-box');
		const mark = rect('chk-mark');
		expect(box).not.toBeNull();
		expect(mark).not.toBeNull();
		// A zeroed inset still counts as "present" and would win over the anchor,
		// collapsing the mark onto the box origin; the controller must drop it.
		expect(center(mark!).x).toBeCloseTo(center(box!).x, 0);
		expect(center(mark!).y).toBeCloseTo(center(box!).y, 0);
		runtime.dispose();
	});

	it('keeps the tick stroke bounded by the mark rect so it cannot overflow the box', () => {
		const { runtime, rect, strokes } = commitPreset(20.8);
		const box = rect('chk-box')!;
		const mark = rect('chk-mark')!;
		expect(strokes).toHaveLength(1);
		const stroke = strokes[0]!;
		expect(stroke.width).toBeLessThanOrEqual(box.width + CENTIPIXEL);
		expect(stroke.height).toBeLessThanOrEqual(box.height + CENTIPIXEL);
		for (const entry of stroke.strokes) {
			for (const [px, py] of entry.points) {
				expect(px * stroke.width + stroke.x).toBeGreaterThanOrEqual(box.x - CENTIPIXEL);
				expect(px * stroke.width + stroke.x).toBeLessThanOrEqual(box.x + box.width + CENTIPIXEL);
				expect(py * stroke.height + stroke.y).toBeGreaterThanOrEqual(box.y - CENTIPIXEL);
				expect(py * stroke.height + stroke.y).toBeLessThanOrEqual(box.y + box.height + CENTIPIXEL);
			}
		}
		expect(mark.width).toBeCloseTo(14, 0);
		runtime.dispose();
	});

	it('measures the label by its line height so alignItems centering has slack to work with', () => {
		const centered = commitPreset(20.8);
		const degenerate = commitPreset(1.3);
		const centeredBox = centered.rect('chk-box')!;
		const centeredLabel = centered.rect('chk-label')!;
		// With a real pixel line height the box is centered against the taller
		// label; a unitless multiplier read as pixels collapses that alignment.
		expect(centeredLabel.height).toBeGreaterThan(10);
		expect(centeredBox.y).toBeCloseTo((centeredLabel.height - 20) / 2, 0);
		expect(degenerate.rect('chk-label')!.height).toBeLessThan(2);
		centered.runtime.dispose();
		degenerate.runtime.dispose();
	});
});
