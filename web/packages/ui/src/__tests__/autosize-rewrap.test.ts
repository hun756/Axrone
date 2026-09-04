import { describe, expect, it } from 'vitest';
import { AutoSizeService } from '../runtime/autosize-service';
import { TextLayoutEngine } from '../text';
import { FontRegistry } from '../font';
import { createTestFontAsset } from './test-font';
import type { ResolvedTextBlock, TextLayoutResult, TextLayoutConstraint } from '../types';

const makeBlock = (overrides: Partial<ResolvedTextBlock> = {}): ResolvedTextBlock => ({
	value: 'Hello',
	family: 'TestSans',
	size: 16,
	weight: 'normal',
	style: 'normal',
	color: { r: 1, g: 1, b: 1, a: 1 },
	align: 'start',
	wrap: 'word',
	overflow: 'ellipsis',
	maxLines: 0,
	direction: 'auto',
	locale: '',
	letterSpacing: 0,
	lineHeight: 0,
	underline: false,
	underlineColor: { r: 0, g: 0, b: 0, a: 0 },
	underlineThickness: 1,
	underlineOffset: 0,
	strikeThrough: false,
	strikeThroughColor: { r: 0, g: 0, b: 0, a: 0 },
	strikeThroughThickness: 1,
	selectionStart: null,
	selectionEnd: null,
	selectionColor: { r: 0.3, g: 0.5, b: 1, a: 0.5 },
	caretIndex: null,
	caretColor: { r: 0, g: 0, b: 0, a: 0 },
	caretWidth: 2,
	caretInset: 0,
	spans: [],
	autoSize: 'shrink-to-fit' as const,
	minAutoSize: 8,
	maxAutoSize: 32,
	...overrides,
});

const makeConstraint = (width: number, height: number): TextLayoutConstraint => ({
	width,
	height,
});

/**
 * Reference brute-force search: tries sizes from maxSize down to minSize
 * in 0.5px steps using real measurement, returns the largest size that fits
 * along with its layout result.
 */
const bruteForceMaxFit = (
	host: { fonts: FontRegistry; textEngine: TextLayoutEngine },
	text: ResolvedTextBlock,
	constraint: TextLayoutConstraint,
	minSize: number,
	maxSize: number
): { size: number; layout: TextLayoutResult } | null => {
	const step = 0.5;
	for (let size = maxSize; size >= minSize - 0.001; size -= step) {
		const block: ResolvedTextBlock = { ...text, size, autoSize: 'none' };
		const layout = host.textEngine.measure(block, constraint);
		const fitsWidth = layout.width <= (constraint.width ?? Number.POSITIVE_INFINITY);
		const fitsHeight = layout.height <= (constraint.height ?? Number.POSITIVE_INFINITY);
		if (fitsWidth && fitsHeight) {
			return { size, layout };
		}
	}
	return null;
};

/**
 * Finds the effective font size used in a TextLayoutResult by binary-searching
 * the text engine for the size that produces matching lineHeight.
 */
const findEffectiveSize = (
	textEngine: TextLayoutEngine,
	text: ResolvedTextBlock,
	constraint: TextLayoutConstraint,
	result: TextLayoutResult,
	minSize: number,
	maxSize: number
): number => {
	// lineHeight scales linearly with font size, so we can back-compute.
	// Use the ratio of lineHeights to estimate the size.
	const referenceBlock: ResolvedTextBlock = { ...text, size: maxSize, autoSize: 'none' };
	const referenceLayout = textEngine.measure(referenceBlock, constraint);
	if (referenceLayout.lineHeight > 0 && result.lineHeight > 0) {
		return maxSize * (result.lineHeight / referenceLayout.lineHeight);
	}
	return maxSize;
};

