import { describe, expect, it } from 'vitest';
import { FontRegistry } from '../font';
import { DisposedUIError } from '../errors';
import { createTestFontAsset } from './test-font';

describe('@axrone/ui FontRegistry', () => {
	describe('registerFace and resolveFace', () => {
		it('registers a font face and resolves it by family', () => {
			const registry = new FontRegistry();
			const faceId = registry.registerFace(createTestFontAsset('TestSans'));
			expect(faceId).toBeDefined();

			const resolved = registry.resolveFace({ family: 'TestSans' });
			expect(resolved).toBe(faceId);
		});

		it('auto-sets the first registered family as default', () => {
			const registry = new FontRegistry();
			registry.registerFace(createTestFontAsset('First'));
			expect(registry.getDefaultFamily()).toBe('First');
		});

		it('resolves the best matching face by weight', () => {
			const registry = new FontRegistry();
			registry.registerFace({ ...createTestFontAsset('Sans'), weight: 400 });
			const boldId = registry.registerFace({ ...createTestFontAsset('Sans'), weight: 700, face: 'Bold' });

			const resolved = registry.resolveFace({ family: 'Sans', weight: 700 });
			expect(resolved).toBe(boldId);
		});

		it('returns null when no family matches', () => {
			const registry = new FontRegistry();
			expect(registry.resolveFace({ family: 'NonExistent' })).toBeNull();
		});
	});

	describe('getFaceInfo', () => {
		it('returns info for a valid face id', () => {
			const registry = new FontRegistry();
			const faceId = registry.registerFace(createTestFontAsset('TestSans'));
			const info = registry.getFaceInfo(faceId);
			expect(info).not.toBeNull();
			expect(info!.family).toBe('TestSans');
			expect(info!.face).toBe('Regular');
			expect(info!.unitsPerEm).toBe(1000);
		});

		it('returns null for null faceId', () => {
			const registry = new FontRegistry();
			expect(registry.getFaceInfo(null)).toBeNull();
		});

		it('returns null for an invalid faceId', () => {
			const registry = new FontRegistry();
			expect(registry.getFaceInfo(999 as never)).toBeNull();
		});
	});

	describe('ensureGlyph and measureGlyph', () => {
		it('creates an atlas entry for a known glyph', () => {
			const registry = new FontRegistry();
			const faceId = registry.registerFace(createTestFontAsset('TestSans'));
			const entry = registry.ensureGlyph(faceId, 65); // 'A'
			expect(entry).not.toBeNull();
			expect(entry!.codePoint).toBe(65);
		});

		it('returns null for an unknown glyph without fallback', () => {
			const registry = new FontRegistry();
			const faceId = registry.registerFace({
				...createTestFontAsset('TestSans'),
				glyphs: [], // no glyphs
			});
			const entry = registry.ensureGlyph(faceId, 9999);
			expect(entry).toBeNull();
		});

		it('caches repeated glyph requests', () => {
			const registry = new FontRegistry();
			const faceId = registry.registerFace(createTestFontAsset('TestSans'));
			const first = registry.ensureGlyph(faceId, 65);
			const second = registry.ensureGlyph(faceId, 65);
			expect(first).toBe(second);
		});

		it('measures a glyph and returns advance + atlas entry', () => {
			const registry = new FontRegistry();
			const faceId = registry.registerFace(createTestFontAsset('TestSans'));
			const measurement = registry.measureGlyph(faceId, 65, 16);
			expect(measurement.codePoint).toBe(65);
			expect(measurement.advance).toBeGreaterThan(0);
			expect(measurement.atlasEntry).not.toBeNull();
		});

		it('returns fallback measurement for null faceId', () => {
			const registry = new FontRegistry();
			const measurement = registry.measureGlyph(null, 65, 16);
			expect(measurement.advance).toBeCloseTo(16 * 0.6);
			expect(measurement.atlasEntry).toBeNull();
		});
	});

	describe('getDefaultFamily', () => {
		it('returns null when no faces are registered', () => {
			const registry = new FontRegistry();
			expect(registry.getDefaultFamily()).toBeNull();
		});

		it('returns the first registered family name', () => {
			const registry = new FontRegistry();
			registry.registerFace(createTestFontAsset('MyFont'));
			expect(registry.getDefaultFamily()).toBe('MyFont');
		});
	});

	describe('clear and dispose', () => {
		it('clears all registered faces and families', () => {
			const registry = new FontRegistry();
			registry.registerFace(createTestFontAsset('TestSans'));
			registry.clear();
			expect(registry.resolveFace({ family: 'TestSans' })).toBeNull();
			// clear() resets the face/family maps but preserves the default family name
			// so re-registered faces in the same family are still found.
		});

		it('throws DisposedUIError after dispose', () => {
			const registry = new FontRegistry();
			registry.dispose();
			expect(() => registry.registerFace(createTestFontAsset('TestSans'))).toThrowError(DisposedUIError);
		});

		it('is idempotent on double dispose', () => {
			const registry = new FontRegistry();
			registry.dispose();
			expect(() => registry.dispose()).not.toThrow();
		});
	});

	describe('snapshot and restore', () => {
		it('round-trips the registry state', () => {
			const registry = new FontRegistry();
			registry.registerFace(createTestFontAsset('TestSans'));
			const snapshot = registry.snapshot();

			const restored = new FontRegistry();
			restored.restore(snapshot);

			expect(restored.resolveFace({ family: 'TestSans' })).not.toBeNull();
			expect(restored.getDefaultFamily()).toBe('TestSans');
		});
	});
});
