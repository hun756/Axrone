import { describe, it, expect, expectTypeOf } from 'vitest';
import type { ColorLike, ReadonlyColor } from '../types';
import { normalizeColor } from '../types/color';

describe('ColorLike vs ReadonlyColor contract', () => {
	it('ReadonlyColor requires all four channels', () => {
		const resolved: ReadonlyColor = { r: 1, g: 0, b: 0, a: 1 };
		expectTypeOf(resolved).toHaveProperty('a');
		expectTypeOf(resolved.a).toEqualTypeOf<number>();
	});

	it('ColorLike allows omitting alpha', () => {
		const input: ColorLike = { r: 1, g: 0, b: 0 };
		expectTypeOf(input.a).toEqualTypeOf<number | undefined>();
	});

	it('ReadonlyColor is assignable to ColorLike (resolved is valid input)', () => {
		const resolved: ReadonlyColor = { r: 0, g: 0, b: 0, a: 1 };
		expectTypeOf(resolved).toMatchTypeOf<ColorLike>();
	});

	it('ColorLike is NOT assignable to ReadonlyColor (input may lack alpha)', () => {
		const input: ColorLike = { r: 1, g: 0, b: 0 };
		// This verifies the structural difference: ColorLike.a is optional,
		// ReadonlyColor.a is required.
		expectTypeOf<ColorLike>().not.toMatchTypeOf<ReadonlyColor>();
	});

	it('normalizeColor returns ReadonlyColor (resolved, not raw input)', () => {
		const fallback: ReadonlyColor = { r: 0, g: 0, b: 0, a: 0 };
		const result = normalizeColor('#FF0000', fallback);
		expectTypeOf(result).toEqualTypeOf<ReadonlyColor>();
	});
});

describe('normalizeColor short array guard', () => {
	const fallback: ReadonlyColor = { r: 0, g: 0, b: 0, a: 1 };

	it('returns fallback for empty array', () => {
		const result = normalizeColor([] as unknown as readonly number[], fallback);
		expect(result).toEqual(fallback);
	});

	it('returns fallback for single-element array', () => {
		const result = normalizeColor([0.5] as unknown as readonly number[], fallback);
		expect(result).toEqual(fallback);
	});

	it('returns fallback for two-element array', () => {
		const result = normalizeColor([0.5, 0.2] as unknown as readonly number[], fallback);
		expect(result).toEqual(fallback);
	});

	it('handles three-element array as RGB with alpha=1', () => {
		const result = normalizeColor([0.5, 0.3, 0.1] as readonly number[], fallback);
		expect(result).toEqual({ r: 0.5, g: 0.3, b: 0.1, a: 1 });
	});

	it('handles four-element array as RGBA', () => {
		const result = normalizeColor([0.5, 0.3, 0.1, 0.8] as readonly number[], fallback);
		expect(result).toEqual({ r: 0.5, g: 0.3, b: 0.1, a: 0.8 });
	});
});
