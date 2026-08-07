import type { StackFrameCapture } from '../core/stack-capture';

export interface PerfettoTraceData {
    readonly version: string;
    readonly sessionId: string;
    readonly startedAtNs: bigint;
    readonly endedAtNs?: bigint;
    readonly events: PerfettoEvent[];
}

export interface PerfettoEvent {
    readonly timestampNs: bigint;
    readonly name: string;
    readonly type: string;
    readonly ph: 'B' | 'b' | 'E' | 'x' | 'i';
    readonly args?: Record<string, unknown>;
    readonly cat?: string;
}

export class PerfettoSerializer implements AsyncDisposable {
    private readonly events: PerfettoEvent[] = [];
    private disposed = false;

    recordSample(stackFrames: StackFrameCapture[], durationNs?: bigint): void {
        if (this.disposed) return;
        const ph: 'x' | 'b' = durationNs !== undefined ? 'x' : 'b';
        const nowNs = BigInt(Math.floor(performance.now() * 1_000_000));
        this.events.push({
            timestampNs: nowNs,
            name: stackFrames.length > 0 ? `sample.${stackFrames[0].function}` : 'sample',
            type: 'process',
            ph,
            args: { stackFrames },
        });

        for (const frame of stackFrames) {
            this.events.push({
                timestampNs: nowNs,
                name: frame.function,
                type: 'function_call',
                ph: 'B',
                args: { lineNumber: frame.lineNumber },
            });
            this.events.push({
                timestampNs: nowNs,
                name: frame.function,
                type: 'function_call',
                ph: 'E',
                args: {},
            });
        }
    }

    getPerfettoData(): PerfettoTraceData | null {
        if (this.disposed || this.events.length === 0) return null;
        const startedAt = this.events[0].timestampNs;
        let endedAt: bigint | undefined;
        for (const event of this.events) {
            if (event.timestampNs > startedAt) endedAt = event.timestampNs;
        }

        return Object.freeze({
            version: '2',
            sessionId: String(Date.now()),
            startedAtNs: startedAt,
            endedAtNs: endedAt,
            events: this.events,
        });
    }

    [Symbol.asyncDispose](): Promise<void> {
        this.disposed = true;
        return Promise.resolve();
    }
}
