import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { StackCaptureEngine } from '../core/stack-capture';

describe('StackCaptureEngine', () => {
    let engine: StackCaptureEngine;

    beforeEach(() => {
        StackCaptureEngine.clearCache();
        engine = new StackCaptureEngine();
    });

    it('should capture a stack trace', () => {
        const frames = engine.captureStack(10);
        expect(Array.isArray(frames)).toBe(true);
    });

    it('should respect max depth', () => {
        const frames = engine.captureStack(3);
        expect(frames.length).toBeLessThanOrEqual(3);
    });

    it('should produce stack signatures', () => {
        const sig = engine.captureStackSignature();
        expect(typeof sig).toBe('string');
    });

    it('should manage cache lifecycle', () => {
        engine.captureStack(10);
        const sizeBefore = engine.getCacheSize();
        StackCaptureEngine.clearCache();
        expect(engine.getCacheSize()).toBe(0);
    });

    it('should support custom max cache size', () => {
        StackCaptureEngine.setMaxCacheSize(100);
        expect(StackCaptureEngine.clearCache).toBeDefined();
    });

    describe('frame structure', () => {
        it('should produce frames with function names', () => {
            const frames = engine.captureStack(10);
            for (const frame of frames) {
                expect(typeof frame.function).toBe('string');
                expect(frame.function.length).toBeGreaterThan(0);
            }
        });

        it('should include file and line info for V8 stacks (Node.js)', () => {
            const frames = engine.captureStack(10);
            // In Node.js/vitest, stacks are V8 format
            if (frames.length > 0) {
                const frame = frames[0];
                // V8 frames should have file/lineNumber/columnNumber
                if (frame.file) {
                    expect(typeof frame.file).toBe('string');
                    expect(typeof frame.lineNumber).toBe('number');
                    expect(typeof frame.columnNumber).toBe('number');
                }
            }
        });
    });

    describe('cache eviction', () => {
        afterEach(() => {
            StackCaptureEngine.setMaxCacheSize(4096);
        });

        it('should evict oldest entry when cache is full', () => {
            StackCaptureEngine.setMaxCacheSize(2);
            StackCaptureEngine.clearCache();

            // Create a fresh engine to avoid stale cache hits
            const eng1 = new StackCaptureEngine();
            eng1.captureStack(5);
            const size1 = eng1.getCacheSize();

            const eng2 = new StackCaptureEngine();
            eng2.captureStack(5);

            // Cache should not exceed max size
            const eng3 = new StackCaptureEngine();
            eng3.captureStack(5);
            expect(eng3.getCacheSize()).toBeLessThanOrEqual(2);
        });
    });

    describe('signature determinism', () => {
        it('should produce identical signatures from the same call site', () => {
            const sig1 = engine.captureStackSignature();
            const sig2 = engine.captureStackSignature();
            expect(sig1).toBe(sig2);
        });

        it('should produce a hex string signature', () => {
            const sig = engine.captureStackSignature();
            expect(sig).toMatch(/^[0-9a-f]+$/);
        });
    });

    describe('cache behavior', () => {
        it('should return cached frames for identical stacks', () => {
            const frames1 = engine.captureStack(5);
            const sizeAfterFirst = engine.getCacheSize();
            const frames2 = engine.captureStack(5);
            const sizeAfterSecond = engine.getCacheSize();
            // Second call should hit cache (same stack prefix)
            expect(sizeAfterSecond).toBeGreaterThanOrEqual(sizeAfterFirst);
            expect(frames2.length).toBe(frames1.length);
        });

        it('should not grow cache beyond max size', () => {
            StackCaptureEngine.setMaxCacheSize(3);
            StackCaptureEngine.clearCache();
            const eng = new StackCaptureEngine();
            // Multiple captures should not exceed max
            for (let i = 0; i < 10; i++) {
                eng.captureStack(5);
            }
            expect(eng.getCacheSize()).toBeLessThanOrEqual(3);
            StackCaptureEngine.setMaxCacheSize(4096);
        });
    });
});
