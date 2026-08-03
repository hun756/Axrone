import { describe, expect, it } from 'vitest';
import { createStackLayout, createOverlayLayout, createAnchoredLayout } from '../controls/layout-factories';

describe('@axrone/ui layout factories', () => {
	describe('createStackLayout', () => {
		it('creates a column stack with defaults', () => {
			const layout = createStackLayout();
			expect(layout.display).toBe('stack');
			expect(layout.direction).toBe('column');
			expect(layout.gap).toBe(0);
		});

		it('creates a row stack with custom gap', () => {
			const layout = createStackLayout('row', 16);
			expect(layout.display).toBe('stack');
			expect(layout.direction).toBe('row');
			expect(layout.gap).toBe(16);
		});

		it('merges overrides on top of defaults', () => {
			const layout = createStackLayout('column', 8, { width: 200, height: 100 });
			expect(layout.display).toBe('stack');
			expect(layout.direction).toBe('column');
			expect(layout.gap).toBe(8);
			expect(layout.width).toBe(200);
			expect(layout.height).toBe(100);
		});

		it('allows overrides to replace direction and gap', () => {
			const layout = createStackLayout('row', 4, { direction: 'column', gap: 20 });
			expect(layout.direction).toBe('column');
			expect(layout.gap).toBe(20);
		});
	});

	describe('createOverlayLayout', () => {
		it('creates an overlay layout with defaults', () => {
			const layout = createOverlayLayout();
			expect(layout.display).toBe('overlay');
		});

		it('merges overrides', () => {
			const layout = createOverlayLayout({ width: 300, clip: true });
			expect(layout.display).toBe('overlay');
			expect(layout.width).toBe(300);
			expect(layout.clip).toBe(true);
		});
	});

	describe('createAnchoredLayout', () => {
		it('creates an absolute layout with top-left anchor by default', () => {
			const layout = createAnchoredLayout();
			expect(layout.position).toBe('absolute');
			expect(layout.anchor).toBe('top-left');
		});

		it('accepts a custom anchor', () => {
			const layout = createAnchoredLayout('center');
			expect(layout.anchor).toBe('center');
		});

		it('merges overrides on top', () => {
			const layout = createAnchoredLayout('top-left', { width: 100, height: 50 });
			expect(layout.position).toBe('absolute');
			expect(layout.anchor).toBe('top-left');
			expect(layout.width).toBe(100);
			expect(layout.height).toBe(50);
		});

		it('allows overrides to replace anchor', () => {
			const layout = createAnchoredLayout('top-left', { anchor: 'stretch' });
			expect(layout.anchor).toBe('stretch');
		});
	});
});
