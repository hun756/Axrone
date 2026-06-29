export interface MemorySampleEvent {
    readonly timestampMs: number;
    readonly usedHeapSizeBytes: bigint;
    readonly heapSizeChangeSinceLastSample?: bigint;
    readonly gcEventsCount?: number;
    readonly availableHeapSizeBytes?: bigint;
    readonly source: 'chromium' | 'nodejs' | 'performance-api' | 'unknown';
}

interface MemoryProvider {
    readonly name: string;
    getHeapUsage(): bigint;
    isAvailable(): boolean;
}

class ChromiumMemoryProvider implements MemoryProvider {
    readonly name = 'chromium';
    isAvailable(): boolean {
        return (
            typeof performance !== 'undefined' &&
            performance !== null &&
            typeof (performance as unknown as Record<string, unknown>).memory === 'object' &&
            (performance as unknown as Record<string, unknown>).memory !== null
        );
    }
    getHeapUsage(): bigint {
        const mem = (performance as unknown as Record<string, unknown>).memory as
            | Record<string, number>
            | undefined;
        return BigInt(mem?.usedJSHeapSize ?? 0);
    }
}

class NodeMemoryProvider implements MemoryProvider {
    readonly name = 'nodejs';
    isAvailable(): boolean {
        return (
            typeof process !== 'undefined' &&
            process !== null &&
            typeof process.memoryUsage === 'function'
        );
    }
    getHeapUsage(): bigint {
        return BigInt(process.memoryUsage().heapUsed);
    }
}

class PerformanceAPIMemoryProvider implements MemoryProvider {
    readonly name = 'performance-api';
    isAvailable(): boolean {
        if (typeof performance === 'undefined' || performance === null) return false;
        const perf = performance as unknown as Record<string, unknown>;
        return typeof perf.measureUserAgentSpecificMemory === 'function';
    }
    getHeapUsage(): bigint {
        return 0n;
    }
}

class NullMemoryProvider implements MemoryProvider {
    readonly name = 'unknown';
    isAvailable(): boolean {
        return true;
    }
    getHeapUsage(): bigint {
        return 0n;
    }
}

function createMemoryProvider(): MemoryProvider {
    const providers: MemoryProvider[] = [
        new ChromiumMemoryProvider(),
        new NodeMemoryProvider(),
        new PerformanceAPIMemoryProvider(),
    ];
    for (const provider of providers) {
        if (provider.isAvailable()) return provider;
    }
    return new NullMemoryProvider();
}

export class MemoryTracker implements AsyncDisposable {
    private lastUsedHeapSize: bigint = 0n;
    private sampleIntervalMs = 50;
    private isTracking = false;
    private timerId?: ReturnType<typeof setTimeout>;
    private readonly onMemoryChange: ((sample: MemorySampleEvent) => void) | undefined;
    private readonly provider: MemoryProvider;
    private totalSamples = 0;

    constructor(options?: { readonly onMemoryChange?: (sample: MemorySampleEvent) => void }) {
        this.onMemoryChange = options?.onMemoryChange;
        this.provider = createMemoryProvider();
    }

    startTracking(options?: { readonly intervalMs?: number }): void {
        if (this.isTracking) return;
        this.isTracking = true;
        this.lastUsedHeapSize = this.provider.getHeapUsage();
        const scheduleNext = () => {
            this.timerId = setTimeout(() => {
                this.sampleUsage();
                if (this.isTracking) scheduleNext();
            }, options?.intervalMs ?? this.sampleIntervalMs);
        };
        scheduleNext();
    }

    stopTracking(): void {
        if (this.timerId !== undefined) {
            clearTimeout(this.timerId);
            this.timerId = undefined;
        }
        this.isTracking = false;
    }

    private sampleUsage(): void {
        if (!this.isTracking) return;
        this.totalSamples++;
        const currentHeapSize = this.provider.getHeapUsage();
        let change: bigint | undefined;
        if (currentHeapSize > this.lastUsedHeapSize) {
            change = currentHeapSize - this.lastUsedHeapSize;
        }

        const source = this.provider.name as MemorySampleEvent['source'];
        const sample: MemorySampleEvent = {
            timestampMs: Date.now(),
            usedHeapSizeBytes: currentHeapSize,
            heapSizeChangeSinceLastSample: change,
            source,
        };

        this.onMemoryChange?.(sample);
        this.lastUsedHeapSize = currentHeapSize;
    }

    getTotalSamples(): number {
        return this.totalSamples;
    }

    getProviderName(): string {
        return this.provider.name;
    }

    [Symbol.asyncDispose](): Promise<void> {
        this.stopTracking();
        return Promise.resolve();
    }
}
