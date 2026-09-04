import { describe, it, expectTypeOf } from 'vitest';
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
