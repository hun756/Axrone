interface SamplerState {
  intervalMs: number;
  tickCounter: number;
  accumulatedTimeNs: bigint;
  lastTickTimestampNs: bigint | null;
  minIntervalMs: number;
  maxIntervalMs: number;
  targetFrameBudgetMs: number;
  adaptationEnabled: boolean;
}

export class ContinuousSampler implements AsyncDisposable {
  private readonly state: SamplerState;
  private timerId?: ReturnType<typeof setTimeout>;
  private readonly onTick: (timestampNs: bigint) => void;
  private isRunning = false;
  private lastDurationMs = 0;

  constructor(options: {
    readonly intervalMs?: number;
    readonly onTick: (timestampNs: bigint) => void;
    readonly minIntervalMs?: number;
    readonly maxIntervalMs?: number;
    readonly targetFrameBudgetMs?: number;
    readonly adaptSampling?: boolean;
  }) {
    this.state = {
      intervalMs: options.intervalMs ?? 10,
      tickCounter: 0,
      accumulatedTimeNs: 0n,
      lastTickTimestampNs: null,
      minIntervalMs: options.minIntervalMs ?? 1,
      maxIntervalMs: options.maxIntervalMs ?? 100,
      targetFrameBudgetMs: options.targetFrameBudgetMs ?? 16,
      adaptationEnabled: options.adaptSampling ?? true,
    };
    this.onTick = options.onTick;
  }

  start(): void {
    if (this.isRunning) return;
    this.isRunning = true;
    const startTimestamp = BigInt(Math.floor(performance.now())) * 1_000_000n;
    const tickFn = () => {
      if (!this.isRunning) {
        clearTimeout(this.timerId!);
        return;
      }
      const tickStart = performance.now();
      const nowNs = BigInt(Math.floor(performance.now())) * 1_000_000n;
      if (this.state.lastTickTimestampNs === null) {
        this.state.lastTickTimestampNs = startTimestamp;
      }
      this.state.tickCounter++;
      this.state.accumulatedTimeNs += nowNs - this.state.lastTickTimestampNs;
      this.state.lastTickTimestampNs = nowNs;
      this.onTick(nowNs);
      const tickEnd = performance.now();
      this.lastDurationMs = tickEnd - tickStart;

      let nextInterval = this.state.intervalMs;
      if (this.state.adaptationEnabled) {
        nextInterval = this.adaptInterval(this.lastDurationMs);
      }
      this.timerId = setTimeout(tickFn, Math.max(nextInterval, 1));
    };
    tickFn();
  }

  private adaptInterval(tickDurationMs: number): number {
    const budget = this.state.targetFrameBudgetMs;
    const overhead = tickDurationMs / budget;
    if (overhead > 0.1) {
      return Math.min(this.state.intervalMs * 2, this.state.maxIntervalMs);
    }
    if (overhead < 0.01 && this.state.intervalMs > this.state.minIntervalMs) {
      return Math.max(this.state.intervalMs * 0.5, this.state.minIntervalMs);
    }
    return this.state.intervalMs;
  }

  stop(): void {
    if (!this.isRunning) return;
    clearTimeout(this.timerId!);
    this.isRunning = false;
  }

  setInterval(ms: number): void {
    this.state.intervalMs = Math.max(1, ms);
  }

  setAdaptation(enabled: boolean): void {
    this.state.adaptationEnabled = enabled;
  }

  setTargetFrameBudget(ms: number): void {
    this.state.targetFrameBudgetMs = Math.max(1, ms);
  }

  getTickCount(): number {
    return this.state.tickCounter;
  }

  getLastDurationMs(): number {
    return this.lastDurationMs;
  }

  getState(): Readonly<SamplerState> {
    return Object.freeze({ ...this.state });
  }

  [Symbol.asyncDispose](): Promise<void> {
    this.stop();
    return Promise.resolve();
  }
}
