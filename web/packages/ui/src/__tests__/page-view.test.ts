import { describe, expect, it } from 'vitest';
import { AXRONE_DEFAULT_UI_FONT_FAMILY, UIRuntime, createUIPageView } from '../index';
import { createTestFontAsset } from './test-font';

const prepareRuntime = () => {
	const runtime = new UIRuntime({ width: 480, height: 240 });
	runtime.fonts.registerFace(createTestFontAsset(AXRONE_DEFAULT_UI_FONT_FAMILY));
	return runtime;
};

describe('@axrone/ui page-view handle', () => {
	describe('page navigation', () => {
		it('starts at page 0 by default', () => {
			const runtime = prepareRuntime();
			const pageView = createUIPageView(runtime, {});
			expect(pageView.getPage()).toBe(0);
		});

		it('retains the provided page index even before pages are added', () => {
			const runtime = prepareRuntime();
			const pageView = createUIPageView(runtime, { page: 2 });
			// The initial page value is stored as-is; clamping only occurs when pages are added.
			expect(pageView.getPage()).toBe(2);
		});

		it('clamps setPage to valid range', () => {
			const runtime = prepareRuntime();
			const pageView = createUIPageView(runtime, {});
			const p1 = runtime.createWidget({ layout: { width: 100, height: 100 } });
			const p2 = runtime.createWidget({ layout: { width: 100, height: 100 } });
			const p3 = runtime.createWidget({ layout: { width: 100, height: 100 } });
			pageView.addPage(p1);
			pageView.addPage(p2);
			pageView.addPage(p3);

			pageView.setPage(1);
			expect(pageView.getPage()).toBe(1);

			pageView.setPage(-5);
			expect(pageView.getPage()).toBe(0);

			pageView.setPage(100);
			expect(pageView.getPage()).toBe(2);
		});
	});

	describe('addPage', () => {
		it('returns the page index', () => {
			const runtime = prepareRuntime();
			const pageView = createUIPageView(runtime, {});
			const p1 = runtime.createWidget({ layout: { width: 100, height: 100 } });
			const p2 = runtime.createWidget({ layout: { width: 100, height: 100 } });
			expect(pageView.addPage(p1)).toBe(0);
			expect(pageView.addPage(p2)).toBe(1);
		});
	});

	describe('next / previous', () => {
		it('navigates forward and backward', () => {
			const runtime = prepareRuntime();
			const pageView = createUIPageView(runtime, {});
			const p1 = runtime.createWidget({ layout: { width: 100, height: 100 } });
			const p2 = runtime.createWidget({ layout: { width: 100, height: 100 } });
			const p3 = runtime.createWidget({ layout: { width: 100, height: 100 } });
			pageView.addPage(p1);
			pageView.addPage(p2);
			pageView.addPage(p3);
			pageView.setPage(0);

			expect(pageView.next()).toBe(1);
			expect(pageView.next()).toBe(2);
			// Already at last page, next stays
			expect(pageView.next()).toBe(2);

			expect(pageView.previous()).toBe(1);
			expect(pageView.previous()).toBe(0);
			// Already at first page, previous stays
			expect(pageView.previous()).toBe(0);
		});

		it('returns 0 when there are no pages', () => {
			const runtime = prepareRuntime();
			const pageView = createUIPageView(runtime, {});
			expect(pageView.next()).toBe(0);
			expect(pageView.previous()).toBe(0);
		});
	});

	describe('dispose', () => {
		it('removes the widget tree', () => {
			const runtime = prepareRuntime();
			const before = runtime.getWidgetCount();
			const pageView = createUIPageView(runtime, {});
			expect(runtime.getWidgetCount()).toBeGreaterThan(before);
			pageView.dispose();
			expect(runtime.getWidgetCount()).toBe(before);
		});
	});

	describe('render output', () => {
		it('produces render commands', () => {
			const runtime = prepareRuntime();
			const pageView = createUIPageView(runtime, {});
			const p1 = runtime.createWidget({ layout: { width: 100, height: 100 } });
			pageView.addPage(p1);
			const frame = runtime.commit();
			expect(frame.commands.length).toBeGreaterThan(0);
		});
	});
});
