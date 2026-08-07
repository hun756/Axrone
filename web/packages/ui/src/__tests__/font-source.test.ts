import { describe, expect, it } from 'vitest';
import {
	normalizeWeight,
	normalizeStyle,
	buildSourceKey,
	normalizeGlyphMap,
	normalizeKerningMap,
	applyRetryDelay,
	isDynamicFontFaceAsset,
	detectBinaryFormatFromContentType,
	detectBinaryFormatFromUrl,
	detectBinaryFormatFromBuffer,
	createAtlasEntryKey,
} from '../font/source';

describe('@axrone/ui font source utilities', () => {
	describe('normalizeWeight', () => {
		it('maps string weights to numbers', () => {
			expect(normalizeWeight('thin')).toBe(100);
			expect(normalizeWeight('extralight')).toBe(200);
			expect(normalizeWeight('light')).toBe(300);
			expect(normalizeWeight('normal')).toBe(400);
			expect(normalizeWeight('medium')).toBe(500);
			expect(normalizeWeight('semibold')).toBe(600);
			expect(normalizeWeight('bold')).toBe(700);
			expect(normalizeWeight('extrabold')).toBe(800);
			expect(normalizeWeight('black')).toBe(900);
		});

		it('defaults undefined to 400', () => {
			expect(normalizeWeight(undefined)).toBe(400);
		});

		it('passes through numeric weights', () => {
			expect(normalizeWeight(350)).toBe(350);
		});
	});

	describe('normalizeStyle', () => {
		it('defaults undefined to normal', () => {
			expect(normalizeStyle(undefined)).toBe('normal');
		});

		it('passes through a valid style', () => {
			expect(normalizeStyle('italic')).toBe('italic');
		});
	});

	describe('createAtlasEntryKey', () => {
		it('creates a deterministic key from codePoint and rasterSize', () => {
			expect(createAtlasEntryKey(65, 16)).toBe('65:16');
		});

		it('defaults rasterSize to 0', () => {
			expect(createAtlasEntryKey(65)).toBe('65:0');
		});
	});

	describe('applyRetryDelay', () => {
		it('returns exponential backoff for the first attempt', () => {
			// base=16, max=250, attempt=1 → min(250, 16 * 2^0) = 16
			expect(applyRetryDelay(undefined, 1)).toBe(16);
		});

		it('caps at maxDelayMs', () => {
			// base=16, max=250, attempt=10 → min(250, 16 * 2^9) = min(250, 8192) = 250
			expect(applyRetryDelay(undefined, 10)).toBe(250);
		});

		it('uses custom policy values', () => {
			const policy = { baseDelayMs: 100, maxDelayMs: 500, jitter: 0 };
			// attempt=2 → min(500, 100 * 2^1) = 200
			expect(applyRetryDelay(policy, 2)).toBe(200);
		});

		it('applies jitter when specified', () => {
			const policy = { baseDelayMs: 100, maxDelayMs: 1000, jitter: 0.5 };
			const result = applyRetryDelay(policy, 1);
			// base=100, attempt=1 → 100 * 2^0 = 100, jitter factor in [0.5, 1.5]
			// result should be between 50 and 150
			expect(result).toBeGreaterThanOrEqual(50);
			expect(result).toBeLessThanOrEqual(150);
		});
	});

	describe('isDynamicFontFaceAsset', () => {
		it('returns true for dynamic assets', () => {
			const asset = { kind: 'dynamic' as const, runtime: {} as never };
			expect(isDynamicFontFaceAsset(asset)).toBe(true);
		});

		it('returns false for static assets', () => {
			const asset = { kind: 'static' as const, family: 'Test' };
			expect(isDynamicFontFaceAsset(asset)).toBe(false);
		});
	});

	describe('normalizeGlyphMap', () => {
		it('returns an empty map for undefined', () => {
			const result = normalizeGlyphMap(undefined);
			expect(result.size).toBe(0);
		});

		it('converts an array of metrics to a map', () => {
			const glyphs = [
				{ codePoint: 65, advance: 500, width: 480, height: 720 },
				{ codePoint: 66, advance: 500, width: 480, height: 720 },
			];
			const result = normalizeGlyphMap(glyphs);
			expect(result.size).toBe(2);
			expect(result.get(65)).toEqual(glyphs[0]);
		});

		it('converts an object of metrics to a map', () => {
			const glyphs = {
				A: { codePoint: 65, advance: 500, width: 480, height: 720 },
			};
			const result = normalizeGlyphMap(glyphs as never);
			expect(result.size).toBe(1);
			expect(result.get(65)).toBeDefined();
		});

		it('copies an existing Map', () => {
			const original = new Map([[65, { codePoint: 65, advance: 500, width: 480, height: 720 }]]);
			const result = normalizeGlyphMap(original as never);
			expect(result.size).toBe(1);
			expect(result).not.toBe(original);
		});
	});

	describe('normalizeKerningMap', () => {
		it('returns an empty map for undefined', () => {
			const result = normalizeKerningMap(undefined);
			expect(result.size).toBe(0);
		});

		it('converts an object to a map', () => {
			const kernings = { '65:66': -20 };
			const result = normalizeKerningMap(kernings as never);
			expect(result.size).toBe(1);
			expect(result.get('65:66')).toBe(-20);
		});

		it('copies an existing Map', () => {
			const original = new Map([['65:66' as const, -20]]);
			const result = normalizeKerningMap(original as never);
			expect(result.size).toBe(1);
			expect(result).not.toBe(original);
		});
	});

	describe('detectBinaryFormatFromContentType', () => {
		it('detects woff2', () => {
			expect(detectBinaryFormatFromContentType('font/woff2')).toBe('woff2');
		});

		it('detects woff', () => {
			expect(detectBinaryFormatFromContentType('font/woff')).toBe('woff');
		});

		it('detects otf', () => {
			expect(detectBinaryFormatFromContentType('font/otf')).toBe('otf');
		});

		it('detects ttf', () => {
			expect(detectBinaryFormatFromContentType('font/ttf')).toBe('ttf');
		});

		it('returns null for unknown', () => {
			expect(detectBinaryFormatFromContentType('application/json')).toBeNull();
		});

		it('returns null for undefined', () => {
			expect(detectBinaryFormatFromContentType(undefined)).toBeNull();
		});
	});

	describe('detectBinaryFormatFromUrl', () => {
		it('detects woff2 from URL', () => {
			expect(detectBinaryFormatFromUrl('https://example.com/font.woff2')).toBe('woff2');
		});

		it('detects ttf from URL', () => {
			expect(detectBinaryFormatFromUrl('/fonts/my-font.ttf')).toBe('ttf');
		});

		it('strips query strings and fragments', () => {
			expect(detectBinaryFormatFromUrl('font.woff2?v=1#section')).toBe('woff2');
		});

		it('returns null for unknown extensions', () => {
			expect(detectBinaryFormatFromUrl('font.json')).toBeNull();
		});
	});

	describe('detectBinaryFormatFromBuffer', () => {
		it('detects woff2 magic bytes', () => {
			const bytes = new Uint8Array([0x77, 0x4f, 0x46, 0x32]); // 'wOF2'
			expect(detectBinaryFormatFromBuffer(bytes)).toBe('woff2');
		});

		it('detects woff magic bytes', () => {
			const bytes = new Uint8Array([0x77, 0x4f, 0x46, 0x46]); // 'wOFF'
			expect(detectBinaryFormatFromBuffer(bytes)).toBe('woff');
		});

		it('detects otf magic bytes', () => {
			const bytes = new Uint8Array([0x4f, 0x54, 0x54, 0x4f]); // 'OTTO'
			expect(detectBinaryFormatFromBuffer(bytes)).toBe('otf');
		});

		it('detects ttf magic bytes', () => {
			const bytes = new Uint8Array([0x00, 0x01, 0x00, 0x00]);
			expect(detectBinaryFormatFromBuffer(bytes)).toBe('ttf');
		});

		it('returns null for too-small buffers', () => {
			expect(detectBinaryFormatFromBuffer(new Uint8Array([1, 2]))).toBeNull();
		});

		it('returns null for unknown magic bytes', () => {
			const bytes = new Uint8Array([0xff, 0xff, 0xff, 0xff]);
			expect(detectBinaryFormatFromBuffer(bytes)).toBeNull();
		});
	});

	describe('buildSourceKey', () => {
		it('builds a URL source key', () => {
			const source = { kind: 'url' as const, url: 'https://example.com/font.woff2' };
			const key = buildSourceKey(source);
			expect(key).toContain('url:');
			expect(key).toContain('https://example.com/font.woff2');
		});

		it('uses cacheKey when provided', () => {
			const source = { kind: 'url' as const, url: 'https://example.com/font.woff2', cacheKey: 'my-custom-key' };
			expect(buildSourceKey(source)).toBe('my-custom-key');
		});

		it('builds a buffer source key', () => {
			const data = new Uint8Array([1, 2, 3, 4]);
			const source = { kind: 'buffer' as const, data };
			const key = buildSourceKey(source);
			expect(key).toContain('buffer:');
		});
	});
});
