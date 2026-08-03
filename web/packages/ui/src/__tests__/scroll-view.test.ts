import { describe, expect, it } from 'vitest';
import { AXRONE_DEFAULT_UI_FONT_FAMILY, UIRuntime, createUIScrollView } from '../index';
import { createTestFontAsset } from './test-font';

const prepareRuntime = () => {
	const runtime = new UIRuntime({ width: 480, height: 240 });
	runtime.fonts.registerFace(createTestFontAsset(AXRONE_DEFAULT_UI_FONT_FAMILY));
	return runtime;
};

describe('@axrone/ui scroll-view handle', () => {
	describe('scroll position', () => {
		it('returns the initial scroll position', () => {
			const runtime = prepareRuntime();
			const scroll = createUIScrollView(runtime, { scrollX: 10, scrollY: 20 });
			expect(scroll.getScroll()).toEqual({ x: 10, y: 20 });
		});

		it('defaults scroll to (0, 0)', () => {
			const runtime = prepareRuntime();
			const scroll = createUIScrollView(runtime, {});
			expect(scroll.getScroll()).toEqual({ x: 0, y: 0 });
		});

		it('clamps negative initial scroll to 0', () => {
			const runtime = prepareRuntime();
			const scroll = createUIScrollView(runtime, { scrollX: -5, scrollY: -10 });
			expect(scroll.getScroll()).toEqual({ x: 0, y: 0 });
		});

		it('sets scroll via setScroll, clamping to >= 0', () => {
			const runtime = prepareRuntime();
			const scroll = createUIScrollView(runtime, {});
			scroll.setScroll(50, 100);
			expect(scroll.getScroll()).toEqual({ x: 50, y: 100 });
			scroll.setScroll(-10, -20);
			expect(scroll.getScroll()).toEqual({ x: 0, y: 0 });
		});

		it('scrolls relatively via scrollBy', () => {
			const runtime = prepareRuntime();
			const scroll = createUIScrollView(runtime, { scrollX: 10, scrollY: 20 });
			scroll.scrollBy(5, -30);
			expect(scroll.getScroll()).toEqual({ x: 15, y: 0 });
		});
	});

	describe('clampToBounds', () => {
		it('does not throw when called before layout', () => {
			const runtime = prepareRuntime();
			const scroll = createUIScrollView(runtime, {});
			expect(() => scroll.clampToBounds()).not.toThrow();
		});

		it('clamps after layout when content is smaller than viewport', () => {
			const runtime = prepareRuntime();
			const scroll = createUIScrollView(runtime, { scrollX: 999, scrollY: 999 });
			runtime.commit();
			scroll.clampToBounds();
			const pos = scroll.getScroll();
			// After clamping, scroll should be >= 0
			expect(pos.x).toBeGreaterThanOrEqual(0);
			expect(pos.y).toBeGreaterThanOrEqual(0);
		});
	});

	describe('dispose', () => {
		it('removes the widget tree', () => {
			const runtime = prepareRuntime();
			const before = runtime.getWidgetCount();
			const scroll = createUIScrollView(runtime, {});
			expect(runtime.getWidgetCount()).toBeGreaterThan(before);
			scroll.dispose();
			expect(runtime.getWidgetCount()).toBe(before);
		});
	});

	describe('render output', () => {
		it('produces render commands after scroll', () => {
			const runtime = prepareRuntime();
			const scroll = createUIScrollView(runtime, {});
			scroll.setScroll(10, 20);
			const frame = runtime.commit();
			expect(frame.commands.length).toBeGreaterThan(0);
		});
	});
});
