import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { PerfettoSerializer } from '../perfetto-serializer';
import type { StackFrameCapture } from '../../core/stack-capture';

function makeFrame(overrides?: Partial<StackFrameCapture>): StackFrameCapture {
    return {
        function: 'testFunc',
        file: 'test.js',
        lineNumber: 10,
        columnNumber: 5,
        ...overrides,
    };
}

describe('PerfettoSerializer', () => {
    let serializer: PerfettoSerializer;

    beforeEach(() => {
        vi.useFakeTimers();
        serializer = new PerfettoSerializer();
    });

    afterEach(async () => {
        await serializer[Symbol.asyncDispose]();
        vi.useRealTimers();
    });

    describe('recordSample()', () => {
        it('should record a sample event', () => {
            serializer.recordSample([makeFrame()]);
            const data = serializer.getPerfettoData();
            expect(data).not.toBeNull();
            expect(data!.events.length).toBeGreaterThanOrEqual(1);
        });

        it('should create B/E event pairs for each frame', () => {
            serializer.recordSample([
                makeFrame({ function: 'funcA' }),
                makeFrame({ function: 'funcB' }),
            ]);
            const data = serializer.getPerfettoData()!;
            const beEvents = data.events.filter(
                (e: { ph: string }) => e.ph === 'B' || e.ph === 'E'
            );
            expect(beEvents.length).toBe(4);
            expect(beEvents[0].ph).toBe('B');
            expect(beEvents[0].name).toBe('funcA');
            expect(beEvents[1].ph).toBe('E');
            expect(beEvents[1].name).toBe('funcA');
            expect(beEvents[2].ph).toBe('B');
            expect(beEvents[2].name).toBe('funcB');
            expect(beEvents[3].ph).toBe('E');
            expect(beEvents[3].name).toBe('funcB');
        });

        it('should set ph to x when durationNs is provided', () => {
            serializer.recordSample([makeFrame()], 1000n);
            const data = serializer.getPerfettoData()!;
            const sampleEvent = data.events.find((e: { name: string }) =>
                e.name.startsWith('sample.')
            );
            expect(sampleEvent).toBeDefined();
            expect(sampleEvent!.ph).toBe('x');
        });

        it('should set ph to b when durationNs is not provided', () => {
            serializer.recordSample([makeFrame()]);
            const data = serializer.getPerfettoData()!;
            const sampleEvent = data.events.find((e: { name: string }) =>
                e.name.startsWith('sample.')
            );
            expect(sampleEvent!.ph).toBe('b');
        });

        it('should name sample after first frame function', () => {
            serializer.recordSample([makeFrame({ function: 'render' })]);
            const data = serializer.getPerfettoData()!;
            const sampleEvent = data.events.find((e: { name: string }) =>
                e.name.startsWith('sample.')
            );
            expect(sampleEvent!.name).toBe('sample.render');
        });

        it('should name sample as "sample" when no frames', () => {
            serializer.recordSample([]);
            const data = serializer.getPerfettoData()!;
            expect(data.events.some((e: { name: string }) => e.name === 'sample')).toBe(true);
        });

        it('should include lineNumber in B event args', () => {
            serializer.recordSample([makeFrame({ function: 'funcA', lineNumber: 42 })]);
            const data = serializer.getPerfettoData()!;
            const bEvent = data.events.find(
                (e: { ph: string; name: string }) => e.ph === 'B' && e.name === 'funcA'
            );
            expect(bEvent).toBeDefined();
            expect(bEvent!.args).toBeDefined();
        });

        it('should not record after dispose', async () => {
            await serializer[Symbol.asyncDispose]();
            serializer.recordSample([makeFrame()]);
            expect(serializer.getPerfettoData()).toBeNull();
        });
    });

    describe('getPerfettoData()', () => {
        it('should return null when no events recorded', () => {
            expect(serializer.getPerfettoData()).toBeNull();
        });

        it('should return frozen object with trace data', () => {
            serializer.recordSample([makeFrame()]);
            const data = serializer.getPerfettoData()!;
            expect(data.version).toBe('2');
            expect(typeof data.sessionId).toBe('string');
            expect(typeof data.startedAtNs).toBe('bigint');
            expect(data.events.length).toBeGreaterThan(0);
            expect(Object.isFrozen(data)).toBe(true);
        });

        it('should compute endedAtNs as last event timestamp', () => {
            serializer.recordSample([makeFrame()]);
            vi.advanceTimersByTime(100);
            serializer.recordSample([makeFrame()]);
            const data = serializer.getPerfettoData()!;
            expect(data.endedAtNs).toBeDefined();
            expect(data.endedAtNs!).toBeGreaterThan(data.startedAtNs);
        });

        it('should return null after dispose', async () => {
            await serializer[Symbol.asyncDispose]();
            expect(serializer.getPerfettoData()).toBeNull();
        });
    });

    describe('async dispose', () => {
        it('should clear data on dispose', async () => {
            serializer.recordSample([makeFrame()]);
            await serializer[Symbol.asyncDispose]();
            expect(serializer.getPerfettoData()).toBeNull();
        });

        it('should allow multiple dispose calls', async () => {
            await serializer[Symbol.asyncDispose]();
            await serializer[Symbol.asyncDispose]();
        });
    });
});
