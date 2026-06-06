import {
    MemoryPoolError,
    MemoryPoolErrorCode,
    type InternalPoolMetrics,
    type PerformanceTimer,
    type PoolPerformanceMetrics,
    createInternalPoolMetrics,
    createPerformanceTimer,
} from '../pool-support';

export class PoolMetricsCollector {
    readonly #metrics: InternalPoolMetrics = createInternalPoolMetrics();
    readonly #name: string;
    readonly #enabled: boolean;

    constructor(name: string, enabled: boolean) {
        this.#name = name;
        this.#enabled = enabled;
    }

    get internal(): InternalPoolMetrics {
        return this.#metrics;
    }

    get isEnabled(): boolean {
        return this.#enabled;
    }

    newTimer(): PerformanceTimer | null {
        if (!this.#enabled) return null;
        const t = createPerformanceTimer();
        t.start();
        return t;
    }

    recordAllocation(fastPath: boolean, hit: boolean): void {
        if (!this.#enabled) return;
        this.#metrics.allocations++;
        if (fastPath) {
            this.#metrics.fastPath++;
            if (hit) this.#metrics.hits++;
        } else {
            this.#metrics.slowPath++;
            this.#metrics.misses++;
        }
    }

    recordRelease(): void {
        if (!this.#enabled) return;
        this.#metrics.releases++;
    }

    recordCreation(time: number): void {
        if (!this.#enabled) return;
        const m = this.#metrics.creationTimer;
        m.count++;
        m.total += time;
        if (time < m.min) m.min = time;
        if (time > m.max) m.max = time;
        m.last = time;
        this.#metrics.creations++;
    }

    recordAllocationTime(elapsed: number): void {
        if (!this.#enabled) return;
        const m = this.#metrics.allocationTimer;
        m.count++;
        m.total += elapsed;
        if (elapsed < m.min) m.min = elapsed;
        if (elapsed > m.max) m.max = elapsed;
        m.last = elapsed;
    }

    recordReleaseTime(elapsed: number): void {
        if (!this.#enabled) return;
        const m = this.#metrics.releaseTimer;
        m.count++;
        m.total += elapsed;
        if (elapsed < m.min) m.min = elapsed;
        if (elapsed > m.max) m.max = elapsed;
        m.last = elapsed;
    }

    recordCompactionTime(elapsed: number): void {
        if (!this.#enabled) return;
        const m = this.#metrics.compactionTimer;
        m.count++;
        m.total += elapsed;
        if (elapsed < m.min) m.min = elapsed;
        if (elapsed > m.max) m.max = elapsed;
        m.last = elapsed;
        this.#metrics.compactions++;
    }

    recordResizeTime(elapsed: number): void {
        if (!this.#enabled) return;
        const m = this.#metrics.resizeTimer;
        m.count++;
        m.total += elapsed;
        if (elapsed < m.min) m.min = elapsed;
        if (elapsed > m.max) m.max = elapsed;
        m.last = elapsed;
    }

    recordLifetime(elapsed: number): void {
        if (!this.#enabled) return;
        const m = this.#metrics.objectLifetime;
        m.count++;
        m.total += elapsed;
        if (elapsed < m.min) m.min = elapsed;
        if (elapsed > m.max) m.max = elapsed;
        m.last = elapsed;
    }

    recordValidationFailure(): void {
        if (!this.#enabled) return;
        this.#metrics.validationFailures++;
    }

    recordExpansion(): void {
        if (!this.#enabled) return;
        this.#metrics.expansions++;
    }

    recordContraction(): void {
        if (!this.#enabled) return;
        this.#metrics.contractions++;
    }

    recordEviction(): void {
        if (!this.#enabled) return;
        this.#metrics.evictions++;
    }

    recordHighWaterMark(allocated: number): void {
        if (!this.#enabled) return;
        if (allocated > this.#metrics.highWaterMark) {
            this.#metrics.highWaterMark = allocated;
        }
    }

    resetAggregate(): void {
        if (!this.#enabled) return;
        this.#metrics.creations = 0;
        this.#metrics.expansions = 0;
        this.#metrics.contractions = 0;
        this.#metrics.highWaterMark = 0;
    }

    snapshot(
        capacity: number,
        available: number,
        estimatedMemoryUsage: number,
        fragmentationRatio: number
    ): PoolPerformanceMetrics {
        if (!this.#enabled) {
            throw new MemoryPoolError(
                'Metrics are disabled for this pool',
                MemoryPoolErrorCode.INVALID_OPERATION,
                this.#name
            );
        }

        const now = Date.now();
        const timeWindow = (now - this.#metrics.startTime) / 1000;
        const allocated = capacity - available;
        this.#metrics.lastUpdateTime = now;

        const avg = (m: { count: number; total: number }) =>
            m.count > 0 ? m.total / m.count : 0;
        const min = (m: { min: number }) => (m.min === Number.MAX_VALUE ? 0 : m.min);

        return {
            name: this.#name,
            capacity,
            available,
            allocated,
            reserved: 0,
            highWaterMark: this.#metrics.highWaterMark,
            allocations: this.#metrics.allocations,
            releases: this.#metrics.releases,
            creations: this.#metrics.creations,
            evictions: this.#metrics.evictions,
            expansions: this.#metrics.expansions,
            contractions: this.#metrics.contractions,
            validationFailures: this.#metrics.validationFailures,
            fastPath: this.#metrics.fastPath,
            slowPath: this.#metrics.slowPath,
            averageAllocationTime: avg(this.#metrics.allocationTimer),
            averageReleaseTime: avg(this.#metrics.releaseTimer),
            peakMemoryUsage: estimatedMemoryUsage,
            fragmentationRatio,
            utilizationRatio: capacity > 0 ? allocated / capacity : 0,
            turnoverRate:
                this.#metrics.allocations > 0
                    ? this.#metrics.releases / this.#metrics.allocations
                    : 0,
            missRate:
                this.#metrics.hits + this.#metrics.misses > 0
                    ? this.#metrics.misses / (this.#metrics.hits + this.#metrics.misses)
                    : 0,
            hitRatio:
                this.#metrics.hits + this.#metrics.misses > 0
                    ? this.#metrics.hits / (this.#metrics.hits + this.#metrics.misses)
                    : 0,
            allocationsPerSecond: timeWindow > 0 ? this.#metrics.allocations / timeWindow : 0,
            releasesPerSecond: timeWindow > 0 ? this.#metrics.releases / timeWindow : 0,
            lastCompactionDuration: this.#metrics.compactionTimer.last,
            compactionCount: this.#metrics.compactions,
            lastResizeDuration: this.#metrics.resizeTimer.last,
            objectCreationTime: {
                min: min(this.#metrics.creationTimer),
                max: this.#metrics.creationTimer.max,
                avg: avg(this.#metrics.creationTimer),
            },
            objectLifetime: {
                min: min(this.#metrics.objectLifetime),
                max: this.#metrics.objectLifetime.max,
                avg: avg(this.#metrics.objectLifetime),
            },
        };
    }
}
