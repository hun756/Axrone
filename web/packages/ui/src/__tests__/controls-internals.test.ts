import { describe, expect, it, vi } from 'vitest';
import {
	clamp,
	normalizeRange,
	normalizeSteppedValue,
	countStepDecimals,
	formatNumericValue,
	clampIndex,
	resolveParentWidget,
	isPointInside,
	createTextBlock,
	resolveFontFamily,
} from '../controls/internals';
import { resolveTheme } from '../controls/theme';
import { UIRuntime } from '../runtime';

describe('@axrone/ui controls internals', () => {
	describe('clamp', () => {
		it('returns min when value is below', () => {
			expect(clamp(-5, 0, 10)).toBe(0);
		});

		it('returns max when value is above', () => {
			expect(clamp(15, 0, 10)).toBe(10);
		});

		it('returns value when within range', () => {
			expect(clamp(5, 0, 10)).toBe(5);
		});

		it('handles equal min and max', () => {
			expect(clamp(5, 3, 3)).toBe(3);
		});
	});

	describe('normalizeRange', () => {
		it('returns the range as-is when min <= max', () => {
			expect(normalizeRange(0, 100)).toEqual({ min: 0, max: 100 });
		});

		it('swaps min and max when min > max', () => {
			expect(normalizeRange(100, 0)).toEqual({ min: 0, max: 100 });
		});

		it('handles equal values', () => {
			expect(normalizeRange(5, 5)).toEqual({ min: 5, max: 5 });
		});
	});

	describe('normalizeSteppedValue', () => {
		it('snaps to the nearest step', () => {
			expect(normalizeSteppedValue(7, 0, 10, 3)).toBe(6);
		});

		it('clamps to min when value is below', () => {
			expect(normalizeSteppedValue(-5, 0, 10, 2)).toBe(0);
		});

		it('clamps to max when value is above', () => {
			expect(normalizeSteppedValue(15, 0, 10, 2)).toBe(10);
		});

		it('uses step=1 when step is zero or NaN', () => {
			expect(normalizeSteppedValue(3.7, 0, 10, 0)).toBe(4);
			expect(normalizeSteppedValue(3.7, 0, 10, Number.NaN)).toBe(4);
		});
	});

	describe('countStepDecimals', () => {
		it('returns 0 for integers', () => {
			expect(countStepDecimals(1)).toBe(0);
			expect(countStepDecimals(100)).toBe(0);
		});

		it('returns the correct decimal count', () => {
			expect(countStepDecimals(0.1)).toBe(1);
			expect(countStepDecimals(0.01)).toBe(2);
			expect(countStepDecimals(0.001)).toBe(3);
		});

		it('returns 0 for NaN or non-positive values', () => {
			expect(countStepDecimals(Number.NaN)).toBe(0);
			expect(countStepDecimals(-1)).toBe(0);
			expect(countStepDecimals(0)).toBe(0);
		});

		it('returns 0 for Infinity', () => {
			expect(countStepDecimals(Number.POSITIVE_INFINITY)).toBe(0);
		});
	});

	describe('formatNumericValue', () => {
		it('formats integers without decimals', () => {
			expect(formatNumericValue(42, 0.01)).toBe('42');
		});

		it('formats values with the precision implied by the step', () => {
			expect(formatNumericValue(3.14, 0.01)).toBe('3.14');
		});

		it('formats with step=1 as integer', () => {
			expect(formatNumericValue(3.7, 1)).toBe('4');
		});
	});

	describe('clampIndex', () => {
		it('clamps negative values to 0', () => {
			expect(clampIndex(-3, 10)).toBe(0);
		});

		it('clamps values beyond max', () => {
			expect(clampIndex(15, 10)).toBe(10);
		});

		it('floors fractional values', () => {
			expect(clampIndex(4.8, 10)).toBe(4);
		});

		it('returns valid indices unchanged', () => {
			expect(clampIndex(5, 10)).toBe(5);
		});
	});

	describe('resolveParentWidget', () => {
		const runtime = new UIRuntime({ width: 100, height: 100 });

		it('returns runtime.root for null', () => {
			expect(resolveParentWidget(runtime, null)).toBe(runtime.root);
		});

		it('returns runtime.root for undefined', () => {
			expect(resolveParentWidget(runtime, undefined)).toBe(runtime.root);
		});

		it('returns the numeric id directly', () => {
			expect(resolveParentWidget(runtime, 42)).toBe(42);
		});

		it('returns content from a handle with content', () => {
			const handle = { root: 10, content: 20, dispose: () => {} };
			expect(resolveParentWidget(runtime, handle)).toBe(20);
		});

		it('returns root from a handle without content', () => {
			const handle = { root: 10, dispose: () => {} };
			expect(resolveParentWidget(runtime, handle)).toBe(10);
		});
	});

	describe('isPointInside', () => {
		const runtime = new UIRuntime({ width: 400, height: 300 });
		const widget = runtime.createWidget({
			layout: { position: 'absolute', anchor: 'top-left', width: 100, height: 50 },
		});
		runtime.appendChild(runtime.root, widget);
		runtime.commit();

		it('returns true for a point inside the widget', () => {
			expect(isPointInside(runtime, widget, 50, 25)).toBe(true);
		});

		it('returns false for a point outside the widget', () => {
			expect(isPointInside(runtime, widget, 200, 200)).toBe(false);
		});

		it('returns true for a point on the edge', () => {
			const box = runtime.getLayoutBox(widget);
			expect(isPointInside(runtime, widget, box.x, box.y)).toBe(true);
		});
	});

	describe('resolveFontFamily', () => {
		const runtime = new UIRuntime({ width: 100, height: 100 });
		const theme = resolveTheme(undefined);

		it('returns the override when provided', () => {
			expect(resolveFontFamily(runtime, theme, 'CustomFont')).toBe('CustomFont');
		});

		it('falls back to theme.fontFamily when no override', () => {
			const customTheme = resolveTheme({ fontFamily: 'ThemeFont' });
			expect(resolveFontFamily(runtime, customTheme)).toBe('ThemeFont');
		});

		it('falls back to empty string when no font is available anywhere', () => {
			expect(resolveFontFamily(runtime, theme)).toBe('');
		});
	});

	describe('createTextBlock', () => {
		const runtime = new UIRuntime({ width: 100, height: 100 });
		const theme = resolveTheme(undefined);

		it('creates a text block with defaults', () => {
			const block = createTextBlock(runtime, 'Hello', theme, undefined, '#ffffffff');
			expect(block.value).toBe('Hello');
			expect(block.size).toBe(theme.fontSize);
			expect(block.color).toBe('#ffffffff');
			expect(block.wrap).toBe('word');
			expect(block.overflow).toBe('ellipsis');
			expect(block.align).toBe('start');
		});

		it('applies overrides', () => {
			const block = createTextBlock(runtime, 'Hi', theme, {
				size: 24,
				weight: 'bold',
				wrap: 'none',
				color: '#00ff00ff',
			}, '#ffffffff');
			expect(block.size).toBe(24);
			expect(block.weight).toBe('bold');
			expect(block.wrap).toBe('none');
			expect(block.color).toBe('#00ff00ff');
		});
	});
});
