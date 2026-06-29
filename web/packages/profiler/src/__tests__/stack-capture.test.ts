import { describe, it, expect } from 'vitest';
import { StackCaptureEngine } from '../core/stack-capture';

describe('StackCaptureEngine', () => {
  let engine: StackCaptureEngine;

  beforeEach(() => {
    StackCaptureEngine.clearCache();
    engine = new StackCaptureEngine();
  });

  it('should capture a non-empty stack trace', () => {
    const frames = engine.captureStack(10);
    expect(frames.length).toBeGreaterThan(0);
    expect(frames[0].function).toBeTruthy();
  });

  it('should respect max depth', () => {
    const frames = engine.captureStack(3);
    expect(frames.length).toBeLessThanOrEqual(3);
  });

  it('should cache stack traces by key', () => {
    const frames1 = engine.captureStack(10);
    const frames2 = engine.captureStack(10);
    expect(frames2.length).toBe(frames1.length);
  });

  it('should produce stack signatures', () => {
    const sig = engine.captureStackSignature();
    expect(typeof sig).toBe('string');
    expect(sig.length).toBeGreaterThan(0);
  });

  it('should clear the cache when requested', () => {
    engine.captureStack(10);
    expect(engine.getCacheSize()).toBeGreaterThan(0);
    StackCaptureEngine.clearCache();
    expect(engine.getCacheSize()).toBe(0);
  });
});
