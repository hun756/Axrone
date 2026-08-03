import { describe, expect, it } from 'vitest';
import { AXRONE_DEFAULT_UI_FONT_FAMILY, UIRuntime, createUIProgressBar } from '../index';
import { createTestFontAsset } from './test-font';

const prepareRuntime = () => {
	const runtime = new UIRuntime({ width: 480, height: 240 });
	runtime.fonts.registerFace(createTestFontAsset(AXRONE_DEFAULT_UI_FONT_FAMILY));
	return runtime;
};

describe('@axrone/ui progress-bar handle', () => {
	it('returns the initial value', () => {
		const runtime = prepareRuntime();
		const bar = createUIProgressBar(runtime, { value: 0.5, min: 0, max: 1 });
		expect(bar.getValue()).toBe(0.5);
	});

	it('defaults to min when no value is provided', () => {
		const runtime = prepareRuntime();
		const bar = createUIProgressBar(runtime, { min: 0, max: 100 });
		expect(bar.getValue()).toBe(0);
	});

	it('clamps value to [min, max] on setValue', () => {
		const runtime = prepareRuntime();
		const bar = createUIProgressBar(runtime, { min: 0, max: 100 });
		bar.setValue(150);
		expect(bar.getValue()).toBe(100);
		bar.setValue(-10);
		expect(bar.getValue()).toBe(0);
		bar.setValue(42);
		expect(bar.getValue()).toBe(42);
	});

	it('normalizes reversed ranges in setRange', () => {
		const runtime = prepareRuntime();
		const bar = createUIProgressBar(runtime, { min: 0, max: 100 });
		bar.setValue(80);
		bar.setRange(100, 0);
		expect(bar.getValue()).toBe(80);
	});

	it('reclamps value when range shrinks', () => {
		const runtime = prepareRuntime();
		const bar = createUIProgressBar(runtime, { min: 0, max: 100, value: 80 });
		bar.setRange(0, 50);
		expect(bar.getValue()).toBe(50);
	});

	it('disposes the widget', () => {
		const runtime = prepareRuntime();
		const bar = createUIProgressBar(runtime, {});
		bar.dispose();
		expect(runtime.getWidgetCount()).toBe(0);
	});

	it('produces render commands after setValue', () => {
		const runtime = prepareRuntime();
		const bar = createUIProgressBar(runtime, { min: 0, max: 1, value: 0.5 });
		const frame = runtime.commit();
		expect(frame.commands.length).toBeGreaterThan(0);
	});
});
