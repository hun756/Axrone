import { describe, expect, it, vi } from 'vitest';
import { AXRONE_DEFAULT_UI_FONT_FAMILY, UIRuntime, createUIToggle } from '../index';
import { createTestFontAsset } from './test-font';

const prepareRuntime = () => {
	const runtime = new UIRuntime({ width: 480, height: 240 });
	runtime.fonts.registerFace(createTestFontAsset(AXRONE_DEFAULT_UI_FONT_FAMILY));
	return runtime;
};

describe('@axrone/ui toggle handle', () => {
	it('defaults to unchecked', () => {
		const runtime = prepareRuntime();
		const toggle = createUIToggle(runtime, {});
		expect(toggle.isChecked()).toBe(false);
	});

	it('starts checked when constructed with checked: true', () => {
		const runtime = prepareRuntime();
		const toggle = createUIToggle(runtime, { checked: true });
		expect(toggle.isChecked()).toBe(true);
	});

	it('toggles state via toggle()', () => {
		const runtime = prepareRuntime();
		const toggle = createUIToggle(runtime, {});
		toggle.toggle();
		expect(toggle.isChecked()).toBe(true);
		toggle.toggle();
		expect(toggle.isChecked()).toBe(false);
	});

	it('sets checked state directly via setChecked', () => {
		const runtime = prepareRuntime();
		const toggle = createUIToggle(runtime, {});
		toggle.setChecked(true);
		expect(toggle.isChecked()).toBe(true);
		toggle.setChecked(false);
		expect(toggle.isChecked()).toBe(false);
	});

	it('fires onChange on toggle() but not on setChecked', () => {
		const runtime = prepareRuntime();
		const onChange = vi.fn();
		const toggle = createUIToggle(runtime, { onChange });

		toggle.toggle();
		expect(onChange).toHaveBeenCalledTimes(1);
		expect(onChange).toHaveBeenCalledWith(true, toggle);

		toggle.setChecked(true);
		expect(onChange).toHaveBeenCalledTimes(1);
	});

	it('does not toggle when disabled', () => {
		const runtime = prepareRuntime();
		const toggle = createUIToggle(runtime, { disabled: true });
		toggle.toggle();
		expect(toggle.isChecked()).toBe(false);
	});

	it('clears pressed/hovered when disabled via setDisabled', () => {
		const runtime = prepareRuntime();
		const toggle = createUIToggle(runtime, {});
		toggle.setDisabled(true);
		runtime.commit();
	});

	it('disposes the widget tree', () => {
		const runtime = prepareRuntime();
		const before = runtime.getWidgetCount();
		const toggle = createUIToggle(runtime, {});
		expect(runtime.getWidgetCount()).toBeGreaterThan(before);
		toggle.dispose();
		expect(runtime.getWidgetCount()).toBe(before);
	});
});
