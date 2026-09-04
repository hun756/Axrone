import { describe, expect, it } from 'vitest';
import { resolveSliceSpans, createSliceSpanTriple, type SliceSpan } from '../nine-slice';

const readSpans = (spans: readonly [SliceSpan, SliceSpan, SliceSpan]) =>
	spans.map((s) => ({ offset: s.offset, size: s.size, uvOffset: s.uvOffset, uvSize: s.uvSize }));

describe('resolveSliceSpans', () => {
	it('resolves normal borders into three spans', () => {
		const out = createSliceSpanTriple();
		resolveSliceSpans(100, 10, 20, 100, 0, 1, out);
		const [start, center, end] = readSpans(out);
		expect(start.offset).toBe(0);
		expect(start.size).toBe(10);
		expect(center.size).toBe(70);
		expect(end.offset).toBe(80);
		expect(end.size).toBe(20);
		// UV coverage should span the full source.
		expect(start.uvSize + center.uvSize + end.uvSize).toBeCloseTo(1);
	});

	it('scales borders down when they exceed the extent', () => {
		const out = createSliceSpanTriple();
		// Borders total 60 but extent is only 30 → scale = 0.5
		resolveSliceSpans(30, 40, 20, 100, 0, 1, out);
		const [start, center, end] = readSpans(out);
		expect(start.size).toBe(20); // 40 * 0.5
		expect(end.size).toBe(10); // 20 * 0.5
		expect(center.size).toBe(0); // 30 - 20 - 10 = 0
	});

	it('handles zero borders', () => {
		const out = createSliceSpanTriple();
		resolveSliceSpans(100, 0, 0, 100, 0, 1, out);
		const [start, center, end] = readSpans(out);
		expect(start.size).toBe(0);
		expect(center.size).toBe(100);
		expect(end.size).toBe(0);
	});

	it('handles zero source extent without division errors', () => {
		const out = createSliceSpanTriple();
		resolveSliceSpans(100, 10, 10, 0, 0.5, 0.5, out);
		const [start, , end] = readSpans(out);
		// sourceExtent = 0 → startUv and endUv should be 0
		expect(start.uvSize).toBe(0);
		expect(end.uvSize).toBe(0);
	});

	it('preserves UV offset and extent', () => {
		const out = createSliceSpanTriple();
		resolveSliceSpans(200, 50, 50, 200, 0.25, 0.5, out);
		const [start, center, end] = readSpans(out);
		expect(start.uvOffset).toBe(0.25);
		// startUv = startBorder / sourceExtent = 50/200 = 0.25
		expect(start.uvSize).toBeCloseTo(0.25);
		// center.uvSize = max(0, uvExtent - startUv - endUv) = max(0, 0.5 - 0.25 - 0.25) = 0
		expect(center.uvSize).toBeCloseTo(0);
		// end.uvOffset = uvOffset + uvExtent - endUv = 0.25 + 0.5 - 0.25 = 0.5
		expect(end.uvOffset).toBeCloseTo(0.5);
		expect(end.uvSize).toBeCloseTo(0.25);
	});
});