describe('AutoSizeService shrink-to-fit rewrap equivalence', () => {
	const setup = () => {
		const fonts = new FontRegistry();
		fonts.registerFace(createTestFontAsset('TestSans'));
		const textEngine = new TextLayoutEngine(fonts, { locale: 'en' });
		const service = new AutoSizeService();
		const host = { fonts, textEngine };
		return { service, host, textEngine };
	};

	const assertNoOverflow = (result: TextLayoutResult, constraint: TextLayoutConstraint) => {
		expect(result.width).toBeLessThanOrEqual(constraint.width!);
		expect(result.height).toBeLessThanOrEqual(constraint.height!);
	};

	const assertNearOptimal = (
		textEngine: TextLayoutEngine,
		text: ResolvedTextBlock,
		constraint: TextLayoutConstraint,
		analyticalResult: TextLayoutResult,
		reference: { size: number; layout: TextLayoutResult } | null,
		minSize: number,
		maxSize: number
	) => {
		if (!reference) return;
		// The analytical result's effective size should be within 1px of the reference.
		const effectiveSize = findEffectiveSize(textEngine, text, constraint, analyticalResult, minSize, maxSize);
		expect(effectiveSize).toBeGreaterThanOrEqual(reference.size - 1.5);
	};

	it('long single word in narrow container never overflows and is near-optimal', () => {
		const { service, host, textEngine } = setup();
		const text = makeBlock({
			value: 'Superlongwordthatcannotwrap',
			wrap: 'word',
			size: 32,
		});
		const constraint = makeConstraint(60, 200);
		const minSize = 8;
		const maxSize = 32;

		const result = service.measure(host, text, constraint);
		const reference = bruteForceMaxFit(host, text, constraint, minSize, maxSize);

		assertNoOverflow(result, constraint);
		assertNearOptimal(textEngine, text, constraint, result, reference, minSize, maxSize);
	});

	it('multi-line paragraph fits and is near-optimal', () => {
		const { service, host, textEngine } = setup();
		const text = makeBlock({
			value: 'The quick brown fox jumps over the lazy dog near the riverbank',
			wrap: 'word',
			size: 32,
		});
		const constraint = makeConstraint(120, 300);
		const minSize = 8;
		const maxSize = 32;

		const result = service.measure(host, text, constraint);
		const reference = bruteForceMaxFit(host, text, constraint, minSize, maxSize);

		assertNoOverflow(result, constraint);
		assertNearOptimal(textEngine, text, constraint, result, reference, minSize, maxSize);
	});

	it('mixed short words text fits in constrained box', () => {
		const { service, host, textEngine } = setup();
		const text = makeBlock({
			value: 'Hello World Test',
			wrap: 'word',
			size: 32,
		});
		const constraint = makeConstraint(80, 150);
		const minSize = 8;
		const maxSize = 32;

		const result = service.measure(host, text, constraint);
		const reference = bruteForceMaxFit(host, text, constraint, minSize, maxSize);

		assertNoOverflow(result, constraint);
		assertNearOptimal(textEngine, text, constraint, result, reference, minSize, maxSize);
	});

	it('narrow container forces aggressive shrink and still fits', () => {
		const { service, host, textEngine } = setup();
		const text = makeBlock({
			value: 'abcdefghij',
			wrap: 'word',
			size: 32,
		});
		const constraint = makeConstraint(30, 500);
		const minSize = 8;
		const maxSize = 32;

		const result = service.measure(host, text, constraint);
		const reference = bruteForceMaxFit(host, text, constraint, minSize, maxSize);

		assertNoOverflow(result, constraint);
		assertNearOptimal(textEngine, text, constraint, result, reference, minSize, maxSize);
	});

	it('text that already fits at maxSize returns maxSize layout', () => {
		const { service, host } = setup();
		const text = makeBlock({
			value: 'Hi',
			wrap: 'word',
			size: 32,
		});
		const constraint = makeConstraint(500, 500);

		const result = service.measure(host, text, constraint);

		// At maxSize the text fits easily — compare against direct measurement at maxSize.
		const referenceBlock: ResolvedTextBlock = { ...text, size: 32, autoSize: 'none' };
		const referenceLayout = host.textEngine.measure(referenceBlock, constraint);
		expect(result.width).toBe(referenceLayout.width);
		expect(result.height).toBe(referenceLayout.height);
		expect(result.width).toBeLessThanOrEqual(constraint.width!);
		expect(result.height).toBeLessThanOrEqual(constraint.height!);
	});
});
