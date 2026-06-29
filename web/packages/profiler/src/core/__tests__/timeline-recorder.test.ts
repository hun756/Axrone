import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { TimelineRecorder } from '../timeline-recorder';
import { ProfilerTickError } from '../../errors';

describe('TimelineRecorder', () => {
  let recorder: TimelineRecorder;

  afterEach(async () => {
    if (recorder) await recorder[Symbol.asyncDispose]();
  });

  describe('construction', () => {
    it('should create with default options', () => {
      recorder = new TimelineRecorder();
      expect(recorder.getEvents()).toHaveLength(0);
    });

    it('should accept onTick callback', () => {
      const cb = vi.fn();
      recorder = new TimelineRecorder({ onTick: cb });
      recorder.tick();
      expect(cb).toHaveBeenCalledTimes(1);
    });
  });

  describe('tick()', () => {
    it('should create a cpu tick event', () => {
      recorder = new TimelineRecorder();
      recorder.tick();
      const events = recorder.getEvents();
      expect(events).toHaveLength(1);
      expect(events[0].category).toBe('cpu');
      expect(events[0].timestampMs).toBeGreaterThan(0);
    });

    it('should throw when exceeding max events', () => {
      recorder = new TimelineRecorder();
      const maxEvents = 1_000_000;
      for (let i = 0; i < maxEvents; i++) {
        recorder.tick();
      }
      expect(() => recorder.tick()).toThrow(ProfilerTickError);
    });
  });

  describe('recordEvent()', () => {
    it('should manually record an event', () => {
      recorder = new TimelineRecorder();
      recorder.recordEvent({
        timestampMs: 1000,
        category: 'memory',
        payload: { bytes: 1234 },
      });
      const events = recorder.getEvents();
      expect(events).toHaveLength(1);
      expect(events[0].category).toBe('memory');
      expect(events[0].payload.bytes).toBe(1234);
    });

    it('should invoke onTick callback when recording', () => {
      const cb = vi.fn();
      recorder = new TimelineRecorder({ onTick: cb });
      recorder.recordEvent({
        timestampMs: 1000,
        category: 'gc',
        payload: {},
      });
      expect(cb).toHaveBeenCalledTimes(1);
    });

    it('should silently discard when at max capacity', () => {
      recorder = new TimelineRecorder();
      for (let i = 0; i < 1_000_000; i++) {
        recorder.recordEvent({ timestampMs: i, category: 'cpu', payload: {} });
      }
      recorder.recordEvent({ timestampMs: 999999, category: 'cpu', payload: {} });
      expect(recorder.getEvents()).toHaveLength(1_000_000);
    });

    it('should not record after dispose', async () => {
      recorder = new TimelineRecorder();
      await recorder[Symbol.asyncDispose]();
      recorder.recordEvent({ timestampMs: 1000, category: 'cpu', payload: {} });
      expect(recorder.getEvents()).toHaveLength(0);
    });
  });

  describe('start() and stop()', () => {
    it('should start periodic ticking', () => {
      vi.useFakeTimers();
      recorder = new TimelineRecorder();
      recorder.start();
      vi.advanceTimersByTime(100);
      recorder.stop();
      expect(recorder.getEvents().length).toBeGreaterThanOrEqual(1);
      vi.useRealTimers();
    });

    it('should stop periodic ticking', () => {
      vi.useFakeTimers();
      recorder = new TimelineRecorder();
      recorder.start();
      vi.advanceTimersByTime(50);
      recorder.stop();
      const countAfterStop = recorder.getEvents().length;
      vi.advanceTimersByTime(100);
      expect(recorder.getEvents().length).toBe(countAfterStop);
      vi.useRealTimers();
    });

    it('should be idempotent on repeated start', () => {
      recorder = new TimelineRecorder();
      recorder.start();
      recorder.start();
      recorder.stop();
    });

    it('should be safe to stop when not started', () => {
      recorder = new TimelineRecorder();
      recorder.stop();
    });
  });

  describe('getEvents()', () => {
    it('should return a copy of the events array', () => {
      recorder = new TimelineRecorder();
      recorder.tick();
      const events1 = recorder.getEvents();
      const events2 = recorder.getEvents();
      expect(events1).toEqual(events2);
      events1.push({ timestampMs: 0, category: 'cpu', payload: {} });
      expect(recorder.getEvents()).toHaveLength(1);
    });
  });

  describe('async dispose', () => {
    it('should stop recording on dispose', async () => {
      vi.useFakeTimers();
      recorder = new TimelineRecorder();
      recorder.start();
      await recorder[Symbol.asyncDispose]();
      const count = recorder.getEvents().length;
      vi.advanceTimersByTime(200);
      expect(recorder.getEvents().length).toBe(count);
      vi.useRealTimers();
    });
  });
});
