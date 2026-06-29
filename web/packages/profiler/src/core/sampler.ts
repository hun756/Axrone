interface SamplerState {
  intervalMs: number;
  tickCounter: number;
  accumulatedTimeNs: bigint;
  lastTickTimestampNs: bigint | null;
}

export class ContinuousSampler implements AsyncDisposable {
  private readonly state: SamplerState;
  private timerId?: ReturnType<typeof setTimeout>;
  private readonly onTick: (timestampNs: bigint) => void;
  private isRunning = false;

  constructor(options: { readonly intervalMs: number; readonly onTick: (timestampNs: bigint) => void }) {
    this.state = {
      intervalMs: options.intervalMs ?? 10,
      tickCounter: 0,
      accumulatedTimeNs: 0n,
      lastTickTimestampNs: null,
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
      const nowNs = BigInt(Math.floor(performance.now())) * 1_000_000n;
      if (this.state.lastTickTimestampNs === null) {
        this.state.lastTickTimestampNs = startTimestamp;
      }
      this.state.tickCounter++;
      this.state.accumulatedTimeNs += nowNs - this.state.lastTickTimestampNs;
      this.state.lastTickTimestampNs = nowNs;
      this.onTick(nowNs);
      this.timerId = setTimeout(tickFn, Math.max(this.state.intervalMs, 1));
    };
    tickFn();
  }

  stop(): void {
    if (!this.isRunning) return;
    clearTimeout(this.timerId!);
    this.isRunning = false;
  }

  getTickCount(): number {
    return this.state.tickCounter;
  }

  [Symbol.asyncDispose](): Promise<void> {
    this.stop();
    return Promise.resolve();
  }
}
