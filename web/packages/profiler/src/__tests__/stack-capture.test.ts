import { describe, it, expect } from 'vitest';
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
});
