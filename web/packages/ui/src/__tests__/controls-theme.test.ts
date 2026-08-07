import { describe, expect, it } from 'vitest';
import { resolveTheme, resolveThemeScale, resolveVariantPalette, defaultUIControlTheme } from '../controls/theme';

describe('@axrone/ui controls theme', () => {
	describe('resolveTheme', () => {
		it('returns the default theme when called with undefined', () => {
			const theme = resolveTheme(undefined);
			expect(theme.fontSize).toBe(defaultUIControlTheme.fontSize);
			expect(theme.controlHeight).toBe(defaultUIControlTheme.controlHeight);
			expect(theme.canvasColor).toBe(defaultUIControlTheme.canvasColor);
		});

		it('merges partial overrides on top of defaults', () => {
			const theme = resolveTheme({ fontSize: 20, accentColor: '#ff0000ff' });
			expect(theme.fontSize).toBe(20);
			expect(theme.accentColor).toBe('#ff0000ff');
			expect(theme.controlHeight).toBe(defaultUIControlTheme.controlHeight);
		});
	});

	describe('resolveThemeScale', () => {
		it('returns 1 for the default theme', () => {
			expect(resolveThemeScale(defaultUIControlTheme)).toBe(1);
		});

		it('scales proportionally to controlHeight', () => {
			const theme = resolveTheme({ controlHeight: 88 });
			expect(resolveThemeScale(theme)).toBe(2);
		});

		it('clamps to a minimum of 0.5', () => {
			const theme = resolveTheme({ controlHeight: 10 });
			expect(resolveThemeScale(theme)).toBe(0.5);
		});
	});

	describe('resolveVariantPalette', () => {
		const theme = resolveTheme(undefined);

		it('returns accent colors for primary variant', () => {
			const palette = resolveVariantPalette(theme, 'primary');
			expect(palette.idle).toBe(theme.accentColor);
			expect(palette.hover).toBe(theme.accentHoverColor);
			expect(palette.pressed).toBe(theme.accentPressedColor);
		});

		it('returns success colors for success variant', () => {
			const palette = resolveVariantPalette(theme, 'success');
			expect(palette.idle).toBe(theme.successColor);
			expect(palette.hover).toBe(theme.successHoverColor);
			expect(palette.pressed).toBe(theme.successPressedColor);
		});

		it('returns warning colors for warning variant', () => {
			const palette = resolveVariantPalette(theme, 'warning');
			expect(palette.idle).toBe(theme.warningColor);
			expect(palette.hover).toBe(theme.warningHoverColor);
			expect(palette.pressed).toBe(theme.warningPressedColor);
		});

		it('returns danger colors for danger variant', () => {
			const palette = resolveVariantPalette(theme, 'danger');
			expect(palette.idle).toBe(theme.dangerColor);
			expect(palette.hover).toBe(theme.dangerHoverColor);
			expect(palette.pressed).toBe(theme.dangerPressedColor);
		});

		it('returns surface colors for neutral variant', () => {
			const palette = resolveVariantPalette(theme, 'neutral');
			expect(palette.idle).toBe(theme.surfaceRaisedColor);
			expect(palette.hover).toBe(theme.surfaceHoverColor);
			expect(palette.pressed).toBe(theme.surfacePressedColor);
			expect(palette.text).toBe(theme.textColor);
		});

		it('falls back to neutral for unknown variants', () => {
			const palette = resolveVariantPalette(theme, 'unknown' as never);
			expect(palette.idle).toBe(theme.surfaceRaisedColor);
		});
	});
});
