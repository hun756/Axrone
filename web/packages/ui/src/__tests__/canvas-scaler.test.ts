import { describe, expect, it } from 'vitest';
import {
    resolveCanvasScale,
    canvasScaleToTransform,
    mapViewportPointToCanvas,
} from '../layout/canvas-scaler';
import type { UICanvasConfig } from '../types/ui-asset';

const makeCanvas = (overrides: Partial<UICanvasConfig> = {}): UICanvasConfig => ({
    referenceWidth: 1920,
    referenceHeight: 1080,
    scaleMode: 'match-width-or-height',
    matchBias: 0.5,
    ...overrides,
});

describe('@axrone/ui canvas-scaler', () => {
    describe('match-width mode', () => {
        const canvas = makeCanvas({ scaleMode: 'match-width', matchBias: 0 });

        it('scales uniformly to match actual width', () => {
            const result = resolveCanvasScale(canvas, 1920, 1080);
            expect(result.scaleX).toBeCloseTo(1);
            expect(result.scaleY).toBeCloseTo(1);
            expect(result.offsetX).toBeCloseTo(0);
            expect(result.offsetY).toBeCloseTo(0);
        });

        it('letterboxes when actual height is larger', () => {
            // 1920x1080 ref → 960x1080 actual (half width)
            const result = resolveCanvasScale(canvas, 960, 1080);
            expect(result.scaleX).toBeCloseTo(0.5);
            expect(result.scaleY).toBeCloseTo(0.5);
            // effective height = 1080 * 0.5 = 540, offset = (1080 - 540) / 2 = 270
            expect(result.offsetY).toBeCloseTo(270);
            expect(result.effectiveHeight).toBeCloseTo(540);
        });

        it('crops when actual height is smaller relative to width', () => {
            // 1920x1080 ref → 3840x1080 actual (2x width, same height)
            const result = resolveCanvasScale(canvas, 3840, 1080);
            expect(result.scaleX).toBeCloseTo(2);
            expect(result.scaleY).toBeCloseTo(2);
            // effective height = 1080 * 2 = 2160, offset = (1080 - 2160) / 2 = -540
            expect(result.offsetY).toBeCloseTo(-540);
            expect(result.effectiveHeight).toBeCloseTo(2160);
        });
    });

    describe('match-height mode', () => {
        const canvas = makeCanvas({ scaleMode: 'match-height', matchBias: 0 });

        it('scales uniformly to match actual height', () => {
            const result = resolveCanvasScale(canvas, 1920, 1080);
            expect(result.scaleX).toBeCloseTo(1);
            expect(result.scaleY).toBeCloseTo(1);
        });

        it('pillarboxes when actual width is larger', () => {
            // 1920x1080 ref → 1920x540 actual (half height)
            const result = resolveCanvasScale(canvas, 1920, 540);
            expect(result.scaleX).toBeCloseTo(0.5);
            expect(result.scaleY).toBeCloseTo(0.5);
            // effective width = 1920 * 0.5 = 960, offset = (1920 - 960) / 2 = 480
            expect(result.offsetX).toBeCloseTo(480);
            expect(result.effectiveWidth).toBeCloseTo(960);
        });
    });

    describe('match-width-or-height mode', () => {
        it('with bias=0 behaves like match-width', () => {
            const canvas = makeCanvas({ scaleMode: 'match-width-or-height', matchBias: 0 });
            const result = resolveCanvasScale(canvas, 960, 1080);
            expect(result.scaleX).toBeCloseTo(0.5);
            expect(result.scaleY).toBeCloseTo(0.5);
            expect(result.offsetX).toBeCloseTo(0);
        });

        it('with bias=1 behaves like match-height', () => {
            const canvas = makeCanvas({ scaleMode: 'match-width-or-height', matchBias: 1 });
            const result = resolveCanvasScale(canvas, 1920, 540);
            expect(result.scaleX).toBeCloseTo(0.5);
            expect(result.scaleY).toBeCloseTo(0.5);
            expect(result.offsetY).toBeCloseTo(0);
        });

        it('with bias=0.5 blends evenly', () => {
            const canvas = makeCanvas({ scaleMode: 'match-width-or-height', matchBias: 0.5 });
            // 1920x1080 ref → 1920x540 actual
            // scaleW = 1.0, scaleH = 0.5, blended = 0.75
            const result = resolveCanvasScale(canvas, 1920, 540);
            expect(result.scaleX).toBeCloseTo(0.75);
            expect(result.scaleY).toBeCloseTo(0.75);
        });

        it('portrait: 1920x1080 ref → 1080x1920 actual', () => {
            const canvas = makeCanvas({ scaleMode: 'match-width-or-height', matchBias: 0.5 });
            const result = resolveCanvasScale(canvas, 1080, 1920);
            // scaleW = 1080/1920 = 0.5625, scaleH = 1920/1080 = 1.7778
            // blended = 0.5625 * 0.5 + 1.7778 * 0.5 = 1.1701
            const expectedScale = (1080 / 1920) * 0.5 + (1920 / 1080) * 0.5;
            expect(result.scaleX).toBeCloseTo(expectedScale);
            expect(result.scaleY).toBeCloseTo(expectedScale);
        });
    });

    describe('fill mode', () => {
        const canvas = makeCanvas({ scaleMode: 'fill' });

        it('stretches non-uniformly to fill viewport', () => {
            const result = resolveCanvasScale(canvas, 960, 540);
            expect(result.scaleX).toBeCloseTo(0.5);
            expect(result.scaleY).toBeCloseTo(0.5);
            expect(result.offsetX).toBeCloseTo(0);
            expect(result.offsetY).toBeCloseTo(0);
            expect(result.effectiveWidth).toBeCloseTo(960);
            expect(result.effectiveHeight).toBeCloseTo(540);
        });

        it('handles non-uniform aspect ratio change', () => {
            // 1920x1080 ref → 3840x540 actual (2x wide, 0.5x tall)
            const result = resolveCanvasScale(canvas, 3840, 540);
            expect(result.scaleX).toBeCloseTo(2);
            expect(result.scaleY).toBeCloseTo(0.5);
        });
    });

    describe('fixed mode', () => {
        const canvas = makeCanvas({ scaleMode: 'fixed' });

        it('does not scale, centers content', () => {
            const result = resolveCanvasScale(canvas, 3840, 2160);
            expect(result.scaleX).toBeCloseTo(1);
            expect(result.scaleY).toBeCloseTo(1);
            expect(result.offsetX).toBeCloseTo((3840 - 1920) / 2);
            expect(result.offsetY).toBeCloseTo((2160 - 1080) / 2);
            expect(result.effectiveWidth).toBeCloseTo(1920);
            expect(result.effectiveHeight).toBeCloseTo(1080);
        });

        it('negative offset when viewport is smaller', () => {
            const result = resolveCanvasScale(canvas, 960, 540);
            expect(result.offsetX).toBeCloseTo((960 - 1920) / 2);
            expect(result.offsetY).toBeCloseTo((540 - 1080) / 2);
        });
    });

    describe('edge cases', () => {
        it('returns identity-like result for zero actual dimensions', () => {
            const canvas = makeCanvas();
            const result = resolveCanvasScale(canvas, 0, 0);
            expect(result.scaleX).toBe(1);
            expect(result.scaleY).toBe(1);
        });

        it('returns identity-like result for zero reference dimensions', () => {
            const canvas = makeCanvas({ referenceWidth: 0, referenceHeight: 0 });
            const result = resolveCanvasScale(canvas, 1920, 1080);
            expect(result.scaleX).toBe(1);
            expect(result.scaleY).toBe(1);
        });

        it('iPhone-like viewport: 1920x1080 ref → 375x812 actual', () => {
            const canvas = makeCanvas({ scaleMode: 'match-width-or-height', matchBias: 0.5 });
            const result = resolveCanvasScale(canvas, 375, 812);
            // scaleW = 375/1920 ≈ 0.1953, scaleH = 812/1080 ≈ 0.7519
            // blended = 0.1953 * 0.5 + 0.7519 * 0.5 ≈ 0.4736
            expect(result.scaleX).toBeGreaterThan(0);
            expect(result.scaleY).toBeGreaterThan(0);
            expect(result.scaleX).toBeCloseTo(result.scaleY); // uniform scale
        });
    });

    describe('canvasScaleToTransform', () => {
        it('produces correct AffineTransform2D', () => {
            const canvas = makeCanvas({ scaleMode: 'match-width', matchBias: 0 });
            const scaleResult = resolveCanvasScale(canvas, 960, 1080);
            const transform = canvasScaleToTransform(scaleResult);

            expect(transform).toHaveLength(6);
            expect(transform[0]).toBeCloseTo(scaleResult.scaleX); // a
            expect(transform[1]).toBe(0);                          // b
            expect(transform[2]).toBe(0);                          // c
            expect(transform[3]).toBeCloseTo(scaleResult.scaleY);  // d
            expect(transform[4]).toBeCloseTo(scaleResult.offsetX); // e
            expect(transform[5]).toBeCloseTo(scaleResult.offsetY); // f
        });
    });

    describe('mapViewportPointToCanvas', () => {
        const scaleModes = [
            'match-width',
            'match-height',
            'match-width-or-height',
            'fill',
            'fixed',
        ] as const;

        it.each(scaleModes)('round-trips reference points through %s scaling', (scaleMode) => {
            const canvas = makeCanvas({ scaleMode, matchBias: 0.5 });
            const scale = resolveCanvasScale(canvas, 375, 812);
            const referencePoints = [
                { x: 0, y: 0 },
                { x: 960, y: 540 },
                { x: 1920, y: 1080 },
                { x: 123.5, y: 987.25 },
            ];
            for (const point of referencePoints) {
                const viewportX = point.x * scale.scaleX + scale.offsetX;
                const viewportY = point.y * scale.scaleY + scale.offsetY;
                const mapped = mapViewportPointToCanvas(scale, viewportX, viewportY);
                expect(mapped.x).toBeCloseTo(point.x, 6);
                expect(mapped.y).toBeCloseTo(point.y, 6);
            }
        });

        it('maps letterbox band points outside the reference bounds', () => {
            // match-width at half size: 1920x1080 ref -> 960x1080 actual,
            // content band is y in [270, 810]; clicks above/below map out of range.
            const canvas = makeCanvas({ scaleMode: 'match-width', matchBias: 0 });
            const scale = resolveCanvasScale(canvas, 960, 1080);

            const aboveBand = mapViewportPointToCanvas(scale, 480, 100);
            expect(aboveBand.y).toBeLessThan(0);

            const belowBand = mapViewportPointToCanvas(scale, 480, 1000);
            expect(belowBand.y).toBeGreaterThan(1080);

            const insideBand = mapViewportPointToCanvas(scale, 480, 540);
            expect(insideBand.x).toBeCloseTo(960);
            expect(insideBand.y).toBeCloseTo(540);
        });

        it('falls back to identity for degenerate scales', () => {
            const mapped = mapViewportPointToCanvas(
                {
                    scaleX: 0,
                    scaleY: 0,
                    offsetX: 10,
                    offsetY: 20,
                    effectiveWidth: 0,
                    effectiveHeight: 0,
                },
                111,
                222
            );
            expect(mapped.x).toBe(111);
            expect(mapped.y).toBe(222);
        });
    });
});
