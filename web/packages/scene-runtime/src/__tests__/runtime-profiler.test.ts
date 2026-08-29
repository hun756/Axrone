import { afterEach, describe, expect, it, vi } from 'vitest';
import {
    SCENE_RUNTIME_PROFILER_PHASES,
    SceneRuntimeProfiler,
    type SceneRuntimeFrameRecord,
} from '../runtime-profiler';

const createPhaseMsExpectation = () => ({
    preUpdate: 0,
    fixedUpdate: 0,
    update: 0,
    postUpdate: 0,
    render: 0,
});

describe('SceneRuntimeProfiler', () => {
    afterEach(() => {
        vi.restoreAllMocks();
    });

    it('records no frames while disabled', () => {
        const profiler = new SceneRuntimeProfiler();
        try {
            expect(profiler.isEnabled).toBe(false);
            profiler.beginFrame(1, 0, 16);
            profiler.endFrame(16, 1);
            expect(profiler.getRecords()).toHaveLength(0);
            expect(profiler.getSummary()).toBeNull();
        } finally {
            profiler.dispose();
        }
    });

    it('executes timed actions without recording when disabled', () => {
        const profiler = new SceneRuntimeProfiler();
        try {
            const action = vi.fn(() => 42);
            expect(profiler.timePhase('update', action)).toBe(42);
            expect(action).toHaveBeenCalledTimes(1);
        } finally {
            profiler.dispose();
        }
    });

    it('records frame timing with phase accumulation', () => {
        vi.spyOn(performance, 'now')
            .mockReturnValueOnce(100) // first timePhase start
            .mockReturnValueOnce(104) // first timePhase end
            .mockReturnValueOnce(104) // second timePhase start
            .mockReturnValueOnce(110); // second timePhase end

        const profiler = new SceneRuntimeProfiler({ enabled: true });
        try {
            profiler.beginFrame(7, 0, 16.6);
            profiler.timePhase('update', () => undefined);
            profiler.timePhase('update', () => undefined);
            profiler.attachRenderStats({ drawCalls: 12, trianglesSubmitted: 900 });
            profiler.attachPhysicsStats({ stepMs: 2, collisionMs: 1, solveMs: 0.5 });
            profiler.endFrame(16.6, 1);

            const records = profiler.getRecords();
            expect(records).toHaveLength(1);
            const record = records[0]!;
            expect(record.frame).toBe(7);
            expect(record.frameTimeMs).toBeCloseTo(16.6, 5);
            expect(record.fps).toBeCloseTo(1000 / 16.6, 3);
            expect(record.deltaMs).toBeCloseTo(16.6, 5);
            expect(record.fixedSteps).toBe(1);
            expect(record.phaseMs.update).toBeCloseTo(10, 5);
            expect(record.render).toEqual({ drawCalls: 12, trianglesSubmitted: 900 });
            expect(record.physics).toEqual({ stepMs: 2, collisionMs: 1, solveMs: 0.5 });
        } finally {
            profiler.dispose();
        }
    });

    it('resets phase accumulators and pending stats between frames', () => {
        const profiler = new SceneRuntimeProfiler({ enabled: true });
        try {
            profiler.beginFrame(1, 0, 16);
            profiler.attachRenderStats({ drawCalls: 3, trianglesSubmitted: 100 });
            profiler.endFrame(16, 0);

            profiler.beginFrame(2, 16, 16);
            profiler.endFrame(32, 0);

            const records = profiler.getRecords();
            expect(records).toHaveLength(2);
            expect(records[1]!.render).toBeNull();
            expect(records[1]!.physics).toBeNull();
            expect(records[1]!.phaseMs).toEqual(createPhaseMsExpectation());
        } finally {
            profiler.dispose();
        }
    });

    it('caps the record buffer at the configured capacity', () => {
        const profiler = new SceneRuntimeProfiler({ enabled: true, capacity: 3 });
        try {
            for (let frame = 1; frame <= 5; frame += 1) {
                profiler.beginFrame(frame, frame * 16, 16);
                profiler.endFrame(frame * 16 + 8, 0);
            }
            const records = profiler.getRecords();
            expect(records).toHaveLength(3);
            expect(records.map((record) => record.frame)).toEqual([3, 4, 5]);
        } finally {
            profiler.dispose();
        }
    });

    it('notifies subscribers on frame commit and supports unsubscribe', () => {
        const profiler = new SceneRuntimeProfiler({ enabled: true });
        try {
            const received: SceneRuntimeFrameRecord[] = [];
            const unsubscribe = profiler.subscribe((record) => {
                received.push(record);
            });

            profiler.beginFrame(1, 0, 16);
            profiler.endFrame(16, 0);
            expect(received).toHaveLength(1);
            expect(received[0]!.frame).toBe(1);

            unsubscribe();
            profiler.beginFrame(2, 16, 16);
            profiler.endFrame(32, 0);
            expect(received).toHaveLength(1);
        } finally {
            profiler.dispose();
        }
    });

    it('summarizes frame statistics over the buffer window', () => {
        const profiler = new SceneRuntimeProfiler({ enabled: true });
        try {
            const frameBegins = [0, 10, 30, 60];
            frameBegins.forEach((begin, index) => {
                profiler.beginFrame(index + 1, begin, 16);
                profiler.endFrame(begin + 5, 0);
            });

            const summary = profiler.getSummary();
            expect(summary).not.toBeNull();
            expect(summary!.frameCount).toBe(4);
            expect(summary!.minFrameTimeMs).toBeCloseTo(5, 5);
            expect(summary!.maxFrameTimeMs).toBeCloseTo(30, 5);
            expect(summary!.avgFrameTimeMs).toBeCloseTo((5 + 10 + 20 + 30) / 4, 5);
            expect(summary!.p95FrameTimeMs).toBeCloseTo(30, 5);
            for (const phase of SCENE_RUNTIME_PROFILER_PHASES) {
                expect(summary!.avgPhaseMs[phase]).toBe(0);
            }

            const capped = profiler.getSummary(2);
            expect(capped!.frameCount).toBe(2);
            expect(capped!.minFrameTimeMs).toBeCloseTo(20, 5);
        } finally {
            profiler.dispose();
        }
    });

    it('measures frame duration wall-to-wall between frame begins', () => {
        const profiler = new SceneRuntimeProfiler({ enabled: true });
        try {
            profiler.beginFrame(1, 0, 16);
            profiler.endFrame(10, 0);
            profiler.beginFrame(2, 100, 16);
            profiler.endFrame(105, 0);

            const records = profiler.getRecords();
            expect(records[0]!.frameTimeMs).toBeCloseTo(10, 5);
            expect(records[1]!.frameTimeMs).toBeCloseTo(100, 5);
            expect(records[1]!.fps).toBeCloseTo(10, 5);
        } finally {
            profiler.dispose();
        }
    });

    it('captures a stack sample at phase entry while recording', () => {
        const profiler = new SceneRuntimeProfiler({ enabled: true });
        try {
            profiler.beginFrame(1, 0, 16);
            profiler.capturePhaseSample('update');
            profiler.endFrame(16, 0);

            const samples = profiler.getStackSamples();
            expect(samples).toHaveLength(1);
            expect(samples[0]!.phase).toBe('update');
            expect(samples[0]!.frames.length).toBeGreaterThan(0);
            expect(typeof samples[0]!.frames[0]!.fn).toBe('string');

            profiler.clearStackSamples();
            expect(profiler.getStackSamples()).toHaveLength(0);
        } finally {
            profiler.dispose();
        }
    });

    it('captures no stack samples while disabled or outside a frame', () => {
        const profiler = new SceneRuntimeProfiler();
        try {
            profiler.capturePhaseSample('update');
            expect(profiler.getStackSamples()).toHaveLength(0);

            profiler.enable();
            profiler.capturePhaseSample('update');
            expect(profiler.getStackSamples()).toHaveLength(0);
        } finally {
            profiler.dispose();
        }
    });

    it('ignores attachments outside an open frame', () => {
        const profiler = new SceneRuntimeProfiler({ enabled: true });
        try {
            profiler.attachRenderStats({ drawCalls: 9, trianglesSubmitted: 9 });
            profiler.beginFrame(1, 0, 16);
            profiler.endFrame(16, 0);
            expect(profiler.getRecords()[0]!.render).toBeNull();
        } finally {
            profiler.dispose();
        }
    });

    it('stops recording after dispose and clears state', () => {
        const profiler = new SceneRuntimeProfiler({ enabled: true });
        profiler.beginFrame(1, 0, 16);
        profiler.endFrame(16, 0);
        profiler.dispose();
        expect(profiler.isDisposed).toBe(true);
        expect(profiler.getRecords()).toHaveLength(0);
        profiler.beginFrame(2, 16, 16);
        profiler.endFrame(32, 0);
        expect(profiler.getRecords()).toHaveLength(0);
    });
});
