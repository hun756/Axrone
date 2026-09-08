import { describe, expect, test } from 'vitest';
import {
    Color,
    ColorBlendMode,
    ColorHarmonyType,
    ColorComparisonMode,
    ColorComparer,
    ColorEqualityComparer,
    type IColorLike,
    type IColorHSL,
    type IColorHSV,
    type IColorCMYK,
    type IColorLab,
    type IColorXYZ,
} from '../color';
import { Fnv1a32 } from '@axrone/hash';
import { EPSILON } from '../common';

const expectColorClose = (actual: IColorLike, expected: IColorLike, eps = 1e-4) => {
    expect(Math.abs(actual.r - expected.r)).toBeLessThan(eps);
    expect(Math.abs(actual.g - expected.g)).toBeLessThan(eps);
    expect(Math.abs(actual.b - expected.b)).toBeLessThan(eps);
    expect(Math.abs((actual.a ?? 1) - (expected.a ?? 1))).toBeLessThan(eps);
};

describe('Color - Extended Coverage', () => {
    // ─── Color Space Conversions (to*) ────────────────────────────────────
    describe('Color Space Conversions', () => {
        describe('toHSL', () => {
            test('red = (0, 1, 0.5)', () => {
                const hsl = new Color(1, 0, 0).toHSL();
                expect(hsl.h).toBeCloseTo(0, 4);
                expect(hsl.s).toBeCloseTo(1, 4);
                expect(hsl.l).toBeCloseTo(0.5, 4);
                expect(hsl.a).toBe(1);
            });

            test('green = (120, 1, 0.5)', () => {
                const hsl = new Color(0, 1, 0).toHSL();
                expect(hsl.h).toBeCloseTo(120, 4);
                expect(hsl.s).toBeCloseTo(1, 4);
                expect(hsl.l).toBeCloseTo(0.5, 4);
            });

            test('blue = (240, 1, 0.5)', () => {
                const hsl = new Color(0, 0, 1).toHSL();
                expect(hsl.h).toBeCloseTo(240, 4);
                expect(hsl.s).toBeCloseTo(1, 4);
                expect(hsl.l).toBeCloseTo(0.5, 4);
            });

            test('gray = (0, 0, 0.5)', () => {
                const hsl = new Color(0.5, 0.5, 0.5).toHSL();
                expect(hsl.h).toBeCloseTo(0, 4);
                expect(hsl.s).toBeCloseTo(0, 4);
                expect(hsl.l).toBeCloseTo(0.5, 4);
            });

            test('white = (0, 0, 1)', () => {
                const hsl = Color.WHITE.toHSL();
                expect(hsl.l).toBeCloseTo(1, 4);
                expect(hsl.s).toBeCloseTo(0, 4);
            });

            test('black = (0, 0, 0)', () => {
                const hsl = Color.BLACK.toHSL();
                expect(hsl.l).toBeCloseTo(0, 4);
            });

            test('preserves alpha', () => {
                const hsl = new Color(1, 0, 0, 0.5).toHSL();
                expect(hsl.a).toBe(0.5);
            });

            test('with out parameter', () => {
                const out = { h: 0, s: 0, l: 0, a: 0 };
                const ret = new Color(1, 0, 0).toHSL(out);
                expect(ret).toBe(out);
                expect(out.h).toBeCloseTo(0, 4);
                expect(out.s).toBeCloseTo(1, 4);
                expect(out.l).toBeCloseTo(0.5, 4);
            });
        });

        describe('toHSV', () => {
            test('red = (0, 1, 1)', () => {
                const hsv = new Color(1, 0, 0).toHSV();
                expect(hsv.h).toBeCloseTo(0, 4);
                expect(hsv.s).toBeCloseTo(1, 4);
                expect(hsv.v).toBeCloseTo(1, 4);
            });

            test('green = (120, 1, 1)', () => {
                const hsv = new Color(0, 1, 0).toHSV();
                expect(hsv.h).toBeCloseTo(120, 4);
                expect(hsv.s).toBeCloseTo(1, 4);
                expect(hsv.v).toBeCloseTo(1, 4);
            });

            test('gray has saturation 0', () => {
                const hsv = new Color(0.5, 0.5, 0.5).toHSV();
                expect(hsv.s).toBeCloseTo(0, 4);
            });

            test('with out parameter', () => {
                const out = { h: 0, s: 0, v: 0, a: 0 };
                const ret = new Color(0, 1, 0).toHSV(out);
                expect(ret).toBe(out);
                expect(out.h).toBeCloseTo(120, 4);
            });
        });

        describe('toCMYK', () => {
            test('black = (0, 0, 0, 1)', () => {
                const cmyk = Color.BLACK.toCMYK();
                expect(cmyk.c).toBeCloseTo(0, 4);
                expect(cmyk.m).toBeCloseTo(0, 4);
                expect(cmyk.y).toBeCloseTo(0, 4);
                expect(cmyk.k).toBeCloseTo(1, 4);
            });

            test('white = (0, 0, 0, 0)', () => {
                const cmyk = Color.WHITE.toCMYK();
                expect(cmyk.c).toBeCloseTo(0, 4);
                expect(cmyk.m).toBeCloseTo(0, 4);
                expect(cmyk.y).toBeCloseTo(0, 4);
                expect(cmyk.k).toBeCloseTo(0, 4);
            });

            test('red = (0, 1, 1, 0)', () => {
                const cmyk = Color.RED.toCMYK();
                expect(cmyk.c).toBeCloseTo(0, 4);
                expect(cmyk.m).toBeCloseTo(1, 4);
                expect(cmyk.y).toBeCloseTo(1, 4);
                expect(cmyk.k).toBeCloseTo(0, 4);
            });

            test('with out parameter', () => {
                const out = { c: 0, m: 0, y: 0, k: 0, a: 0 };
                const ret = Color.RED.toCMYK(out);
                expect(ret).toBe(out);
                expect(out.a).toBe(1);
            });
        });

        describe('toXYZ', () => {
            test('white has Y close to D65 reference (1.0)', () => {
                const xyz = Color.WHITE.toXYZ();
                expect(xyz.y).toBeCloseTo(1.0, 2);
            });

            test('black = (0, 0, 0)', () => {
                const xyz = Color.BLACK.toXYZ();
                expect(xyz.x).toBeCloseTo(0, 4);
                expect(xyz.y).toBeCloseTo(0, 4);
                expect(xyz.z).toBeCloseTo(0, 4);
            });

            test('preserves alpha', () => {
                const xyz = new Color(1, 1, 1, 0.5).toXYZ();
                expect(xyz.alpha).toBe(0.5);
            });

            test('with out parameter', () => {
                const out = { x: 0, y: 0, z: 0, alpha: 0 };
                const ret = Color.WHITE.toXYZ(out);
                expect(ret).toBe(out);
            });
        });

        describe('toLab', () => {
            test('white has L close to 100', () => {
                const lab = Color.WHITE.toLab();
                expect(lab.l).toBeCloseTo(100, 0);
            });

            test('black has L close to 0', () => {
                const lab = Color.BLACK.toLab();
                expect(lab.l).toBeCloseTo(0, 0);
            });

            test('preserves alpha', () => {
                const lab = new Color(1, 0, 0, 0.7).toLab();
                expect(lab.alpha).toBe(0.7);
            });

            test('with out parameter', () => {
                const out = { l: 0, a: 0, b: 0, alpha: 0 };
                const ret = Color.RED.toLab(out);
                expect(ret).toBe(out);
            });
        });
    });

    // ─── Color Space Factories (from*) ────────────────────────────────────
    describe('Color Space Factories', () => {
        describe('fromHSL roundtrip', () => {
            test('red roundtrip', () => {
                const c = Color.fromHSL(0, 1, 0.5);
                expectColorClose(c, { r: 1, g: 0, b: 0, a: 1 }, 1e-4);
            });

            test('green roundtrip', () => {
                const c = Color.fromHSL(120, 1, 0.5);
                expectColorClose(c, { r: 0, g: 1, b: 0, a: 1 }, 1e-4);
            });

            test('blue roundtrip', () => {
                const c = Color.fromHSL(240, 1, 0.5);
                expectColorClose(c, { r: 0, g: 0, b: 1, a: 1 }, 1e-4);
            });

            test('arbitrary color roundtrip through toHSL', () => {
                const original = Color.fromHSL(210, 0.7, 0.4);
                const hsl = original.toHSL();
                const reconstructed = Color.fromHSL(hsl.h, hsl.s, hsl.l, hsl.a);
                expectColorClose(reconstructed, original, 1e-4);
            });
        });

        describe('fromHSV roundtrip', () => {
            test('red roundtrip', () => {
                const c = Color.fromHSV(0, 1, 1);
                expectColorClose(c, { r: 1, g: 0, b: 0, a: 1 }, 1e-4);
            });

            test('arbitrary color roundtrip through toHSV', () => {
                const original = Color.fromHSV(150, 0.6, 0.8);
                const hsv = original.toHSV();
                const reconstructed = Color.fromHSV(hsv.h, hsv.s, hsv.v, hsv.a);
                expectColorClose(reconstructed, original, 1e-4);
            });
        });

        describe('fromCMYK roundtrip', () => {
            test('CMYK black', () => {
                const c = Color.fromCMYK(0, 0, 0, 1);
                expectColorClose(c, Color.BLACK, 1e-4);
            });

            test('CMYK white', () => {
                const c = Color.fromCMYK(0, 0, 0, 0);
                expectColorClose(c, Color.WHITE, 1e-4);
            });

            test('arbitrary roundtrip through toCMYK', () => {
                const original = new Color(0.3, 0.6, 0.9);
                const cmyk = original.toCMYK();
                const reconstructed = Color.fromCMYK(cmyk.c, cmyk.m, cmyk.y, cmyk.k, cmyk.a);
                expectColorClose(reconstructed, original, 1e-4);
            });
        });

        describe('fromLab', () => {
            test('approximate roundtrip with toLab', () => {
                const original = new Color(0.5, 0.3, 0.7);
                const lab = original.toLab();
                const reconstructed = Color.fromLab(lab.l, lab.a, lab.b, lab.alpha);
                // Lab roundtrip may have gamut clipping, use looser tolerance
                expectColorClose(reconstructed, original, 1e-2);
            });
        });

        describe('fromXYZ', () => {
            test('approximate roundtrip with toXYZ', () => {
                const original = new Color(0.4, 0.6, 0.8);
                const xyz = original.toXYZ();
                const reconstructed = Color.fromXYZ(xyz.x, xyz.y, xyz.z, xyz.alpha);
                expectColorClose(reconstructed, original, 1e-2);
            });
        });

        describe('fromTemperature', () => {
            test('warm temperature (2000K) is reddish', () => {
                const c = Color.fromTemperature(2000);
                expect(c.r).toBeGreaterThan(c.b);
                expect(c.r).toBeGreaterThan(0.5);
            });

            test('cool temperature (10000K) is bluish', () => {
                const c = Color.fromTemperature(10000);
                expect(c.b).toBeGreaterThan(c.r);
            });

            test('clamping at low boundary (1000K)', () => {
                const c = Color.fromTemperature(100);
                expect(Number.isFinite(c.r)).toBe(true);
                expect(Number.isFinite(c.g)).toBe(true);
                expect(Number.isFinite(c.b)).toBe(true);
            });

            test('clamping at high boundary (40000K)', () => {
                const c = Color.fromTemperature(100000);
                expect(Number.isFinite(c.r)).toBe(true);
            });

            test('alpha parameter', () => {
                const c = Color.fromTemperature(5000, 0.5);
                expect(c.a).toBe(0.5);
            });
        });

        describe('fromRGB', () => {
            test('0-1 range', () => {
                const c = Color.fromRGB(0.5, 0.3, 0.7);
                expect(c.r).toBeCloseTo(0.5, 4);
                expect(c.g).toBeCloseTo(0.3, 4);
                expect(c.b).toBeCloseTo(0.7, 4);
            });

            test('0-255 range', () => {
                const c = Color.fromRGBBytes(255, 128, 0);
                expect(c.r).toBeCloseTo(1, 4);
                expect(c.g).toBeCloseTo(128 / 255, 4);
                expect(c.b).toBeCloseTo(0, 4);
            });

            test('alpha in 0-1 range', () => {
                const c = Color.fromRGB(0.5, 0.5, 0.5, 0.5);
                expect(c.a).toBeCloseTo(0.5, 4);
            });
        });
    });

    // ─── Luminance and Accessibility ──────────────────────────────────────
    describe('Luminance and Accessibility', () => {
        describe('luminance', () => {
            test('BLACK = 0', () => {
                expect(Color.luminance(Color.BLACK)).toBeCloseTo(0, 4);
            });

            test('WHITE = 1', () => {
                expect(Color.luminance(Color.WHITE)).toBeCloseTo(1, 4);
            });

            test('instance luminance', () => {
                expect(new Color(0, 0, 0).luminance()).toBeCloseTo(0, 4);
                expect(new Color(1, 1, 1).luminance()).toBeCloseTo(1, 4);
            });

            test('green has higher luminance than red', () => {
                const lumR = Color.luminance(Color.RED);
                const lumG = Color.luminance(Color.GREEN);
                expect(lumG).toBeGreaterThan(lumR);
            });
        });

        describe('contrastRatio', () => {
            test('BLACK/WHITE = 21:1', () => {
                const ratio = Color.contrastRatio(Color.BLACK, Color.WHITE);
                expect(ratio).toBeCloseTo(21, 0);
            });

            test('same color = 1:1', () => {
                const c = new Color(0.5, 0.5, 0.5);
                const ratio = Color.contrastRatio(c, c);
                expect(ratio).toBeCloseTo(1, 4);
            });

            test('is symmetric', () => {
                const a = new Color(0.2, 0.4, 0.6);
                const b = new Color(0.8, 0.3, 0.1);
                expect(Color.contrastRatio(a, b)).toBeCloseTo(Color.contrastRatio(b, a), 10);
            });

            test('instance contrastRatio', () => {
                const ratio = new Color(0, 0, 0).contrastRatio(Color.WHITE);
                expect(ratio).toBeCloseTo(21, 0);
            });
        });

        describe('isAccessible', () => {
            test('AA: black on white passes', () => {
                expect(Color.isAccessible(Color.BLACK, Color.WHITE, 'AA')).toBe(true);
            });

            test('AAA: black on white passes', () => {
                expect(Color.isAccessible(Color.BLACK, Color.WHITE, 'AAA')).toBe(true);
            });

            test('AA: similar grays fail', () => {
                const a = new Color(0.5, 0.5, 0.5);
                const b = new Color(0.55, 0.55, 0.55);
                expect(Color.isAccessible(a, b, 'AA')).toBe(false);
            });

            test('instance isAccessible', () => {
                expect(Color.BLACK.isAccessible(Color.WHITE, 'AA')).toBe(true);
            });
        });
    });

    // ─── Distance ─────────────────────────────────────────────────────────
    describe('Distance', () => {
        test('distance: same color = 0', () => {
            const c = new Color(0.5, 0.3, 0.7);
            expect(Color.distance(c, c)).toBe(0);
        });

        test('distance: BLACK to WHITE includes alpha channel', () => {
            // dr=1, dg=1, db=1, da=0 => sqrt(3)
            const d = Color.distance(Color.BLACK, Color.WHITE);
            expect(d).toBeCloseTo(Math.sqrt(3), 4);
        });

        test('instance distance', () => {
            const d = new Color(0, 0, 0).distance(new Color(1, 0, 0));
            expect(d).toBeCloseTo(1, 4);
        });

        test('distanceLab: same color = 0', () => {
            const c = new Color(0.5, 0.3, 0.7);
            expect(Color.distanceLab(c, c)).toBeCloseTo(0, 4);
        });

        test('distanceLab: BLACK to WHITE is large', () => {
            const d = Color.distanceLab(Color.BLACK, Color.WHITE);
            expect(d).toBeGreaterThan(50);
        });

        test('distanceLab is symmetric', () => {
            const a = new Color(0.2, 0.4, 0.6);
            const b = new Color(0.8, 0.3, 0.1);
            expect(Color.distanceLab(a, b)).toBeCloseTo(Color.distanceLab(b, a), 4);
        });
    });

    // ─── Arithmetic Operations ────────────────────────────────────────────
    describe('Arithmetic Operations', () => {
        describe('add', () => {
            test('static add clamped to [0,1]', () => {
                const result = Color.add({ r: 0.7, g: 0.3, b: 0.5, a: 1 }, { r: 0.5, g: 0.5, b: 0.5, a: 1 });
                expect(result.r).toBeCloseTo(1, 4); // 0.7+0.5 = 1.2, clamped
                expect(result.g).toBeCloseTo(0.8, 4);
                expect(result.b).toBeCloseTo(1, 4); // 0.5+0.5 = 1.0
            });

            test('static add with out', () => {
                const out = { r: 0, g: 0, b: 0, a: 0 };
                const ret = Color.add({ r: 0.2, g: 0.3, b: 0.4, a: 1 }, { r: 0.1, g: 0.1, b: 0.1, a: 0.5 }, out);
                expect(ret).toBe(out);
                expect(out.r).toBeCloseTo(0.3, 4);
            });

            test('instance add mutates and clamps', () => {
                const c = new Color(0.7, 0.3, 0.5);
                c.add({ r: 0.5, g: 0.5, b: 0.5, a: 1 });
                expect(c.r).toBeCloseTo(1, 4);
                expect(c.g).toBeCloseTo(0.8, 4);
            });
        });

        describe('subtract', () => {
            test('static subtract clamped to [0,1]', () => {
                const result = Color.subtract({ r: 0.3, g: 0.8, b: 0.5, a: 1 }, { r: 0.5, g: 0.3, b: 0.5, a: 1 });
                expect(result.r).toBeCloseTo(0, 4); // 0.3-0.5 = -0.2, clamped
                expect(result.g).toBeCloseTo(0.5, 4);
                expect(result.b).toBeCloseTo(0, 4);
            });

            test('static subtract with out', () => {
                const out = { r: 0, g: 0, b: 0, a: 0 };
                const ret = Color.subtract({ r: 0.5, g: 0.5, b: 0.5, a: 1 }, { r: 0.1, g: 0.2, b: 0.3, a: 0.5 }, out);
                expect(ret).toBe(out);
                expect(out.r).toBeCloseTo(0.4, 4);
            });

            test('instance subtract mutates', () => {
                const c = new Color(0.8, 0.6, 0.4);
                c.subtract({ r: 0.3, g: 0.3, b: 0.3, a: 1 });
                expect(c.r).toBeCloseTo(0.5, 4);
                expect(c.g).toBeCloseTo(0.3, 4);
            });
        });

        describe('multiply', () => {
            test('static multiply channel-wise', () => {
                const result = Color.multiply({ r: 0.5, g: 0.5, b: 0.5, a: 1 }, { r: 0.5, g: 0.8, b: 1.0, a: 0.5 });
                expect(result.r).toBeCloseTo(0.25, 4);
                expect(result.g).toBeCloseTo(0.4, 4);
                expect(result.b).toBeCloseTo(0.5, 4);
                expect(result.a).toBeCloseTo(0.5, 4);
            });

            test('static multiply with out', () => {
                const out = { r: 0, g: 0, b: 0, a: 0 };
                const ret = Color.multiply({ r: 0.5, g: 0.5, b: 0.5, a: 1 }, { r: 0.5, g: 0.5, b: 0.5, a: 1 }, out);
                expect(ret).toBe(out);
                expect(out.r).toBeCloseTo(0.25, 4);
            });

            test('instance multiply mutates', () => {
                const c = new Color(0.5, 0.6, 0.7);
                c.multiply({ r: 0.5, g: 0.5, b: 0.5, a: 1 });
                expect(c.r).toBeCloseTo(0.25, 4);
                expect(c.g).toBeCloseTo(0.3, 4);
            });
        });

        describe('multiplyScalar', () => {
            test('static multiplyScalar clamped', () => {
                const result = Color.multiplyScalar({ r: 0.5, g: 0.3, b: 0.2, a: 1 }, 2);
                expect(result.r).toBeCloseTo(1, 4); // 0.5*2 = 1.0
                expect(result.g).toBeCloseTo(0.6, 4);
                expect(result.b).toBeCloseTo(0.4, 4);
            });

            test('static multiplyScalar with out', () => {
                const out = { r: 0, g: 0, b: 0, a: 0 };
                const ret = Color.multiplyScalar({ r: 0.3, g: 0.3, b: 0.3, a: 1 }, 0.5, out);
                expect(ret).toBe(out);
                expect(out.r).toBeCloseTo(0.15, 4);
            });

            test('instance multiplyScalar mutates', () => {
                const c = new Color(0.4, 0.5, 0.6);
                c.multiplyScalar(3);
                expect(c.r).toBeCloseTo(1, 4); // 0.4*3 = 1.2, clamped
                expect(c.g).toBeCloseTo(1, 4); // 0.5*3 = 1.5, clamped
            });

            test('alpha is preserved (not scaled) in static', () => {
                const result = Color.multiplyScalar({ r: 0.5, g: 0.5, b: 0.5, a: 0.7 }, 2);
                expect(result.a).toBe(0.7);
            });
        });
    });

    // ─── Invert and Grayscale ─────────────────────────────────────────────
    describe('Invert and Grayscale', () => {
        describe('invert', () => {
            test('static invert: 1 - channel', () => {
                const result = Color.invert({ r: 0.3, g: 0.7, b: 0.2, a: 0.8 });
                expect(result.r).toBeCloseTo(0.7, 4);
                expect(result.g).toBeCloseTo(0.3, 4);
                expect(result.b).toBeCloseTo(0.8, 4);
            });

            test('static invert preserves alpha', () => {
                const result = Color.invert({ r: 0.5, g: 0.5, b: 0.5, a: 0.3 });
                expect(result.a).toBeCloseTo(0.3, 4);
            });

            test('static invert with out', () => {
                const out = { r: 0, g: 0, b: 0, a: 0 };
                const ret = Color.invert({ r: 0.5, g: 0.5, b: 0.5, a: 1 }, out);
                expect(ret).toBe(out);
                expect(out.r).toBeCloseTo(0.5, 4);
            });

            test('instance invert', () => {
                const c = new Color(0.3, 0.7, 0.2);
                c.invert();
                expect(c.r).toBeCloseTo(0.7, 4);
                expect(c.g).toBeCloseTo(0.3, 4);
                expect(c.b).toBeCloseTo(0.8, 4);
            });

            test('double invert = identity', () => {
                const c = new Color(0.3, 0.7, 0.2);
                const original = c.clone();
                c.invert();
                c.invert();
                expectColorClose(c, original);
            });
        });

        describe('grayscale', () => {
            test('uses luminance weights (0.299, 0.587, 0.114)', () => {
                const result = Color.grayscale({ r: 1, g: 0, b: 0, a: 1 });
                expect(result.r).toBeCloseTo(0.299, 4);
                expect(result.g).toBeCloseTo(0.299, 4);
                expect(result.b).toBeCloseTo(0.299, 4);
            });

            test('all channels equal', () => {
                const result = Color.grayscale({ r: 0.3, g: 0.6, b: 0.9, a: 1 });
                expect(result.r).toBe(result.g);
                expect(result.g).toBe(result.b);
            });

            test('preserves alpha', () => {
                const result = Color.grayscale({ r: 0.5, g: 0.5, b: 0.5, a: 0.3 });
                expect(result.a).toBeCloseTo(0.3, 4);
            });

            test('with out parameter', () => {
                const out = { r: 0, g: 0, b: 0, a: 0 };
                const ret = Color.grayscale({ r: 1, g: 1, b: 1, a: 1 }, out);
                expect(ret).toBe(out);
                expect(out.r).toBeCloseTo(1, 4);
            });

            test('instance grayscale', () => {
                const c = new Color(1, 0, 0);
                c.grayscale();
                expect(c.r).toBeCloseTo(0.299, 4);
                expect(c.g).toBeCloseTo(0.299, 4);
                expect(c.b).toBeCloseTo(0.299, 4);
            });
        });
    });

    // ─── String Conversions ───────────────────────────────────────────────
    describe('String Conversions', () => {
        describe('toRGB', () => {
            test('red = rgb(255, 0, 0)', () => {
                expect(new Color(1, 0, 0).toRGB()).toBe('rgb(255, 0, 0)');
            });

            test('with alpha = rgba(255, 0, 0, 1)', () => {
                expect(new Color(1, 0, 0).toRGB(true)).toBe('rgba(255, 0, 0, 1)');
            });

            test('mid values', () => {
                const c = new Color(0.5, 0.5, 0.5);
                const str = c.toRGB();
                expect(str).toBe('rgb(128, 128, 128)');
            });

            test('alpha with partial transparency', () => {
                const c = new Color(1, 0, 0, 0.5);
                expect(c.toRGB(true)).toBe('rgba(255, 0, 0, 0.5)');
            });
        });

        describe('toHSLString', () => {
            test('red formatted output', () => {
                const str = new Color(1, 0, 0).toHSLString();
                expect(str).toContain('hsla(');
                expect(str).toContain('0');
            });

            test('includes alpha', () => {
                const str = new Color(1, 0, 0, 0.5).toHSLString();
                expect(str).toContain('0.5');
            });
        });

        describe('toString', () => {
            test('returns hex with alpha', () => {
                const str = new Color(1, 0, 0).toString();
                expect(str).toBe('#FF0000FF');
            });
        });
    });

    // ─── Full ColorComparer (6 modes) ─────────────────────────────────────
    describe('ColorComparer (6 modes)', () => {
        test('LUMINANCE: white > black', () => {
            const cmp = new ColorComparer(ColorComparisonMode.LUMINANCE);
            expect(cmp.compare(Color.WHITE as Color, Color.BLACK as Color)).toBe(1);
            expect(cmp.compare(Color.BLACK as Color, Color.WHITE as Color)).toBe(-1);
        });

        test('LUMINANCE: equal luminance returns 0', () => {
            const a = new Color(0.5, 0.5, 0.5);
            const b = new Color(0.5, 0.5, 0.5);
            const cmp = new ColorComparer(ColorComparisonMode.LUMINANCE);
            expect(cmp.compare(a, b)).toBe(0);
        });

        test('HUE: orders by hue angle', () => {
            const cmp = new ColorComparer(ColorComparisonMode.HUE);
            const red = Color.fromHSL(0, 1, 0.5);
            const green = Color.fromHSL(120, 1, 0.5);
            expect(cmp.compare(red, green)).toBe(-1);
            expect(cmp.compare(green, red)).toBe(1);
        });

        test('HUE: same hue returns 0', () => {
            const a = Color.fromHSL(45, 0.5, 0.5);
            const b = Color.fromHSL(45, 0.8, 0.3);
            const cmp = new ColorComparer(ColorComparisonMode.HUE);
            expect(cmp.compare(a, b)).toBe(0);
        });

        test('SATURATION: orders by saturation', () => {
            const cmp = new ColorComparer(ColorComparisonMode.SATURATION);
            const low = Color.fromHSL(0, 0.2, 0.5);
            const high = Color.fromHSL(0, 0.8, 0.5);
            expect(cmp.compare(low, high)).toBe(-1);
            expect(cmp.compare(high, low)).toBe(1);
        });

        test('RGB_DISTANCE: orders by RGB magnitude', () => {
            const cmp = new ColorComparer(ColorComparisonMode.RGB_DISTANCE);
            const dark = new Color(0.1, 0.1, 0.1);
            const bright = new Color(0.9, 0.9, 0.9);
            expect(cmp.compare(dark, bright)).toBe(-1);
            expect(cmp.compare(bright, dark)).toBe(1);
        });

        test('LAB_DISTANCE: orders by Lab magnitude', () => {
            const cmp = new ColorComparer(ColorComparisonMode.LAB_DISTANCE);
            const dark = Color.BLACK as Color;
            const bright = Color.WHITE as Color;
            expect(cmp.compare(dark, bright)).toBe(-1);
        });

        test('ALPHA: orders by alpha', () => {
            const cmp = new ColorComparer(ColorComparisonMode.ALPHA);
            const low = new Color(0.5, 0.5, 0.5, 0.2);
            const high = new Color(0.5, 0.5, 0.5, 0.8);
            expect(cmp.compare(low, high)).toBe(-1);
            expect(cmp.compare(high, low)).toBe(1);
        });

        test('ALPHA: equal alpha returns 0', () => {
            const a = new Color(0.5, 0.5, 0.5, 0.5);
            const b = new Color(0.3, 0.7, 0.1, 0.5);
            const cmp = new ColorComparer(ColorComparisonMode.ALPHA);
            expect(cmp.compare(a, b)).toBe(0);
        });

        test('default mode is LUMINANCE', () => {
            const cmp = new ColorComparer();
            expect(cmp.compare(Color.WHITE as Color, Color.BLACK as Color)).toBe(1);
        });
    });

    // ─── ColorEqualityComparer ────────────────────────────────────────────
    describe('ColorEqualityComparer', () => {
        test('epsilon-based equality', () => {
            const eq = new ColorEqualityComparer();
            const a = new Color(0.5, 0.5, 0.5);
            const b = new Color(0.5 + EPSILON * 0.5, 0.5, 0.5);
            expect(eq.equals(a, b)).toBe(true);
        });

        test('different colors are not equal', () => {
            const eq = new ColorEqualityComparer();
            expect(eq.equals(new Color(0, 0, 0), new Color(1, 1, 1))).toBe(false);
        });

        test('same reference is equal', () => {
            const eq = new ColorEqualityComparer();
            const a = new Color(0.5, 0.5, 0.5);
            expect(eq.equals(a, a)).toBe(true);
        });

        test('null/undefined handling', () => {
            const eq = new ColorEqualityComparer();
            const c = new Color(0.5, 0.5, 0.5);
            expect(eq.equals(null as any, c)).toBe(false);
            expect(eq.equals(c, null as any)).toBe(false);
            // null === null returns true (same reference check)
            expect(eq.equals(null as any, null as any)).toBe(true);
        });

        test('hash is deterministic', () => {
            const eq = new ColorEqualityComparer();
            const c = new Color(0.3, 0.6, 0.9);
            expect(eq.hash(c)).toBe(eq.hash(c));
        });

        test('equal colors have same hash', () => {
            const eq = new ColorEqualityComparer();
            const a = new Color(0.3, 0.6, 0.9);
            const b = new Color(0.3, 0.6, 0.9);
            expect(eq.hash(a)).toBe(eq.hash(b));
        });

        test('hash of null returns 0', () => {
            const eq = new ColorEqualityComparer();
            expect(eq.hash(null as any)).toBe(0);
        });

        test('custom epsilon', () => {
            const eq = new ColorEqualityComparer(0.1);
            const a = new Color(0.5, 0.5, 0.5);
            const b = new Color(0.55, 0.55, 0.55);
            expect(eq.equals(a, b)).toBe(true);
        });
    });

    // ─── Hash Integration ─────────────────────────────────────────────────
    describe('Hash Integration', () => {
        test('getHashCode is deterministic', () => {
            const c = new Color(0.3, 0.6, 0.9, 0.5);
            expect(c.getHashCode()).toBe(c.getHashCode());
        });

        test('equal colors have same hash', () => {
            const a = new Color(0.3, 0.6, 0.9);
            const b = new Color(0.3, 0.6, 0.9);
            expect(a.getHashCode()).toBe(b.getHashCode());
        });

        test('hashInto integrates with IHasher', () => {
            const hasher = new Fnv1a32();
            const c = new Color(0.3, 0.6, 0.9, 0.5);
            c.hashInto(hasher);
            const hash1 = hasher.digest();
            expect(typeof hash1).toBe('number');
        });

        test('hashInto produces different hashes for different colors', () => {
            const h1 = new Fnv1a32();
            new Color(0.1, 0.2, 0.3).hashInto(h1);
            const hash1 = h1.digest();

            const h2 = new Fnv1a32();
            new Color(0.9, 0.8, 0.7).hashInto(h2);
            const hash2 = h2.digest();

            expect(hash1).not.toBe(hash2);
        });
    });

    // ─── Random Variants ──────────────────────────────────────────────────
    describe('Random Variants', () => {
        test('random: channels in [0,1]', () => {
            for (let i = 0; i < 20; i++) {
                const c = Color.random();
                expect(c.r).toBeGreaterThanOrEqual(0);
                expect(c.r).toBeLessThanOrEqual(1);
                expect(c.g).toBeGreaterThanOrEqual(0);
                expect(c.g).toBeLessThanOrEqual(1);
                expect(c.b).toBeGreaterThanOrEqual(0);
                expect(c.b).toBeLessThanOrEqual(1);
                expect(c.a).toBe(1);
            }
        });

        test('random with alpha parameter', () => {
            const c = Color.random(0.5);
            expect(c.a).toBe(0.5);
        });

        test('randomHue: valid color', () => {
            const c = Color.randomHue();
            expect(c.r).toBeGreaterThanOrEqual(0);
            expect(c.r).toBeLessThanOrEqual(1);
            expect(c.g).toBeGreaterThanOrEqual(0);
            expect(c.g).toBeLessThanOrEqual(1);
            expect(c.b).toBeGreaterThanOrEqual(0);
            expect(c.b).toBeLessThanOrEqual(1);
        });

        test('randomHue with custom saturation and lightness', () => {
            const c = Color.randomHue(0.5, 0.5, 0.8);
            expect(c.a).toBe(0.8);
        });

        test('randomPastel: high lightness, medium saturation', () => {
            for (let i = 0; i < 20; i++) {
                const c = Color.randomPastel();
                const hsl = c.toHSL();
                expect(hsl.l).toBeGreaterThanOrEqual(0.7);
                expect(hsl.l).toBeLessThanOrEqual(1.0);
                expect(hsl.s).toBeGreaterThanOrEqual(0.3);
                expect(hsl.s).toBeLessThanOrEqual(0.7);
            }
        });

        test('randomPastel alpha parameter', () => {
            const c = Color.randomPastel(0.3);
            expect(c.a).toBe(0.3);
        });

        test('randomVibrant: high saturation, medium lightness', () => {
            for (let i = 0; i < 20; i++) {
                const c = Color.randomVibrant();
                const hsl = c.toHSL();
                expect(hsl.s).toBeGreaterThanOrEqual(0.8);
                expect(hsl.s).toBeLessThanOrEqual(1.0);
                expect(hsl.l).toBeGreaterThanOrEqual(0.4);
                expect(hsl.l).toBeLessThanOrEqual(0.7);
            }
        });

        test('randomVibrant alpha parameter', () => {
            const c = Color.randomVibrant(0.6);
            expect(c.a).toBe(0.6);
        });
    });

    // ─── Full Harmony Coverage ────────────────────────────────────────────
    describe('Full Harmony Coverage', () => {
        const base = new Color(1, 0, 0); // red, hue=0

        test('COMPLEMENTARY: 2 colors, 180 degrees apart', () => {
            const colors = Color.generateHarmony(base, ColorHarmonyType.COMPLEMENTARY);
            expect(colors.length).toBe(2);
            const hsl0 = colors[0].toHSL();
            const hsl1 = colors[1].toHSL();
            expect(Math.abs(hsl1.h - 180)).toBeLessThan(1);
        });

        test('TRIADIC: 3 colors, 120 degrees apart', () => {
            const colors = Color.generateHarmony(base, ColorHarmonyType.TRIADIC);
            expect(colors.length).toBe(3);
            const hues = colors.map(c => c.toHSL().h);
            expect(Math.abs(hues[1] - 120)).toBeLessThan(1);
            expect(Math.abs(hues[2] - 240)).toBeLessThan(1);
        });

        test('SPLIT_COMPLEMENTARY: 3 colors', () => {
            const colors = Color.generateHarmony(base, ColorHarmonyType.SPLIT_COMPLEMENTARY);
            expect(colors.length).toBe(3);
        });

        test('TETRADIC: 4 colors, 90 degrees apart', () => {
            const colors = Color.generateHarmony(base, ColorHarmonyType.TETRADIC);
            expect(colors.length).toBe(4);
            const hues = colors.map(c => c.toHSL().h);
            expect(Math.abs(hues[1] - 90)).toBeLessThan(1);
            expect(Math.abs(hues[2] - 180)).toBeLessThan(1);
            expect(Math.abs(hues[3] - 270)).toBeLessThan(1);
        });

        test('SQUARE: 4 colors, 90 degrees apart', () => {
            const colors = Color.generateHarmony(base, ColorHarmonyType.SQUARE);
            expect(colors.length).toBe(4);
        });

        test('MONOCHROMATIC: default 5 colors', () => {
            const colors = Color.generateHarmony(base, ColorHarmonyType.MONOCHROMATIC);
            expect(colors.length).toBe(5);
        });

        test('MONOCHROMATIC: respects count parameter', () => {
            const colors = Color.generateHarmony(base, ColorHarmonyType.MONOCHROMATIC, 3);
            expect(colors.length).toBe(3);
        });

        test('ANALOGOUS: default 5 colors', () => {
            const colors = Color.generateHarmony(base, ColorHarmonyType.ANALOGOUS);
            expect(colors.length).toBe(5);
        });

        test('ANALOGOUS: respects count parameter', () => {
            const colors = Color.generateHarmony(base, ColorHarmonyType.ANALOGOUS, 7);
            expect(colors.length).toBe(7);
        });

        test('ANALOGOUS: adjacent hues are 30 degrees apart (accounting for wrap)', () => {
            const colors = Color.generateHarmony(base, ColorHarmonyType.ANALOGOUS);
            const hues = colors.map(c => c.toHSL().h);
            // Colors should be at -60, -30, 0, 30, 60 from base hue (wrapping around 360)
            for (let i = 1; i < hues.length; i++) {
                let diff = Math.abs(hues[i] - hues[i - 1]);
                // Account for circular wrap: 330->0 = 30 degrees
                if (diff > 180) diff = 360 - diff;
                expect(diff).toBeCloseTo(30, 0);
            }
        });
    });

    // ─── Clone / Equals / Serialization ───────────────────────────────────
    describe('Clone / Equals / Serialization', () => {
        test('clone is deep copy', () => {
            const c = new Color(0.3, 0.6, 0.9, 0.5);
            const cl = c.clone();
            expectColorClose(cl, c);
            cl.r = 0;
            expect(c.r).toBeCloseTo(0.3, 4);
        });

        test('equals: same values', () => {
            expect(new Color(0.5, 0.5, 0.5).equals(new Color(0.5, 0.5, 0.5))).toBe(true);
        });

        test('equals: non-Color returns false', () => {
            expect(new Color(0.5, 0.5, 0.5).equals({ r: 0.5, g: 0.5, b: 0.5 } as any)).toBe(false);
        });

        test('constructor clamps values to [0,1]', () => {
            const c = new Color(1.5, -0.5, 0.5, 2);
            expect(c.r).toBe(1);
            expect(c.g).toBe(0);
            expect(c.b).toBe(0.5);
            expect(c.a).toBe(1);
        });

        test('from IColorLike with missing alpha defaults to 1', () => {
            const c = Color.from({ r: 0.5, g: 0.5, b: 0.5 });
            expect(c.a).toBe(1);
        });

        test('fromArray with offset', () => {
            const c = Color.fromArray([0, 0.5, 0.3, 0.7, 1], 1);
            expect(c.r).toBeCloseTo(0.5, 4);
            expect(c.g).toBeCloseTo(0.3, 4);
            expect(c.b).toBeCloseTo(0.7, 4);
        });

        test('fromArray throws on negative offset', () => {
            expect(() => Color.fromArray([1, 2, 3], -1)).toThrow('negative');
        });

        test('fromArray throws on insufficient length', () => {
            expect(() => Color.fromArray([1, 2], 0)).toThrow('at least');
        });

        test('fromArray with 3 elements defaults alpha to 1', () => {
            const c = Color.fromArray([0.5, 0.3, 0.7]);
            expect(c.a).toBe(1);
        });
    });

    // ─── Instance Method Variants ─────────────────────────────────────────
    describe('Instance Method Variants', () => {
        test('instance lighten matches static', () => {
            const c = new Color(0.4, 0.5, 0.6);
            const ref = Color.lighten(c, 0.2);
            const c2 = new Color(0.4, 0.5, 0.6);
            c2.lighten(0.2);
            expectColorClose(c2, ref, 1e-4);
        });

        test('instance darken matches static', () => {
            const c = new Color(0.4, 0.5, 0.6);
            const ref = Color.darken(c, 0.1);
            const c2 = new Color(0.4, 0.5, 0.6);
            c2.darken(0.1);
            expectColorClose(c2, ref, 1e-4);
        });

        test('instance saturate matches static', () => {
            const c = new Color(0.5, 0.3, 0.7);
            const ref = Color.saturate(c, 0.2);
            const c2 = new Color(0.5, 0.3, 0.7);
            c2.saturate(0.2);
            expectColorClose(c2, ref, 1e-4);
        });

        test('instance desaturate matches static', () => {
            const c = new Color(0.5, 0.3, 0.7);
            const ref = Color.desaturate(c, 0.1);
            const c2 = new Color(0.5, 0.3, 0.7);
            c2.desaturate(0.1);
            expectColorClose(c2, ref, 1e-4);
        });

        test('instance adjustHue matches static', () => {
            const c = new Color(1, 0, 0);
            const ref = Color.adjustHue(c, 120);
            const c2 = new Color(1, 0, 0);
            c2.adjustHue(120);
            expectColorClose(c2, ref, 1e-4);
        });
    });

    // ─── Integration Tests ────────────────────────────────────────────────
    describe('Integration Tests', () => {
        test('HSL roundtrip: fromHSL -> toHSL -> fromHSL preserves color', () => {
            const original = Color.fromHSL(210, 0.7, 0.4);
            const hsl = original.toHSL();
            const reconstructed = Color.fromHSL(hsl.h, hsl.s, hsl.l, hsl.a);
            expectColorClose(reconstructed, original, 1e-4);
        });

        test('CMYK roundtrip: fromCMYK -> toCMYK -> fromCMYK preserves color', () => {
            const original = new Color(0.3, 0.6, 0.9);
            const cmyk = original.toCMYK();
            const reconstructed = Color.fromCMYK(cmyk.c, cmyk.m, cmyk.y, cmyk.k);
            expectColorClose(reconstructed, original, 1e-4);
        });

        test('invert preserves alpha', () => {
            const c = new Color(0.3, 0.7, 0.2, 0.5);
            c.invert();
            expect(c.a).toBeCloseTo(0.5, 4);
        });

        test('grayscale of white = white', () => {
            const c = Color.WHITE.clone();
            c.grayscale();
            expectColorClose(c, Color.WHITE, 1e-4);
        });

        test('grayscale of black = black', () => {
            const c = Color.BLACK.clone();
            c.grayscale();
            expectColorClose(c, Color.BLACK, 1e-4);
        });

        test('add then subtract returns approximately original', () => {
            const original = new Color(0.3, 0.5, 0.7);
            const c = original.clone();
            const delta = { r: 0.1, g: 0.1, b: 0.1, a: 0 };
            c.add(delta);
            c.subtract(delta);
            expectColorClose(c, original, 1e-4);
        });

        test('multiplyScalar(1) is identity', () => {
            const c = new Color(0.3, 0.6, 0.9);
            const result = Color.multiplyScalar(c, 1);
            expectColorClose(result, c, 1e-4);
        });
    });
});
