import { describe, expect, it } from 'vitest';
import { measureImageContent } from '../runtime/runtime-frame';
import type { ResolvedWidgetImage } from '../types';

const makeImage = (overrides: Partial<ResolvedWidgetImage> = {}): ResolvedWidgetImage => ({
	source: { width: 200, height: 100 },
	fit: 'contain',
	alignX: 0.5,
	alignY: 0.5,
	border: { left: 0, top: 0, right: 0, bottom: 0 },
	tint: { r: 1, g: 1, b: 1, a: 1 },
	sampling: 'linear',
	...overrides,
});

describe('@axrone/ui runtime-frame', () => {
	describe('measureImageContent', () => {
		it('returns intrinsic size with infinite constraints', () => {
			const image = makeImage({ fit: 'contain' });
			const result = measureImageContent(image, { width: Number.POSITIVE_INFINITY, height: Number.POSITIVE_INFINITY });
			expect(result.width).toBe(200);
			expect(result.height).toBe(100);
		});

		describe('fit=fill', () => {
			it('stretches to fill the constraint', () => {
				const image = makeImage({ fit: 'fill' });
				const result = measureImageContent(image, { width: 400, height: 300 });
				expect(result.width).toBe(400);
				expect(result.height).toBe(300);
			});

			it('fills only the constrained axis when the other is infinite', () => {
				const image = makeImage({ fit: 'fill' });
				const result = measureImageContent(image, { width: 400, height: Number.POSITIVE_INFINITY });
				expect(result.width).toBe(400);
				expect(result.height).toBe(100);
			});
		});

		describe('fit=contain', () => {
			it('scales down to fit within the constraint', () => {
				const image = makeImage({ fit: 'contain' });
				const result = measureImageContent(image, { width: 100, height: 100 });
				// 200x100 into 100x100 → scale = min(0.5, 1) = 0.5
				expect(result.width).toBeCloseTo(100);
				expect(result.height).toBeCloseTo(50);
			});

			it('does not scale up beyond intrinsic size when constraint is larger', () => {
				const image = makeImage({ fit: 'contain' });
				const result = measureImageContent(image, { width: 800, height: 600 });
				// 200x100 into 800x600 → scale = min(4, 6) = 4
				expect(result.width).toBeCloseTo(800);
				expect(result.height).toBeCloseTo(400);
			});
		});

		describe('fit=cover', () => {
			it('scales to cover the entire constraint', () => {
				const image = makeImage({ fit: 'cover' });
				const result = measureImageContent(image, { width: 100, height: 100 });
				// 200x100 into 100x100 → scale = max(0.5, 1) = 1
				expect(result.width).toBeCloseTo(200);
				expect(result.height).toBeCloseTo(100);
			});
		});

		describe('fit=none', () => {
			it('returns intrinsic size clamped to constraint', () => {
				const image = makeImage({ fit: 'none' });
				const result = measureImageContent(image, { width: 150, height: 80 });
				// min(200, 150) = 150, min(100, 80) = 80
				expect(result.width).toBe(150);
				expect(result.height).toBe(80);
			});

			it('returns intrinsic size when constraint is larger', () => {
				const image = makeImage({ fit: 'none' });
				const result = measureImageContent(image, { width: 500, height: 500 });
				expect(result.width).toBe(200);
				expect(result.height).toBe(100);
			});
		});

		describe('fit=scale-down', () => {
			it('behaves like none when intrinsic is smaller than constraint', () => {
				const image = makeImage({ fit: 'scale-down' });
				const result = measureImageContent(image, { width: 500, height: 500 });
				// scale-down = min(1, containScale). containScale = min(2.5, 5) = 2.5. min(1, 2.5) = 1
				expect(result.width).toBe(200);
				expect(result.height).toBe(100);
			});

			it('behaves like contain when intrinsic is larger than constraint', () => {
				const image = makeImage({ fit: 'scale-down' });
				const result = measureImageContent(image, { width: 100, height: 100 });
				// containScale = min(0.5, 1) = 0.5. min(1, 0.5) = 0.5
				expect(result.width).toBeCloseTo(100);
				expect(result.height).toBeCloseTo(50);
			});
		});
	});
});
