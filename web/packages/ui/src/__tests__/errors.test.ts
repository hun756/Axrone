import { describe, expect, it } from 'vitest';
import {
	UIError,
	UIErrorCode,
	WidgetNotFoundError,
	WidgetTreeIntegrityError,
	FontLoadError,
	FontFamilyNotFoundError,
	FontFaceNotFoundError,
	DisposedUIError,
	InvalidUIAssetError,
} from '../errors';

describe('@axrone/ui errors', () => {
	describe('UIError', () => {
		it('carries the code, message, and details', () => {
			const error = new UIError(UIErrorCode.InvalidArgument, 'bad arg', { field: 'x' });
			expect(error).toBeInstanceOf(Error);
			expect(error).toBeInstanceOf(UIError);
			expect(error.name).toBe('UIError');
			expect(error.code).toBe(UIErrorCode.InvalidArgument);
			expect(error.message).toBe('bad arg');
			expect(error.details).toEqual({ field: 'x' });
		});

		it('defaults details to undefined', () => {
			const error = new UIError(UIErrorCode.Disposed, 'gone');
			expect(error.details).toBeUndefined();
		});
	});

	describe('WidgetNotFoundError', () => {
		it('uses WidgetNotFound code and embeds the widget id', () => {
			const error = new WidgetNotFoundError(42);
			expect(error).toBeInstanceOf(UIError);
			expect(error).toBeInstanceOf(Error);
			expect(error.name).toBe('WidgetNotFoundError');
			expect(error.code).toBe(UIErrorCode.WidgetNotFound);
			expect(error.message).toContain('42');
			expect(error.details).toEqual({ widgetId: 42 });
		});
	});

	describe('WidgetTreeIntegrityError', () => {
		it('uses TreeIntegrity code', () => {
			const error = new WidgetTreeIntegrityError('cycle detected', { node: 7 });
			expect(error.name).toBe('WidgetTreeIntegrityError');
			expect(error.code).toBe(UIErrorCode.TreeIntegrity);
			expect(error.details).toEqual({ node: 7 });
		});
	});

	describe('FontLoadError', () => {
		it('uses FontLoadFailed code', () => {
			const error = new FontLoadError('network timeout');
			expect(error.name).toBe('FontLoadError');
			expect(error.code).toBe(UIErrorCode.FontLoadFailed);
			expect(error.message).toBe('network timeout');
		});
	});

	describe('FontFamilyNotFoundError', () => {
		it('uses FontFamilyNotFound code and embeds the family name', () => {
			const error = new FontFamilyNotFoundError('Inter');
			expect(error.name).toBe('FontFamilyNotFoundError');
			expect(error.code).toBe(UIErrorCode.FontFamilyNotFound);
			expect(error.message).toContain('Inter');
			expect(error.details).toEqual({ family: 'Inter' });
		});
	});

	describe('FontFaceNotFoundError', () => {
		it('uses FontFaceNotFound code', () => {
			const error = new FontFaceNotFoundError({ query: 'bold' });
			expect(error.name).toBe('FontFaceNotFoundError');
			expect(error.code).toBe(UIErrorCode.FontFaceNotFound);
			expect(error.details).toEqual({ query: 'bold' });
		});

		it('defaults details to undefined', () => {
			const error = new FontFaceNotFoundError();
			expect(error.details).toBeUndefined();
		});
	});

	describe('DisposedUIError', () => {
		it('uses Disposed code and embeds the target name', () => {
			const error = new DisposedUIError('FontRegistry');
			expect(error.name).toBe('DisposedUIError');
			expect(error.code).toBe(UIErrorCode.Disposed);
			expect(error.message).toContain('FontRegistry');
			expect(error.details).toEqual({ target: 'FontRegistry' });
		});
	});

	describe('InvalidUIAssetError', () => {
		it('uses InvalidAsset code', () => {
			const error = new InvalidUIAssetError('missing root', { path: 'root' });
			expect(error.name).toBe('InvalidUIAssetError');
			expect(error.code).toBe(UIErrorCode.InvalidAsset);
			expect(error.details).toEqual({ path: 'root' });
		});
	});
});
