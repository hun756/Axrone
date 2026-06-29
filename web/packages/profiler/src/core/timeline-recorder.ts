import { ProfilerTickError } from '../errors';

export interface TimelineRecordEvent {
  readonly timestampMs: number;
  readonly category: 'cpu' | 'memory' | 'gc' | 'allocation';
  readonly payload: Record<string, unknown>;
}

export class TimelineRecorder implements AsyncDisposable {
  private readonly events: TimelineRecordEvent[] = [];
  private readonly maxEvents = 1_000_000;
  private isRecording = false;
  private disposed = false;
  private timerId?: ReturnType<typeof setInterval>;
  private readonly onTick: ((event: TimelineRecordEvent) => void) | undefined;

  constructor(options?: { readonly onTick?: (event: TimelineRecordEvent) => void }) {
    this.onTick = options?.onTick;
  }

  start(): void {
    if (this.isRecording || this.disposed) return;
    this.isRecording = true;
    const tickFn = () => {
      if (!this.isRecording) {
        clearInterval(this.timerId!);
        return;
      }
      try {
        this.tick();
      } catch (error) {
        if (!(error instanceof ProfilerTickError)) throw error;
      }
    };
    this.timerId = setInterval(tickFn, 50);
  }

  stop(): void {
    if (!this.isRecording) return;
    clearInterval(this.timerId!);
    this.isRecording = false;
  }

  tick(): void {
    if (this.events.length >= this.maxEvents) {
      throw new ProfilerTickError('Timeline recorder exceeded maximum events');
    }
    const event: TimelineRecordEvent = {
      timestampMs: Date.now(),
      category: 'cpu',
      payload: { stackFrameCount: 0 },
    };
    this.events.push(event);
    this.onTick?.(event);
  }

  getEvents(): readonly TimelineRecordEvent[] {
    return [...this.events];
  }

  [Symbol.asyncDispose](): Promise<void> {
    this.stop();
    return Promise.resolve();
  }
}
