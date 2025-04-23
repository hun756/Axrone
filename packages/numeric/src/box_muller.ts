export type RandomGenerator<T = number> = {
    next(): T;
};

export type DistributionSample<T = number> = {
    readonly value: T;
    readonly zscore: number;
};

export type NormalDistribution<T = number> = {
    readonly sample: () => T;
    readonly sampleMany: (count: number) => readonly T[];
    readonly sampleWithMetadata?: () => DistributionSample<T>;
    readonly sampleManyWithMetadata?: (count: number) => readonly DistributionSample<T>[];
    readonly probability?: (value: number) => number;
    readonly cumulativeProbability?: (value: number) => number;
    readonly quantile?: (p: number) => number;
};

export type BoxMullerOptions<TRandom extends RandomGenerator = DefaultRandomGenerator> = {
    readonly randomGenerator?: TRandom | (() => TRandom);
    readonly mean?: number;
    readonly standardDeviation?: number;
    readonly useCache?: boolean;
    readonly precision?: number;
    readonly algorithm?: 'standard' | 'polar' | 'ziggurat';
    readonly optimizeFor?: 'speed' | 'memory';
};

export type BoxMullerError = {
    readonly code: (typeof ErrorCodes)[keyof typeof ErrorCodes];
    readonly message: string;
};

export const ErrorCodes = {
    INVALID_PARAMETER: 'INVALID_PARAMETER',
    RUNTIME_ERROR: 'RUNTIME_ERROR',
    INVALID_STATE: 'INVALID_STATE',
    INVALID_OPERATION: 'INVALID_OPERATION',
    PRECISION_ERROR: 'PRECISION_ERROR',
} as const;

export type PrecisionMode = 'high' | 'standard' | 'low';

export type DistributionMetadata = {
    readonly mean: number;
    readonly standardDeviation: number;
    readonly variance: number;
    readonly algorithm: 'standard' | 'polar' | 'ziggurat';
};

const DEFAULT_MEAN = 0;
const DEFAULT_STD_DEV = 1;
const DEFAULT_CACHE = true;
const DEFAULT_ALGORITHM = 'polar';
const DEFAULT_OPTIMIZATION = 'speed';
const DEFAULT_PRECISION = 'standard';
const TWO_PI = 2.0 * Math.PI;
const SQRT_TWO_PI = Math.sqrt(2.0 * Math.PI);
const INV_SQRT_TWO_PI = 1.0 / SQRT_TWO_PI;
const LN_2 = Math.log(2);
const MAX_ITERATIONS = 100;
const EPSILON = 1e-10;

export const createDefaultRandomGenerator = (): DefaultRandomGenerator =>
    new DefaultRandomGenerator();

export class DefaultRandomGenerator implements RandomGenerator {
    private static instance: DefaultRandomGenerator | null = null;

    static getInstance(): DefaultRandomGenerator {
        if (!DefaultRandomGenerator.instance) {
            DefaultRandomGenerator.instance = new DefaultRandomGenerator();
        }
        return DefaultRandomGenerator.instance;
    }

    next(): number {
        return Math.random();
    }

    nextInRange(min: number, max: number): number {
        return min + (max - min) * this.next();
    }

    nextInt(min: number, max: number): number {
        return Math.floor(this.nextInRange(min, max + 1));
    }
}
