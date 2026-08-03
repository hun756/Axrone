import { describe, expect, it } from 'vitest';
import {
    createColor,
    createGradientLookupTable,
    createGradientStop,
    createLinearGradientPaint,
    createPaint,
    createPaintFingerprint,
    createRadialGradientPaint,
    createSolidPaint,
    createStroke,
    isGradientPaint,
    isLinearGradientPaint,
    isRadialGradientPaint,
    isShapePaint,
    isSolidPaint,
    modulatePaintAlpha,
    PaintValidationError,
    samplePaint,
    ShapeValidationError,
} from '../index';

describe('@axrone/shapes-2d paint module', () => {
    describe('createColor', () => {
        it('parses hex string', () => {
            const c = createColor('#ff0000');
            expect(c.r).toBeCloseTo(1, 2);
            expect(c.g).toBeCloseTo(0, 2);
            expect(c.b).toBeCloseTo(0, 2);
            expect(c.a).toBeCloseTo(1, 2);
        });

        it('parses named color', () => {
            const c = createColor('red');
            expect(c.r).toBeCloseTo(1, 2);
            expect(c.g).toBeCloseTo(0, 2);
            expect(c.b).toBeCloseTo(0, 2);
        });

        it('parses RGB tuple', () => {
            const c = createColor([0.5, 0.5, 0.5]);
            expect(c.r).toBeCloseTo(0.5, 2);
            expect(c.a).toBeCloseTo(1, 2);
        });

        it('parses RGBA tuple', () => {
            const c = createColor([1, 0, 0, 0.5]);
            expect(c.r).toBeCloseTo(1, 2);
            expect(c.a).toBeCloseTo(0.5, 2);
        });

        it('parses IColorLike object', () => {
            const c = createColor({ r: 0, g: 1, b: 0 });
            expect(c.g).toBeCloseTo(1, 2);
            expect(c.a).toBeCloseTo(1, 2);
        });

        it('parses IColorLike with alpha', () => {
            const c = createColor({ r: 0, g: 0, b: 1, a: 0.25 });
            expect(c.b).toBeCloseTo(1, 2);
            expect(c.a).toBeCloseTo(0.25, 2);
        });

        it('throws for empty string', () => {
            expect(() => createColor('')).toThrow(PaintValidationError);
            expect(() => createColor('   ')).toThrow(PaintValidationError);
        });

        it('throws for invalid hex', () => {
            expect(() => createColor('#xyz')).toThrow(PaintValidationError);
        });

        it('throws for tuple with fewer than 3 channels', () => {
            expect(() => createColor([1, 2] as unknown as [number, number, number])).toThrow(
                PaintValidationError
            );
        });

        it('throws for unsupported input', () => {
            expect(() => createColor(42 as unknown as string)).toThrow(PaintValidationError);
        });
    });

    describe('createGradientStop', () => {
        it('creates a valid stop', () => {
            const stop = createGradientStop(0.5, '#ff0000');
            expect(stop.offset).toBe(0.5);
            expect(stop.color.r).toBeCloseTo(1, 2);
        });

        it('throws for offset out of range', () => {
            expect(() => createGradientStop(-0.1, '#000')).toThrow(PaintValidationError);
            expect(() => createGradientStop(1.1, '#000')).toThrow(PaintValidationError);
        });
    });

    describe('createSolidPaint', () => {
        it('creates solid paint with kind "solid"', () => {
            const paint = createSolidPaint('#00ff00');
            expect(paint.kind).toBe('solid');
            expect(paint.color.g).toBeCloseTo(1, 2);
        });
    });

    describe('createLinearGradientPaint', () => {
        const baseInput = {
            start: [0, 0] as [number, number],
            end: [1, 0] as [number, number],
            stops: [
                { offset: 0, color: '#ff0000' },
                { offset: 1, color: '#0000ff' },
            ],
        };

        it('creates with defaults', () => {
            const paint = createLinearGradientPaint(baseInput);
            expect(paint.kind).toBe('linear-gradient');
            expect(paint.spread).toBe('pad');
            expect(paint.colorSpace).toBe('srgb');
            expect(paint.units).toBe('shape-bounds');
            expect(paint.stops).toHaveLength(2);
        });

        it('accepts custom options', () => {
            const paint = createLinearGradientPaint({
                ...baseInput,
                spread: 'repeat',
                colorSpace: 'hsl',
                units: 'local',
            });
            expect(paint.spread).toBe('repeat');
            expect(paint.colorSpace).toBe('hsl');
            expect(paint.units).toBe('local');
        });

        it('throws for empty stops', () => {
            expect(() =>
                createLinearGradientPaint({ start: [0, 0], end: [1, 0], stops: [] })
            ).toThrow(PaintValidationError);
        });

        it('sorts stops by offset', () => {
            const paint = createLinearGradientPaint({
                start: [0, 0],
                end: [1, 0],
                stops: [
                    { offset: 1, color: '#0000ff' },
                    { offset: 0, color: '#ff0000' },
                ],
            });
            expect(paint.stops[0]!.offset).toBe(0);
            expect(paint.stops[1]!.offset).toBe(1);
        });
    });

    describe('createRadialGradientPaint', () => {
        const baseInput = {
            center: [0.5, 0.5] as [number, number],
            radius: 10,
            stops: [{ offset: 0, color: '#fff' }, { offset: 1, color: '#000' }],
        };

        it('creates with defaults', () => {
            const paint = createRadialGradientPaint(baseInput);
            expect(paint.kind).toBe('radial-gradient');
            expect(paint.radius).toBe(10);
            expect(paint.spread).toBe('pad');
        });

        it('throws for non-positive radius', () => {
            expect(() => createRadialGradientPaint({ ...baseInput, radius: 0 })).toThrow(
                ShapeValidationError
            );
        });
    });

    describe('createPaint', () => {
        it('passes through SolidPaint', () => {
            const solid = createSolidPaint('#ff0000');
            const result = createPaint(solid);
            expect(result.kind).toBe('solid');
        });

        it('passes through LinearGradientPaint', () => {
            const lg = createLinearGradientPaint({
                start: [0, 0],
                end: [1, 0],
                stops: [{ offset: 0, color: '#f00' }, { offset: 1, color: '#00f' }],
            });
            const result = createPaint(lg);
            expect(result.kind).toBe('linear-gradient');
        });

        it('passes through RadialGradientPaint', () => {
            const rg = createRadialGradientPaint({
                center: [0, 0],
                radius: 5,
                stops: [{ offset: 0, color: '#fff' }],
            });
            const result = createPaint(rg);
            expect(result.kind).toBe('radial-gradient');
        });

        it('creates solid from color-like input', () => {
            expect(createPaint('#ff0000').kind).toBe('solid');
            expect(createPaint([1, 0, 0]).kind).toBe('solid');
            expect(createPaint({ r: 1, g: 0, b: 0 }).kind).toBe('solid');
        });

        it('creates linear gradient from input with start/end', () => {
            const result = createPaint({
                start: [0, 0],
                end: [1, 0],
                stops: [{ offset: 0, color: '#f00' }, { offset: 1, color: '#00f' }],
            });
            expect(result.kind).toBe('linear-gradient');
        });

        it('creates radial gradient from input with center/radius', () => {
            const result = createPaint({
                center: [0.5, 0.5],
                radius: 10,
                stops: [{ offset: 0, color: '#fff' }],
            });
            expect(result.kind).toBe('radial-gradient');
        });

        it('throws for unsupported input', () => {
            expect(() => createPaint(42 as unknown as string)).toThrow(PaintValidationError);
        });
    });

    describe('createStroke', () => {
        it('creates stroke with default alignment', () => {
            const stroke = createStroke({ paint: '#000', width: 2 });
            expect(stroke.paint.kind).toBe('solid');
            expect(stroke.width).toBe(2);
            expect(stroke.alignment).toBe('center');
        });

        it('accepts explicit alignment', () => {
            const stroke = createStroke({ paint: '#000', width: 3, alignment: 'inside' });
            expect(stroke.alignment).toBe('inside');
        });

        it('throws for non-positive width', () => {
            expect(() => createStroke({ paint: '#000', width: 0 })).toThrow(ShapeValidationError);
        });
    });

    describe('paint type guards', () => {
        const solid = createSolidPaint('#ff0000');
        const linear = createLinearGradientPaint({
            start: [0, 0], end: [1, 0],
            stops: [{ offset: 0, color: '#f00' }, { offset: 1, color: '#00f' }],
        });
        const radial = createRadialGradientPaint({
            center: [0, 0], radius: 5,
            stops: [{ offset: 0, color: '#fff' }],
        });

        it('isSolidPaint', () => {
            expect(isSolidPaint(solid)).toBe(true);
            expect(isSolidPaint(linear)).toBe(false);
            expect(isSolidPaint(null)).toBe(false);
        });

        it('isLinearGradientPaint', () => {
            expect(isLinearGradientPaint(linear)).toBe(true);
            expect(isLinearGradientPaint(solid)).toBe(false);
            expect(isLinearGradientPaint(undefined)).toBe(false);
        });

        it('isRadialGradientPaint', () => {
            expect(isRadialGradientPaint(radial)).toBe(true);
            expect(isRadialGradientPaint(solid)).toBe(false);
        });

        it('isGradientPaint', () => {
            expect(isGradientPaint(linear)).toBe(true);
            expect(isGradientPaint(radial)).toBe(true);
            expect(isGradientPaint(solid)).toBe(false);
        });

        it('isShapePaint', () => {
            expect(isShapePaint(solid)).toBe(true);
            expect(isShapePaint(linear)).toBe(true);
            expect(isShapePaint(radial)).toBe(true);
            expect(isShapePaint(null)).toBe(false);
            expect(isShapePaint(42)).toBe(false);
        });
    });

    describe('createGradientLookupTable', () => {
        const linearPaint = createLinearGradientPaint({
            start: [0, 0], end: [1, 0],
            stops: [
                { offset: 0, color: '#ff0000' },
                { offset: 1, color: '#0000ff' },
            ],
        });

        it('creates table with default size', () => {
            const table = createGradientLookupTable(linearPaint);
            expect(table).toBeInstanceOf(Float32Array);
            expect(table.length).toBe(256 * 4);
        });

        it('creates table with custom size', () => {
            const table = createGradientLookupTable(linearPaint, 8);
            expect(table.length).toBe(32);
        });

        it('throws for size < 2', () => {
            expect(() => createGradientLookupTable(linearPaint, 1)).toThrow(PaintValidationError);
        });

        it('returns cached table for same paint and size', () => {
            const t1 = createGradientLookupTable(linearPaint, 16);
            const t2 = createGradientLookupTable(linearPaint, 16);
            expect(t1).toBe(t2);
        });
    });

    describe('samplePaint', () => {
        it('returns solid color regardless of point', () => {
            const solid = createSolidPaint('#ff0000');
            const c = samplePaint(solid, [50, 50]);
            expect(c.r).toBeCloseTo(1, 2);
            expect(c.g).toBeCloseTo(0, 2);
        });

        it('samples linear gradient at start (t=0)', () => {
            const lg = createLinearGradientPaint({
                start: [0, 0], end: [100, 0],
                units: 'local',
                stops: [
                    { offset: 0, color: '#ff0000' },
                    { offset: 1, color: '#0000ff' },
                ],
            });
            const c = samplePaint(lg, [0, 0]);
            expect(c.r).toBeCloseTo(1, 1);
            expect(c.b).toBeCloseTo(0, 1);
        });

        it('samples linear gradient at end (t=1)', () => {
            const lg = createLinearGradientPaint({
                start: [0, 0], end: [100, 0],
                units: 'local',
                stops: [
                    { offset: 0, color: '#ff0000' },
                    { offset: 1, color: '#0000ff' },
                ],
            });
            const c = samplePaint(lg, [100, 0]);
            expect(c.r).toBeCloseTo(0, 1);
            expect(c.b).toBeCloseTo(1, 1);
        });

        it('samples radial gradient at center', () => {
            const rg = createRadialGradientPaint({
                center: [50, 50], radius: 50,
                units: 'local',
                stops: [
                    { offset: 0, color: '#ffffff' },
                    { offset: 1, color: '#000000' },
                ],
            });
            const c = samplePaint(rg, [50, 50]);
            expect(c.r).toBeCloseTo(1, 1);
            expect(c.g).toBeCloseTo(1, 1);
        });

        it('throws when bounds required but missing for shape-bounds units', () => {
            const lg = createLinearGradientPaint({
                start: [0, 0.5], end: [1, 0.5],
                units: 'shape-bounds',
                stops: [{ offset: 0, color: '#f00' }, { offset: 1, color: '#00f' }],
            });
            expect(() => samplePaint(lg, [50, 50])).toThrow(PaintValidationError);
        });

        it('samples with linear-srgb color space', () => {
            const lg = createLinearGradientPaint({
                start: [0, 0], end: [100, 0],
                units: 'local',
                colorSpace: 'linear-srgb',
                stops: [
                    { offset: 0, color: '#ff0000' },
                    { offset: 1, color: '#0000ff' },
                ],
            });
            const c = samplePaint(lg, [50, 0]);
            expect(c.r).toBeGreaterThan(0);
            expect(c.b).toBeGreaterThan(0);
        });

        it('samples with hsl color space', () => {
            const lg = createLinearGradientPaint({
                start: [0, 0], end: [100, 0],
                units: 'local',
                colorSpace: 'hsl',
                stops: [
                    { offset: 0, color: '#ff0000' },
                    { offset: 1, color: '#0000ff' },
                ],
            });
            const c = samplePaint(lg, [50, 0]);
            expect(c).toBeDefined();
            expect(c.r).toBeGreaterThanOrEqual(0);
        });

        it('samples with lab color space', () => {
            const lg = createLinearGradientPaint({
                start: [0, 0], end: [100, 0],
                units: 'local',
                colorSpace: 'lab',
                stops: [
                    { offset: 0, color: '#ff0000' },
                    { offset: 1, color: '#0000ff' },
                ],
            });
            const c = samplePaint(lg, [50, 0]);
            expect(c).toBeDefined();
        });

        it('handles single-stop gradient', () => {
            const lg = createLinearGradientPaint({
                start: [0, 0], end: [100, 0],
                units: 'local',
                stops: [{ offset: 0.5, color: '#00ff00' }],
            });
            const c = samplePaint(lg, [50, 0]);
            expect(c.g).toBeCloseTo(1, 1);
        });
    });

    describe('modulatePaintAlpha', () => {
        it('passes through at opacity 1', () => {
            const c = modulatePaintAlpha({ r: 1, g: 0, b: 0, a: 1 }, 1);
            expect(c.a).toBeCloseTo(1, 4);
        });

        it('halves alpha at opacity 0.5', () => {
            const c = modulatePaintAlpha({ r: 1, g: 0, b: 0, a: 1 }, 0.5);
            expect(c.a).toBeCloseTo(0.5, 4);
        });

        it('clamps to 0 at opacity 0', () => {
            const c = modulatePaintAlpha({ r: 1, g: 0, b: 0, a: 1 }, 0);
            expect(c.a).toBeCloseTo(0, 4);
        });

        it('treats missing alpha as 1', () => {
            const c = modulatePaintAlpha({ r: 1, g: 0, b: 0 } as unknown as { r: number; g: number; b: number; a: number }, 0.5);
            expect(c.a).toBeCloseTo(0.5, 4);
        });
    });

    describe('createPaintFingerprint', () => {
        it('produces solid fingerprint with expected prefix', () => {
            const fp = createPaintFingerprint(createSolidPaint('#ff0000'));
            expect(fp.startsWith('solid:')).toBe(true);
        });

        it('produces linear gradient fingerprint', () => {
            const fp = createPaintFingerprint(
                createLinearGradientPaint({
                    start: [0, 0], end: [1, 0],
                    stops: [{ offset: 0, color: '#f00' }, { offset: 1, color: '#00f' }],
                })
            );
            expect(fp.startsWith('linear:')).toBe(true);
        });

        it('produces radial gradient fingerprint', () => {
            const fp = createPaintFingerprint(
                createRadialGradientPaint({
                    center: [0, 0], radius: 5,
                    stops: [{ offset: 0, color: '#fff' }],
                })
            );
            expect(fp.startsWith('radial:')).toBe(true);
        });

        it('same paint produces same fingerprint', () => {
            const a = createPaintFingerprint(createSolidPaint('#123456'));
            const b = createPaintFingerprint(createSolidPaint('#123456'));
            expect(a).toBe(b);
        });

        it('different paint produces different fingerprint', () => {
            const a = createPaintFingerprint(createSolidPaint('#ff0000'));
            const b = createPaintFingerprint(createSolidPaint('#00ff00'));
            expect(a).not.toBe(b);
        });
    });
});
