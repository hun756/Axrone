export interface MemorySampleEvent {
  readonly timestampMs: number;
  readonly usedHeapSizeBytes: bigint;
  readonly heapSizeChangeSinceLastSample?: bigint;
  readonly gcEventsCount?: number;
}

export class MemoryTracker implements AsyncDisposable {
  private lastUsedHeapSize = 0;
  private sampleIntervalMs = 50;
  private isTracking = false;
  private timerId?: ReturnType<typeof setTimeout>;
  private readonly onMemoryChange: ((sample: MemorySampleEvent) => void) | undefined;

  constructor(options?: { readonly onMemoryChange?: (sample: MemorySampleEvent) => void }) {
    this.onMemoryChange = options?.onMemoryChange;
  }

  startTracking(options?: { readonly intervalMs?: number }): void {
    if (this.isTracking) return;
    this.isTracking = true;
    const sampleFn = () => this.sampleUsage();
    this.timerId = setTimeout(sampleFn, options?.intervalMs ?? this.sampleIntervalMs);
  }

  stopTracking(): void {
    clearTimeout(this.timerId!);
    this.isTracking = false;
  }

  private sampleUsage(): void {
    if (!this.isTracking) return;
    const currentHeapSize = typeof performance !== 'undefined'
      ? ((performance as unknown as Record<string, unknown>).memory as Record<string, number> | undefined)?.usedJSHeapSize ?? 0
      : 0;
    let change: bigint | undefined;
    if (currentHeapSize > this.lastUsedHeapSize) {
      change = BigInt(currentHeapSize - this.lastUsedHeapSize);
    }

    const sample: MemorySampleEvent = {
      timestampMs: Date.now(),
      usedHeapSizeBytes: BigInt(currentHeapSize),
      heapSizeChangeSinceLastSample: change,
    };

    this.onMemoryChange?.(sample);

    this.lastUsedHeapSize = currentHeapSize;
  }

  [Symbol.asyncDispose](): Promise<void> {
    this.stopTracking();
    return Promise.resolve();
  }
}
