import type { FontWeight, TextLayoutResult, TextLayoutConstraint, ResolvedTextBlock } from '../types';
import type { FontRegistry } from '../font';
import type { TextLayoutEngine } from '../text';
import { LruCache, createCacheKey } from '../text/internals';

/**
 * Adapter interface providing the AutoSizeService access to font and text
 * measurement without circular imports.
 */
export interface AutoSizeHost {
    readonly fonts: FontRegistry;
    readonly textEngine: TextLayoutEngine;
}

/**
 * Manages auto-size measurement and caching for shrink-to-fit text layouts.
 *
 * Responsibilities:
 * - Maintain an LRU cache of auto-size measurement results
 * - Resolve shrink-to-fit layouts using analytical scaling
 * - Build cache keys consistent with TextLayoutEngine's format
 */
export class AutoSizeService {
    private readonly cache = new LruCache<string, TextLayoutResult>(64);

    /**
     * Measures text with auto-size support. If the text block uses
     * shrink-to-fit, the result is cached. Otherwise, delegates to the
     * text engine directly.
     */
    measure(host: AutoSizeHost, text: ResolvedTextBlock, constraints: TextLayoutConstraint): TextLayoutResult {
        if (text.autoSize !== 'shrink-to-fit') {
            return host.textEngine.measure(text, constraints);
        }

        // Build the cache key with the same createCacheKey format the
        // TextLayoutEngine uses (plus the autosize parameters that shape the
        // result), so both caches stay consistent as the block type evolves.
        const faceId = host.fonts.resolveFace({
            family: text.family,
            weight: text.weight as FontWeight,
            style: text.style,
            locale: text.locale,
        });
        const cacheKey = `${createCacheKey(
            text,
            faceId,
            constraints.width ?? Number.POSITIVE_INFINITY,
            constraints.height ?? Number.POSITIVE_INFINITY
        )}|autosize|${text.autoSize}|${text.minAutoSize}|${text.maxAutoSize}`;
        const cached = this.cache.get(cacheKey);
        if (cached) {
            return cached;
        }

        const maxWidth = constraints.width ?? Number.POSITIVE_INFINITY;
        const maxHeight = constraints.height ?? Number.POSITIVE_INFINITY;
        // When either constraint is unbounded, text already fits by definition — no-op.
        if (!Number.isFinite(maxWidth) && !Number.isFinite(maxHeight)) {
            return host.textEngine.measure(text, constraints);
        }
        const maxSize = Math.min(text.size, text.maxAutoSize);
        const minSize = text.minAutoSize;
        if (minSize >= maxSize) {
            const clampedBlock: ResolvedTextBlock = { ...text, size: minSize, autoSize: 'none' };
            return host.textEngine.measure(clampedBlock, constraints);
        }
        const result = this.resolveShrinkToFitLayout(host, text, constraints, minSize, maxSize, maxWidth, maxHeight);

        // Store result in the autosize cache (eviction handled by the LRU).
        this.cache.set(cacheKey, result);
        return result;
    }

    /**
     * Invalidates the auto-size cache. Called when fonts or text engine state changes.
     */
    invalidate(): void {
        this.cache.clear();
    }

    /**
     * Resolves the largest font size ≤ maxSize whose layout fits the given
     * constraints, using analytical scaling instead of a binary search.
     *
     * Layout dimensions scale ~linearly with font size, so the measured
     * overflow/headroom ratio estimates the fitting size directly. Bounded to
     * at most 4 full measures (down-scale refinement, one headroom nudge, a
     * min-size bottom-out) versus the previous 8-iteration binary search that
     * re-measured the whole text on every step.
     */
    private resolveShrinkToFitLayout(
        host: AutoSizeHost,
        text: ResolvedTextBlock,
        constraints: TextLayoutConstraint,
        minSize: number,
        maxSize: number,
        maxWidth: number,
        maxHeight: number
    ): TextLayoutResult {
        const measureAt = (size: number): TextLayoutResult =>
            host.textEngine.measure({ ...text, size, autoSize: 'none' }, constraints);

        let size = maxSize;
        let layout = measureAt(size);
        if (this.textLayoutFits(layout, maxWidth, maxHeight)) {
            return layout;
        }

        // Down-scale: the overflow ratio directly estimates the fitting size.
        // The 0.995 safety factor absorbs non-linear effects (letter spacing,
        // re-wrapping); the 0.25px floor prevents sub-pixel thrash.
        for (let refinement = 0; refinement < 2; refinement += 1) {
            const ratio = Math.min(1, this.textScaleRatio(layout, maxWidth, maxHeight));
            if (ratio >= 1) {
                break;
            }
            const estimated = size * ratio * 0.995;
            const next = estimated >= size - 0.25 ? Math.max(minSize, size - 0.5) : Math.max(minSize, estimated);
            if (next === size) {
                break;
            }
            size = next;
            layout = measureAt(size);
            if (this.textLayoutFits(layout, maxWidth, maxHeight)) {
                break;
            }
        }

        if (!this.textLayoutFits(layout, maxWidth, maxHeight)) {
            // Estimates still overshoot; bottom out at the smallest size.
            return measureAt(minSize);
        }

        // Recover conservative estimates: re-wrapping can free a whole line,
        // leaving real headroom between the scaled estimate and the true fit.
        if (size < maxSize) {
            const headroom = this.textScaleRatio(layout, maxWidth, maxHeight);
            if (headroom > 1.001) {
                const nudged = Math.min(maxSize, size * headroom * 0.995);
                if (nudged > size + 0.25) {
                    const nudgedLayout = measureAt(nudged);
                    if (this.textLayoutFits(nudgedLayout, maxWidth, maxHeight)) {
                        return nudgedLayout;
                    }
                }
            }
        }
        return layout;
    }

    /** Headroom factor: >1 when the layout could grow, ≤1 when it overflows. */
    private textScaleRatio(layout: TextLayoutResult, maxWidth: number, maxHeight: number): number {
        let ratio = Number.POSITIVE_INFINITY;
        if (Number.isFinite(maxWidth) && layout.width > 0) {
            ratio = Math.min(ratio, maxWidth / layout.width);
        }
        if (Number.isFinite(maxHeight) && layout.height > 0) {
            ratio = Math.min(ratio, maxHeight / layout.height);
        }
        return Number.isFinite(ratio) ? ratio : 1;
    }

    private textLayoutFits(layout: TextLayoutResult, maxWidth: number, maxHeight: number): boolean {
        return (
            layout.width <= (Number.isFinite(maxWidth) ? maxWidth : Number.POSITIVE_INFINITY) &&
            layout.height <= (Number.isFinite(maxHeight) ? maxHeight : Number.POSITIVE_INFINITY)
        );
    }
}
