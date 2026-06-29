export interface HistogramBucketEntry {
  readonly boundaries: number[];
  readonly countsPerBucket: bigint[];
}

export class LogBoundedHistogram {
  private readonly bucketBoundaries: readonly number[];
  private readonly maxBuckets = 256;
  private readonly countsPerBucket: bigint[] = [];
  private readonly sums: bigint[] = [];
  private minValueInternal = Infinity;
  private maxValueInternal = -Infinity;

  constructor(options?: { readonly minMs?: number; readonly maxMs?: number }) {
    const min = options?.minMs ?? 0;
    const max = options?.maxMs ?? 10_000;
    this.bucketBoundaries = this.generateBucketBoundaries(min, max);
    if (this.bucketBoundaries.length > this.maxBuckets) {
      throw new Error('Histogram bucket count exceeds maximum');
    }
  }

  private generateBucketBoundaries(min: number, max: number): readonly number[] {
    const boundaries: number[] = [];
    let current = min;
    while (boundaries.length < this.maxBuckets && current <= max) {
      boundaries.push(current);
      current *= 2;
    }
    return Object.freeze(boundaries.slice(0, Math.min(this.maxBuckets, boundaries.length)));
  }

  record(durationMs: number): void {
    if (durationMs <= 0) return;
    const bucketIndex = this.findBucket(durationMs);
    while (this.countsPerBucket.length <= bucketIndex) {
      this.countsPerBucket.push(0n);
      this.sums.push(0n);
    }
    this.countsPerBucket[bucketIndex] += 1n;
    this.sums[bucketIndex] += BigInt(durationMs);
    if (durationMs < this.minValueInternal) this.minValueInternal = durationMs;
    if (durationMs > this.maxValueInternal) this.maxValueInternal = durationMs;
  }

  private findBucket(value: number): number {
    for (let i = 0; i < this.bucketBoundaries.length - 1; i++) {
      if (value >= this.bucketBoundaries[i] && (i + 1 >= this.bucketBoundaries.length || value < this.bucketBoundaries[i + 1])) {
        return i;
      }
    }
    return Math.max(0, this.bucketBoundaries.length - 1);
  }

  getStatistics(): Record<string, unknown> {
    const total = this.sums.reduce((acc, val) => acc + val, 0n);
    if (total === 0n) return { totalDurationMs: 0n, histogramBuckets: [] };

    const mean = this.sums.length > 0
      ? Number(this.sums.reduce((acc, val) => acc + val, 0n) / BigInt(this.sums.length))
      : 0;

    const sorted: Array<{ value: number; cumulativeCount: bigint }> = [];
    let cumulative = 0n;
    for (let i = 0; i < this.countsPerBucket.length; i++) {
      if (this.countsPerBucket[i] > 0n) {
        sorted.push({ value: this.bucketBoundaries[i] ?? 0, cumulativeCount: cumulative + this.countsPerBucket[i] });
        cumulative += this.countsPerBucket[i];
      }
    }

    const percentiles = [50, 75, 90, 95, 99];
    const result: Record<string, unknown> = {
      totalDurationMs: total,
      histogramBuckets: this.bucketBoundaries.slice(),
      countsPerBucket: this.countsPerBucket.slice(),
      means: this.sums.map((v, i) => Number(v / BigInt(this.countsPerBucket[i] ?? 1))),
      meanMs: mean,
      minMs: this.minValueInternal,
      maxMs: this.maxValueInternal,
    };

    for (const percentile of percentiles) {
      result[`p${percentile}Ms`] = this.computePercentile(sorted, cumulative, percentile);
    }

    return Object.freeze(result);
  }

  private computePercentile(sorted: Array<{ value: number; cumulativeCount: bigint }>, total: bigint, p: number): number | undefined {
    if (sorted.length === 0) return undefined;
    const index = Math.floor((p / 100) * Number(total));
    let cumulative = 0n;
    for (const entry of sorted) {
      cumulative += entry.cumulativeCount;
      if (cumulative >= BigInt(index)) return entry.value;
    }
    return sorted[sorted.length - 1].value;
  }

  minValue(): number {
    return this.minValueInternal;
  }

  maxValue(): number {
    return this.maxValueInternal;
  }

  [Symbol.dispose](): void {
    this.countsPerBucket.length = 0;
    this.sums.length = 0;
  }
}
