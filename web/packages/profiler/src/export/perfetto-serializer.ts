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
  readonly ph: 'b' | 'x' | 'i';
  readonly args?: Record<string, unknown>;
  readonly cat?: string;
}

export class PerfettoSerializer implements AsyncDisposable {
  private readonly events: PerfettoEvent[] = [];
  private disposed = false;

  recordSample(stackFrames: StackFrameCapture[], durationNs?: bigint): void {
    if (this.disposed) return;
    const ph: 'x' | 'b' = durationNs !== undefined ? 'x' : 'b';
    const nowNs = BigInt(Date.now()) * 1_000_000n;
    this.events.push({
      timestampNs: nowNs,
      name: stackFrames.length > 0 ? `sample.${stackFrames[0].function}` : 'sample',
      type: 'process',
      ph,
      args: { stackFrames },
    });

    for (const frame of stackFrames) {
      this.events.push({
        timestampNs: BigInt(Date.now()) * 1_000_000n,
        name: frame.function,
        type: 'function_call_start',
        ph: 'b',
        args: { lineNumber: frame.lineNumber },
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
      sessionId: '',
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
