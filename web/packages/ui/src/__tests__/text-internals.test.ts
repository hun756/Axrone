import { describe, expect, it } from 'vitest';
import { LruCache, createCacheKey, isWhitespace, detectDirection, createGraphemeSegments, getSegmenterCacheSize } from '../text/internals';
import { TextLayoutEngine } from '../text';
import { FontRegistry } from '../font';
import { createTestFontAsset } from './test-font';
import type { ResolvedTextBlock } from '../types';

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
	...overrides,
});

describe('@axrone/ui text internals', () => {
	describe('LruCache', () => {
		it('stores and retrieves values', () => {
			const cache = new LruCache<string, number>(3);
			cache.set('a', 1);
			cache.set('b', 2);
			expect(cache.get('a')).toBe(1);
			expect(cache.get('b')).toBe(2);
		});

		it('returns undefined for missing keys', () => {
			const cache = new LruCache<string, number>(3);
			expect(cache.get('missing')).toBeUndefined();
		});

		it('evicts the least-recently-used entry at capacity', () => {
			const cache = new LruCache<string, number>(2);
			cache.set('a', 1);
			cache.set('b', 2);
			cache.set('c', 3); // should evict 'a'
			expect(cache.get('a')).toBeUndefined();
			expect(cache.get('b')).toBe(2);
			expect(cache.get('c')).toBe(3);
		});

		it('promotes accessed entries so they are not evicted', () => {
			const cache = new LruCache<string, number>(2);
			cache.set('a', 1);
			cache.set('b', 2);
			cache.get('a'); // promote 'a'
			cache.set('c', 3); // should evict 'b' (LRU)
			expect(cache.get('a')).toBe(1);
			expect(cache.get('b')).toBeUndefined();
			expect(cache.get('c')).toBe(3);
		});

		it('updates existing keys without increasing size', () => {
			const cache = new LruCache<string, number>(2);
			cache.set('a', 1);
			cache.set('a', 10);
			expect(cache.get('a')).toBe(10);
			cache.set('b', 2);
			// 'a' should still be there since update refreshed it
			expect(cache.get('a')).toBe(10);
			expect(cache.get('b')).toBe(2);
		});

		it('clears all entries', () => {
			const cache = new LruCache<string, number>(3);
			cache.set('a', 1);
			cache.set('b', 2);
			cache.clear();
			expect(cache.get('a')).toBeUndefined();
			expect(cache.get('b')).toBeUndefined();
		});

		it('enforces a minimum limit of 1', () => {
			const cache = new LruCache<string, number>(0);
			cache.set('a', 1);
			cache.set('b', 2);
			// With limit=1, only the last entry survives
			expect(cache.get('a')).toBeUndefined();
			expect(cache.get('b')).toBe(2);
		});
	});

	describe('isWhitespace', () => {
		it('returns true for space', () => {
			expect(isWhitespace(' ')).toBe(true);
		});

		it('returns true for tab', () => {
			expect(isWhitespace('\t')).toBe(true);
		});

		it('returns false for newline', () => {
			expect(isWhitespace('\n')).toBe(false);
		});

		it('returns false for non-whitespace', () => {
			expect(isWhitespace('a')).toBe(false);
		});

		it('returns true for multiple spaces', () => {
			expect(isWhitespace('   ')).toBe(true);
		});
	});

	describe('detectDirection', () => {
		it('returns ltr for English text', () => {
			expect(detectDirection('Hello world', 'auto')).toBe('ltr');
		});

		it('returns rtl for Hebrew text', () => {
			expect(detectDirection('שלום', 'auto')).toBe('rtl');
		});

		it('returns rtl for Arabic text', () => {
			expect(detectDirection('مرحبا', 'auto')).toBe('rtl');
		});

		it('honors explicit ltr request', () => {
			expect(detectDirection('שלום', 'ltr')).toBe('ltr');
		});

		it('honors explicit rtl request', () => {
			expect(detectDirection('hello', 'rtl')).toBe('rtl');
		});

		it('defaults to ltr for empty text', () => {
			expect(detectDirection('', 'auto')).toBe('ltr');
		});
	});

	describe('createCacheKey', () => {
		it('produces a deterministic key from block params', () => {
			const block = makeBlock({ value: 'Hello', size: 16 });
			const key1 = createCacheKey(block, 1, 100, 200);
			const key2 = createCacheKey(block, 1, 100, 200);
			expect(key1).toBe(key2);
		});

		it('produces different keys for different text', () => {
			const block1 = makeBlock({ value: 'Hello' });
			const block2 = makeBlock({ value: 'World' });
			expect(createCacheKey(block1, 1, 100, 200)).not.toBe(createCacheKey(block2, 1, 100, 200));
		});

		it('produces different keys for different constraints', () => {
			const block = makeBlock();
			expect(createCacheKey(block, 1, 100, 200)).not.toBe(createCacheKey(block, 1, 200, 300));
		});

		it('produces different keys for different faceIds', () => {
			const block = makeBlock();
			expect(createCacheKey(block, 1, 100, 200)).not.toBe(createCacheKey(block, 2, 100, 200));
		});

		it('handles null faceId', () => {
			const block = makeBlock();
			const key = createCacheKey(block, null, 100, 200);
			expect(key).toContain('none');
		});
	});

	describe('createGraphemeSegments', () => {
		it('segments basic ASCII text', () => {
			const segments = createGraphemeSegments('abc', 'en');
			expect(segments).toEqual(['a', 'b', 'c']);
		});

		it('returns an empty array for empty string', () => {
			expect(createGraphemeSegments('', 'en')).toEqual([]);
		});

		it('handles emoji as single segments when Intl.Segmenter is available', () => {
			const segments = createGraphemeSegments('a😀b', 'en');
			// With Intl.Segmenter: ['a', '😀', 'b']
			// Without: ['a', '😀', 'b'] (Array.from handles BMP+surrogate pairs)
			expect(segments.length).toBe(3);
			expect(segments[0]).toBe('a');
			expect(segments[segments.length - 1]).toBe('b');
		});

		it('reuses cached segmenter instances across calls', () => {
			// Warm up the cache with a call using a valid locale
			createGraphemeSegments('hello', 'de');
			const sizeAfterFirst = getSegmenterCacheSize();
			// Second call with same locale should reuse the cached instance
			createGraphemeSegments('world', 'de');
			const sizeAfterSecond = getSegmenterCacheSize();
			// Cache size should not grow since the same locale was used
			expect(sizeAfterSecond).toBe(sizeAfterFirst);
		});

		it('size reflects actual entry count after evictions', () => {
			// Use a small LruCache to test eviction tracking
			const cache = new LruCache<string, number>(3);
			cache.set('a', 1);
			cache.set('b', 2);
			cache.set('c', 3);
			expect(cache.size).toBe(3);
			// Adding a 4th entry evicts the LRU entry
			cache.set('d', 4);
			// Size must reflect actual entries (3), not high-water mark (4)
			expect(cache.size).toBe(3);
			expect(cache.get('a')).toBeUndefined();
			expect(cache.get('d')).toBe(4);
		});
	});

	describe('measureClusters scratch buffers', () => {
		it('two consecutive measurements produce independent results', () => {
			const fonts = new FontRegistry();
			fonts.registerFace(createTestFontAsset('ScratchTest'));
			const engine = new TextLayoutEngine(fonts, { locale: 'en' });
			const block1 = makeBlock({ value: 'AB', family: 'ScratchTest' });
			const block2 = makeBlock({ value: 'XY', family: 'ScratchTest' });
			const result1 = engine.measure(block1);
			const result2 = engine.measure(block2);
			// Results must reflect each block's own text, not scratch buffer aliasing.
			expect(result1.text).toBe('AB');
			expect(result2.text).toBe('XY');
			expect(result1.clusters.length).toBe(2);
			expect(result2.clusters.length).toBe(2);
			expect(result1.clusters[0].text).toBe('A');
			expect(result2.clusters[0].text).toBe('X');
		});
	});
});
