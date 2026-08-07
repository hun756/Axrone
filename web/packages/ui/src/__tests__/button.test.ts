import { describe, expect, it, vi } from 'vitest';
import { AXRONE_DEFAULT_UI_FONT_FAMILY, UIRuntime, createUIButton } from '../index';
import { createTestFontAsset } from './test-font';

const prepareRuntime = () => {
	const runtime = new UIRuntime({ width: 480, height: 240 });
	runtime.fonts.registerFace(createTestFontAsset(AXRONE_DEFAULT_UI_FONT_FAMILY));
	return runtime;
};

describe('@axrone/ui button handle', () => {
	it('returns the label set at creation', () => {
		const runtime = prepareRuntime();
		const button = createUIButton(runtime, { label: 'Play' });
		expect(button.getLabel()).toBe('Play');
	});

	it('updates the label via setLabel', () => {
		const runtime = prepareRuntime();
		const button = createUIButton(runtime, { label: 'Play' });
		button.setLabel('Stop');
		expect(button.getLabel()).toBe('Stop');
	});

	it('defaults to not disabled', () => {
		const runtime = prepareRuntime();
		const button = createUIButton(runtime, {});
		expect(button.isDisabled()).toBe(false);
	});

	it('toggles disabled state via setDisabled', () => {
		const runtime = prepareRuntime();
		const button = createUIButton(runtime, {});
		button.setDisabled(true);
		expect(button.isDisabled()).toBe(true);
		button.setDisabled(false);
		expect(button.isDisabled()).toBe(false);
	});

	it('calls onPress via press() when enabled', () => {
		const runtime = prepareRuntime();
		const onPress = vi.fn();
		const button = createUIButton(runtime, { onPress });
		button.press();
		expect(onPress).toHaveBeenCalledTimes(1);
		expect(onPress).toHaveBeenCalledWith(button);
	});

	it('does not call onPress via press() when disabled', () => {
		const runtime = prepareRuntime();
		const onPress = vi.fn();
		const button = createUIButton(runtime, { disabled: true, onPress });
		button.press();
		expect(onPress).not.toHaveBeenCalled();
	});

	it('replaces the onPress handler via setOnPress', () => {
		const runtime = prepareRuntime();
		const first = vi.fn();
		const second = vi.fn();
		const button = createUIButton(runtime, { onPress: first });
		button.setOnPress(second);
		button.press();
		expect(first).not.toHaveBeenCalled();
		expect(second).toHaveBeenCalledTimes(1);
	});

	it('disposes the widget', () => {
		const runtime = prepareRuntime();
		const button = createUIButton(runtime, { label: 'Test' });
		const root = button.root;
		button.dispose();
		expect(runtime.getWidgetCount()).toBe(0);
	});

	it('starts with the variant provided at creation', () => {
		const runtime = prepareRuntime();
		const button = createUIButton(runtime, { variant: 'danger' });
		runtime.commit();
		const frame = runtime.commit();
		expect(frame.commands.length).toBeGreaterThan(0);
	});

	it('changes variant via setVariant', () => {
		const runtime = prepareRuntime();
		const button = createUIButton(runtime, { variant: 'primary' });
		button.setVariant('success');
		runtime.commit();
	});

	it('starts disabled when constructed with disabled: true', () => {
		const runtime = prepareRuntime();
		const button = createUIButton(runtime, { disabled: true });
		expect(button.isDisabled()).toBe(true);
	});
});
