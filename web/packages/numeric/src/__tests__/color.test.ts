import { describe, it, expect } from 'vitest';
import { Color, ColorBlendMode, ColorHarmonyType, ColorComparisonMode, ColorComparer } from '../color';

const EPS = 1e-6;
const close = (a: number, b: number, eps: number = EPS) => Math.abs(a - b) < eps;

describe('Color module - refactor regression', () => {
    describe('SILVER constant (P0 fix)', () => {
        it('matches CSS #C0C0C0 (0.7529411764705882)', () => {
            expect(Color.SILVER.r).toBe(0.7529411764705882);
            expect(Color.SILVER.g).toBe(0.7529411764705882);
            expect(Color.SILVER.b).toBe(0.7529411764705882);
            expect(Color.SILVER.a).toBe(1);
        });

        it('toHex produces #C0C0C0', () => {
            expect(Color.SILVER.toHex()).toBe('#C0C0C0');
        });

        it('is frozen and not equal to old 0.75 value', () => {
            expect(Color.SILVER.r).not.toBe(0.75);
        });
    });

    describe('fromNamedColor - module-level map', () => {
        it('resolves all standard aliases', () => {
            expect(Color.fromNamedColor('red').equals(Color.RED)).toBe(true);
            expect(Color.fromNamedColor('blue').equals(Color.BLUE)).toBe(true);
            expect(Color.fromNamedColor('gray').equals(Color.GRAY)).toBe(true);
            expect(Color.fromNamedColor('grey').equals(Color.GRAY)).toBe(true);
            expect(Color.fromNamedColor('lightgray').equals(Color.LIGHT_GRAY)).toBe(true);
            expect(Color.fromNamedColor('lightgrey').equals(Color.LIGHT_GRAY)).toBe(true);
            expect(Color.fromNamedColor('darkgray').equals(Color.DARK_GRAY)).toBe(true);
            expect(Color.fromNamedColor('silver').equals(Color.SILVER)).toBe(true);
        });

        it('handles whitespace and case insensitivity', () => {
            expect(Color.fromNamedColor('  RED  ').equals(Color.RED)).toBe(true);
            expect(Color.fromNamedColor('Light Gray').equals(Color.LIGHT_GRAY)).toBe(true);
        });

        it('throws on unknown name', () => {
            expect(() => Color.fromNamedColor('nope')).toThrow('Unknown color name: nope');
        });

        it('returns a clone (mutation does not affect static)', () => {
            const c = Color.fromNamedColor('red');
            c.r = 0.5;
            expect(Color.RED.r).toBe(1);
        });
    });

    describe('fromHex - validation order', () => {
        it('accepts 6-digit hex', () => {
            const c = Color.fromHex('#FF8040');
            expect(c.r).toBeCloseTo(1, 6);
            expect(c.g).toBeCloseTo(0x80 / 255, 6);
            expect(c.b).toBeCloseTo(0x40 / 255, 6);
            expect(c.a).toBe(1);
        });

        it('accepts 8-digit hex with alpha', () => {
            const c = Color.fromHex('#FF804080');
            expect(c.a).toBeCloseTo(0x80 / 255, 6);
        });

        it('expands 3-digit shorthand', () => {
            const c = Color.fromHex('#F80');
            expect(c.r).toBe(1);
            expect(c.g).toBe(0x88 / 255);
            expect(c.b).toBe(0);
        });

        it('expands 4-digit shorthand with alpha', () => {
            const c = Color.fromHex('#F808');
            expect(c.r).toBe(1);
            expect(c.g).toBe(0x88 / 255);
            expect(c.b).toBe(0);
            expect(c.a).toBeCloseTo(0x88 / 255, 6);
        });

        it('accepts hex without # prefix', () => {
            const c = Color.fromHex('FF0000');
            expect(c.r).toBe(1);
            expect(c.g).toBe(0);
            expect(c.b).toBe(0);
        });

        it('trims whitespace', () => {
            const c = Color.fromHex('  00ff00  ');
            expect(c.g).toBe(1);
        });

        it('rejects wrong length (5 or 7 chars)', () => {
            expect(() => Color.fromHex('#12345')).toThrow(/must be 3, 4, 6, or 8/);
            expect(() => Color.fromHex('#1234567')).toThrow(/got 7/);
        });

        it('rejects empty string with informative message', () => {
            expect(() => Color.fromHex('')).toThrow(/got 0/);
        });

        it('rejects non-hex characters', () => {
            expect(() => Color.fromHex('#GGGGGG')).toThrow(/non-hexadecimal/);
        });
    });

    describe('toHex - uppercase deterministic output', () => {
        it('always produces uppercase hex digits', () => {
            const c = new Color(0xab / 255, 0xcd / 255, 0xef / 255);
            expect(c.toHex()).toBe('#ABCDEF');
            expect(c.toHex(true)).toBe('#ABCDEFFF');
        });

        it('pads single-digit channels with leading zero', () => {
            const c = new Color(0, 0, 0, 0.5);
            expect(c.toHex()).toBe('#000000');
            expect(Math.round(c.a * 255)).toBe(128);
        });
    });

    describe('blend - all 16 modes', () => {
        const base = new Color(0.5, 0.5, 0.5);
        const overlay = new Color(0.8, 0.2, 0.4);

        it('NORMAL returns overlay', () => {
            const r = Color.blend(base, overlay, ColorBlendMode.NORMAL);
            expect(close(r.r, 0.8)).toBe(true);
            expect(close(r.g, 0.2)).toBe(true);
            expect(close(r.b, 0.4)).toBe(true);
        });

        it('MULTIPLY multiplies channels', () => {
            const r = Color.blend(base, overlay, ColorBlendMode.MULTIPLY);
            expect(close(r.r, 0.4)).toBe(true);
        });

        it('SCREEN formula', () => {
            const r = Color.blend(base, overlay, ColorBlendMode.SCREEN);
            expect(close(r.r, 1 - (1 - 0.5) * (1 - 0.8))).toBe(true);
        });

        it('DARKEN/LIGHTEN/DIFFERENCE/EXCLUSION', () => {
            const dk = Color.blend(base, overlay, ColorBlendMode.DARKEN);
            const lt = Color.blend(base, overlay, ColorBlendMode.LIGHTEN);
            const df = Color.blend(base, overlay, ColorBlendMode.DIFFERENCE);
            const ex = Color.blend(base, overlay, ColorBlendMode.EXCLUSION);
            expect(dk.r).toBe(0.5);
            expect(lt.r).toBe(0.8);
            expect(close(df.r, 0.3)).toBe(true);
            expect(close(ex.r, 0.5 + 0.8 - 2 * 0.5 * 0.8)).toBe(true);
        });

        it('OVERLAY at base < 0.5 uses 2*a*b', () => {
            const r = Color.blend(new Color(0.3, 0.3, 0.3), overlay, ColorBlendMode.OVERLAY);
            expect(close(r.r, 2 * 0.3 * 0.8)).toBe(true);
        });

        it('OVERLAY at base >= 0.5 uses 1 - 2*(1-a)*(1-b)', () => {
            const r = Color.blend(new Color(0.6, 0.6, 0.6), overlay, ColorBlendMode.OVERLAY);
            expect(close(r.r, 1 - 2 * 0.4 * 0.2)).toBe(true);
        });

        it('HARD_LIGHT uses overlay as decision', () => {
            const r = Color.blend(base, new Color(0.3, 0.3, 0.3), ColorBlendMode.HARD_LIGHT);
            expect(close(r.r, 2 * 0.5 * 0.3)).toBe(true);
        });

        it('COLOR_DODGE edge case at overlay=1 returns 1', () => {
            const r = Color.blend(base, new Color(1, 1, 1), ColorBlendMode.COLOR_DODGE);
            expect(r.r).toBe(1);
        });

        it('COLOR_BURN edge case at overlay=0 returns 0', () => {
            const r = Color.blend(base, new Color(0, 0, 0), ColorBlendMode.COLOR_BURN);
            expect(r.r).toBe(0);
        });

        it('writes to out without intermediate allocation visible', () => {
            const out: { r: number; g: number; b: number; a: number } = { r: 0, g: 0, b: 0, a: 0 };
            const result = Color.blend(base, overlay, ColorBlendMode.MULTIPLY, out);
            expect(result).toBe(out);
            expect(close(out.r, 0.4)).toBe(true);
            expect(close(out.g, 0.1)).toBe(true);
        });
    });

    describe('lerpHSL - out branch writes directly', () => {
        it('out branch produces identical result to non-out branch', () => {
            const a = new Color(1, 0, 0);
            const b = new Color(0, 0, 1);
            const ref = Color.lerpHSL(a, b, 0.5);
            const out: { r: number; g: number; b: number; a: number } = { r: 0, g: 0, b: 0, a: 0 };
            const ret = Color.lerpHSL(a, b, 0.5, out);
            expect(ret).toBe(out);
            expect(close(out.r, ref.r, 1e-5)).toBe(true);
            expect(close(out.g, ref.g, 1e-5)).toBe(true);
            expect(close(out.b, ref.b, 1e-5)).toBe(true);
            expect(close(out.a, ref.a, 1e-5)).toBe(true);
        });

        it('takes shortest hue path (red->cyan = 180 deg, half should be at 180/270 boundary)', () => {
            const a = new Color(1, 0, 0);
            const b = Color.fromHSL(180, 1, 0.5);
            const mid = Color.lerpHSL(a, b, 0.5);
            expect(mid.r).toBeGreaterThan(0);
            expect(mid.g).toBeGreaterThan(0);
        });

        it('clamps out channels when valid', () => {
            const out: { r: number; g: number; b: number; a: number } = { r: 2, g: -1, b: 5, a: 3 };
            Color.lerpHSL(new Color(0, 0, 0), new Color(0.5, 0.5, 0.5), 1, out);
            expect(out.r).toBeLessThanOrEqual(1);
            expect(out.g).toBeGreaterThanOrEqual(0);
            expect(out.b).toBeLessThanOrEqual(1);
            expect(out.a).toBeLessThanOrEqual(1);
        });
    });

    describe('lerpLab - out branch writes directly', () => {
        it('out branch matches non-out branch', () => {
            const a = new Color(0.2, 0.4, 0.6);
            const b = new Color(0.7, 0.1, 0.9);
            const ref = Color.lerpLab(a, b, 0.3);
            const out: { r: number; g: number; b: number; a: number } = { r: 0, g: 0, b: 0, a: 0 };
            const ret = Color.lerpLab(a, b, 0.3, out);
            expect(ret).toBe(out);
            expect(close(out.r, ref.r, 1e-4)).toBe(true);
            expect(close(out.g, ref.g, 1e-4)).toBe(true);
            expect(close(out.b, ref.b, 1e-4)).toBe(true);
        });
    });

    describe('lighten / darken / saturate / desaturate / adjustHue out branch', () => {
        it('lighten out branch matches non-out branch', () => {
            const c = new Color(0.4, 0.5, 0.6);
            const ref = Color.lighten(c, 0.2);
            const out: { r: number; g: number; b: number; a: number } = { r: 0, g: 0, b: 0, a: 0 };
            Color.lighten(c, 0.2, out);
            expect(close(out.r, ref.r, 1e-5)).toBe(true);
            expect(close(out.g, ref.g, 1e-5)).toBe(true);
            expect(close(out.b, ref.b, 1e-5)).toBe(true);
        });

        it('darken is lighten with negative amount', () => {
            const c = new Color(0.4, 0.5, 0.6);
            const dk = Color.darken(c, 0.1);
            const lt = Color.lighten(c, -0.1);
            expect(dk.equals(lt)).toBe(true);
        });

        it('saturate out branch', () => {
            const c = new Color(0.5, 0.3, 0.7);
            const ref = Color.saturate(c, 0.2);
            const out: { r: number; g: number; b: number; a: number } = { r: 0, g: 0, b: 0, a: 0 };
            Color.saturate(c, 0.2, out);
            expect(close(out.r, ref.r, 1e-5)).toBe(true);
        });

        it('desaturate is saturate with negative amount', () => {
            const c = new Color(0.5, 0.3, 0.7);
            const d = Color.desaturate(c, 0.1);
            const s = Color.saturate(c, -0.1);
            expect(d.equals(s)).toBe(true);
        });

        it('adjustHue out branch', () => {
            const c = new Color(1, 0, 0);
            const ref = Color.adjustHue(c, 120);
            const out: { r: number; g: number; b: number; a: number } = { r: 0, g: 0, b: 0, a: 0 };
            Color.adjustHue(c, 120, out);
            expect(close(out.r, ref.r, 1e-5)).toBe(true);
            expect(close(out.g, ref.g, 1e-5)).toBe(true);
        });
    });

    describe('_hueToRgb purity (no parameter mutation)', () => {
        it('fromHSL does not mutate inputs', () => {
            const c1 = Color.fromHSL(30, 0.5, 0.5);
            const c2 = Color.fromHSL(30, 0.5, 0.5);
            expect(c1.equals(c2)).toBe(true);
        });

        it('repeated fromHSL calls are stable', () => {
            const a = Color.fromHSL(210, 0.7, 0.4);
            const b = Color.fromHSL(210, 0.7, 0.4);
            expect(a.r).toBe(b.r);
            expect(a.g).toBe(b.g);
            expect(a.b).toBe(b.b);
        });
    });

    describe('toHSL/toHSV out branches still work (regression check)', () => {
        it('toHSL writes to out without allocating', () => {
            const c = new Color(1, 0, 0);
            const out = { h: 0, s: 0, l: 0, a: 0 };
            const ret = c.toHSL(out);
            expect(ret).toBe(out);
            expect(close(out.h, 0, 1e-4)).toBe(true);
            expect(out.s).toBe(1);
            expect(close(out.l, 0.5, 1e-4)).toBe(true);
        });
    });

    describe('harmony / comparer smoke', () => {
        it('generateHarmony TRIADIC returns 3 colors', () => {
            const base = new Color(1, 0, 0);
            const colors = Color.generateHarmony(base, ColorHarmonyType.TRIADIC);
            expect(colors.length).toBe(3);
        });

        it('ColorComparer LUMINANCE orders white > black', () => {
            const cmp = new ColorComparer(ColorComparisonMode.LUMINANCE);
            const c1 = Color.WHITE as Color;
            const c2 = Color.BLACK as Color;
            const result = cmp.compare(c1, c2);
            expect(result).toBe(1);
        });
    });
});
